using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// 様々なGameObjectをプールして再利用するための汎用的なオブジェクトプーラー。
/// isPersistentフラグに応じて、シーン用(false)と永続(true)の2種類を管理できます。
/// </summary>
public class ObjectPooler : MonoBehaviour
{
    #region Singleton & Inspector Settings
#pragma warning disable 0414 // 使われていない変数の警告（CS0414）を一時的に無効化
    [InfoBox("このスクリプトはDebugSceneでも用います。\nそのため、プレハブしておいてください。")]
    [ReadOnly]
    [SerializeField]
    private string _instruction = "設定不要";
#pragma warning restore 0414 // 警告の無効化を解除（これ以降のコードでは通常通り警告を出す）
    /// <summary>
    /// シーン固有のオブジェクト（敵など）用プール。
    /// シーン切り替えで破棄されます。
    /// </summary>
    public static ObjectPooler SceneInstance { get; private set; }

    /// <summary>
    /// ゲーム全体で共通のオブジェクト（エフェクトなど）用プール。
    /// シーンをまたいで永続します。
    /// </summary>
    public static ObjectPooler PersistentInstance { get; private set; }

    [Header("プールの種類")]
    [SerializeField]
    [Tooltip("trueにすると、シーンをまたいで破棄されない永続プールになります。")]
    private bool isPersistent = false;

    [Header("プールするオブジェクトのリスト")]
    [Tooltip("インスペクターからプールしたいプレハブとその初期数を設定します。")]
    public List<Pool> pools;

    #endregion

    #region Internal Data Structures

    /// <summary>
    /// プールするプレハブの設定情報を保持するクラス
    /// </summary>
    [System.Serializable]
    public class Pool
    {
        [Tooltip("プールを識別するための名前（タグ）")]
        public string tag;

        [Tooltip("プールするプレハブ")]
        public GameObject prefab;

        [Tooltip("プールに最初に用意しておくオブジェクトの数")]
        public int size;
    }

    /// <summary>
    /// オブジェクトの初期状態を保存するための構造体。
    /// オブジェクト再利用時に、前の使用時のスケールやタグが変更されたままになるのを防ぐために使用します。
    /// </summary>
    private struct InitialObjectSettings
    {
        public Vector3 localScale;
        public string tag;
        // 今後ここに追加可能（例: public int layer; public Quaternion defaultRotation; 等）
    }

    #endregion

    #region Private Fields

    // --- コアデータ ---
    // プール本体。タグをキーとして、オブジェクトのキュー(待機列)を管理する
    private Dictionary<string, Queue<GameObject>> poolDictionary;

    // --- 管理用データ ---
    // プールごとの「親Transform」を記憶する辞書（ヒエラルキー整理用）
    private Dictionary<string, Transform> poolParentDictionary;

    // アクティブな（現在貸し出し中の）オブジェクトを追跡するための辞書
    // <GameObjectそのもの, プールのタグ>
    private Dictionary<GameObject, string> activeObjects;

    // オブジェクトのInstanceIDをキーにして初期設定を保持する辞書
    // GameObjectそのものではなくInstanceID(int)をキーにすることで、GC(ガベージコレクション)発生を抑え軽量化します
    private Dictionary<int, InitialObjectSettings> initialSettingsMap;

    #endregion

    #region Unity Lifecycle Methods

    private void Awake()
    {
        // シングルトンの初期化処理
        if (isPersistent)
        {
            // 【永続インスタンスの処理】
            if (PersistentInstance == null)
            {
                PersistentInstance = this;
                // 注意: DontDestroyOnLoadはプレハブの配置方法やシーン管理の仕組みに応じて外部で呼ばれるか、ここで呼ぶか決まります
                // DontDestroyOnLoad(gameObject); // シーンが切り替わっても破棄しない
            }
            else
            {
                // 既に永続インスタンスが存在する場合は、重複なので自身を破棄
                Destroy(gameObject);
                return;
            }
        }
        else
        {
            // 【シーンインスタンスの処理】
            if (SceneInstance != null)
            {
                // シーン内に既にシーン用プーラーがある場合は警告し、自身を破棄
                Debug.LogWarning(
                    $"シーン用ObjectPoolerが '{SceneInstance.gameObject.name}' と '{this.gameObject.name}' の2つ存在します。"
                );
                Destroy(gameObject);
                return;
            }
            SceneInstance = this;
        }

        // --- プールの初期化処理を Awake で実行 ---
        // (Startで行うと、他のスクリプトのAwake/StartでSpawnFromPoolを呼んだ時にエラーになるため)
        InitializePools();
    }

    private void OnDestroy()
    {
        // オブジェクトが破棄される際、シングルトン参照を解除してメモリリークを防ぐ
        if (isPersistent && PersistentInstance == this)
        {
            PersistentInstance = null;
        }
        else if (!isPersistent && SceneInstance == this)
        {
            SceneInstance = null;
        }
    }

    #endregion

    #region Initialization Methods

    /// <summary>
    /// インスペクターで設定された pools リストを元に、実際のキューとオブジェクトを生成して初期化します。
    /// </summary>
    private void InitializePools()
    {
        poolDictionary = new Dictionary<string, Queue<GameObject>>();
        poolParentDictionary = new Dictionary<string, Transform>();
        activeObjects = new Dictionary<GameObject, string>();
        initialSettingsMap = new Dictionary<int, InitialObjectSettings>();

        foreach (Pool pool in pools)
        {
            Queue<GameObject> objectQueue = new Queue<GameObject>();

            // 生成したオブジェクトを、このプーラーの子オブジェクトにして、
            // Unityエディタのヒエラルキーを見やすく整理する
            Transform poolParent = new GameObject(pool.tag + " Pool").transform;
            poolParent.SetParent(this.transform, false);

            // 親Transformを辞書に登録
            poolParentDictionary.Add(pool.tag, poolParent);

            // 指定された数だけ事前にオブジェクトを生成してキューに入れる
            for (int i = 0; i < pool.size; i++)
            {
                // poolParentの子として生成。worldPositionStays: false を指定し、ローカル座標/スケールを維持する
                GameObject obj = Instantiate(pool.prefab, poolParent, false);

                // 初期状態を保存
                RegisterInitialSettings(obj);

                // PoolableObjectコンポーネントがあれば、プールタグとタイプ（永続かシーンか）を設定
                var poolable = obj.GetComponent<PoolableObject>();
                if (poolable != null)
                {
                    poolable.SetPoolTag(pool.tag);
                    poolable.SetPoolType(isPersistent ? PoolType.Persistent : PoolType.Scene);
                }

                obj.SetActive(false); // 初期状態は非アクティブ
                objectQueue.Enqueue(obj);
            }

            // 完成したキューを辞書に追加
            poolDictionary.Add(pool.tag, objectQueue);
        }
    }

    /// <summary>
    /// オブジェクトの初期状態（スケールやタグなど）を辞書に登録するメソッド。
    /// </summary>
    private void RegisterInitialSettings(GameObject obj)
    {
        InitialObjectSettings settings = new InitialObjectSettings
        {
            localScale = obj.transform.localScale,
            tag = obj.tag,
            // 将来拡張時はここに追加: layer = obj.layer, etc...
        };

        // InstanceIDをキーにして保存（ハッシュ計算が高速）
        initialSettingsMap[obj.GetInstanceID()] = settings;
    }

    #endregion

    #region Core Pooling Logic (Spawn & Return)

    /// <summary>
    /// プールからオブジェクトを取り出して有効化します。
    /// </summary>
    /// <param name="tag">取得したいオブジェクトのプールタグ</param>
    /// <param name="position">出現させるワールド座標</param>
    /// <param name="rotation">出現させるワールド回転</param>
    /// <returns>プールから取り出されたGameObject</returns>
    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
    {
        // 存在しないタグが指定された場合
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogError($"オブジェクトプール '{tag}' が存在しません。");
            return null;
        }

        GameObject objectToSpawn;

        // プールに待機中のオブジェクトがあれば、それを取り出す
        if (poolDictionary[tag].Count > 0)
        {
            objectToSpawn = poolDictionary[tag].Dequeue();

            // プールから取り出す際に親子関係を解除（ルートに移動）
            // worldPositionStays: false を指定して、
            // 親を解除する際にローカルスケール（プレハブ本来のスケール）を維持する
            objectToSpawn.transform.SetParent(null, false);
        }
        // プールが空っぽ（全てのオブジェクトが使用中）だった場合
        else
        {
            // プールの初期サイズが不足していることを開発者に知らせる警告
            Debug.LogWarning(
                $"タグ '{tag}' を持つプールが空でした。プールを動的に拡張します。"
                    + " パフォーマンス低下を防ぐため、インスペクターで初期サイズ(size)を増やすことを検討してください。"
            );

            // 元のプレハブ情報を探して、新しいオブジェクトを動的に生成する
            Pool pool = pools.Find(p => p.tag == tag);
            if (pool != null)
            {
                objectToSpawn = Instantiate(pool.prefab);

                // 動的に生成した場合も初期状態を登録しておく
                RegisterInitialSettings(objectToSpawn);

                // 動的生成時もPoolableObjectの初期化を行う
                var poolable = objectToSpawn.GetComponent<PoolableObject>();
                if (poolable != null)
                {
                    poolable.SetPoolTag(pool.tag);
                    poolable.SetPoolType(isPersistent ? PoolType.Persistent : PoolType.Scene);
                }
            }
            else
            {
                // タグに対応するプレハブが見つからない（ありえないが念のため）
                return null;
            }
        }

        // --- 初期状態（スケールやタグ）のリセット ---
        // オブジェクトが前回使われた際にアニメーション等でスケール変更等されていた場合に元に戻す
        if (
            initialSettingsMap.TryGetValue(
                objectToSpawn.GetInstanceID(),
                out InitialObjectSettings settings
            )
        )
        {
            objectToSpawn.transform.localScale = settings.localScale;
            objectToSpawn.tag = settings.tag;
            // 将来拡張時はここに追加: objectToSpawn.layer = settings.layer;
        }

        // オブジェクトを有効化し、位置と回転を適用
        objectToSpawn.SetActive(true);
        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;

        // 貸し出したオブジェクトを追跡リストに追加
        activeObjects[objectToSpawn] = tag; // Addではなくインデクサーで上書きすることでエラーを回避
        

        return objectToSpawn;
    }

    /// <summary>
    /// 使用済みのオブジェクトを非表示にし、プールに返却します。
    /// </summary>
    /// <param name="tag">返却する先のプールのタグ</param>
    /// <param name="objectToReturn">返却するGameObject</param>
    public void ReturnToPool(string tag, GameObject objectToReturn)
    {
        // すでに破棄されている場合は何もしない（MissingReference対策）
        if (objectToReturn == null)
            return;

        // 存在しないプールのタグが指定された場合
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"Pool with tag '{tag}' doesn't exist. Destroying object.");
            // プールがない場合は、オブジェクトを単純に破棄する
            Destroy(objectToReturn);
            return;
        }

        // 返却されたオブジェクトが追跡リスト（貸出中リスト）に存在しない場合、
        // 既に返却済みとみなして、キューへの重複登録を防ぐために処理をスキップする
        if (!activeObjects.ContainsKey(objectToReturn))
        {
            return; // ※この return; がないとエラーの原因になります
        }

        // 存在する場合はリストから削除
        activeObjects.Remove(objectToReturn);

        // 親を null ではなく、InitializePools で作成した整理用の親 (poolParent) に戻す
        if (poolParentDictionary.TryGetValue(tag, out Transform poolParent))
        {
            // worldPositionStays: false でローカルスケールの意図しない変更を防ぎながら親を設定
            objectToReturn.transform.SetParent(poolParent, false);
        }
        else
        {
            // (フォールバック) 整理用親が見つからない場合はルートに置く
            objectToReturn.transform.SetParent(null, false);
        }

        // オブジェクトを無効化
        objectToReturn.SetActive(false);

        // 使用済みのオブジェクトをキューの末尾に戻す (Enqueue)
        poolDictionary[tag].Enqueue(objectToReturn);
    }

    /// <summary>
    /// 指定した時間(秒)が経過した後に、オブジェクトをプールに返却します。
    /// Destroy(gameObject, delay)の代替として使用します。
    /// </summary>
    /// <param name="tag">返却する先のプールのタグ</param>
    /// <param name="objectToReturn">返却するGameObject</param>
    /// <param name="delay">返却するまでの遅延時間（秒）</param>
    public void ReturnToPoolAfterDelay(string tag, GameObject objectToReturn, float delay)
    {
        // 実際の遅延処理は、内部のプライベートなコルーチンに任せる
        StartCoroutine(ReturnToPoolCoroutine(tag, objectToReturn, delay));
    }

    /// <summary>
    /// 遅延処理を実行するコルーチン本体
    /// </summary>
    private IEnumerator ReturnToPoolCoroutine(string tag, GameObject objectToReturn, float delay)
    {
        // 指定された時間だけ待機
        yield return new WaitForSeconds(delay);

        // 遅延後、オブジェクトがまだ存在し、かつアクティブな（貸し出し中の）場合のみ返却処理を行う
        // （待っている間に親が破棄されるなど、オブジェクトが既に消えている可能性があるため）
        if (objectToReturn != null && objectToReturn.activeInHierarchy)
        {
            ReturnToPool(tag, objectToReturn);
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 現在アクティブな、プールから生成された全てのオブジェクトをそれぞれのプールに強制的に返却します。
    /// ボスが倒された時やシーンのリセット時（画面内の弾を一掃するなど）に呼び出すことを想定しています。
    /// </summary>
    public void ReturnAllToPool()
    {
        // activeObjectsをToList()でコピーしてからループする。
        // ループ中にReturnToPoolが呼ばれると元のコレクション(activeObjects)が変更されるため、
        // コピーしておかないと InvalidOperationException (コレクションが変更されました) エラーになるのを防ぐため。
        foreach (var pair in activeObjects.ToList())
        {
            // nullチェック（破棄済みのオブジェクトへのアクセス防止）
            if (pair.Key != null)
            {
                ReturnToPool(pair.Value, pair.Key);
            }
        }

        // 念のため貸出リストを完全にクリア
        activeObjects.Clear();
    }

    /// <summary>
    /// 指定したタグのオブジェクトが現在いくつアクティブ（出現中）かを返します。
    /// 画面内に特定の敵が何体いるか制限したい場合などに使用します。
    /// </summary>
    /// <param name="tag">確認したいプールのタグ</param>
    /// <returns>アクティブなオブジェクトの数</returns>
    public int GetActiveCount(string tag)
    {
        // activeObjectsには <貸し出し中のオブジェクト, タグ> のペアが登録されています。
        // Values(タグのリスト)の中から、引数のtagと一致するものの個数を数えて返します。
        return activeObjects.Values.Count(t => t == tag);
    }

    #endregion
}

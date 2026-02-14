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
    [InfoBox("このスクリプトはDebugSceneでも用います。\nそのため、プレハブしておいてください。")]
    [ReadOnly]
    [SerializeField]
    private string _instruction = "設定不要";

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

    [System.Serializable]
    public class Pool
    {
        public string tag; // プールを識別するための名前（タグ）
        public GameObject prefab; // プールするプレハブ
        public int size; // プールに最初に用意しておくオブジェクトの数
    }

    [Header("プールするオブジェクトのリスト")]
    public List<Pool> pools;

    // プール本体。タグをキーとして、オブジェクトのキューを管理する
    private Dictionary<string, Queue<GameObject>> poolDictionary;

    // プールごとの「親Transform」を記憶する辞書を追加
    private Dictionary<string, Transform> poolParentDictionary;

    //アクティブな（貸し出し中の）オブジェクトを追跡するための辞書
    private Dictionary<GameObject, string> activeObjects = new Dictionary<GameObject, string>();

    // 初期状態を保存するための構造体
    private struct InitialObjectSettings
    {
        public Vector3 localScale;
        public string tag;
        // 今後ここに追加可能（例: public int layer; public Quaternion defaultRotation; 等）
    }

    // オブジェクトのInstanceIDをキーにして初期設定を保持する辞書
    // GameObjectそのものではなくInstanceID(int)をキーにすることで、GC発生を抑え軽量化します
    private Dictionary<int, InitialObjectSettings> initialSettingsMap =
        new Dictionary<int, InitialObjectSettings>();

    private void Awake()
    {
        if (isPersistent)
        {
            // 【永続インスタンスの処理】
            if (PersistentInstance == null)
            {
                PersistentInstance = this;
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

        // --- 3. プールの初期化処理を Awake に移動 ---
        // (Startだと、AwakeでSpawnFromPoolを呼んだ時にエラーになるため)
        InitializePools();
    }

    private void InitializePools()
    {
        poolDictionary = new Dictionary<string, Queue<GameObject>>();
        poolParentDictionary = new Dictionary<string, Transform>();
        initialSettingsMap = new Dictionary<int, InitialObjectSettings>();

        foreach (Pool pool in pools)
        {
            Queue<GameObject> objectQueue = new Queue<GameObject>();

            // 生成したオブジェクトを、このプーラーの子オブジェクトにして、
            // ヒエラルキーを見やすくする
            Transform poolParent = new GameObject(pool.tag + " Pool").transform;
            poolParent.SetParent(this.transform, false);

            //親Transformを辞書に登録
            poolParentDictionary.Add(pool.tag, poolParent);

            for (int i = 0; i < pool.size; i++)
            {
                // (poolParentの子として生成し、worldPositionStays: false は正しい)
                GameObject obj = Instantiate(pool.prefab, poolParent, false);

                //初期状態を保存
                RegisterInitialSettings(obj);

                // PoolableObjectコンポーネントがあれば、プールタグとタイプを設定
                var poolable = obj.GetComponent<PoolableObject>();
                if (poolable != null)
                {
                    poolable.SetPoolTag(pool.tag);
                    poolable.SetPoolType(isPersistent ? PoolType.Persistent : PoolType.Scene);
                }

                obj.SetActive(false);
                objectQueue.Enqueue(obj);
            }

            poolDictionary.Add(pool.tag, objectQueue);
        }
    }

    /// <summary>
    /// オブジェクトの初期状態を辞書に登録するメソッド
    /// </summary>
    private void RegisterInitialSettings(GameObject obj)
    {
        InitialObjectSettings settings = new InitialObjectSettings
        {
            localScale = obj.transform.localScale,
            tag = obj.tag,
            // 将来拡張時はここに追加: layer = obj.layer, etc...
        };

        // InstanceIDをキーにして保存（高速）
        initialSettingsMap[obj.GetInstanceID()] = settings;
    }

    /// <summary>
    /// プールからオブジェクトを取り出して有効化する
    /// </summary>
    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogError($"オブジェクトプール '{tag}' が存在しません。");
            return null;
        }

        //プールが空（全てのオブジェクトが使用中）の場合の処理
        GameObject objectToSpawn;

        // プールに待機中のオブジェクトがあれば、それを取り出す
        if (poolDictionary[tag].Count > 0)
        {
            objectToSpawn = poolDictionary[tag].Dequeue();

            // プールから取り出す際に親子関係を解除（ルートに移動）
            // worldPositionStays: false を指定して、
            // 親を解除する際にローカルスケール（プレハブのスケール）を維持する
            objectToSpawn.transform.SetParent(null, false);
        }
        // プールが空っぽ（全てのオブジェクトが使用中）だった場合
        else
        {
            // プールの初期サイズが不足していることを開発者に知らせる警告
            Debug.LogWarning(
                $"タグ '{tag}' を持つプールが空でした。プールを拡張します。"
                    + " インスペクターで初期サイズを増やすことを検討してください。"
            );

            // 元のプレハブ情報を探して、新しいオブジェクトを動的に生成する
            Pool pool = pools.Find(p => p.tag == tag);
            if (pool != null)
            {
                objectToSpawn = Instantiate(pool.prefab);
                // 動的に生成した場合も初期状態を登録
                RegisterInitialSettings(objectToSpawn);
            }
            else
            {
                // タグに対応するプレハブが見つからない（ありえないが念のため）
                return null;
            }
        }

        // 初期状態（スケールやタグ）をリセット
        // オブジェクトが前回使われた際にスケール変更等されていた場合に元に戻す
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

        objectToSpawn.SetActive(true);
        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;

        // 貸し出したオブジェクトを追跡リストに追加
        activeObjects.Add(objectToSpawn, tag);
        return objectToSpawn;
    }

    /// <summary>
    /// 使用済みのオブジェクトを非表示にし、プールに返却する
    /// </summary>
    /// <param name="tag">返却する先のプールのタグ</param>
    /// <param name="objectToReturn">返却するGameObject</param>
    public void ReturnToPool(string tag, GameObject objectToReturn)
    {
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"Pool with tag '{tag}' doesn't exist.");
            // プールがない場合は、オブジェクトを単純に破棄する
            Destroy(objectToReturn);
            return;
        }

        //返却されたオブジェクトを追跡リストから削除
        if (activeObjects.ContainsKey(objectToReturn))
        {
            activeObjects.Remove(objectToReturn);
        }

        // 親を null ではなく、InitializePools で作成した整理用の親 (poolParent) に戻す
        if (poolParentDictionary.TryGetValue(tag, out Transform poolParent))
        {
            // worldPositionStays: false でローカルスケールの変更を防ぎながら親を設定
            objectToReturn.transform.SetParent(poolParent, false);
        }
        else
        {
            // (フォールバック)
            objectToReturn.transform.SetParent(null, false);
        }

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

    /// <summary>
    /// 現在アクティブな、プールから生成された全てのオブジェクトをそれぞれのプールに返却します。
    /// ボスが倒された時やシーンのリセット時に呼び出すことを想定しています。
    /// </summary>
    public void ReturnAllToPool()
    {
        // activeObjectsをToList()でコピーしてからループする。
        // ループ中に元のコレクション(activeObjects)が変更されることによるエラーを防ぐため。
        foreach (var pair in activeObjects.ToList())
        {
            ReturnToPool(pair.Value, pair.Key);
        }
    }

    /// <summary>
    /// 指定したタグのオブジェクトが現在いくつアクティブ（出現中）かを返します。
    /// </summary>
    /// <param name="tag">確認したいプールのタグ</param>
    /// <returns>アクティブなオブジェクトの数</returns>
    public int GetActiveCount(string tag)
    {
        // activeObjectsには貸し出し中のオブジェクトとタグのペアが登録されています。
        // Values(タグ)の中から、引数のtagと一致するものの個数を数えて返します。
        return activeObjects.Values.Count(t => t == tag);
    }

    private void OnDestroy()
    {
        // シングルトン参照を解除
        if (isPersistent && PersistentInstance == this)
        {
            PersistentInstance = null;
        }
        else if (!isPersistent && SceneInstance == this)
        {
            SceneInstance = null;
        }
    }
}

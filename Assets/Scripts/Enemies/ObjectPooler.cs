using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 様々なGameObjectをプールして再利用するための汎用的なオブジェクトプーラー。
/// </summary>
public class ObjectPooler : MonoBehaviour
{
    // --- シングルトンインスタンス ---
    public static ObjectPooler instance;

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

    //アクティブな（貸し出し中の）オブジェクトを追跡するための辞書
    private Dictionary<GameObject, string> activeObjects = new Dictionary<GameObject, string>();

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        poolDictionary = new Dictionary<string, Queue<GameObject>>();

        // インスペクターで設定された各プールを初期化
        foreach (Pool pool in pools)
        {
            Queue<GameObject> objectQueue = new Queue<GameObject>();

            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = Instantiate(pool.prefab);
                obj.SetActive(false); // 非表示にしておく
                objectQueue.Enqueue(obj); // キューに追加
            }

            poolDictionary.Add(pool.tag, objectQueue);
        }
    }

    /// <summary>
    /// プールからオブジェクトを取り出して有効化する
    /// </summary>
    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"Pool with tag '{tag}' doesn't exist.");
            return null;
        }

        // 修正点：プールが空（全てのオブジェクトが使用中）の場合の処理
        GameObject objectToSpawn;

        // プールに待機中のオブジェクトがあれば、それを取り出す
        if (poolDictionary[tag].Count > 0)
        {
            objectToSpawn = poolDictionary[tag].Dequeue();

            // プールから取り出す際に親子関係を解除（ルートに移動）
            objectToSpawn.transform.SetParent(null);
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
            }
            else
            {
                // タグに対応するプレハブが見つからない（ありえないが念のため）
                return null;
            }
        }

        objectToSpawn.SetActive(true);
        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;

        // 変更点：貸し出したオブジェクトを追跡リストに追加
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

        objectToReturn.transform.SetParent(null); // 親をリセット
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
}

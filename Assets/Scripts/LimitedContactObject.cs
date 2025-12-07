using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// 自身がTriggerであっても、指定LayerのTriggerでない相手との接触に反応し、指定回数で消滅
/// </summary>
public class LimitedContactObject : MonoBehaviour
{
    [Header("接触制限設定")]
    [Tooltip("このオブジェクトが接触可能な最大回数")]
    [SerializeField]
    private int maxContactCount = 1;

    [Header("時間制限設定")]
    [SerializeField]
    [Tooltip("出現からの寿命（秒）。0以下の場合は時間で消滅しない。")]
    private float lifetime = 3.0f;

    [Header("ObjectPooler 設定")]
    [SerializeField]
    [Tooltip("trueの場合、DestroyせずObjectPoolerに返却しようとします")]
    private bool usePooling = false;

    [SerializeField, ShowIf(nameof(usePooling))]
    [Tooltip("このオブジェクトの返却先となる ObjectPooler の「タグ」")]
    private string myPoolTag = "";

    [SerializeField, ShowIf(nameof(usePooling))]
    [Tooltip("返却先のプールの種類（Persistent=永続, Scene=シーン用）")]
    private PoolType returnToPool = PoolType.Persistent;

    private int currentContactCount = 0;

    public int MaxContactCount
    {
        get => maxContactCount;
        set => maxContactCount = Mathf.Max(0, value);
    }
    private LayerMask groundLayer; //接触をカウントする対象のレイヤー

    private void Awake()
    {
        groundLayer = LayerMask.GetMask(GameConstants.PhysicsLayerName_Ground); // Groundレイヤーを取得
    }

    /// <summary>
    /// オブジェクトが有効化されたとき
    /// </summary>
    private void OnEnable()
    {
        // 接触回数をリセット
        currentContactCount = 0;

        // 寿命が設定されている（0より大きい）場合、指定時間後にDestroyを予約
        if (lifetime > 0f)
        {
            // プールを使用し、かつタグが設定されているかチェック
            if (usePooling && !string.IsNullOrEmpty(myPoolTag))
            {
                // ObjectPoolerに遅延返却を依頼
                ReturnToPoolAfterDelay(lifetime);
            }
            else
            {
                // プール管理されていないなら、従来通りDestroy
                Destroy(gameObject, lifetime);
            }
        }
    }

    /// <summary>
    /// 他のオブジェクトとTriggerとして接触したとき
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 相手がTriggerの場合は無視（物理的な接触ではないため）
        if (other.isTrigger)
            return;

        // 相手のレイヤーがgroundLayerに含まれているか確認
        if ((groundLayer.value & (1 << other.gameObject.layer)) == 0)
            return;

        currentContactCount++;

        if (currentContactCount >= maxContactCount)
        {
            ReturnToPoolNow();
        }
    }

    /// <summary>
    /// このオブジェクトを即座にプールに返却（または破棄）します。
    /// </summary>
    public void ReturnToPoolNow()
    {
        // プールを使用し、かつタグが設定されていれば、プール返却処理を行う
        if (usePooling && !string.IsNullOrEmpty(myPoolTag))
        {
            // オブジェクトを非アクティブ化（これにより ObjectPooler の遅延返却コルーチンも止まる）
            // （注：ObjectPooler.ReturnToPool側でも非アクティブ化されますが、
            // 　　　即時停止の意図を明確にするため、ここでも呼び出します）
            gameObject.SetActive(false);

            bool returned = false;
            if (returnToPool == PoolType.Persistent)
            {
                if (ObjectPooler.PersistentInstance != null)
                {
                    ObjectPooler.PersistentInstance.ReturnToPool(myPoolTag, this.gameObject);
                    returned = true;
                }
            }
            else // (returnToPool == PoolType.Scene)
            {
                if (ObjectPooler.SceneInstance != null)
                {
                    ObjectPooler.SceneInstance.ReturnToPool(myPoolTag, this.gameObject);
                    returned = true;
                }
            }

            // 返却先のプールが見つからなかった場合
            if (!returned)
            {
                Debug.LogWarning(
                    $"返却先の {returnToPool} プール（タグ: {myPoolTag}）が見つかりません。オブジェクトを破棄します。",
                    this
                );
                Destroy(gameObject);
            }
        }
        else
        {
            // プールを使用しない設定、またはタグが未設定の場合は、単純に破棄
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// ObjectPooler の遅延返却機能を呼び出します。
    /// </summary>
    private void ReturnToPoolAfterDelay(float delay)
    {
        // 適切なObjectPoolerインスタンスの遅延返却メソッドを呼ぶ
        if (returnToPool == PoolType.Persistent)
        {
            if (ObjectPooler.PersistentInstance != null)
            {
                ObjectPooler.PersistentInstance.ReturnToPoolAfterDelay(
                    myPoolTag,
                    this.gameObject,
                    delay
                );
            }
            else
            {
                // プールがないなら遅延破棄
                Destroy(gameObject, delay);
            }
        }
        else // (returnToPool == PoolType.Scene)
        {
            if (ObjectPooler.SceneInstance != null)
            {
                ObjectPooler.SceneInstance.ReturnToPoolAfterDelay(
                    myPoolTag,
                    this.gameObject,
                    delay
                );
            }
            else
            {
                // プールがないなら遅延破棄
                Destroy(gameObject, delay);
            }
        }
    }
}

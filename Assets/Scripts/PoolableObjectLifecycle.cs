using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// 時間経過または特定の接触条件によって、オブジェクトをプールに返却（消滅）させるライフサイクル管理クラス
/// </summary>
public class PoolableObjectLifecycle : PoolableObject
{
    [Header("時間制限設定")]
    [SerializeField]
    [Tooltip("出現からの寿命（秒）。0以下の場合は時間で消滅しない。")]
    private float lifetime = 3.0f;

    [Header("接触制限設定")]
    [Tooltip("接触したら消滅させるかどうか")]
    [SerializeField]
    private bool enableContactLimit = false;

    [Tooltip("このオブジェクトがGroundLayerに接触可能な最大回数")]
    [SerializeField, ShowIf(nameof(enableContactLimit))]
    private int maxContactCount = 1;

    private int currentContactCount = 0;

    public int MaxContactCount
    {
        get => maxContactCount;
        set => maxContactCount = Mathf.Max(0, value);
    }
    private LayerMask groundLayer; //接触をカウントする対象のレイヤー

    // 現在実行中の自動消滅コルーチンを保持する変数
    private Coroutine currentDespawnCoroutine;

    private void Awake()
    {
        groundLayer = LayerMask.GetMask(GameConstants.PhysicsLayerName_Ground); // Groundレイヤーを取得
    }

    private void OnEnable()
    {
        currentContactCount = 0; // 接触カウントをリセット
        if (lifetime > 0f)
        {
            // lifetime秒後に自動で返却
            StartCoroutine(ReturnToPoolDelayCoroutine(lifetime));
        }
    }

    private void OnDisable()
    {
        // オブジェクトが無効化されたらコルーチンの参照を切る
        currentDespawnCoroutine = null;
    }

    /// <summary>
    /// 現在の寿命タイマーをキャンセルし、指定した時間後に消えるように再設定します。
    /// </summary>
    /// <param name="duration">新しく設定する消滅までの時間（秒）</param>
    public void OverrideDespawnTimer(float duration)
    {
        // 既に動いている寿命コルーチンがあれば停止する
        if (currentDespawnCoroutine != null)
        {
            StopCoroutine(currentDespawnCoroutine);
        }

        // 新しい時間でコルーチンを開始し直す
        currentDespawnCoroutine = StartCoroutine(ReturnToPoolDelayCoroutine(duration));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 相手がTriggerの場合は無視（物理的な接触ではないため）
        // また、接触制限が無効な場合も無視
        if (other.isTrigger || !enableContactLimit)
            return;

        // 相手のレイヤーがgroundLayerに含まれているか確認
        if ((groundLayer.value & (1 << other.gameObject.layer)) == 0)
            return;

        currentContactCount++;

        if (currentContactCount >= maxContactCount)
        {
            // 接触回数が上限に達したらプールに返却
            ReturnToPool();
        }
    }
}

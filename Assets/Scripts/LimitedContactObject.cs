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

    [Tooltip("接触をカウントする対象のレイヤー")]
    [SerializeField]
    private LayerMask groundLayer;

    private int currentContactCount = 0;

    public int MaxContactCount
    {
        get => maxContactCount;
        set => maxContactCount = Mathf.Max(0, value);
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
            Destroy(gameObject);
        }
    }
}
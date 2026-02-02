using UnityEngine;

/// <summary>
/// このエリアに接触したPoolableObjectを持つオブジェクトを自動的にプールへ返却（回収）するクラス。
/// 画面外に出た弾やエフェクトの削除用エリア、あるいは落下死判定エリアとして使用します。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class PoolReturnArea : MonoBehaviour
{
    private BoxCollider2D boxCollider;

    private void Awake()
    {
        // BoxCollider2Dの参照を取得
        boxCollider = GetComponent<BoxCollider2D>();

        // 物理的な壁として機能しないよう、Triggerモードになっているか確認
        if (!boxCollider.isTrigger)
        {
            boxCollider.isTrigger = true;
            Debug.LogWarning(
                $"{gameObject.name} のBoxCollider2Dで 'Is Trigger' が有効になっていませんでした。自動的に有効化しました。",
                this
            );
        }
    }

    /// <summary>
    /// 他のコライダーがトリガー範囲に入ったときの処理
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 接触したオブジェクトが PoolableObject コンポーネントを持っているか確認
        var poolableObj = other.GetComponent<PoolableObject>();

        if (poolableObj != null)
        {
            // プールへ返却を実行
            // (PoolableObject.ReturnToPool 内でタグの確認や、プールがない場合のDestroy処理が行われます)
            poolableObj.ReturnToPool();
        }
    }

    /// <summary>
    /// シーンビューでの可視化（Gizmos）
    /// </summary>
    private void OnDrawGizmos()
    {
        // Awake前（編集中）でも動作するように、nullなら取得を試みる
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider2D>();
        }

        if (boxCollider != null)
        {
            // --- 色の設定 ---
            // 既存の赤、グレー、マゼンタ、シアン、緑、オレンジ、黄色と被らない「青色」を採用
            Color fillColor = new Color(0f, 0f, 1f, 0.2f); // 半透明の青
            Color borderColor = Color.blue; // 青

            // BoxCollider2Dの範囲情報を使ってGizmoを描画
            Gizmos.matrix = transform.localToWorldMatrix;
            
            Gizmos.color = fillColor;
            Gizmos.DrawCube(boxCollider.offset, boxCollider.size);

            Gizmos.color = borderColor;
            Gizmos.DrawWireCube(boxCollider.offset, boxCollider.size);
        }
    }
}
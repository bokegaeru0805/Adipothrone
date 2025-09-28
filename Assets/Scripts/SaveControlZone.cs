using UnityEngine;

/// <summary>
/// プレイヤーがトリガー範囲内に入るとセーブを無効化し、出ると有効化する
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class SaveControlZone : MonoBehaviour
{
    private BoxCollider2D boxCollider;

    private void Awake()
    {
        //BoxCollider2Dの参照を最初に取得して保持（キャッシュ）する
        boxCollider = GetComponent<BoxCollider2D>();
        if (!boxCollider.isTrigger)
        {
            Debug.LogWarning(
                $"{gameObject.name} のBoxCollider2Dで 'Is Trigger' が有効になっていません。",
                this
            );
        }
    }

    /// <summary>
    /// 他のコライダーがトリガー範囲に入ったときに一度だけ呼ばれる
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(GameConstants.PlayerTagName))
        {
            SaveLoadManager.instance.DisableSave();
        }
    }

    /// <summary>
    /// 他のコライダーがトリガー範囲から出たときに一度だけ呼ばれる
    /// </summary>
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(GameConstants.PlayerTagName))
        {
            SaveLoadManager.instance.EnableSave();
        }
    }

    /// <summary>
    /// シーンビューでコライダーの範囲を視覚的に表示する
    /// </summary>
    private void OnDrawGizmos()
    {
        // Awakeが呼ばれる前（編集中）にも対応するため、colliderがnullなら取得を試みる
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider2D>();
        }

        // Gizmoの色を設定
        Color fillColor = new Color(1f, 0f, 0f, 0.1f); // 半透明の赤色
        Color borderColor = Color.red;

        // BoxCollider2Dの範囲情報を取得してGizmoを描画
        // .bounds.center と .bounds.size で、コライダーの正確な中心と大きさを取得できる
        Gizmos.color = fillColor;
        Gizmos.DrawCube(boxCollider.bounds.center, boxCollider.bounds.size);

        Gizmos.color = borderColor;
        Gizmos.DrawWireCube(boxCollider.bounds.center, boxCollider.bounds.size);
    }
}

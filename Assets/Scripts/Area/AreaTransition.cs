using UnityEngine;

public class AreaTransition : MonoBehaviour
{
    [Tooltip("移動先の座標")]
    [SerializeField]
    private Vector2 movePos;

    private void Awake()
    {
        if (movePos == Vector2.zero)
        {
            Debug.LogError($"MovePosが設定されていません", this);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // プレイヤーがトリガーに入った場合
        if (Time.timeScale > 0)
        {
            //プレイヤーが操作不能状態でない場合のみ移動させる
            if (
                !PlayerManager.instance.isControlLocked
                && collision.CompareTag(GameConstants.PLAYER_TAG_NAME)
            )
            {
                DoorOpener.OpenDoor(movePos, this, DoorOpener.DoorType.None);
            }
        }
    }

    /// <summary>
    /// コンテキストメニュー（スクリプトを右クリック）から実行。
    /// コライダーの見た目の位置を変えずに、Transformの位置を中心へ移動し、Offsetを(0,0)にします。
    /// </summary>
    [ContextMenu("Center Pivot to Collider")]
    private void CenterPivotToCollider()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col == null)
            return;

        // オフセットが既にほぼ0なら何もしない
        if (col.offset.sqrMagnitude < 0.0001f)
            return;

        // 1. 現在のコライダーの中心座標（ワールド座標）を計算
        // TransformPointを使うことで、回転やスケールも考慮された正確な位置が取れます
        Vector3 worldCenter = transform.TransformPoint(col.offset);

        // 2. 親オブジェクトへの影響を考慮し、Undoシステムに登録（Editor操作の安全策）
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(transform, "Center Pivot");
        UnityEditor.Undo.RecordObject(col, "Center Pivot");
#endif

        // 3. Transformの位置を、計算した中心座標へ移動
        transform.position = worldCenter;

        // 4. コライダーのオフセットを(0,0)にリセット
        col.offset = Vector2.zero;

        Debug.Log($"[{gameObject.name}] Pivot centered to collider bounds.", this);
    }

    // Editor上でギズモを表示
    private void OnDrawGizmos()
    {
        // 1. トリガーエリアの描画
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
            return;

        // エリアの色 (青で透明度0.2)
        Color fillColor = new Color(0f, 0f, 1f, 0.2f);
        Gizmos.color = fillColor;
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);

        // 枠線 (純粋な青)
        Color borderColor = Color.blue;
        Gizmos.color = borderColor;
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);

        // 2. 移動先への線とポイント描画
        // このオブジェクトのワールド座標
        Vector3 startPosition = transform.position;
        // 移動先 (Z座標は現在のオブジェクトと同じにする)
        Vector3 endPosition = new Vector3(movePos.x, movePos.y, startPosition.z);

        // 線を描画 (緑色)
        Gizmos.color = Color.green;
        Gizmos.DrawLine(startPosition, endPosition);

        // 移動先に球体を描画 (半透明の緑)
        Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
        Gizmos.DrawSphere(endPosition, 0.5f);

        // 球体の枠線 (不透明の緑)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(endPosition, 0.5f);
    }
}

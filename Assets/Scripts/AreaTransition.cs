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

    // Editor上でギズモを表示
    private void OnDrawGizmos()
    {
        // トリガーゾーンのギズモ描画 (transformの位置とスケールを使用)
        Vector3 gizmoCenter = transform.position;
        Vector3 gizmoSize = transform.localScale;

        // 1. トリガーエリアの描画
        // 塗りつぶし色 (青で透明度0.2)
        Gizmos.color = new Color(0f, 0f, 1f, 0.2f);
        Gizmos.DrawCube(gizmoCenter, gizmoSize);

        // 輪郭線 (純粋な青)
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(gizmoCenter, gizmoSize);

        // 2. 移動先への線とポイント描画
        // このオブジェクトのワールド座標
        Vector3 startPosition = transform.position;
        // 移動先 (Z座標は現在のオブジェクトと同じにする)
        Vector3 endPosition = new Vector3(movePos.x, movePos.y, startPosition.z);

        // 線を描画 (緑色)
        Gizmos.color = Color.green;
        Gizmos.DrawLine(startPosition, endPosition);

        // --- 追加: 移動先のポイント描画 ---

        // 移動先に球体を描画 (半透明の緑)
        Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
        Gizmos.DrawSphere(endPosition, 0.5f);

        // 球体の枠線 (不透明の緑)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(endPosition, 0.5f);
    }
}

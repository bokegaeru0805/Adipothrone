using UnityEngine;
using UnityEngine.SceneManagement; // シーン管理のために必要

public class AreaTransition : MonoBehaviour
{
    [SerializeField]
    private Vector2 movePos; //移動位置を保存する変数

    private void Awake()
    {
        if (movePos == Vector2.zero)
        {
            Debug.LogError($"{this.name}のmovePosが設定されていません");
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
                && collision.CompareTag("Player")
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
        Vector3 gizmoSize = transform.localScale; // transformのscaleを直接サイズとして使用

        // 塗りつぶし色を設定 (青で透明度0.2)
        Gizmos.color = new Color(0f, 0f, 1f, 0.2f); // new Color(R, G, B, A) -> (青, 透明度0.2)
        Gizmos.DrawCube(gizmoCenter, gizmoSize); // 塗りつぶし立方体

        // 輪郭線色を設定 (純粋な青)
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(gizmoCenter, gizmoSize); // 輪郭線立方体

        // ギズモの色を設定
        Gizmos.color = Color.green; // 例えば緑色にする

        // このオブジェクトのワールド座標を取得
        Vector3 startPosition = transform.position;

        // movePosはVector2なので、Z座標を0としてVector3に変換
        Vector3 endPosition = new Vector3(movePos.x, movePos.y, startPosition.z);

        // オブジェクトの座標からmovePosまで線を引く
        Gizmos.DrawLine(startPosition, endPosition);
    }
}

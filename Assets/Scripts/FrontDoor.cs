using UnityEngine;

public class FrontDoor : MonoBehaviour
{
    [SerializeField]
    private Vector2 movepos = Vector2.zero; //移動位置を保存する変数

    [SerializeField]
    private DoorOpener.DoorType doorType = DoorOpener.DoorType.None; //ドアの種類

    private void Awake()
    {
        if (movepos == Vector2.zero)
        {
            Debug.LogError($"{this.name}のmoveposが設定されていません");
        }

        if (doorType == DoorOpener.DoorType.None)
        {
            Debug.LogError($"{this.name}のdoorTypeが設定されていません");
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (Time.timeScale > 0)
        {
            //プレイヤーが操作不能状態でない場合のみドアを開く
            if (
                !PlayerManager.instance.isControlLocked
                && InputManager.instance.GetInteract()
                && collision.CompareTag(GameConstants.PLAYER_TAG_NAME)
            )
            {
                DoorOpener.OpenDoor(movepos, this, doorType);
            }
        }
    }

    private void OnDrawGizmos()
    {
        // ギズモの色を設定
        Gizmos.color = Color.green; // 緑色にする

        // Y方向のオフセット値を取得
        float yOffset = 0f;
        // オブジェクトにアタッチされているCollider2Dを取得
        Collider2D col = GetComponent<Collider2D>();

        // Collider2Dが存在する場合
        if (col != null)
        {
            // collider.bounds.extents.y は、コライダーの高さのちょうど半分
            yOffset = col.bounds.extents.y;
        }

        // このオブジェクトのワールド座標を取得
        Vector3 startPosition = transform.position;
        // Y座標にオフセットを加える（ドアの中心から線を引くため）
        startPosition.y += yOffset;

        // movePosはVector2なので、Z座標を0としてVector3に変換し、オフセットを加える
        Vector3 endPosition = new Vector3(movepos.x, movepos.y, startPosition.z);

        // オブジェクトの座標からmovePosまで線を引く
        Gizmos.DrawLine(startPosition, endPosition);

        // --- 移動先のポイント描画 ---

        // 移動先に球体を描画 (半透明の緑)
        Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
        Gizmos.DrawSphere(endPosition, 0.5f);

        // 球体の枠線 (不透明の緑)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(endPosition, 0.5f);
    }
}

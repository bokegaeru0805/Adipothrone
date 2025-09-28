using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FrontDoor : MonoBehaviour
{
    [SerializeField]
    private Vector2 movepos = Vector2.zero; //移動位置を保存する変数

    [SerializeField]
    private DoorOpener.DoorType doorType = DoorOpener.DoorType.None; //ドアの種類

    [SerializeField]
    private List<DoorSpriteData> doorSprites;

    [System.Serializable]
    public class DoorSpriteData
    {
        public DoorOpener.DoorType doorType;
        public Sprite sprite;
    }

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
        else
        {
            // 現在のdoorTypeに対応するスプライトデータをリストから検索
            DoorSpriteData foundSpriteData = doorSprites.FirstOrDefault(data =>
                data.doorType == doorType
            );

            if (foundSpriteData != null)
            {
                // 対応するスプライトデータが見つかった場合、SpriteRendererに設定
                SpriteRenderer spriteRenderer = this.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = foundSpriteData.sprite;
                }
                else
                {
                    Debug.LogWarning(
                        $"{this.name} に SpriteRenderer が見つかりません。スプライトを設定できませんでした。"
                    );
                }
            }
            else
            {
                // 対応するスプライトデータが見つからなかった場合
                Debug.LogWarning(
                    $"{this.name} に設定されたドアタイプ ({doorType}) に対応するスプライトが 'Door Sprites' リスト内に見つかりません。"
                );
            }
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
                && collision.CompareTag(GameConstants.PlayerTagName)
            )
            {
                DoorOpener.OpenDoor(movepos, this, doorType);
            }
        }
    }

    // Gizmosを描画するためのメソッド
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
        // Y座標にオフセットを加える
        startPosition.y += yOffset;

        // movePosはVector2なので、Z座標を0としてVector3に変換し、オフセットを加える
        Vector3 endPosition = new Vector3(movepos.x, movepos.y, startPosition.z);

        // オブジェクトの座標からmovePosまで線を引く
        Gizmos.DrawLine(startPosition, endPosition);
    }
}

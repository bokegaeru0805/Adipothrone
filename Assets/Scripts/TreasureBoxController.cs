using System;
using UnityEngine;

public class TreasureBoxController : MonoBehaviour
{
    [SerializeField, Header("この宝箱の設定データ")]
    private TreasureBoxData boxData = null;

    [SerializeField]
    private Sprite opensprite; //開いている状態のスプライト
    private TreasureBoxName treasureBoxID; //宝箱のID
    private Enum containedItemID = null; //宝箱の中に入っているアイテムのID
    private int itemAmount = 1; //宝箱の中に入っているアイテムの個数
    private bool isBoxOpened = false;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (boxData == null)
        {
            Debug.LogWarning($"{this.gameObject.name}はTreasureBoxDataを持っていません");
            return;
        }

        treasureBoxID = boxData.treasureBoxID;

        var treasureData = GameManager.instance.savedata.TreasureData;
        if (treasureData == null)
        {
            Debug.LogWarning("宝箱に関するセーブデータが存在しません");
            return;
        }

        //宝箱の開封状態を確認
        if (treasureData.GetTreasureOpened(treasureBoxID))
        {
            this.tag = "Untagged"; //tagを外す
            spriteRenderer.sprite = opensprite; //spriteを変更
            isBoxOpened = true;
            return;
        }

        if (BaseItemManager.instance == null)
        {
            Debug.LogWarning("BaseItemManagerが存在しません");
            return;
        }

        //宝箱の中のアイテムのIDをEnum型に変換して取得する
        if (boxData.baseItemData == null)
        {
            Debug.LogWarning(
                $"{boxData.treasureBoxID}は適当なアイテムデータが設定されていない可能性があります"
            );
            return;
        }

        containedItemID = BaseItemManager.instance.GetItemIDFromData(boxData.baseItemData);
        if (containedItemID == null)
        {
            Debug.LogWarning(
                $"{boxData.treasureBoxID}は適当なアイテムIDが設定されていない可能性があります"
            );
        }
        itemAmount = boxData.itemAmount;
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (Time.timeScale > 0 && !isBoxOpened)
        {
            if (
                InputManager.instance.GetInteract()
                && collision.CompareTag(GameConstants.PlayerTagName)
            )
            {
                var treasureData = GameManager.instance.savedata.TreasureData;
                if (treasureData == null)
                {
                    Debug.LogWarning("宝箱に関するセーブデータが存在しません");
                    return;
                }

                isBoxOpened = true; //宝箱を開封した状態にする
                treasureData.SetTreasureOpened(treasureBoxID, true); //セーブデータに開封状況を保存
                //インベントリにアイテムを保存はFungusのFlowchartで行います
                // GameManager.instance.AddAllTypeIDToInventory(containedItemID, itemAmount);

                this.tag = "Untagged"; //tagを外す
                spriteRenderer.sprite = opensprite; //spriteを変更
                GameManager.instance.TreasureFungus(boxData.baseItemData, itemAmount); //Fungusを起動
                SEManager.instance?.PlayFieldSE(SE_Field.OpenTreasurebox1); //宝箱開封時の効果音を鳴らす
            }
        }
    }
}

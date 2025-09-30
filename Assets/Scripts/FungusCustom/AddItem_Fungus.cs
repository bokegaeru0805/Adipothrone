using System;
using System.Collections.Generic;
using Fungus;
using UnityEngine;

[CommandInfo(
    "Item",
    "Add Item",
    "指定したアイテムをインベントリに追加します。取得メッセージと画像を表示できます。"
)]
[AddComponentMenu("")]
public class AddItem_Fungus : Command
{
    [Tooltip("追加するアイテムのデータ（ScriptableObject）")]
    [SerializeField]
    private BaseItemData itemData;

    [Tooltip("追加する個数")]
    [SerializeField]
    private int quantity = 1;
    private bool showAcquisitionDialog = true; //アイテム取得時にメッセージを表示するかどうかの設定

    public void SetItemData(BaseItemData newItemData, int newQuantity)
    {
        itemData = newItemData;
        quantity = newQuantity;
    }

    public override void OnEnter()
    {
        if (itemData == null)
        {
            Debug.LogError("追加するアイテムが設定されていません。");
            Continue();
            return;
        }

        Enum containedItemID = BaseItemManager.instance.GetItemIDFromData(itemData);
        if (containedItemID == null)
        {
            Debug.LogWarning(
                $"{itemData.itemName}は適当なアイテムIDが設定されていない可能性があります"
            );
            Continue();
            return;
        }

        GameManager.instance.AddAllTypeIDToInventory(containedItemID, quantity); //インベントにアイテムを保存
        SEManager.instance?.PlaySystemEventSE(SE_SystemEvent.ItemGet1); // アイテム取得のSEを再生

        //変更点：メッセージ表示がオフなら、ここで処理を終えて次のコマンドへ
        if (!showAcquisitionDialog)
        {
            Continue();
            return;
        }

        // ① GameManagerからアイテムの語頭を取得する
        string itemPrefix = GameManager.instance.GetItemTypePrefix(containedItemID);
        string formattedItemName;

        // ② 語頭が存在する場合は「語頭「アイテム名」」の形式に、なければ「アイテム名」のままにする
        if (!string.IsNullOrEmpty(itemPrefix))
        {
            formattedItemName = $"{itemPrefix}「{itemData.itemName}」";
        }
        else
        {
            formattedItemName = itemData.itemName;
        }

        // ③ 最終的な表示テキストを組み立てる
        string displayText;
        if (quantity == 1)
        {
            displayText = $"{formattedItemName}を手に入れた！";
        }
        else
        {
            displayText = $"{formattedItemName}を\n{quantity}個手に入れた！";
        }

        //③ SayDialogを準備し、表示内容を設定
        SayDialog sayDialog = SayDialog.GetSayDialog();
        if (sayDialog == null)
        {
            Debug.LogError("SayDialogが見つかりません。メッセージを表示できません。");
            Continue(); // ダイアログがなければ、とりあえず次に進む
            return;
        }

        // SayDialogは普段非表示になっているため、コルーチンを開始する前に表示状態にする必要がある
        if (sayDialog.gameObject.activeSelf == false)
        {
            sayDialog.gameObject.SetActive(true);
        }

        // // アイテム表示の際はキャラクター名を消すため、キャラクターをnullに設定
        // sayDialog.SetCharacter(null);
        // SayDialogのキャラクター画像の位置に、アイテムのスプライトを設定
        sayDialog.SetCharacterImage(itemData.itemSprite);

        // ④ SayDialogにテキスト表示を命令
        // SayDialog.Say()は非同期処理。完了後（プレイヤーがクリック後）にContinue()を呼ぶ
        sayDialog.Say(
            displayText,
            true,
            true,
            true,
            true,
            false,
            null,
            () =>
            {
                // この部分は、プレイヤーがダイアログをクリックした後に実行される

                // 表示したアイテム画像をクリアする
                SayDialog.GetSayDialog().SetCharacterImage(null);
                // Flowchartの次のコマンドへ進む
                Continue();
            }
        );
    }

    // GetSummaryとGetButtonColorは変更なし
    public override string GetSummary()
    {
        if (itemData == null)
        {
            return "Error: No item data set";
        }
        return $"Add: {itemData.itemName} (x{quantity})";
    }

    public override Color GetButtonColor()
    {
        return new Color32(255, 235, 150, 255);
    }
}

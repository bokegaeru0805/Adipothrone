using Fungus;
using UnityEngine;

/// <summary>
/// 指定したアイテムを、指定個数だけインベントリから減らすFungusコマンド。
/// </summary>
[CommandInfo("Item", "Remove Item", "指定したアイテムをインベントリから減らします")]
[AddComponentMenu("")]
public class RemoveItemCommand : Command
{
    [Tooltip("減らすアイテムのデータ（ScriptableObject）")]
    [SerializeField]
    private BaseItemData itemData;

    [Tooltip("減らす個数（デフォルトは1）")]
    [SerializeField]
    private IntegerData quantity = new IntegerData(1);

    public override void OnEnter()
    {
        if (itemData == null)
        {
            Debug.LogWarning("RemoveItem: Item Dataが設定されていません。");
            Continue();
            return;
        }

        if (GameManager.instance == null || GameManager.instance.savedata == null)
        {
            Debug.LogError("RemoveItem: GameManagerまたはSaveDataが存在しません。");
            Continue();
            return;
        }

        if (quantity.Value <= 0)
        {
            Debug.LogWarning("RemoveItem: 減らす個数は1以上を指定してください。");
            Continue();
            return;
        }

        System.Enum itemID = itemData.GetItemID();
        int typeNumber = EnumIDUtility.ExtractTypeID(EnumIDUtility.ToID(itemID));
        if (!IsRemovableType(typeNumber))
        {
            Debug.LogWarning($"RemoveItem: {itemData.itemName}は削除に対応していない種類です。");
            Continue();
            return;
        }

        int currentAmount = GameManager.instance.GetAllTypeIDToAmount(itemID);
        if (currentAmount < quantity.Value)
        {
            Debug.LogWarning(
                $"RemoveItem: {itemData.itemName}の所持数が不足しています。"
                    + $" 所持数: {currentAmount}, 必要数: {quantity.Value}"
            );
            Continue();
            return;
        }

        GameManager.instance.RemoveAllTypeIDFromInventory(itemData, quantity.Value);
        Continue();
    }

    private bool IsRemovableType(int typeNumber)
    {
        switch (typeNumber)
        {
            case (int)TypeID.Blade:
            case (int)TypeID.Shoot:
            case (int)TypeID.HealItem:
            case (int)TypeID.StatusEnhanceItem:
            case (int)TypeID.MaterialItem:
            case (int)TypeID.KeyItem:
                return true;
            default:
                return false;
        }
    }

    public override string GetSummary()
    {
        if (itemData == null)
        {
            return "Error: No item data selected";
        }

        return $"Remove: {itemData.itemName} (x{quantity.Value})";
    }

    public override Color GetButtonColor()
    {
        return new Color32(255, 235, 150, 255);
    }
}

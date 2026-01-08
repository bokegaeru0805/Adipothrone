using Fungus;
using UnityEngine;

/// <summary>
/// 指定したアイテム（ScriptableObject）を、指定個数以上持っているかを判定するFungusコマンド。
/// </summary>
[CommandInfo("Flow", "If Has Item", "指定したアイテムを所持しているか判定します")]
[AddComponentMenu("")]
public class IfHasItemCommand : Condition
{
    [Tooltip("判定したいアイテムのデータ (HealItemData, KeyItemDataなど)")]
    [SerializeField]
    protected BaseItemData targetItemData;

    [Tooltip("必要な個数 (デフォルトは1)")]
    [SerializeField]
    protected IntegerData requiredAmount = new IntegerData(1);

    protected override bool EvaluateCondition()
    {
        if (targetItemData == null)
        {
            Debug.LogWarning("IfHasItem: Item Dataが設定されていません。");
            return false;
        }

        if (GameManager.instance == null)
        {
            Debug.LogError("IfHasItem: GameManagerが存在しません。");
            return false;
        }

        // 1. ScriptableObjectからアイテムID (Enum) を取り出す
        System.Enum itemID = targetItemData.GetItemID();

        if (itemID == null)
        {
            Debug.LogError($"IfHasItem: サポートされていないアイテムデータ型です: {targetItemData.GetType().Name}");
            return false;
        }

        // 2. GameManagerを使って所持数を取得
        int currentAmount = GameManager.instance.GetAllTypeIDToAmount(itemID);

        // 3. 判定 (所持数 >= 必要数)
        return currentAmount >= requiredAmount.Value;
    }

    public override string GetSummary()
    {
        if (targetItemData == null)
        {
            return "Error: No item data selected";
        }

        return $"{targetItemData.itemName} >= {requiredAmount.Value}";
    }
    
    public override Color GetButtonColor()
    {
        return new Color32(253, 253, 150, 255); // 薄い黄色（If系コマンドの色）
    }
}
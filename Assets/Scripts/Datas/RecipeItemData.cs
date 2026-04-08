using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 合成に必要な素材と個数のペア
/// </summary>
[System.Serializable]
public class CraftingMaterial
{
    #region エディタ表示用
    [HideInInspector]
    public string _inspectorLabel; // Unityの仕様を利用してインスペクターの要素名にするための隠し変数
    #endregion

    [Header("素材アイテム")]
    public BaseItemData item;

    [Header("必要個数")]
    public int requiredAmount = 1;
}

/// <summary>
/// レシピアイテムのデータ本体。BaseItemDataを継承。
/// </summary>
[CreateAssetMenu(fileName = "NewRecipeItem", menuName = "Items/RecipeItem")]
public class RecipeItemData : BaseItemData
{
    public RecipeItemName itemID; // レシピのID（Enum）

    [Header("必要な素材のリスト")]
    public List<CraftingMaterial> materials = new List<CraftingMaterial>();

    [Header("合成後に完成するアイテム")]
    public BaseItemData craftedItem;

    [Header("最大合成可能回数 (0以下の場合は無制限)")]
    public int maxCraftCount = 0;

    public override System.Enum GetItemID()
    {
        return itemID;
    }

    /// <summary>
    /// このレシピが無制限に合成可能かどうかを判定します
    /// </summary>
    public bool IsUnlimitedCrafting()
    {
        return maxCraftCount <= 0;
    }

    #region エディタ専用処理
#if UNITY_EDITOR
    /// <summary>
    /// インスペクターで値が変更されたときに自動で呼ばれるメソッド
    /// </summary>
    private void OnValidate()
    {
        // 1. レシピ名自身の自動更新処理
        // craftedItemがセットされていれば、そのアイテム名に自動更新する
        if (craftedItem != null && !string.IsNullOrEmpty(craftedItem.itemName))
        {
            // 完全に同じ名前にする場合
            itemName = craftedItem.itemName;

            // 「〜のレシピ」のように少しアレンジを加えたい場合はこちら
            // itemName = $"{craftedItem.itemName}のレシピ";
        }

        // 2. 素材リストの表示用ラベル更新処理
        if (materials != null)
        {
            foreach (var mat in materials)
            {
                if (mat == null)
                    continue;

                if (mat.item != null)
                {
                    // 例: "薬草 × 3" のように表示させる
                    mat._inspectorLabel = $"{mat.item.itemName} × {mat.requiredAmount}";
                }
                else
                {
                    mat._inspectorLabel = "未設定 (Empty)";
                }
            }
        }
    }
#endif
    #endregion
}

using UnityEngine;

#region 派生クラス：素材アイテムパネル
/// <summary>
/// 素材アイテムを管理するパネルクラス
/// プロンプト表示などの特殊な機能を持たない、最もシンプルな実装です。
/// </summary>
public class MaterialItemPanelActive : ItemPanelActiveBase
{
    #region 基底クラスの実装
    /// <summary>
    /// このパネルが扱うアイテムタイプを指定します
    /// </summary>
    protected override InventoryItemData.ItemType TargetItemType =>
        InventoryItemData.ItemType.MaterialItem;
    #endregion
}
#endregion

using UnityEngine;

public abstract class BaseItemData : ScriptableObject
{
    public string itemName;
    public Sprite itemSprite;
    public ItemRank itemRank; // アイテムのランク(レア度)
    public int buyPrice; // 購入価格
    public int sellPrice; // 売却価格
    public bool isSellable = true; // 売却可能かどうかのフラグ (デフォルトはtrue)

    [TextArea]
    public string description;
}

public enum ItemRank
{
    None = 0, // ランクなし
    E = 100,
    D = 200,
    C = 300,
    B = 400,
    A = 500,
    S = 600,
}

using System.Collections.Generic;
using UnityEngine;

// 強化対象のステータス
public enum EnhanceTargetStatus
{
    HP = 1,
    Attack = 2,
    Defense = 3,
    Speed = 4,
    Luck = 5,
}

// 強化の種類（将来的な拡張用）
public enum EnhanceType
{
    MaxLevelUp = 1, // 最大レベル（上限）を上げる
    // BonusValueUp   // 将来用：レベルごとの上昇値自体を底上げする
}

/// <summary>
/// 強化効果の具体的な内容を定義するクラス。
/// インスペクター上でリストとして複数設定できるようにします。
/// </summary>
[System.Serializable]
public class EnhanceEffect
{
    [Header("強化するステータス")]
    public EnhanceTargetStatus targetStatus;

    [Header("強化の種類")]
    public EnhanceType enhanceType = EnhanceType.MaxLevelUp;

    [Header("上昇量")]
    public int amount = 1; // 通常は1レベルずつ上げる
}

/// <summary>
/// ステータス強化アイテムのデータ本体。BaseItemDataを継承します。
/// </summary>
[CreateAssetMenu(fileName = "NewEnhanceItem", menuName = "Items/EnhanceItem")]
public class StatusEnhanceItemData : BaseItemData
{
    public EnhanceItemName itemID; // アイテムのID

    // 1つのアイテムが持つ複数の効果（例：攻撃力と防御力を同時に上げる等）
    [Header("強化効果のリスト")]
    public List<EnhanceEffect> enhanceEffects = new List<EnhanceEffect>();

    public override System.Enum GetItemID()
    {
        return itemID;
    }
}

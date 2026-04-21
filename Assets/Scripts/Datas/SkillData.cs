using UnityEngine;

/// <summary>
/// スキルのカテゴリ
/// </summary>
public enum SkillCategory
{
    Basic, // 基本型
    Exploration, // 探索型
    Attack, // 攻撃型
    Defense, // 防御型
    Luck, // 幸運型
    Item, // アイテム型
    Special // 特殊型
    ,
}

/// <summary>
/// スキルの基本データを定義するScriptableObject
/// アイテムとは完全に切り離した独立した概念として作成
/// </summary>
[CreateAssetMenu(fileName = "NewSkillData", menuName = "Skills/SkillData")]
public class SkillData : ScriptableObject, IItemIDProvider
{
    public SkillName skillID;

    [Header("基本情報")]
    public string skillName;

    [Header("カテゴリ")]
    public SkillCategory category;

    [TextArea]
    public string description;

    // public Sprite icon; // UI表示用のアイコンなどが必要であれば追加

    [Header("コスト")]
    [Tooltip("このスキルを装備(有効化)するために必要なスキルポイント")]
    public int requiredPoints = 1;

    public System.Enum GetItemID()
    {
        return skillID;
    }
}

using UnityEngine;

/// <summary>
/// スキルのカテゴリ
/// </summary>
public enum SkillCategory
{
    None = 0,
    Basic = 10, // 基本型
    Exploration = 20, // 探索型
    Attack = 30, // 攻撃型
    Defense = 40, // 防御型
    Luck =  50, // 幸運型
    Item  = 60, // アイテム型
    Special  = 70 // 特殊型
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

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スキルのカテゴリ
/// </summary>
public enum SkillCategory
{
    None = 0,
    Mobility = 10, // 機動型
    Exploration = 20, // 探索型
    Attack = 30, // 攻撃型
    Defense = 40, // 防御型
    Reward = 50, // 報酬型
    Tool = 60, // 道具型
    Special = 70, // 特殊型
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
    [Min(0)]
    public int requiredPoints = 1;

    [Header("成長・装備条件")]
    [Tooltip("このスキルが到達できる最大レベル")]
    [Min(1)]
    public int maxLevel = 1;

    [Tooltip("装備するために解放が必要なスキル")]
    public List<SkillName> prerequisiteSkills = new List<SkillName>();

    [Tooltip("同じ0以外の値を持つスキル同士は同時に装備できません")]
    [Min(0)]
    public int exclusiveGroupID = 0;

    public System.Enum GetItemID()
    {
        return skillID;
    }
}

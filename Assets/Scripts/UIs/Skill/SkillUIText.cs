/// <summary>
/// Skill UIで使用する表示文言を一か所に集約します。
/// </summary>
public static class SkillUIText
{
    public const string LockedName = "？？？";
    public const string LockedDescription = "条件を満たすと詳細が判明します。";

    public static string GetCostText(int requiredPoints)
    {
        return $"コスト: {requiredPoints}";
    }

    public static string GetCategoryName(SkillCategory category)
    {
        switch (category)
        {
            case SkillCategory.Basic:
                return "基本型";
            case SkillCategory.Exploration:
                return "探索型";
            case SkillCategory.Attack:
                return "攻撃型";
            case SkillCategory.Defense:
                return "防御型";
            case SkillCategory.Luck:
                return "幸運型";
            case SkillCategory.Item:
                return "アイテム型";
            case SkillCategory.Special:
                return "特殊型";
            default:
                return category.ToString();
        }
    }

    public static string GetEquipFailureMessage(SkillEquipResult result)
    {
        switch (result)
        {
            case SkillEquipResult.NotEnoughPoints:
                return "スキルポイントが足りません。";
            case SkillEquipResult.PrerequisiteNotMet:
                return "前提スキルが解放されていません。";
            case SkillEquipResult.ExclusiveSkillEquipped:
                return "同時に装備できないスキルが装備されています。";
            case SkillEquipResult.NotUnlocked:
                return "未解放のスキルです。";
            case SkillEquipResult.AlreadyEquipped:
                return "すでに装備されています。";
            default:
                return "スキルを装備できませんでした。";
        }
    }
}

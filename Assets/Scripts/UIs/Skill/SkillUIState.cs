/// <summary>
/// Skill UIへ渡す実行時専用の表示状態です。SaveDataには保存しません。
/// </summary>
public readonly struct SkillUIState
{
    public SkillData SkillData { get; }
    public SkillName SkillID => SkillData != null ? SkillData.skillID : SkillName.None;
    public bool IsUnlocked { get; }
    public bool IsEquipped { get; }
    public bool IsNew { get; }
    public int Level { get; }
    public int RequiredPoints => SkillData != null ? SkillData.requiredPoints : 0;

    public SkillUIState(
        SkillData skillData,
        bool isUnlocked,
        bool isEquipped,
        bool isNew,
        int level
    )
    {
        SkillData = skillData;
        IsUnlocked = isUnlocked;
        IsEquipped = isEquipped;
        IsNew = isNew;
        Level = level;
    }
}

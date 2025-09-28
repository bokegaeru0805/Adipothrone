using System.Collections.Generic;

public enum StatusEffectType
{
    Attack = 1,
    Defense = 2,
    Speed = 3,
    Luck = 4,
    Poison = 21,
}

public enum StatusEffectRank
{
    none = 0,
    I = 1,
    II = 2,
    III = 3,
}

public static class StatusEffectUtility
{
    // 効果ランクごとの持続時間（秒）
    private static readonly Dictionary<StatusEffectRank, float> rankDurations =
        new()
        {
            { StatusEffectRank.I, 10f },
            { StatusEffectRank.II, 30f },
            { StatusEffectRank.III, 60f },
        };

    /// <summary>
    /// 指定されたランクに対応する効果時間（秒）を返す
    /// </summary>
    public static float GetDurationByRank(StatusEffectRank rank)
    {
        return rankDurations.TryGetValue(rank, out float duration) ? duration : 0f;
    }

    private static readonly Dictionary<StatusEffectType, string> typeNames =
        new()
        {
            { StatusEffectType.Attack, "攻撃力上昇" },
            { StatusEffectType.Defense, "防御力上昇" },
            { StatusEffectType.Speed, "素早さ上昇" },
            { StatusEffectType.Luck, "幸運上昇" },
        };

    private static readonly Dictionary<StatusEffectRank, string> rankNames =
        new()
        {
            { StatusEffectRank.I, "Level1" },
            { StatusEffectRank.II, "Level2" },
            { StatusEffectRank.III, "Level3" },
        };

    public static string GetDisplayName(
        StatusEffectType type,
        StatusEffectRank rank = StatusEffectRank.none
    )
    {
        string typeName = typeNames.TryGetValue(type, out var tName) ? tName : "特になし";
        string rankName = rankNames.TryGetValue(rank, out var rName) ? rName : "";

        return typeName + rankName;
    }
}

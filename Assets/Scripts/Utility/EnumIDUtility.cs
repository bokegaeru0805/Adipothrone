using System;
using System.Collections.Generic;

public static class EnumIDUtility
{
    public static int ToID<T>(T value)
        where T : Enum
    {
        return Convert.ToInt32(value);
    }

    public static Enum FromID(int id)
    {
        int typeID = ExtractTypeID(id);

        return typeID switch
        {
            0 => null,
            (int)TypeID.Blade => (BladeName)id,
            (int)TypeID.Shoot => (ShootName)id,
            (int)TypeID.HealItem => (HealItemName)id,
            (int)TypeID.ProgressLog => (ProgressLogName)id,
            (int)TypeID.Tips => (TipsName)id,
            _ => throw new ArgumentException($"不明なID種別: {id}"),
        };
    }

    public static int ExtractTypeID(int id)
    {
        return (id / 1000) % 100;
    }
}

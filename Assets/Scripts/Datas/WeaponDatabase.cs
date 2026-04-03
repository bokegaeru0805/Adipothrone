using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponItemDatabase", menuName = "Weapons/WeaponItem Database")]
public class WeaponItemDatabase : ScriptableObject
{
    public List<ShootWeaponData> shoots = new List<ShootWeaponData>();
    public List<BladeWeaponData> blades = new List<BladeWeaponData>();

    public ShootWeaponData GetShootByID(Enum id)
    {
        if (id is ShootName shootID)
        {
            return shoots.Find(s => s.weaponID == shootID);
        }

        return null;
    }

    public BladeWeaponData GetBladeByID(Enum id)
    {
        if (id is BladeName bladeID)
        {
            return blades.Find(b => b.weaponID == bladeID);
        }

        // 型が違う場合は null などを返す（必ず return が必要）
        return null;
    }

    /// <summary>
    /// 武器IDから対応するWeaponData（ShootまたはBlade）を取得
    /// </summary>
    public WeaponData GetWeaponData(Enum id)
    {
        if (id is ShootName)
        {
            return GetShootByID(id);
        }
        else if (id is BladeName)
        {
            return GetBladeByID(id);
        }

        Debug.LogWarning($"未対応の武器ID: {id}");
        return null;
    }

    /// <summary>
    /// データベース上の武器の並び順（インデックス）を取得します。
    /// UIでのソートなどに使用します。
    /// </summary>
    public int GetWeaponIndex(Enum id)
    {
        if (id is ShootName shootID)
        {
            int index = shoots.FindIndex(s => s.weaponID == shootID);
            return index != -1 ? index : int.MaxValue; // 見つからない場合は一番最後に回す
        }
        else if (id is BladeName bladeID)
        {
            int index = blades.FindIndex(b => b.weaponID == bladeID);
            return index != -1 ? index : int.MaxValue;
        }

        return int.MaxValue;
    }
}

using System;

//注意
//新しい要素を追加したらWeaponDataEditor.csも修正すること
[Serializable]
public abstract class WeaponData : BaseItemData
{
    public float wpCost; // WP消費量
}
using System;

//注意
//新しい要素を追加したらWeaponDataEditor.csも修正すること
[Serializable]
public class WeaponData : BaseItemData
{
    public float wpCost; // WP消費量
}


// [CreateAssetMenu(fileName = "WaveWeapon", menuName = "Weapons/WaveWeapon")]
// public class WaveWeaponData : WeaponData
// {
//     public float vanishTime; //消滅時間(秒)
//     public float maxRadius; //最大半径
// }
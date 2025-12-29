using UnityEngine;

[CreateAssetMenu(fileName = "BladeWeapon", menuName = "Weapons/BladeWeapon")]
public class BladeWeaponData : WeaponData
{
    public BladeName weaponID; //ID

    [Tooltip("この武器を使用した際の攻撃モーションデータ")]
    public BladeAttackActionData attackActionData;
    public int power; //武器の攻撃力

    [Tooltip("攻撃のクールダウン時間(秒)")]
    public float cooldownTime;
    public Vector2 ColliderOffset; //Colliderの座標offset
    public Vector2 ColliderSize; //Colliderの大きさ
}

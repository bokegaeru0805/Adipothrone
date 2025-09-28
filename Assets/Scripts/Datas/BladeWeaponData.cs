using UnityEngine;

[CreateAssetMenu(fileName = "BladeWeapon", menuName = "Weapons/BladeWeapon")]
public class BladeWeaponData : WeaponData
{
    public BladeName weaponID; //ID
    public int power; //武器の攻撃力
    public float cooldownTime; //敵に当たってから次の敵に当たるまでの時間
    public float attackTime; //一回の振りにかかる時間(秒)
    public Vector2 ColliderOffset; //Colliderの座標offset
    public Vector2 ColliderSize; //Colliderの大きさ
}
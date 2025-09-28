using UnityEngine;

[CreateAssetMenu(fileName = "ShootWeapon", menuName = "Weapons/ShootWeapon")]
public class ShootWeaponData : WeaponData
{
    public ShootName weaponID; //ID
    public int power; //武器の攻撃力
    public float cooldownTime; //敵に当たってから次の敵に当たるまでの時間
    public float shootSpeed; //弾の速度
    public float vanishTime; //消滅時間(秒)
    public float shotInterval; // 発射間隔
    public int penetrationLimitCount; //貫通できるオブジェクトの数
    public ShootMoveType moveType; //弾の移動タイプ
    public Vector2 colliderOffset; //Colliderの座標offset
    public float colliderRadius; //Colliderの半径
    public AnimationClip shootAnimation; //発射アニメーション

    public enum ShootMoveType
    {
        None = 0, //なし
        Straight = 10, // 直線
        Parallel3Way = 20, // 3方向に平行散弾
    }
}

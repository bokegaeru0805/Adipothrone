using NaughtyAttributes;
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

    [Header("山なり軌道(Parabola)用設定")]
    [Tooltip("重力の強さ（大きいほど早く落下します）")]
    [AllowNesting]
    [ShowIf(nameof(moveType), ShootMoveType.Parabola)]
    public float gravityScale = 1.0f;

    [Tooltip("発射時の上方向への打ち出し角度（度数法）")]
    [AllowNesting]
    [ShowIf(nameof(moveType), ShootMoveType.Parabola)]
    public float upwardAngle = 30.0f;

    public enum ShootMoveType
    {
        None = 0, //なし
        Straight = 10, // 直線
        Parallel3Way = 20, // 3方向に平行散弾
        Parabola = 30, // 山なり（放物線）
    }

    public override System.Enum GetItemID()
    {
        return weaponID;
    }
}

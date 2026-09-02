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

    [Header("ブーメラン軌道用設定")]
    [Min(0.01f)]
    public float boomerangFlyTime = 2.0f; // 発射からロボットへ戻るまでの時間

    [Min(0f)]
    public float boomerangDistance = 6.0f; // 進行方向へ膨らむ距離

    [Min(0f)]
    public float boomerangCurveWidth = 3.0f; // 軌道の上下方向への膨らみ

    public bool isBoomerangOverhand = true; // trueなら上回り、falseなら下回り
    public float boomerangMinYOffset = -3.0f; // 発射位置を基準とした最低高度

    [Min(0f)]
    public float boomerangRotationSpeed = 720f; // 1秒あたりの回転角度

    [Min(1)]
    public int maxActiveBoomerangCount = 1; // 同時に発射できる最大数

    [Header("バウンド軌道用設定")]
    [Range(0f, 90f)]
    public float bouncingLaunchAngle = 45f; // 発射角度

    [Min(0f)]
    public float bouncingGravityScale = 1f; // 落下に使用する重力倍率

    [Min(0)]
    public int bouncingMaxCount = 3; // 地面でバウンドできる最大回数

    [Min(0f)]
    public float bouncingHeight = 2f; // バウンド後の最高到達点までの高さ

    public enum ShootMoveType
    {
        None = 0, //なし
        Straight = 10, // 直線
        Parallel3Way = 20, // 3方向に平行散弾
        Parabola = 30, // 山なり（放物線）
        Boomerang = 40, // ロボットの現在位置へ戻るブーメラン
        Bouncing = 50, // 地面で複数回跳ねる弾
    }

    public override System.Enum GetItemID()
    {
        return weaponID;
    }
}

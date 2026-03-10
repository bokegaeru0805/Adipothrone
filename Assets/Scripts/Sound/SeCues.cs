/// <summary>
/// SEのカテゴリ分類
/// </summary>
public enum SECategory
{
    UI = 1000,
    PlayerAction = 1005,
    EnemyAction = 1010,
    Field = 1015,
    SystemEvent = 1020,
}

/// <summary>
/// UI操作系SE
/// </summary>
public enum SE_UI
{
    Beep1 = 2000,
    Complete1 = 2005,
    Decision1 = 2010,
    WeaponDecision1 = 2015,
    Register1 = 2020,
    DialogVoice = 2025,
}

/// <summary>
/// プレイヤーアクション系SE
/// </summary>
public enum SE_PlayerAction
{
    Boot1 = 3000,
    Bound1 = 3005,
    Bound2 = 3010,
    Bound3 = 3011,
    Damage1 = 3015,
    Eat1 = 3020,
    GichiGichi1 = 3025,
    MuchiMuchi = 3030,
    Shoot1_Player = 3035,
    ShockWave1 = 3040,
    Swing1 = 3045,
    Swing2 = 3050,
    Walk1 = 3055,
    SoftBounce1 = 3060,
    Jump1 = 3065,
    Land1 = 3070,
    HealItem1 = 3075,
    Hit1 = 3080,
    Buff1 = 3085,
    AttackMiss1 = 3090,
    Death1 = 3095,
    FallDown1 = 3100,
    Shoot_Fill = 3105,
    ShieldDeploy1 = 3110,
    Reflection1 = 3115,
    ShieldBreak1 = 3120,
}

/// <summary>
/// 敵アクション系SE
/// </summary>
public enum SE_EnemyAction
{
    ChargePower1 = 4000,
    Damage2 = 4005,
    FastMove1 = 4010,
    Roar1 = 4015,
    Shoot1_Enemy = 4020,
    Shoot2_Enemy = 4021,
    Shoot_Water1 = 4025,
    Impact_iron1 = 4035,
    Attack_slime1 = 4040,
    Attack_slime_boss = 4041,
    Attack_fly1 = 4045,
    Kick1 = 4050,
    Land_enemy1 = 4055,
    MagicWave1 = 4060,
    SwordSlash1 = 4065,
    SwordSlash2 = 4066,
    SwordSlash3 = 4067,
    SwordThrow1 = 4070,
    RareEnemyAppear = 4075,
    Death1 = 4080,
    Walk1 = 4085,
    Attack_wind1 = 4090,
    Attack_throw1 = 4095,
    SandEmerge = 4100,
    SandSubmerge = 4101,
    Drop_Metal = 4102,
    GearTurn = 4103,
    LaserAttack1 = 4105,
    Spawn1 = 4110,
    ShieldCrack1 = 4115,
    SheldBreak1 = 4120,
}

/// <summary>
/// ギミック・環境音系SE
/// </summary>
public enum SE_Field
{
    DoorLock = 5000,
    DoorOpen_Metal = 5005,
    DoorOpenLock = 5010,
    OpenTreasurebox1 = 5015,
    Collapse1 = 5020,
    Collapse2 = 5021,
    Collapse3 = 5022,
    SmallBomb = 5030,
    SmallCollapse = 5035,
    SwitchOn = 5040,
    WaterDrip1 = 5045,
    WaterDrop1 = 5050,
    CoinGet1 = 5055,
    FlameOn = 5060,
    FlameOff = 5061,
    GroundRumble1 = 5065,
    LiftMove_Wood = 5070,
    FireBurning1 = 5075,
    WindGust_weak = 5080,
    WindGust_strong = 5081,
    Sand1 = 5085,
    Clash_WaterGlass = 5090,
    WaterMove1 = 5095,
    LaserShoot = 5100,
    LaserExpand = 5101,
    SawBlade = 5105,
    HorseWalk = 5106,
    HorseCarriage = 5107,
    BreakCultivationTank = 5108,
    InsertDevice1 = 5110,
    LeverPull1 = 5115,
    LightOn1 = 5120,
}

/// <summary>
/// システムイベント・演出系SE
/// </summary>
public enum SE_SystemEvent
{
    Impact1 = 6000,
    Quake = 6005,
    Vanish1 = 6010,
    Warning1 = 6015,
    Warp1 = 6020,
    WarpStandby1 = 6025,
    ItemGet1 = 6030,
    ItemGet2 = 6035,
    Effect_Buff = 6040,
    CashRegister = 6045,
    LevelUp = 6050,
    Flash1 = 6055,
}

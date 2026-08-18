/// <summary>
/// KeyID を新しく追加した場合、
/// ・このループで自動的に初期化されるので個別に処理を追加する必要はありません。
/// ・ただし、対応するドア条件（doorConditions）のリストには別途追加が必要です。
/// </summary>
public enum KeyID
{
    K1_1 = 101,

    K2_1 = 201,
    K2_2 = 202,
    K2_3 = 203,

    // K3_1 = 301,
    // K3_2 = 302,
    // K3_3 = 303,

    //--チュートリアルステージのクリスタルのキー--
    K4_1 = 401,
    K4_2 = 402,
    K4_3 = 403,

    // --- 砂漠の神殿のそれぞれのステージへのキー ---
    K5 = 501,
    K6 = 601,

    // --- 砂漠の神殿Stage1-2のキー ---
    K7_1 = 701,
    K7_2 = 702,
    K7_3 = 703,
    K7_4 = 704,

    // --- 砂漠の神殿のそれぞれのステージからのキー
    K8 = 801,
    K9 = 901,
    K10 = 1001,

    // --- 雪国の洞窟のキー ---
    K11_1 = 1101,
    K11_2 = 1102,
    K11_3 = 1103,
    K11_4 = 1104,

    // --- 塔の扉のキー ---
    K12 = 1201,
}

/// 【重要】
/// 新たに Enum で管理するフラグ（例：Chapter1Event, Chapter2Event など）を追加した場合、
/// 以下の3つの処理をFlagManagerに忘れずに更新してください：
///
/// 1. InitializeAllEnums に初期化関数を追加（ゲーム開始時に登録される）
/// 2. LoadFlagData に LoadBoolFlags / LoadIntFlags の呼び出しを追加（セーブから復元される）
/// 3. SaveFlagData は型を問わずEnum→int変換で自動対応するため、追加は不要
/// 4. FlagConditionDrawerPro.cs の boolEnumTypes / intEnumTypes に新しいEnum型を追加
/// 5. FungusSetGameBoolFlag.cs の FlagCategory enum と各章のフラグ変数を追加
/// 6. Timeline/BoolFlagフォルダ内の各スクリプトに新しいEnum型とフラグ変数を追加
/// 7. GimmickSwitch.cs の boolEnumTypes / intEnumTypes に新しいEnum型を追加
/// 8. FlagSearchToolWindow.cs のtargetEnumTypesに新しいEnum型を追加


/// <summary>
///プロローグステージのbool型のフラグ
/// </summary>
public enum PrologueTriggeredEvent
{
    // 進行イベント
    PrologueStart = 001, // プロローグが始まった
    PrologueEndStart = 002, // プロローグの終わりが始まった
    TutorialStart = 003, // チュートリアル開始
    RobotEncounter = 004, // ロボットと出会った
    CrystalQuestComplete = 007, // クリスタルのクエストを完了した
    SecondPrologueStart = 005, // 2回目のプロローグが始まった
    SecondPrologueEndStart = 006, // 2回目のプロローグの終わりが始まった

    //マップ系イベント
    Stage1EnterEnemyRoom = 101, // 敵部屋に初侵入
    WomanEventStart = 102, // 謎の女性イベント発生
    RockDebrisField1Reached = 103, // 岩のがれきフィールドに到達

    //人物系イベント


    // 敵・ボスイベント
    DefeatTutorialGolem = 301, // ゴーレム撃破
    BeforeFirstBoss = 302, // 初ボス直前イベント
    FirstBossAppear = 303, // 初ボス出現
    DefeatFirstBoss = 304, // 初ボス撃破

    //その他イベント
    TutorialEventDoorOpened = 401, // チュートリアル用ドア開放
    OpenTreasurebox = 402, // 宝箱を開けた
}

/// <summary>
/// プロローグステージのint型のフラグ
/// </summary>
public enum PrologueCountedEvent
{
    // 進行イベント
    DonutMountainCount =
        501 // ドーナツの山を食べた回数
    ,
}

/// <summary>
/// 第一章ステージのbool型のフラグ
/// </summary>
public enum Chapter1TriggeredEvent
{
    // 進行イベント
    Chapter1Start = 1001, // 第一章が始まった
    VillageTourComplete = 1003, // 村の観光が完了した
    WellQuestReceived = 1005, // 井戸のクエストを受け取った
    WellQuestComplete = 1006, // 井戸のクエストを完了した
    RiverQuestReceived = 1009, // 川のクエストを受け取った
    RockDestructionRequested = 1012, // 岩の破壊を依頼した
    HeardRumorAboutShopGirl = 1015, // ショップの女の子についての噂を聞いた
    LeftVillageForDesert = 1016, // 砂漠へ村を出発した

    // マップ系イベント
    FirstEnteredVillage = 1002, // 初めて村に入った
    UpperRiverReached = 1104, // 上流の川に到達
    ShopGirlHouseUnlocked = 1107, // ショップの女の子の家が解放された

    // 人物系イベント
    ShopGirlMissing = 1201, // ショップの女の子がいなくなった
    ShopGirlFirstMet = 1202, // ショップの女の子と初めて会った
    Girl2ItemReceived1 = 1205, // 女の子2からアイテムを受け取った1
    TalkedToShopGirlAfterCaveBoss = 1206, // 洞窟のボス後にショップの女の子と話した

    // 敵・ボスイベント
    WellEnemyEncounter = 1301, // 井戸の敵と遭遇
    WellEnemyDefeated = 1303, // 井戸の敵を撃破
    BeforeRiverBoss = 1306, // 川のボス直前イベント
    RiverBossAppear = 1307, // 川のボス出現
    RiverBossDefeated = 1308, // 川のボス撃破
    BeforeCaveBoss = 1309, // 洞窟のボス直前イベント
    CaveBossAppear = 1310, // 洞窟のボス出現
    CaveBossDefeated = 1311, // 洞窟のボス撃破
    // その他イベント
}

/// <summary>
/// 第一章ステージのint型のフラグ
/// </summary>
public enum Chapter1CountedEvent
{
    // 進行イベント
}

/// <summary>
/// 第二章ステージのbool型のフラグ
/// </summary>
public enum Chapter2TriggeredEvent
{
    None = 0, // 何も起こっていない状態を表すフラグ（デフォルト値として使用）
    Chapter2Start = 2001, // 第二章が始まった
    FirstEnteredVillage = 2002, // 初めて村に入った
    FirstMetCoachman = 2005, // 初めて御者に会った
    FirstMetAlchemistess = 2006, // 初めて錬金術師に会った
    FirstEnteredWaterSourceFrontField = 2007, // オアシスの源泉前フィールドに初めて入った
    OasisSpringEnemiesDefeated = 2008, // オアシスの源泉の敵を全て倒した
    AttemptedToReportCoachmanQuest = 2009, // 御者にクエスト完了の報告をしようとした
    VillageInquiryComplete1 = 2010, // 村で聞き込みを完了した(1)
    VillageInquiryComplete2 = 2011, // 村で聞き込みを完了した(2)
    VillageInquiryComplete3 = 2012, // 村で聞き込みを完了した(3)
    ReportedCoachmanQuestComplete = 2014, // 御者にクエスト完了の報告をした
    FirstMetDesertTempleBoss = 2015, // 砂漠の神殿のボスと初めて会った
    OasisDriedUpByDesertTempleBoss = 2016, // 砂漠の神殿のボスによってオアシスが干上がった
    FirstEnteredDeepDesert = 2021, // 砂漠の奥地に初めて入った
    BeforeDustDevilBoss = 2023, // 砂嵐のボス直前イベント
    DustDevilBossDefeated = 2025, // 砂嵐のボスを倒した
    OasisPartiallyRestoredByFill = 2030, // Fillによってオアシスが部分的に復活した
    BeforeEnteringDesertTemple = 2033, // 砂漠の神殿に入る前のイベント
    TempleBossSmokeDefeated = 2035, // 砂漠の神殿のボス（煙）を倒した
    BlueOrbPlacedInDevice = 2036, // 青いオーブを装置に置いた
    GreenOrbPlacedInDevice = 2040, // 緑のオーブを装置に置いた
    HeardHintAboutGreenOrb = 2041, // 緑のオーブについてのヒントを聞いた
    OrangeOrbPlacedInDevice = 2050, // オレンジのオーブを装置に置いた
    HeardHintAboutOrangeOrb = 2051, // オレンジのオーブについてのヒントを聞いた
    PurpleOrbPlacedInDevice = 2060, // 紫のオーブを装置に置いた
    HeardHintAboutPurpleOrb = 2061, // 紫のオーブについてのヒントを聞いた
    ReceivedInfoAboutPurpleOrbFromFill = 2062, // Fillから紫のオーブについての情報を得た
    FirstMetLotteryManager = 2065, // くじ屋の店主と初めて会った
    AllOrbsPlacedInDevice = 2070, // 全てのオーブを装置に置いた
    TalkedToFillAfterAllOrbsPlaced = 2075, // 全てのオーブを置いた後にFillと話した
    TempleBossDefeated = 2080, // 砂漠の神殿のボスを倒した
    TalkedToFillBeforeParting = 2085, // 別れの前にFillと話した
    TalkedToCoachmanBeforeLeavingDesert = 2086, // 砂漠を出発する前に御者と話した
    LeftDesertForRoyalCapital = 2088, // 王都へ向けて砂漠を出発した
}

/// <summary>
/// 第二章ステージのint型のフラグ
/// </summary>
public enum Chapter2CountedEvent { }

/// <summary>
/// 第三章ステージのbool型のフラグ
/// </summary>
public enum Chapter3TriggeredEvent
{
    None = 0, // 何も起こっていない状態を表すフラグ（デフォルト値として使用）
    FirstEnteredGuild = 3001, // 初めてギルドに入った
    FirstMetGuildReceptionist = 3004, // 初めてギルドの受付嬢に会った
    GuildInquiryComplete1 = 3007, // ギルドで聞き込みを完了した(1)
    GuildInquiryComplete2 = 3008, // ギルドで聞き込みを完了した(2)
    // GuildInquiryComplete3 = 3009, // ギルドで聞き込みを完了した(3)
    GuildInquiryCompleteAll = 3010, // ギルドで聞き込みを完了した
    AskedReceptionistAboutNextDestination = 3013, // 受付嬢に次の目的地について尋ねた
    FirstEnteredSnowCountry = 3016, // 初めて雪国に入った
    FirstEnteredSnowVillage = 3019, // 初めて雪国の村に入った
    FirstTalkedToVillageChief = 3022, // 初めて村長と話した
    FirstMetBoy = 3025, // 初めて少年に会った
    ReachedCaveEntrance = 3028, // 洞窟の入口に到達した
    ReachedTowerGate = 3031, // 塔の入り口に到達した
    ReachedTowerEntrance = 3034, // 塔の入り口に到達した
    ReachedTowerLanding1 = 3037, // 塔の中間地点1に到達した
    ReachedTowerHallEntrance = 3040, // 塔のホールの入り口に到達した
    ApothecaryDefeated = 3045, // 薬屋のボスを倒した
    ApothecaryQuestComplete = 3050, // 薬屋のクエストを完了した
    FellUnderground = 3053, // 地下に落ちた
}

/// <summary>
/// 第三章ステージのint型のフラグ
/// </summary>
public enum Chapter3CountedEvent { }

/// <summary>
/// チュートリアルのbool型のフラグ
/// </summary>
public enum TutorialEvent
{
    // 操作系チュートリアル
    InteractTutorialComplete = 21001, // インタラクトのチュートリアル完了
    JumpTutorialComplete = 21004, // ジャンプチュートリアル完了

    // DipTutorialComplete = 21007, // 降下（ディップ）チュートリアル完了
    CrystalTutorialComplete = 21011, // クリスタル関連チュートリアル完了
    ItemUseTutorialComplete = 21012, // アイテム使用チュートリアル完了
    QuickItemTutorialComplete = 21013, // クイックアイテムチュートリアル完了
    BreakableShootTutorialComplete = 21014, // 破壊可能物チュートリアル完了
    BodyStateTutorial2Complete = 21017, // 体形チュートリアル2完了
    BodyStateTutorial3Complete = 21021, // 体形チュートリアル3完了
    EnemyTutorialComplete = 21024, // 敵との戦闘チュートリアル完了
    SwordTutorialComplete = 21027, // 剣のチュートリアル完了
    DeathFastTravelTutorialComplete = 21028, // 死亡ファストトラベルチュートリアル完了
}

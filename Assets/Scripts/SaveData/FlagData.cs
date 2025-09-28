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
}

/// 【重要】
/// 新たに Enum で管理するフラグ（例：Chapter1Event, Chapter2Event など）を追加した場合、
/// 以下の3つの処理をFlagManagerに忘れずに更新してください：
///
/// 1. InitializeAllEnums に初期化関数を追加（ゲーム開始時に登録される）
/// 2. LoadFlagData に LoadBoolFlags / LoadIntFlags の呼び出しを追加（セーブから復元される）
/// 3. SaveFlagData は型を問わずEnum→int変換で自動対応するため、追加は不要
/// 4. FlagConditionDrawerPro.cs の boolEnumTypes / intEnumTypes に新しいEnum型を追加


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

    // マップ系イベント
    FirstEnteredVillage = 1002, // 初めて村に入った
    UpperRiverReached = 1104, // 上流の川に到達
    ShopGirlHouseUnlocked = 1107, // ショップの女の子の家が解放された

    // 人物系イベント
    ShopGirlMissing = 1201, // ショップの女の子がいなくなった
    ShopGirlFirstMet = 1202, // ショップの女の子と初めて会った
    Girl2ItemReceived1 = 1205, // 女の子2からアイテムを受け取った1

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

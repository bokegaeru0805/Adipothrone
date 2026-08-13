public enum ProgressLogName
{
    // =================================================================
    // プロローグ　8種類(2025/07/30現在)
    // =================================================================

    GameFirstStart = 16001, // 初回ゲーム起動(FieldEvent_Prologue.csで登録)
    TutorialStart = 16002, // チュートリアル開始(FieldEvent_Prologue.csで登録)
    FirstMetRobot = 16003, // 初めてロボットに出会う(FieldEvent_Prologue.csで登録)

    // --- クリスタルのクエスト ---
    CrystalQuestStart = 16004, // クリスタルのクエストを開始(FieldEvent_Prologue.csで登録)
    CrystalQuestComplete = 16005, // クリスタルのクエストを完了(FieldEvent_Prologue.csで登録)

    // --- 最初のボス ---
    FirstBossAppear = 16006, // 初ボス出現(FieldEvent_Prologue.csで登録)
    DefeatFirstBoss = 16007, // 初ボス撃破(BossAfterDeath.csで登録)
    AfterMysteriousWomanEvent = 16008, // 謎の女性イベント後(FieldEvent_Prologue.csで登録)

    // =================================================================
    // 第1章　15種類(2026/03/04現在)
    // =================================================================

    Chapter1Start = 16009, // 第一章開始(Chapter1FieldEvent.csで登録)
    VillageTourStart = 16010, // 村の観光開始(Chapter1FieldEvent.csで登録)

    // --- 井戸のクエスト ---
    WellQuestStart = 16011, // 井戸のクエスト受け取り(Chapter1FieldEvent.csで登録)
    WellQuestComplete = 16012, // 井戸のクエスト完了(Chapter1FieldEvent.csで登録)

    // --- 川のクエスト ---
    RiverQuestStart = 16013, // 川のクエスト受け取り(Chapter1FieldEvent.csで登録)
    EncounterRiverRock = 16014, // 川の岩に遭遇(Chapter1FieldEvent.csで登録)
    RequestRockDestruction = 16015, // 岩の破壊依頼(Chapter1FieldEvent.csで登録)
    CompleteRockDestruction = 16016, // 岩の破壊依頼完了(Chapter1FieldEvent.csで登録)
    RiverBossAppear = 16017, // 川のボス出現(Chapter1FieldEvent.csで登録)
    DefeatRiverBoss = 16018, // 川のボス撃破(BossAfterDeath.csで登録)

    // --- 店の少女のクエスト ---
    HeardShopGirlRumor = 16019, // ショップの女の子についての噂を聞いた(Chapter1FieldEvent.csで登録)
    StartShopGirlSearch = 16020, // 村の店の少女の探索を開始(Chapter1FieldEvent.csで登録)
    HouseCaveBossAppear = 16021, // 家の洞窟のボス出現(Chapter1FieldEvent.csで登録)
    DefeatHouseCaveBoss = 16022, // 家の洞窟のボス撃破(BossAfterDeath.csで登録)
    ToDesert = 16023, // 砂漠へ向かう

    // =================================================================
    // 第2章
    // =================================================================

    // --- 砂漠の村・序盤 ---
    Chapter2Start = 16024, // 第二章開始
    FirstEnteredDesertVillage = 16025, // 砂漠の村へ初めて到着
    FirstMetCoachman = 16026, // アクスとの初対面

    // --- オアシスの源泉クエスト ---
    OasisSpringEnemiesAppear = 16027, // オアシスの源泉の敵が出現
    OasisSpringEnemiesDefeat = 16028, // オアシスの源泉の敵を撃破(Flowchartで登録)
    AttemptedToReportCoachmanQuest = 16029, // アクスへ納品報告をしようとした
    ReportedCoachmanQuestComplete = 16030, // アクスへ納品報告を完了(Flowchartで登録)

    // --- 砂漠の秘宝を探す ---
    FirstMetDesertTempleBoss = 16031, // 神殿入口でレヴィアスと初邂逅
    DustDevilBossAppear = 16032, // 砂漠の秘宝を守るボス出現
    DustDevilBossDefeat = 16033, // 砂漠の秘宝を守るボス撃破
    OasisPartiallyRestoredByFill = 16034, // フィルによるオアシスの部分的な回復

    // --- 砂漠の神殿・内部探索 ---
    TempleBossSmokeDefeat = 16035, // レヴィアス(1回目)撃破
    AllOrbsPlacedInDesertTemple = 16036, // 砂漠の神殿の装置を全て起動
    TalkToFillAfterAllOrbsPlaced = 16037, // 神殿の決戦前のフィルとの会話
    TempleBossAppear = 16038, // レヴィアス(2回目)出現
}

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
    // 第1章　14種類(2025/07/30現在)
    // =================================================================

    Chapter1Start = 16009, // 第一章開始(FieldEvent_Chapter1.csで登録)
    VillageTourStart = 16010, // 村の観光開始(FieldEvent_Chapter1.csで登録)

    // --- 井戸のクエスト ---
    WellQuestStart = 16011, // 井戸のクエスト受け取り(FieldEvent_Chapter1.csで登録)
    WellQuestComplete = 16012, // 井戸のクエスト完了(FieldEvent_Chapter1.csで登録)

    // --- 川のクエスト ---
    RiverQuestStart = 16013, // 川のクエスト受け取り(FieldEvent_Chapter1.csで登録)
    EncounterRiverRock = 16014, // 川の岩に遭遇(FieldEvent_Chapter1.csで登録)
    RequestRockDestruction = 16015, // 岩の破壊依頼(FieldEvent_Chapter1.csで登録)
    CompleteRockDestruction = 16016, // 岩の破壊依頼完了(FieldEvent_Chapter1.csで登録)
    RiverBossAppear = 16017, // 川のボス出現(FieldEvent_Chapter1.csで登録)
    DefeatRiverBoss = 16018, // 川のボス撃破(BossAfterDeath.csで登録)

    // --- 店の少女のクエスト ---
    HeardShopGirlRumor = 16019, // ショップの女の子についての噂を聞いた(FieldEvent_Chapter1.csで登録)
    StartShopGirlSearch = 16020, // 村の店の少女の探索を開始(FieldEvent_Chapter1.csで登録)
    HouseCaveBossAppear = 16021, // 家の洞窟のボス出現(FieldEvent_Chapter1.csで登録)
    DefeatHouseCaveBoss = 16022, // 家の洞窟のボス撃破(BossAfterDeath.csで登録)

    // =================================================================
    // 第2章
    // =================================================================
    // (ここに新しいログを追加)
}

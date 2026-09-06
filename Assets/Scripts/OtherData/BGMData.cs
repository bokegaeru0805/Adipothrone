using System.Collections.Generic;

/// <summary>
/// ゲーム内で使用するBGMの識別子。
/// </summary>
public enum BGMCategory
{
    // 未設定
    None = 0,

    // タイトル・ゲームオーバー
    Title = 5,
    GameOver = 10,

    // フィールド
    Field_Quiet = 200,
    Field_Tutorial = 205,
    Field_Waterfall1 = 210,
    Field_FirstVillage = 215,
    Field_Plains = 220,
    Field_Cave1 = 225,
    Field_DesertVillage = 226,
    Field_Desert = 227,
    Field_DesertTemple = 230,
    Field_Park = 235,
    Field_Guild = 240,
    Field_SnowVillage = 245,
    Field_SnowField = 250,
    Field_SnowUnderField = 255,
    Field_SnowMountain = 260,
    Field_Tower = 265,

    // ボス
    Boss_Electric = 400,
    Boss_Chapter = 405,
    Boss_Unique = 406,
    Boss_Decision = 407,
    Boss_Decision2 = 408,
    Boss_Mid = 410,

    // 環境音
    Env_Water_Stream1 = 600,
    Env_Birds = 605,

    // イベント
    Event_DecisiveBattle_Before = 800,
    Event_Confrontation = 805,
    Event_Crisis = 806,
    Event_Threat = 807,
    Event_Peaceful = 808,
    Event_Farewell = 809,
    Event_Labyrinth = 810,
    Event_Encounter = 811,
    Event_Anxiety = 812,

    // テーマ
    Theme_Fill = 1000,
    Theme_GadTheme = 1005,
    Theme_ApothecaryTheme = 1015,

    // デバッグ
    bgm0 = 1000, // デバッグ用
    bgm1 = 1001, // デバッグ用
}

/// <summary>
/// BGMの識別子とCRIWAREのCue名の対応を提供します。
/// </summary>
public static class BGMData
{
    private static readonly Dictionary<BGMCategory, string> CueNames = new Dictionary<
        BGMCategory,
        string
    >()
    {
        { BGMCategory.Title, "Title" },
        { BGMCategory.GameOver, "GameOver" },

        { BGMCategory.Field_Quiet, "QuietField" },
        { BGMCategory.Field_Tutorial, "TutorialField" },
        { BGMCategory.Field_Waterfall1, "WaterFall1" },
        { BGMCategory.Field_FirstVillage, "FirstVillage" },
        { BGMCategory.Field_Plains, "PlainsField1" },
        { BGMCategory.Field_Cave1, "CaveField_Amb1" },
        { BGMCategory.Field_DesertVillage, "DesertVillage" },
        { BGMCategory.Field_Desert, "DesertField" },
        { BGMCategory.Field_DesertTemple, "DesertTemple" },
        { BGMCategory.Field_Park, "ParkField" },
        { BGMCategory.Field_Guild, "GuildField" },
        { BGMCategory.Field_SnowVillage, "SnowVillage" },
        { BGMCategory.Field_SnowField, "SnowField" },
        { BGMCategory.Field_SnowUnderField, "SnowUnderField" },
        { BGMCategory.Field_SnowMountain, "SnowMountain" },
        { BGMCategory.Field_Tower, "TowerField" },

        { BGMCategory.Boss_Electric, "ElectricBoss" },
        { BGMCategory.Boss_Chapter, "ChapterBoss" },
        { BGMCategory.Boss_Unique, "UniqueBoss" },
        { BGMCategory.Boss_Decision, "DecisionBoss" },
        { BGMCategory.Boss_Decision2, "DecisionBoss2" },
        { BGMCategory.Boss_Mid, "MidBoss" },

        { BGMCategory.Env_Water_Stream1, "WaterStream1" },
        { BGMCategory.Env_Birds, "PlainsField_Amb1" },
        { BGMCategory.Event_DecisiveBattle_Before, "DecisiveBattle_Before" },
        { BGMCategory.Event_Confrontation, "Confrontation" },
        { BGMCategory.Event_Crisis, "Crisis" },
        { BGMCategory.Event_Threat, "Threat" },
        { BGMCategory.Event_Peaceful, "Peaceful" },
        { BGMCategory.Event_Farewell, "Farewell" },
        { BGMCategory.Event_Labyrinth, "Labyrinth" },
        { BGMCategory.Event_Encounter, "Encounter" },
        { BGMCategory.Event_Anxiety, "Anxiety" },

        { BGMCategory.Theme_Fill, "FillTheme" },
        { BGMCategory.Theme_GadTheme, "GadTheme" },
        { BGMCategory.Theme_ApothecaryTheme, "ApothecaryTheme" },
        
        { BGMCategory.None, "" },
    };

    /// <summary>
    /// BGMの識別子に対応するCRIWAREのCue名を取得します。
    /// </summary>
    /// <param name="category">Cue名を取得するBGMの識別子</param>
    /// <param name="cueName">取得したCue名</param>
    /// <returns>対応するCue名が登録されている場合はtrue</returns>
    public static bool TryGetCueName(BGMCategory category, out string cueName)
    {
        return CueNames.TryGetValue(category, out cueName);
    }
}

using UnityEngine;

/// <summary>
/// 雪原に配置するフィールドイベントを管理します。
/// </summary>
public class SnowFieldEvent : BaseFieldEvent
{
    #region インスペクター設定

    [SerializeField]
    private FieldName fieldName = FieldName.None;

    #endregion

    #region イベント定義

    private enum FieldName
    {
        None = 0,
        EventField1 = 1, // 雪原のイベントフィールド1
        LeftBridgeField = 3, // 雪原の橋の左側のフィールド
        VillageEntrance = 6, // 雪原の村入口
        VillageHouse = 11, // 雪原の村の家
        VillageCenter = 16, // 雪原の村の中心
        MountainEntrance = 19, // 雪原の山入口
        ParkField = 20, // 公園フィールド
        CaveEntrance = 21, // 雪原の洞窟入口
        TowerGate = 26, // 雪原の塔の入り口
        TowerEntrance = 31, // 雪原の塔の入り口
        TowerLobby = 32, // 雪原の塔のロビー
        TowerLanding1 = 36, // 雪原の塔の中間地点1
        TowerLanding2 = 37, // 雪原の塔の中間地点2
        TowerHall = 41, // 雪原の塔のホール
        Under2Field = 46, // 地下施設2
        UnderExit = 51, // 地下施設の出口
        LastBattleEntrance = 71, // 最終決戦の入り口
        LastBattleField = 76, // 最終決戦のフィールド
    }

    #endregion

    #region プロパティ

    protected override string EventName => fieldName.ToString();

    #endregion

    #region Unityライフサイクル

    protected override void Awake()
    {
        base.Awake();

        if (fieldName == FieldName.None)
        {
            Debug.LogWarning(
                $"{gameObject.name} の SnowFieldEvent にフィールド名が設定されていません。",
                this
            );
        }
    }

    #endregion

    #region イベント処理

    protected override void HandleEvent()
    {
        switch (fieldName)
        {
            case FieldName.EventField1:
                FungusHelper.ExecuteBlock(targetFlowchart, "HeadToSnowCountry");
                isEventTriggered = true; // イベントがトリガーされたことを記録
                break;
            case FieldName.LeftBridgeField:
                if (
                    flagManager.GetBoolFlag(Chapter3TriggeredEvent.WitnessedVillageChange)
                    && !flagManager.GetBoolFlag(
                        Chapter3TriggeredEvent.TalkedToCoachmanAboutVillageChange
                    )
                )
                {
                    flagManager.SetBoolFlag(
                        Chapter3TriggeredEvent.TalkedToCoachmanAboutVillageChange,
                        true
                    );
                    FungusHelper.ExecuteBlock(targetFlowchart, "TalkToCoachmanAboutVillageChange");
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                }
                break;
            case FieldName.VillageEntrance:
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.FirstEnteredSnowVillage))
                {
                    flagManager.SetBoolFlag(Chapter3TriggeredEvent.FirstEnteredSnowVillage, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "EnterSnowVillage");
                }
                else if (
                    flagManager.GetBoolFlag(Chapter3TriggeredEvent.ApothecaryCaptured)
                    && !flagManager.GetBoolFlag(Chapter3TriggeredEvent.SensedVillageAnomaly)
                )
                {
                    flagManager.SetBoolFlag(Chapter3TriggeredEvent.SensedVillageAnomaly, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "SensedVillageAnomaly");
                }
                else if (
                    flagManager.GetBoolFlag(
                        Chapter3TriggeredEvent.TalkedToCoachmanAboutVillageChange
                    )
                    && !flagManager.GetBoolFlag(
                        Chapter3TriggeredEvent.StartedInvestigatingVillageAnomaly
                    )
                )
                {
                    flagManager.SetBoolFlag(
                        Chapter3TriggeredEvent.StartedInvestigatingVillageAnomaly,
                        true
                    );
                    FungusHelper.ExecuteBlock(
                        targetFlowchart,
                        "StartedInvestigatingVillageAnomaly"
                    );
                }
                break;
            case FieldName.VillageHouse:
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.FirstTalkedToVillageChief))
                {
                    // フラグを立てるのはFlowchart内で行う
                    // flagManager.SetBoolFlag(Chapter3TriggeredEvent.FirstTalkedToVillageChief, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstTalkedToVillageChief");
                }
                else if (
                    flagManager.GetBoolFlag(Chapter3TriggeredEvent.ApothecaryCaptured)
                    && !flagManager.GetBoolFlag(Chapter3TriggeredEvent.WitnessedVillageChange)
                )
                {
                    flagManager.SetBoolFlag(Chapter3TriggeredEvent.WitnessedVillageChange, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "WitnessedVillageChange");
                    flagManager.SetBoolFlag(Chapter3TriggeredEvent.SensedVillageAnomaly, true); // 村の異変を察知したフラグも立てる
                }
                break;
            case FieldName.VillageCenter:
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.FirstMetBoy))
                {
                    // フラグを立てるのはFlowchart内で行う
                    // flagManager.SetBoolFlag(Chapter3TriggeredEvent.FirstMetBoy, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstMetBoy");
                }
                break;
            case FieldName.MountainEntrance:
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.DiscoveredParkEntrance))
                {
                    flagManager.SetBoolFlag(Chapter3TriggeredEvent.DiscoveredParkEntrance, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "DiscoveredParkEntrance");
                }
                else if (
                    flagManager.GetBoolFlag(Chapter3TriggeredEvent.HeroineCapturedByVillagers)
                    && !flagManager.GetBoolFlag(Chapter3TriggeredEvent.ReunitedWithRobot)
                )
                {
                    flagManager.SetBoolFlag(Chapter3TriggeredEvent.ReunitedWithRobot, true);
                    GameManager.instance.savedata.EnemyRecordData.RegisterEncounter(
                        EnemyName.Slime_Snow
                    ); // スライム(雪原)と遭遇済みとして登録
                    GameManager.instance.savedata.EnemyRecordData.RegisterEncounter(
                        EnemyName.Fairy_Snow
                    ); // フェアリー(雪原)と遭遇済みとして登録
                    GameManager.instance.savedata.EnemyRecordData.RegisterEncounter(
                        EnemyName.Golem_Snowfield_Small
                    ); // 雪原ゴーレム(小型)と遭遇済みとして登録
                    GameManager.instance.savedata.EnemyRecordData.RegisterEncounter(
                        EnemyName.Golem_Snowfield_Medium
                    ); // 雪原ゴーレム(中型)と遭遇済みとして登録
                    GameManager.instance.savedata.EnemyRecordData.RegisterEncounter(
                        EnemyName.Golem_Snowfield_Large
                    ); // 雪原ゴーレム(大型)と遭遇済みとして登録
                    FungusHelper.ExecuteBlock(targetFlowchart, "ReunitedWithRobot");
                }
                break;
            case FieldName.ParkField:
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.FirstTalkedToFrogShopManager))
                {
                    flagManager.SetBoolFlag(
                        Chapter3TriggeredEvent.FirstTalkedToFrogShopManager,
                        true
                    );
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstTalkedToFrogShopManager");
                }
                break;
            case FieldName.CaveEntrance:
                // 通過記録だけを保存する
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.ReachedCaveEntrance))
                {
                    flagManager.SetBoolFlag(Chapter3TriggeredEvent.ReachedCaveEntrance, true);
                }
                break;
            case FieldName.TowerGate:
                // 通過記録だけを保存する
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.ReachedTowerGate))
                {
                    flagManager.SetBoolFlag(Chapter3TriggeredEvent.ReachedTowerGate, true);
                }
                break;
            case FieldName.TowerEntrance:
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.ReachedTowerEntrance))
                {
                    flagManager.SetBoolFlag(Chapter3TriggeredEvent.ReachedTowerEntrance, true);
                    GameManager.instance.savedata.EnemyRecordData.RegisterEncounter(
                        EnemyName.Gargoyle
                    ); // ガーゴイルと遭遇済みとして登録
                    GameManager.instance.savedata.EnemyRecordData.RegisterEncounter(
                        EnemyName.Bat_Tower
                    ); // バットと遭遇済みとして登録
                    GameManager.instance.savedata.EnemyRecordData.RegisterEncounter(
                        EnemyName.Golem_Tower
                    ); // ゴーレムと遭遇済みとして登録
                    GameManager.instance.savedata.EnemyRecordData.RegisterEncounter(
                        EnemyName.Fairy_Rare
                    ); // レアフェアリーと遭遇済みとして登録
                    FungusHelper.ExecuteBlock(targetFlowchart, "ReachedTowerEntrance");
                }
                break;
            case FieldName.TowerLobby:
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.ReachedTowerHallEntrance))
                {
                    flagManager.SetBoolFlag(Chapter3TriggeredEvent.ReachedTowerHallEntrance, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "ReachedTowerHallEntrance");
                }
                break;
            case FieldName.TowerLanding1:
                // 通過記録だけを保存する
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.ReachedTowerLanding1))
                {
                    flagManager.SetBoolFlag(Chapter3TriggeredEvent.ReachedTowerLanding1, true);
                }
                break;
            case FieldName.TowerLanding2:
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.ReachedTowerLanding2))
                {
                    flagManager.SetBoolFlag(Chapter3TriggeredEvent.ReachedTowerLanding2, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "ReachedTowerLanding2");
                }
                break;
            case FieldName.TowerHall:
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.ApothecaryDefeated))
                {
                    FungusHelper.ExecuteBlock(targetFlowchart, "ApothecaryAppear");
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                }
                break;
            case FieldName.Under2Field:
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.FirstEnteredUnder2Field))
                {
                    flagManager.SetBoolFlag(Chapter3TriggeredEvent.FirstEnteredUnder2Field, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstEnteredUnder2Field");
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                }
                break;
            case FieldName.UnderExit:
                if (
                    flagManager.GetBoolFlag(Chapter3TriggeredEvent.Under4FieldComplete)
                    && !flagManager.GetBoolFlag(Chapter3TriggeredEvent.ApothecaryCaptured)
                )
                {
                    flagManager.SetBoolFlag(Chapter3TriggeredEvent.ApothecaryCaptured, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "ReachedUnderExit");
                    GameManager.instance.savedata.FastTravelData.RegisterFastTravelData(
                        FastTravelName.Tower
                    ); // タワーのファストトラベル地点を解放(詰み防止)
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                }
                break;
            case FieldName.LastBattleEntrance:
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.ReachedFinalBattleEntrance))
                {
                    flagManager.SetBoolFlag(
                        Chapter3TriggeredEvent.ReachedFinalBattleEntrance,
                        true
                    );
                    FungusHelper.ExecuteBlock(targetFlowchart, "BeforeChapter3BossBattle");
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                }
                break;
            case FieldName.LastBattleField:
                FungusHelper.ExecuteBlock(targetFlowchart, "Chapter3BossAppear");
                isEventTriggered = true; // イベントがトリガーされたことを記録
                break;
        }
    }

    #endregion

    #region TowerLobbyの鍵を開ける処理
    public void UnlockTowerLobby()
    {
        flagManager.SetKeyOpened(KeyID.K12, true);
    }
    #endregion
}

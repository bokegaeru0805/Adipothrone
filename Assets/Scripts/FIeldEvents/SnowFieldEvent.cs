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
        VillageEntrance = 6, // 雪原の村入口
        VillageHouse = 11, // 雪原の村の家
        VillageCenter = 16, // 雪原の村の中心
        CaveEntrance = 21, // 雪原の洞窟入口
        TowerGate = 26, // 雪原の塔の入り口
        TowerEntrance = 31, // 雪原の塔の入り口
        TowerLobby = 32, // 雪原の塔のロビー
        TowerLanding1 = 36, // 雪原の塔の中間地点1
        TowerHall = 41, // 雪原の塔のホール
        Under2Field = 46, // 地下施設2
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
            case FieldName.VillageEntrance:
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.FirstEnteredSnowVillage))
                {
                    flagManager.SetBoolFlag(Chapter3TriggeredEvent.FirstEnteredSnowVillage, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "EnterSnowVillage");
                }
                break;
            case FieldName.VillageHouse:
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.FirstTalkedToVillageChief))
                {
                    // フラグを立てるのはFlowchart内で行う
                    // flagManager.SetBoolFlag(Chapter3TriggeredEvent.FirstTalkedToVillageChief, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstTalkedToVillageChief");
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
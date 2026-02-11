using UnityEngine;

public class FieldEvent_Chapter2 : BaseFieldEvent
{
    [SerializeField]
    private FieldName fieldname = FieldName.None; // フィールド名を設定するための変数

    private enum FieldName
    {
        None = 0, // フィールド名が設定されていない場合の初期値

        // Chapter2StartField = 1, // 第二章開始フィールド
        VillageEntranceField = 2, // 村の入り口フィールド
        CoachmanField = 3, // 馬車の御者フィールド
        WaterSourceFrontField = 7, // オアシスの源泉前フィールド
        WaterSourceField = 4, // オアシスの源泉フィールド
        TempleEntranceField = 5, // 砂漠の神殿入口フィールド
        BeforeDeepDesertField = 6, // 砂漠の奥地手前フィールド
        DeepDesertField = 8, // 砂漠の奥地フィールド
        DustWindBossField = 9, // 砂嵐のボスフィールド
    }

    protected override string EventName => fieldname.ToString();

    protected override void Awake()
    {
        base.Awake();

        if (fieldname == FieldName.None)
        {
            Debug.LogWarning(
                $"{this.gameObject.name} の FieldEvent_Chapter2 にフィールド名が設定されていません。",
                this
            );
        }
    }

    protected override void HandleEvent()
    {
        switch (fieldname)
        {
            case FieldName.VillageEntranceField:
                if (!flagManager.GetBoolFlag(Chapter2TriggeredEvent.FirstEnteredVillage))
                {
                    flagManager.SetBoolFlag(Chapter2TriggeredEvent.FirstEnteredVillage, true);
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                    GameManager.instance.savedata.FastTravelData.RegisterFastTravelData(
                        FastTravelName.DesertVillage
                    ); // ファストトラベル地点を登録
                    GameManager.instance.savedata.FastTravelData.SetLastUsedFastTravel(
                        FastTravelName.DesertVillage
                    ); // 最後に使用した地点を設定
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstEnteredVillage");
                }
                break;
            case FieldName.CoachmanField:
                if (!flagManager.GetBoolFlag(Chapter2TriggeredEvent.FirstMetCoachman))
                {
                    flagManager.SetBoolFlag(Chapter2TriggeredEvent.FirstMetCoachman, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstMetCoachman");
                }
                else if (
                    flagManager.GetBoolFlag(Chapter2TriggeredEvent.OasisSpringEnemiesDefeated)
                    && !flagManager.GetBoolFlag(
                        Chapter2TriggeredEvent.ReportedCoachmanQuestComplete
                    )
                )
                {
                    flagManager.SetBoolFlag(
                        Chapter2TriggeredEvent.ReportedCoachmanQuestComplete,
                        true
                    );
                    FungusHelper.ExecuteBlock(targetFlowchart, "ReportCoachManQuestComplete");
                }
                break;
            case FieldName.WaterSourceFrontField:
                if (
                    !flagManager.GetBoolFlag(
                        Chapter2TriggeredEvent.FirstEnteredWaterSourceFrontField
                    )
                )
                {
                    flagManager.SetBoolFlag(
                        Chapter2TriggeredEvent.FirstEnteredWaterSourceFrontField,
                        true
                    );
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstEnteredWaterSourceFront");
                }
                break;
            case FieldName.WaterSourceField:
                if (!flagManager.GetBoolFlag(Chapter2TriggeredEvent.OasisSpringEnemiesDefeated))
                {
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstMetFill");
                }
                break;
            case FieldName.TempleEntranceField:
                if (
                    flagManager.GetBoolFlag(Chapter2TriggeredEvent.ReportedCoachmanQuestComplete)
                    && !flagManager.GetBoolFlag(Chapter2TriggeredEvent.FirstMetDesertTempleBoss)
                )
                {
                    flagManager.SetBoolFlag(Chapter2TriggeredEvent.FirstMetDesertTempleBoss, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstMetDesertTempleBoss");
                }
                break;
            case FieldName.BeforeDeepDesertField:
                if (
                    !flagManager.GetBoolFlag(Chapter2TriggeredEvent.FirstEnteredDeepDesert)
                )
                {
                    flagManager.SetBoolFlag(
                        Chapter2TriggeredEvent.FirstEnteredDeepDesert,
                        true
                    );
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                }
                break;
            case FieldName.DeepDesertField:
                if (
                    !flagManager.GetBoolFlag(Chapter2TriggeredEvent.BeforeDustDevilBoss)
                )
                {
                    flagManager.SetBoolFlag(Chapter2TriggeredEvent.BeforeDustDevilBoss, true);
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                    FungusHelper.ExecuteBlock(targetFlowchart, "BeforeDustDevilBoss");
                }
                break;
            case FieldName.DustWindBossField:
                if (!flagManager.GetBoolFlag(Chapter2TriggeredEvent.DustDevilBossDefeated))
                {
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                    FungusHelper.ExecuteBlock(targetFlowchart, "DustDevilBossAppear");
                }
                break;
        }
    }
}

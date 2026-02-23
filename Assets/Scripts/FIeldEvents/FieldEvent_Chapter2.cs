using NaughtyAttributes;
using UnityEngine;

public class FieldEvent_Chapter2 : BaseFieldEvent
{
    [SerializeField]
    private FieldName fieldname = FieldName.None; // フィールド名を設定するための変数

    [SerializeField]
    [ShowIf(nameof(requiresOrbItem))]
    private BaseItemData orbItemData;
    private bool requiresOrbItem =>
        fieldname == FieldName.BlueOrbDeviceField
        || fieldname == FieldName.GreenOrbDeviceField
        || fieldname == FieldName.OrangeOrbDeviceField
        || fieldname == FieldName.PurpleOrbDeviceField;

    [SerializeField]
    [ShowIf(nameof(fieldname), FieldName.CoachmanField)]
    private BaseItemData waterOasisSourceItemData;

    private enum FieldName
    {
        None = 0, // フィールド名が設定されていない場合の初期値

        Chapter2StartField = 1, // 第二章開始フィールド
        VillageEntranceField = 2, // 村の入り口フィールド
        CoachmanField = 3, // 馬車の御者フィールド
        WaterSourceFrontField = 7, // オアシスの源泉前フィールド
        WaterSourceField = 4, // オアシスの源泉フィールド
        TempleBuildingField = 5, // 砂漠の神殿建物フィールド
        BeforeDeepDesertField = 6, // 砂漠の奥地手前フィールド
        DeepDesertField = 8, // 砂漠の奥地フィールド
        DustWindBossField = 9, // 砂嵐のボスフィールド
        TempleEntranceField = 15, // 砂漠の神殿入り口フィールド
        BlueOrbDeviceField = 21, // 青いオーブの装置フィールド
        GreenOrbDeviceField = 22, // 緑のオーブの装置フィールド
        OrangeOrbDeviceField = 23, // オレンジのオーブの装置フィールド
        PurpleOrbDeviceField = 24, // 紫のオーブの装置フィールド
        TempleRoofField = 30, // 砂漠の神殿の屋上フィールド
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
            case FieldName.Chapter2StartField:
                if (!flagManager.GetBoolFlag(Chapter2TriggeredEvent.Chapter2Start))
                {
                    flagManager.SetBoolFlag(Chapter2TriggeredEvent.Chapter2Start, true);
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                    FungusHelper.ExecuteBlock(targetFlowchart, "Chapter2Start");
                }
                break;
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
                        Chapter2TriggeredEvent.AttemptedToReportCoachmanQuest
                    )
                )
                {
                    flagManager.SetBoolFlag(
                        Chapter2TriggeredEvent.AttemptedToReportCoachmanQuest,
                        true
                    );
                    FungusHelper.ExecuteBlock(targetFlowchart, "AttemptedToReportCoachmanQuest");
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
            case FieldName.TempleBuildingField:
                if (
                    flagManager.GetBoolFlag(Chapter2TriggeredEvent.ReportedCoachmanQuestComplete)
                    && !flagManager.GetBoolFlag(Chapter2TriggeredEvent.FirstMetDesertTempleBoss)
                )
                {
                    // フラグを立てるのはFlowchart内で行う
                    // flagManager.SetBoolFlag(Chapter2TriggeredEvent.FirstMetDesertTempleBoss, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstMetDesertTempleBoss");
                }
                else if (
                    flagManager.GetBoolFlag(Chapter2TriggeredEvent.DustDevilBossDefeated)
                    && !flagManager.GetBoolFlag(Chapter2TriggeredEvent.OasisPartiallyRestoredByFill)
                )
                {
                    // フラグを立てるのはFlowchart内で行う
                    // flagManager.SetBoolFlag(Chapter2TriggeredEvent.OasisPartiallyRestoredByFill, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "OasisPartiallyRestored");
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                }
                break;
            case FieldName.BeforeDeepDesertField:
                if (!flagManager.GetBoolFlag(Chapter2TriggeredEvent.FirstEnteredDeepDesert))
                {
                    flagManager.SetBoolFlag(Chapter2TriggeredEvent.FirstEnteredDeepDesert, true);
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                }
                break;
            case FieldName.DeepDesertField:
                if (!flagManager.GetBoolFlag(Chapter2TriggeredEvent.BeforeDustDevilBoss))
                {
                    flagManager.SetBoolFlag(Chapter2TriggeredEvent.BeforeDustDevilBoss, true);
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                    FungusHelper.ExecuteBlock(targetFlowchart, "BeforeDustDevilBoss");
                }
                break;
            case FieldName.DustWindBossField:
                if (!flagManager.GetBoolFlag(Chapter2TriggeredEvent.DustDevilBossDefeated))
                {
                    // フラグを立てるのはFlowchart内で行う
                    //flagManager.SetBoolFlag(Chapter2TriggeredEvent.DustDevilBossDefeated, true);
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                    FungusHelper.ExecuteBlock(targetFlowchart, "DustDevilBossAppear");
                }
                break;
            case FieldName.TempleEntranceField:
                if (!flagManager.GetBoolFlag(Chapter2TriggeredEvent.TempleBossSmokeDefeated))
                {
                    FungusHelper.ExecuteBlock(targetFlowchart, "TempleBossSmokeAppear");
                }
                else if (
                    !flagManager.GetBoolFlag(Chapter2TriggeredEvent.TalkedToFillAfterAllOrbsPlaced)
                )
                {
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                    FungusHelper.ExecuteBlock(targetFlowchart, "TalkToFillAfterAllOrbsPlaced");
                }
                break;
            case FieldName.BlueOrbDeviceField:
                HandleOrbDevice(
                    Chapter2TriggeredEvent.BlueOrbPlacedInDevice,
                    "BlueOrbPlacedInDevice"
                );
                break;

            case FieldName.GreenOrbDeviceField:
                HandleOrbDevice(
                    Chapter2TriggeredEvent.GreenOrbPlacedInDevice,
                    "GreenOrbPlacedInDevice"
                );
                break;

            case FieldName.OrangeOrbDeviceField:
                HandleOrbDevice(
                    Chapter2TriggeredEvent.OrangeOrbPlacedInDevice,
                    "OrangeOrbPlacedInDevice"
                );
                break;

            case FieldName.PurpleOrbDeviceField:
                HandleOrbDevice(
                    Chapter2TriggeredEvent.PurpleOrbPlacedInDevice,
                    "PurpleOrbPlacedInDevice"
                );
                break;
            case FieldName.TempleRoofField:
                isEventTriggered = true; // イベントがトリガーされたことを記録
                FungusHelper.ExecuteBlock(targetFlowchart, "TempleBossAppear");
                break;
        }
    }

    #region オーブ装置関連の共通処理
    /// <summary>
    /// オーブ装置共通の処理
    /// </summary>
    /// <param name="orbFlag">この装置に対応する完了フラグ</param>
    /// <param name="blockName">実行するFungusブロック名</param>
    private void HandleOrbDevice(Chapter2TriggeredEvent orbFlag, string blockName)
    {
        // まずは全てのオーブが配置されたか確認して、完了イベントが発火していないなら発火させる
        CheckAllOrbsPlaced();

        // 既にこのオーブがはまっているなら何もしない
        if (flagManager.GetBoolFlag(orbFlag))
            return;

        // アイテムを持っているか確認
        if (GameManager.instance.savedata.ItemInventoryData.GetItemAmount(orbItemData) > 0)
        {
            // 1. フラグを即座に立てる（これにより、直後の全数チェックでカウントされる）
            flagManager.SetBoolFlag(orbFlag, true);

            // 2. この装置のイベント(オーブをはめる演出)を実行
            // ※isEventTriggeredはこの装置単体の再起動防止用
            // （BaseFieldEventのExecuteEventBlockを使うと楽ですが、ここでは明示的に書きます）
            isEventTriggered = true;
            FungusHelper.ExecuteBlock(targetFlowchart, blockName);
        }
    }

    /// <summary>
    /// 全てのオーブが配置されたか確認し、完了イベントを実行する
    /// </summary>
    private void CheckAllOrbsPlaced()
    {
        // 既に完了イベントが発火済みなら何もしない
        if (flagManager.GetBoolFlag(Chapter2TriggeredEvent.AllOrbsPlacedInDevice))
            return;

        // 4つのオーブフラグが全てTrueか確認
        bool isAllPlaced =
            flagManager.GetBoolFlag(Chapter2TriggeredEvent.BlueOrbPlacedInDevice)
            && flagManager.GetBoolFlag(Chapter2TriggeredEvent.GreenOrbPlacedInDevice)
            && flagManager.GetBoolFlag(Chapter2TriggeredEvent.OrangeOrbPlacedInDevice)
            && flagManager.GetBoolFlag(Chapter2TriggeredEvent.PurpleOrbPlacedInDevice);

        if (isAllPlaced)
        {
            // 完了フラグを立てる
            flagManager.SetBoolFlag(Chapter2TriggeredEvent.AllOrbsPlacedInDevice, true);

            // 全てのオーブがはまったときのイベントを実行
            FungusHelper.ExecuteBlock(targetFlowchart, "AllOrbsPlacedInDevice");
        }
    }
    #endregion

    #region 村聞き込み関連の共通処理


    /// <summary>
    /// 全ての聞き込みが完了したか確認し、完了イベントを実行する
    /// Fungusから呼び出されることを想定している
    /// </summary>
    public void CheckVillageInquiryComplete()
    {
        // 既に完了イベントが発火済みなら何もしない
        if (flagManager.GetBoolFlag(Chapter2TriggeredEvent.ReportedCoachmanQuestComplete))
            return;

        // 全てのの聞き込みフラグがTrueか確認
        bool isComplete =
            flagManager.GetBoolFlag(Chapter2TriggeredEvent.AttemptedToReportCoachmanQuest)
            && flagManager.GetBoolFlag(Chapter2TriggeredEvent.VillageInquiryComplete1)
            && flagManager.GetBoolFlag(Chapter2TriggeredEvent.VillageInquiryComplete2)
            && flagManager.GetBoolFlag(Chapter2TriggeredEvent.VillageInquiryComplete3);

        if (isComplete)
        {
            GameManager.instance.RemoveAllTypeIDFromInventory(waterOasisSourceItemData, 1); // アイテムを1つ減らす
            flagManager.SetBoolFlag(Chapter2TriggeredEvent.ReportedCoachmanQuestComplete, true);
            FungusHelper.ExecuteBlock(targetFlowchart, "ReportedCoachmanQuestComplete");
        }
    }
    #endregion
}

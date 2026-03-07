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
        WaterSourceFrontField = 7, // オアシスの源泉前フィールド1
        WaterSourceField1 = 4, // オアシスの源泉フィールド
        TempleBuildingField = 5, // 砂漠の神殿建物フィールド
        BeforeDeepDesertField = 6, // 砂漠の奥地手前フィールド
        DeepDesertField = 8, // 砂漠の奥地フィールド
        DustWindBossField = 9, // 砂嵐のボスフィールド
        WaterSourceField2 = 11, // オアシスの源泉フィールド2
        TempleEntranceField1 = 15, // 砂漠の神殿入り口フィールド1
        TempleEntranceField2 = 16, // 砂漠の神殿入り口フィールド2
        BlueOrbDeviceField = 21, // 青いオーブの装置フィールド
        GreenOrbDeviceField = 22, // 緑のオーブの装置フィールド
        OrangeOrbDeviceField = 23, // オレンジのオーブの装置フィールド
        PurpleOrbDeviceField = 24, // 紫のオーブの装置フィールド
        LotteryManagerField = 27, // くじ屋の店主フィールド
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
                    FungusHelper.ExecuteBlock(targetFlowchart, "Chapter2Start");
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.Chapter2Start
                    ); //進行ログを登録
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                }
                break;
            case FieldName.VillageEntranceField:
                if (!flagManager.GetBoolFlag(Chapter2TriggeredEvent.FirstEnteredVillage))
                {
                    flagManager.SetBoolFlag(Chapter2TriggeredEvent.FirstEnteredVillage, true);
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.FirstEnteredDesertVillage
                    ); //進行ログを登録
                    GameManager.instance.savedata.FastTravelData.RegisterFastTravelData(
                        FastTravelName.DesertVillage
                    ); // ファストトラベル地点を登録
                    GameManager.instance.savedata.FastTravelData.SetLastUsedFastTravel(
                        FastTravelName.DesertVillage
                    ); // 最後に使用した地点を設定
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstEnteredVillage");
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                }
                break;
            case FieldName.CoachmanField:
                if (!flagManager.GetBoolFlag(Chapter2TriggeredEvent.FirstMetCoachman))
                {
                    flagManager.SetBoolFlag(Chapter2TriggeredEvent.FirstMetCoachman, true);
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.FirstMetCoachman
                    ); //進行ログを登録
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
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.AttemptedToReportCoachmanQuest
                    ); //進行ログを登録
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

                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstEnteredWaterSourceFront");
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                }
                break;
            case FieldName.WaterSourceField1:
                if (!flagManager.GetBoolFlag(Chapter2TriggeredEvent.OasisSpringEnemiesDefeated))
                {
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.OasisSpringEnemiesAppear
                    ); //進行ログを登録
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstMetFill");
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                }
                break;
            case FieldName.WaterSourceField2:
                if (
                    flagManager.GetBoolFlag(Chapter2TriggeredEvent.DustDevilBossDefeated)
                    && !flagManager.GetBoolFlag(Chapter2TriggeredEvent.OasisPartiallyRestoredByFill)
                )
                {
                    // フラグを立てるのはFlowchart内で行う
                    // flagManager.SetBoolFlag(Chapter2TriggeredEvent.OasisPartiallyRestoredByFill, true);
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.OasisPartiallyRestoredByFill
                    ); //進行ログを登録
                    FungusHelper.ExecuteBlock(targetFlowchart, "OasisPartiallyRestored");
                    isEventTriggered = true; // イベントがトリガーされたことを記録
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
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.FirstMetDesertTempleBoss
                    ); //進行ログを登録
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstMetDesertTempleBoss");
                }
                else if (
                    flagManager.GetBoolFlag(Chapter2TriggeredEvent.OasisPartiallyRestoredByFill)
                    && !flagManager.GetBoolFlag(Chapter2TriggeredEvent.BeforeEnteringDesertTemple)
                )
                {
                    flagManager.SetBoolFlag(
                        Chapter2TriggeredEvent.BeforeEnteringDesertTemple,
                        true
                    );
                    FungusHelper.ExecuteBlock(targetFlowchart, "BeforeEnteringDesertTemple");
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
                    FungusHelper.ExecuteBlock(targetFlowchart, "BeforeDustDevilBoss");
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                }
                break;
            case FieldName.DustWindBossField:
                if (!flagManager.GetBoolFlag(Chapter2TriggeredEvent.DustDevilBossDefeated))
                {
                    // フラグを立てるのはFlowchart内で行う
                    //flagManager.SetBoolFlag(Chapter2TriggeredEvent.DustDevilBossDefeated, true);
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.DustDevilBossAppear
                    ); //進行ログを登録
                    FungusHelper.ExecuteBlock(targetFlowchart, "DustDevilBossAppear");
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                }
                break;
            case FieldName.TempleEntranceField1:
                if (!flagManager.GetBoolFlag(Chapter2TriggeredEvent.TempleBossSmokeDefeated))
                {
                    FungusHelper.ExecuteBlock(targetFlowchart, "TempleBossSmokeAppear");
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                }
                break;
            case FieldName.TempleEntranceField2:
                if (
                    flagManager.GetBoolFlag(Chapter2TriggeredEvent.AllOrbsPlacedInDevice)
                    && !flagManager.GetBoolFlag(
                        Chapter2TriggeredEvent.TalkedToFillAfterAllOrbsPlaced
                    )
                )
                {
                    // フラグを立てるのはFlowchart内で行う
                    // flagManager.SetBoolFlag(
                    //     Chapter2TriggeredEvent.TalkedToFillAfterAllOrbsPlaced,
                    //     true
                    // );
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.TalkToFillAfterAllOrbsPlaced
                    ); //進行ログを登録
                    FungusHelper.ExecuteBlock(targetFlowchart, "TalkToFillAfterAllOrbsPlaced");
                    isEventTriggered = true; // イベントがトリガーされたことを記録
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
                    orbFlag: Chapter2TriggeredEvent.GreenOrbPlacedInDevice,
                    blockName: "GreenOrbPlacedInDevice",
                    hintFlag: Chapter2TriggeredEvent.HeardHintAboutGreenOrb,
                    hintBlockName: "GreenOrbHint"
                );
                break;

            case FieldName.OrangeOrbDeviceField:
                HandleOrbDevice(
                    orbFlag: Chapter2TriggeredEvent.OrangeOrbPlacedInDevice,
                    blockName: "OrangeOrbPlacedInDevice",
                    hintFlag: Chapter2TriggeredEvent.HeardHintAboutOrangeOrb,
                    hintBlockName: "OrangeOrbHint"
                );
                break;

            case FieldName.PurpleOrbDeviceField:
                HandleOrbDevice(
                    orbFlag: Chapter2TriggeredEvent.PurpleOrbPlacedInDevice,
                    blockName: "PurpleOrbPlacedInDevice",
                    hintFlag: Chapter2TriggeredEvent.HeardHintAboutPurpleOrb,
                    hintBlockName: "PurpleOrbHint"
                );
                break;
            case FieldName.LotteryManagerField:
                if (
                    flagManager.GetBoolFlag(Chapter2TriggeredEvent.TempleBossSmokeDefeated)
                    && !flagManager.GetBoolFlag(Chapter2TriggeredEvent.FirstMetLotteryManager)
                )
                {
                    flagManager.SetBoolFlag(Chapter2TriggeredEvent.FirstMetLotteryManager, true);
                    flagManager.SetBoolFlag(
                        Chapter2TriggeredEvent.ReceivedInfoAboutPurpleOrbFromFill,
                        true
                    ); // 紫のオーブについてFillから情報を得たことも同時にフラグを立てる
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstMetLotteryManager");
                }
                break;
            case FieldName.TempleRoofField:
                GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                    ProgressLogName.TempleBossAppear
                ); //進行ログを登録
                FungusHelper.ExecuteBlock(targetFlowchart, "TempleBossAppear");
                isEventTriggered = true; // イベントがトリガーされたことを記録
                break;
        }
    }

    #region オーブ装置関連の共通処理
    /// <summary>
    /// オーブ装置共通の処理
    /// </summary>
    /// <param name="orbFlag">この装置に対応する完了フラグ</param>
    /// <param name="blockName">実行するFungusブロック名</param>
    /// <param name="hintFlag">（任意）ヒントを聞いたかのフラグ。ヒントがない場合はNoneを指定</param>
    /// <param name="hintBlockName">（任意）ヒントを聞いていない場合に実行するFungusブロック名。ヒントがない場合や、hintFlagがNoneの場合はnullを指定</param>
    private void HandleOrbDevice(
        Chapter2TriggeredEvent orbFlag,
        string blockName,
        Chapter2TriggeredEvent hintFlag = Chapter2TriggeredEvent.None,
        string hintBlockName = null
    )
    {
        // まずは全てのオーブが配置されたか確認して、完了イベントが発火していないなら発火させる
        CheckAllOrbsPlaced();

        // 既にこのオーブがはまっているなら何もしない
        if (flagManager.GetBoolFlag(orbFlag))
            return;

        // アイテムを持っているか確認
        if (GameManager.instance.savedata.ItemInventoryData.GetItemAmount(orbItemData) > 0)
        {
            // フラグを立てるのはFlowchart内で行う
            // // 1. フラグを即座に立てる（これにより、直後の全数チェックでカウントされる）
            // flagManager.SetBoolFlag(orbFlag, true);

            // 2. この装置のイベント(オーブをはめる演出)を実行
            // （BaseFieldEventのExecuteEventBlockを使うと楽ですが、ここでは明示的に書きます）
            GameManager.instance.savedata.ItemInventoryData.UseItem(orbItemData); // アイテムを1つ減らす
            FungusHelper.ExecuteBlock(targetFlowchart, blockName);
        }
        else if (
            hintFlag != Chapter2TriggeredEvent.None
            && !flagManager.GetBoolFlag(hintFlag)
            && !string.IsNullOrEmpty(hintBlockName)
        )
        {
            // ヒントフラグが設定されていて、まだヒントを聞いていない場合はヒントイベントを発火させる

            // フラグを立てるのはFlowchart内で行う
            // flagManager.SetBoolFlag(hintFlag, true);
            FungusHelper.ExecuteBlock(targetFlowchart, hintBlockName);
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
            //進行ログを登録
            GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                ProgressLogName.AllOrbsPlacedInDesertTemple
            );
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

using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceClassName: "FieldEvent_Chapter1")]
public class Chapter1FieldEvent : BaseFieldEvent
{
    [SerializeField]
    private FieldName fieldname = FieldName.None; // フィールド名を設定するための変数

    private enum FieldName
    {
        None = 0, // フィールド名が設定されていない場合の初期値
        Chapter1StartField = 1, // 第一章開始フィールド
        VillageEntranceField = 2, // 村の入り口フィールド
        ShopGirlField = 4, // 村のショップの女の子フィールド
        VillageManField = 6, // 村の男フィールド
        WellField = 5, // 井戸フィールド
        UpperRiverField = 7, // 上流の川フィールド
        RiverBossField = 10, // 川のボスフィールド
        ShopGirlHouse = 12, // ショップの女の子の家
        CaveBossField = 13, // 洞窟のボスフィールド
    }

    protected override string EventName => fieldname.ToString();

    protected override void Awake()
    {
        base.Awake();

        if (fieldname == FieldName.None)
        {
            Debug.LogWarning(
                $"{this.gameObject.name} の Chapter1FieldEvent にフィールド名が設定されていません。",
                this
            );
        }
    }

    protected override void HandleEvent()
    {
        switch (fieldname)
        {
            case FieldName.Chapter1StartField:
                if (!flagManager.GetBoolFlag(Chapter1TriggeredEvent.Chapter1Start))
                {
                    flagManager.SetBoolFlag(Chapter1TriggeredEvent.Chapter1Start, true);
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                    FungusHelper.ExecuteBlock(targetFlowchart, "Chapter1Start");
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.Chapter1Start
                    ); // 第一章開始のログを登録
                    GameManager.instance.savedata.EnemyRecordData.RegisterEncounter(
                        EnemyName.Slime_Normal
                    ); // スライム（ノーマル）と遭遇済みとして登録
                    GameManager.instance.savedata.EnemyRecordData.RegisterEncounter(
                        EnemyName.Bird_Normal
                    ); // 鳥（ノーマル）と遭遇済みとして登録
                    GameManager.instance.savedata.EnemyRecordData.RegisterEncounter(
                        EnemyName.Slime_Rare
                    ); // スライム（レア）と遭遇済みとして登録
                }
                break;
            case FieldName.VillageEntranceField:
                if (!flagManager.GetBoolFlag(Chapter1TriggeredEvent.FirstEnteredVillage))
                {
                    flagManager.SetBoolFlag(Chapter1TriggeredEvent.FirstEnteredVillage, true);
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstEnteredVillage");
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.VillageTourStart
                    ); // 村の観光開始のログを登録
                }
                break;
            case FieldName.WellField:
                if (!flagManager.GetBoolFlag(TutorialEvent.SwordTutorialComplete))
                {
                    flagManager.SetBoolFlag(TutorialEvent.SwordTutorialComplete, true);
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                    GameManager.instance.savedata.WeaponInventoryData.AddWeapon(
                        BladeName.Blade_wood
                    ); //木の剣を入手
                    WeaponManager.instance.ReplaceEquippedWeapon(BladeName.Blade_wood); //木の剣を装備
                    PlayerManager.instance.SetPlayerAttackType(PlayerAttackType.Blade); // プレイヤーの攻撃方法を剣に変更
                    PlayerManager.instance.SetPlayerBoolStatus(
                        PlayerStatusBoolName.isChangeAttackType,
                        false
                    );
                    //プレイヤーが攻撃方法を変更できないようにする
                    FungusHelper.ExecuteBlock(targetFlowchart, "SwordTutorialComplete");
                    GameManager.instance.savedata.TipsData.RegisterTipsData(
                        TipsName.WeaponTypeChange
                    ); // 武器タイプ変更のヒントを登録
                    GameManager.instance.savedata.TipsData.RegisterTipsData(TipsName.WeaponChange); // 武器変更のヒントを登録
                }
                break;
            case FieldName.VillageManField:
                if (!flagManager.GetBoolFlag(Chapter1TriggeredEvent.VillageTourComplete))
                {
                    flagManager.SetBoolFlag(Chapter1TriggeredEvent.VillageTourComplete, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "VillageTourComplete");
                }
                else if (
                    flagManager.GetBoolFlag(Chapter1TriggeredEvent.WellQuestComplete)
                    && !flagManager.GetBoolFlag(Chapter1TriggeredEvent.RiverQuestReceived)
                )
                {
                    flagManager.SetBoolFlag(Chapter1TriggeredEvent.RiverQuestReceived, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "RiverQuestReceived");
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.RiverQuestStart
                    ); // 川のクエスト受け取りのログを登録
                    GameManager.instance.savedata.TipsData.RegisterTipsData(TipsName.GameOver); // ゲームオーバーのヒントを登録
                }
                else if (
                    flagManager.GetBoolFlag(Chapter1TriggeredEvent.UpperRiverReached)
                    && !flagManager.GetBoolFlag(Chapter1TriggeredEvent.RockDestructionRequested)
                )
                {
                    flagManager.SetBoolFlag(Chapter1TriggeredEvent.RockDestructionRequested, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "RockDestructionRequested");
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.RequestRockDestruction
                    ); // 岩の破壊依頼のログを登録
                }
                else if (
                    flagManager.GetBoolFlag(Chapter1TriggeredEvent.RiverBossDefeated)
                    && !flagManager.GetBoolFlag(Chapter1TriggeredEvent.HeardRumorAboutShopGirl)
                )
                {
                    flagManager.SetBoolFlag(Chapter1TriggeredEvent.HeardRumorAboutShopGirl, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "HeardRumorAboutShopGirl");
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.HeardShopGirlRumor
                    ); // ショップの女の子についての噂を聞いたログを登録
                }
                break;
            case FieldName.ShopGirlField:
                if (!flagManager.GetBoolFlag(Chapter1TriggeredEvent.ShopGirlFirstMet))
                {
                    flagManager.SetBoolFlag(Chapter1TriggeredEvent.ShopGirlFirstMet, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "ShopGirlFirstMet");
                }
                else if (
                    flagManager.GetBoolFlag(Chapter1TriggeredEvent.VillageTourComplete)
                    && !flagManager.GetBoolFlag(Chapter1TriggeredEvent.WellQuestReceived)
                )
                {
                    flagManager.SetBoolFlag(Chapter1TriggeredEvent.WellQuestReceived, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "WellQuestReceived");
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.WellQuestStart
                    ); // 井戸のクエスト受け取りのログを登録
                }
                else if (
                    flagManager.GetBoolFlag(Chapter1TriggeredEvent.WellEnemyDefeated)
                    && !flagManager.GetBoolFlag(Chapter1TriggeredEvent.WellQuestComplete)
                )
                {
                    flagManager.SetBoolFlag(Chapter1TriggeredEvent.WellQuestComplete, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "WellQuestComplete");
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.WellQuestComplete
                    ); // 井戸のクエスト完了のログを登録
                }
                else if (
                    flagManager.GetBoolFlag(Chapter1TriggeredEvent.RiverBossDefeated)
                    && !flagManager.GetBoolFlag(Chapter1TriggeredEvent.ShopGirlMissing)
                )
                {
                    flagManager.SetBoolFlag(Chapter1TriggeredEvent.ShopGirlMissing, true);
                    flagManager.SetBoolFlag(Chapter1TriggeredEvent.ShopGirlHouseUnlocked, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "ShopGirlMissing");
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.StartShopGirlSearch
                    ); // 村の店の少女の探索を開始のログを登録
                }
                break;
            case FieldName.UpperRiverField:
                if (!flagManager.GetBoolFlag(Chapter1TriggeredEvent.UpperRiverReached))
                {
                    flagManager.SetBoolFlag(Chapter1TriggeredEvent.UpperRiverReached, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "UpperRiverReached");
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.EncounterRiverRock
                    ); // 川の岩に遭遇のログを登録
                    GameManager.instance.savedata.TipsData.RegisterTipsData(TipsName.FastTravel); // ファストトラベルのヒントを登録
                }
                else if (
                    flagManager.GetBoolFlag(Chapter1TriggeredEvent.RockDestructionRequested)
                    && !flagManager.GetBoolFlag(Chapter1TriggeredEvent.BeforeRiverBoss)
                )
                {
                    flagManager.SetBoolFlag(Chapter1TriggeredEvent.BeforeRiverBoss, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "BeforeRiverBoss");
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.CompleteRockDestruction
                    ); // 岩の破壊依頼完了のログを登録
                }
                break;
            case FieldName.RiverBossField:
                if (!flagManager.GetBoolFlag(Chapter1TriggeredEvent.RiverBossDefeated))
                {
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                    FungusHelper.ExecuteBlock(targetFlowchart, "RiverBossAppear");
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.RiverBossAppear
                    ); // 川のボス出現のログを登録
                }
                break;
            case FieldName.ShopGirlHouse:
                if (!flagManager.GetBoolFlag(Chapter1TriggeredEvent.BeforeCaveBoss))
                {
                    flagManager.SetBoolFlag(Chapter1TriggeredEvent.BeforeCaveBoss, true);
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                    FungusHelper.ExecuteBlock(targetFlowchart, "BeforeCaveBoss");
                }
                break;
            case FieldName.CaveBossField:
                if (!flagManager.GetBoolFlag(Chapter1TriggeredEvent.CaveBossDefeated))
                {
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                    FungusHelper.ExecuteBlock(targetFlowchart, "CaveBossAppear");
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.HouseCaveBossAppear
                    ); // 家の洞窟のボス出現のログを登録
                }
                break;
        }
    }
}

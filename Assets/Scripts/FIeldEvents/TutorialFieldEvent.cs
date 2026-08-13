using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceClassName: "FieldEvent_Tutorial")]
public class TutorialFieldEvent : BaseFieldEvent
{
    [SerializeField]
    private FieldName fieldname = FieldName.None; // フィールド名を設定するための変数

    [ShowIf(nameof(fieldname), FieldName.EnemyTutorialField)]
    [Header("Enemy Tutorial Settings")]
    [Tooltip("EnemyTutorialFieldでドロップアイテムを全開放する敵のデータのリスト")]
    [SerializeField]
    private List<EnemyData> targetEnemiesForTutorial = new List<EnemyData>();

    private enum FieldName
    {
        None = 0,
        PrologueStartField = 1,
        PrologueEndField = 5,
        StartField = 10,
        RobotEncounterField = 15,
        EnemyTutorialField = 20,
        jumpTutorialField = 25,

        // dipTutorialField = 30,
        CrystalTutorialField = 35,
        CrystalQuestCompleteField = 37, // クリスタルのクエストを完了したフィールド
        ItemTutorialField = 36,
        Stage1EnterEnemyField = 40,
        donutMountainDiscover = 45,
        BreakableShootTutorialField = 50,
        FirstBossAppearField = 55,
        WomanAppearField = 60,
        SecondPrologueStartField = 65,
        SecondPrologueEndField = 70,
        BeforeFirstBossField = 75,
        InteractTutorialField = 80,
    }

    protected override string EventName => fieldname.ToString();

    protected override void Awake()
    {
        base.Awake();

        if (fieldname == FieldName.None)
        {
            Debug.LogWarning(
                $"{this.gameObject.name}のTutorialFieldEventにフィールド名が設定されていません",
                this
            );
        }
    }

    protected override void HandleEvent()
    {
        switch (fieldname)
        {
            //プロローグスタート
            case FieldName.PrologueStartField:
                if (!flagManager.GetBoolFlag(PrologueTriggeredEvent.PrologueStart))
                {
                    flagManager.SetBoolFlag(PrologueTriggeredEvent.PrologueStart, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "PrologueStartField");
                    isEventTriggered = true;
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.GameFirstStart
                    ); // 初回ゲーム起動のログを登録
                    GameManager.instance.savedata.TipsData.RegisterTipsData(TipsName.BasicControls); // 基本操作のヒントを登録
                    GameManager.instance.savedata.TipsData.RegisterTipsData(TipsName.UIControls); // UI操作のヒントを登録
                    GameManager.instance.savedata.TipsData.RegisterTipsData(TipsName.GuideMenu); // ガイドメニューのヒントを登録
                }
                break;

            case FieldName.InteractTutorialField:
                if (!flagManager.GetBoolFlag(TutorialEvent.InteractTutorialComplete))
                {
                    flagManager.SetBoolFlag(TutorialEvent.InteractTutorialComplete, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "InteractTutorialField");
                    isEventTriggered = true;
                    GameManager.instance.savedata.TipsData.RegisterTipsData(
                        TipsName.InteractionIcons
                    ); // ふきだしのヒントを登録
                }
                break;

            case FieldName.PrologueEndField:
                if (!flagManager.GetBoolFlag(PrologueTriggeredEvent.PrologueEndStart))
                {
                    flagManager.SetBoolFlag(PrologueTriggeredEvent.PrologueEndStart, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "PrologueEndField");
                    isEventTriggered = true;
                }
                break;

            case FieldName.StartField:
                if (!flagManager.GetBoolFlag(PrologueTriggeredEvent.TutorialStart))
                {
                    flagManager.SetBoolFlag(PrologueTriggeredEvent.TutorialStart, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "StartField");
                    GameManager.instance.savedata.TipsData.RegisterTipsData(TipsName.StatusLevel); // ステータスレベルのヒントを登録
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.TutorialStart
                    ); // チュートリアル開始のログを登録
                    isEventTriggered = true;
                }
                break;

            case FieldName.RobotEncounterField:
                if (!flagManager.GetBoolFlag(PrologueTriggeredEvent.RobotEncounter))
                {
                    //フラグの変更はtargetFlowchartに任せる
                    FungusHelper.ExecuteBlock(targetFlowchart, "RobotEncounterField");
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.FirstMetRobot
                    ); // 初めてロボットに出会うログを登録
                    isEventTriggered = true;
                }
                break;

            case FieldName.EnemyTutorialField:
                if (!flagManager.GetBoolFlag(TutorialEvent.EnemyTutorialComplete))
                {
                    flagManager.SetBoolFlag(TutorialEvent.EnemyTutorialComplete, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "EnemyTutorialField");
                    isEventTriggered = true;
                    GameManager.instance.savedata.TipsData.RegisterTipsData(TipsName.HudDisplay); // HUD表示のヒントを登録
                    if (targetEnemiesForTutorial != null && targetEnemiesForTutorial.Count > 0)
                    {
                        foreach (var enemyData in targetEnemiesForTutorial)
                        {
                            if (enemyData != null)
                            {
                                GameManager.instance.savedata.EnemyRecordData.UnlockAllDropItems(
                                    enemyData
                                );
                            }
                        }
                    }
                }
                break;

            case FieldName.jumpTutorialField:
                if (!flagManager.GetBoolFlag(TutorialEvent.JumpTutorialComplete))
                {
                    flagManager.SetBoolFlag(TutorialEvent.JumpTutorialComplete, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "JumpTutorialField");
                    isEventTriggered = true;
                }
                break;

            case FieldName.CrystalTutorialField:
                if (!flagManager.GetBoolFlag(TutorialEvent.CrystalTutorialComplete))
                {
                    flagManager.SetBoolFlag(TutorialEvent.CrystalTutorialComplete, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "CrystalTutorialField");
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.CrystalQuestStart
                    ); // クリスタルのクエスト開始のログを登録
                    isEventTriggered = true;
                }
                break;

            case FieldName.CrystalQuestCompleteField:
                if (
                    !flagManager.GetBoolFlag(PrologueTriggeredEvent.CrystalQuestComplete)
                    && flagManager.IsDoorUnlocked(4)
                )
                {
                    flagManager.SetBoolFlag(PrologueTriggeredEvent.CrystalQuestComplete, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "CrystalQuestCompleteField");
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.CrystalQuestComplete
                    ); // クリスタルのクエスト完了のログを登録
                    isEventTriggered = true;
                }
                break;
            case FieldName.ItemTutorialField:
                if (!flagManager.GetBoolFlag(TutorialEvent.ItemUseTutorialComplete))
                {
                    flagManager.SetBoolFlag(TutorialEvent.ItemUseTutorialComplete, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "ItemUseTutorialField");
                    isEventTriggered = true;
                    GameManager.instance.savedata.TipsData.RegisterTipsData(TipsName.ItemUsage); // アイテム使用のヒントを登録
                    GameManager.instance.savedata.TipsData.RegisterTipsData(TipsName.ItemDetail); // アイテム詳細のヒントを登録
                    GameManager.instance.savedata.TipsData.RegisterTipsData(
                        TipsName.CurrentEffects
                    ); // 現在の効果のヒントを登録
                    GameManager.instance.savedata.TipsData.RegisterTipsData(TipsName.EffectTypes1); // 効果の種類1のヒントを登録
                    break;
                }

                if (!flagManager.GetBoolFlag(TutorialEvent.QuickItemTutorialComplete))
                {
                    flagManager.SetBoolFlag(TutorialEvent.QuickItemTutorialComplete, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "QuickItemTutorialField");
                    isEventTriggered = true;
                    GameManager.instance.savedata.TipsData.RegisterTipsData(TipsName.QuickSlot); // クイックスロットリストのヒントを登録
                }
                break;

            case FieldName.Stage1EnterEnemyField:
                if (!flagManager.GetBoolFlag(PrologueTriggeredEvent.Stage1EnterEnemyRoom))
                {
                    flagManager.SetBoolFlag(PrologueTriggeredEvent.Stage1EnterEnemyRoom, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "Stage1EnterEnemyField");
                    isEventTriggered = true;
                    GameManager.instance?.savedata?.TipsData?.RegisterTipsData(TipsName.EnemyTypes); // 敵の種類とアウトラインのヒントを登録
                }
                break;

            case FieldName.donutMountainDiscover:
                if (flagManager.GetIntFlag(PrologueCountedEvent.DonutMountainCount) == 0)
                {
                    FungusHelper.ExecuteBlock(targetFlowchart, "DonutMountainField");
                    flagManager.SetIntFlag(PrologueCountedEvent.DonutMountainCount, 1);
                    isEventTriggered = true;
                }
                break;

            case FieldName.BreakableShootTutorialField:
                // if (!flagManager.GetBoolFlag(TutorialEvent.BreakableShootTutorialComplete))
                // {
                //     flagManager.SetBoolFlag(TutorialEvent.BreakableShootTutorialComplete, true);
                //     FungusHelper.ExecuteBlock(targetFlowchart, "BreakableShootTutorialField");

                // }
                isEventTriggered = true;
                break;

            // 初ボス直前イベント
            case FieldName.BeforeFirstBossField:
                if (
                    !flagManager.GetBoolFlag(PrologueTriggeredEvent.BeforeFirstBoss)
                    && flagManager.IsDoorUnlocked(4)
                )
                {
                    flagManager.SetBoolFlag(PrologueTriggeredEvent.BeforeFirstBoss, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "BeforeFirstBossField");
                    isEventTriggered = true;
                }
                break;

            // 初ボス出現イベント
            case FieldName.FirstBossAppearField:
                if (!flagManager.GetBoolFlag(PrologueTriggeredEvent.DefeatFirstBoss))
                {
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstBossAppearField");
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.FirstBossAppear
                    ); // 初ボス出現のログを登録
                    isEventTriggered = true;
                }
                break;

            case FieldName.WomanAppearField:
                if (!flagManager.GetBoolFlag(PrologueTriggeredEvent.WomanEventStart))
                {
                    flagManager.SetBoolFlag(PrologueTriggeredEvent.WomanEventStart, true);
                    flagManager.SetBoolFlag(PrologueTriggeredEvent.PrologueStart, true);
                    flagManager.SetBoolFlag(PrologueTriggeredEvent.PrologueEndStart, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "WomanAppearField");
                    isEventTriggered = true;
                }
                break;

            case FieldName.SecondPrologueStartField:
                if (
                    !flagManager.GetBoolFlag(PrologueTriggeredEvent.SecondPrologueStart)
                    && flagManager.GetBoolFlag(PrologueTriggeredEvent.WomanEventStart)
                )
                {
                    flagManager.SetBoolFlag(PrologueTriggeredEvent.SecondPrologueStart, true);
                    flagManager.SetBoolFlag(PrologueTriggeredEvent.PrologueEndStart, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "SecondPrologueStartField");
                    GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                        ProgressLogName.AfterMysteriousWomanEvent
                    ); // 謎の女性イベント後のログを登録
                    isEventTriggered = true;
                }
                break;

            case FieldName.SecondPrologueEndField:
                if (
                    !flagManager.GetBoolFlag(PrologueTriggeredEvent.SecondPrologueEndStart)
                    && flagManager.GetBoolFlag(PrologueTriggeredEvent.WomanEventStart)
                )
                {
                    flagManager.SetBoolFlag(PrologueTriggeredEvent.SecondPrologueEndStart, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "SecondPrologueEndField");
                    isEventTriggered = true;
                }
                break;
        }
    }
}

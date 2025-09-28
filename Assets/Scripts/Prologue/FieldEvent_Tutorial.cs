using UnityEngine;

public class FieldEvent_Tutorial : MonoBehaviour
{
    public Fungus.Flowchart flowchart = null;
    private FlagManager flagManager; // フラグマネージャーのインスタンス
    private bool isEventTriggered = false;

    [SerializeField]
    private FieldName fieldname = FieldName.None; // フィールド名を設定するための変数

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

    private void Awake()
    {
        if (flowchart == null)
        {
            Debug.LogWarning($"{this.gameObject.name}にはFlowChartが設定されていません");
        }

        if (fieldname == FieldName.None)
        {
            Debug.LogWarning($"{this.gameObject.name}のフィールド名が設定されていません");
        }
    }

    private void Start()
    {
        flagManager = FlagManager.instance;

        if (flagManager == null)
        {
            Debug.LogError("FlagManagerが見つかりません。フィールドイベントが正しく動作しません。");
            return;
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        bool canTriggerEvent =
            collision.CompareTag(GameConstants.PlayerTagName) // プレイヤーがトリガーに触れているか
            && Time.timeScale > 0 // ゲームが一時停止していないか
            && !isEventTriggered // イベントがまだトリガーされていないか
            && (PlayerManager.instance?.isControlLocked ?? false) == false; // プレイヤーの操作がロックされていないか

        if (canTriggerEvent)
        {
            if (flagManager != null)
            {
                switch (fieldname)
                {
                    //プロローグスタート
                    case FieldName.PrologueStartField:
                        if (!flagManager.GetBoolFlag(PrologueTriggeredEvent.PrologueStart))
                        {
                            flagManager.SetBoolFlag(PrologueTriggeredEvent.PrologueStart, true);
                            FungusHelper.ExecuteBlock(flowchart, "PrologueStartField");
                            isEventTriggered = true;
                            GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                                ProgressLogName.GameFirstStart
                            ); // 初回ゲーム起動のログを登録
                            GameManager.instance.savedata.TipsData.RegisterTipsData(
                                TipsName.BasicControls
                            ); // 基本操作のヒントを登録
                            GameManager.instance.savedata.TipsData.RegisterTipsData(
                                TipsName.UIControls
                            ); // UI操作のヒントを登録
                            GameManager.instance.savedata.TipsData.RegisterTipsData(
                                TipsName.GuideMenu
                            ); // ガイドメニューのヒントを登録
                        }
                        break;

                    case FieldName.InteractTutorialField:
                        if (!flagManager.GetBoolFlag(TutorialEvent.InteractTutorialComplete))
                        {
                            flagManager.SetBoolFlag(TutorialEvent.InteractTutorialComplete, true);
                            FungusHelper.ExecuteBlock(flowchart, "InteractTutorialField");
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
                            FungusHelper.ExecuteBlock(flowchart, "PrologueEndField");
                            isEventTriggered = true;
                        }
                        break;

                    case FieldName.StartField:
                        if (!flagManager.GetBoolFlag(PrologueTriggeredEvent.TutorialStart))
                        {
                            flagManager.SetBoolFlag(PrologueTriggeredEvent.TutorialStart, true);
                            FungusHelper.ExecuteBlock(flowchart, "StartField");
                            GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                                ProgressLogName.TutorialStart
                            ); // チュートリアル開始のログを登録
                            isEventTriggered = true;
                        }
                        break;

                    case FieldName.RobotEncounterField:
                        if (!flagManager.GetBoolFlag(PrologueTriggeredEvent.RobotEncounter))
                        {
                            //フラグの変更はFlowchartに任せる
                            FungusHelper.ExecuteBlock(flowchart, "RobotEncounterField");
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
                            FungusHelper.ExecuteBlock(flowchart, "EnemyTutorialField");
                            isEventTriggered = true;
                            GameManager.instance.savedata.TipsData.RegisterTipsData(
                                TipsName.HudDisplay
                            ); // HUD表示のヒントを登録
                        }
                        break;

                    case FieldName.jumpTutorialField:
                        if (!flagManager.GetBoolFlag(TutorialEvent.JumpTutorialComplete))
                        {
                            flagManager.SetBoolFlag(TutorialEvent.JumpTutorialComplete, true);
                            FungusHelper.ExecuteBlock(flowchart, "JumpTutorialField");
                            isEventTriggered = true;
                        }
                        break;

                    case FieldName.CrystalTutorialField:
                        if (!flagManager.GetBoolFlag(TutorialEvent.CrystalTutorialComplete))
                        {
                            flagManager.SetBoolFlag(TutorialEvent.CrystalTutorialComplete, true);
                            FungusHelper.ExecuteBlock(flowchart, "CrystalTutorialField");
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
                            flagManager.SetBoolFlag(
                                PrologueTriggeredEvent.CrystalQuestComplete,
                                true
                            );
                            FungusHelper.ExecuteBlock(flowchart, "CrystalQuestCompleteField");
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
                            FungusHelper.ExecuteBlock(flowchart, "ItemUseTutorialField");
                            isEventTriggered = true;
                            GameManager.instance.savedata.TipsData.RegisterTipsData(
                                TipsName.ItemUsage
                            ); // アイテム使用のヒントを登録
                            GameManager.instance.savedata.TipsData.RegisterTipsData(
                                TipsName.ItemDetail
                            ); // アイテム詳細のヒントを登録
                            GameManager.instance.savedata.TipsData.RegisterTipsData(
                                TipsName.CurrentEffects
                            ); // 現在の効果のヒントを登録
                            GameManager.instance.savedata.TipsData.RegisterTipsData(
                                TipsName.EffectTypes1
                            ); // 効果の種類1のヒントを登録
                            break;
                        }

                        if (!flagManager.GetBoolFlag(TutorialEvent.QuickItemTutorialComplete))
                        {
                            flagManager.SetBoolFlag(TutorialEvent.QuickItemTutorialComplete, true);
                            FungusHelper.ExecuteBlock(flowchart, "QuickItemTutorialField");
                            isEventTriggered = true;
                            GameManager.instance.savedata.TipsData.RegisterTipsData(
                                TipsName.QuickSlot
                            ); // クイックスロットリストのヒントを登録
                        }
                        break;

                    case FieldName.Stage1EnterEnemyField:
                        if (!flagManager.GetBoolFlag(PrologueTriggeredEvent.Stage1EnterEnemyRoom))
                        {
                            flagManager.SetBoolFlag(
                                PrologueTriggeredEvent.Stage1EnterEnemyRoom,
                                true
                            );
                            FungusHelper.ExecuteBlock(flowchart, "Stage1EnterEnemyField");
                            isEventTriggered = true;
                            GameManager.instance?.savedata?.TipsData?.RegisterTipsData(
                                TipsName.EnemyTypes
                            ); // 敵の種類とアウトラインのヒントを登録
                        }
                        break;

                    case FieldName.donutMountainDiscover:
                        if (flagManager.GetIntFlag(PrologueCountedEvent.DonutMountainCount) == 0)
                        {
                            FungusHelper.ExecuteBlock(flowchart, "DonutMountainField");
                            flagManager.SetIntFlag(PrologueCountedEvent.DonutMountainCount, 1);
                            isEventTriggered = true;
                        }
                        break;

                    case FieldName.BreakableShootTutorialField:
                        if (!flagManager.GetBoolFlag(TutorialEvent.BreakableShootTutorialComplete))
                        {
                            flagManager.SetBoolFlag(
                                TutorialEvent.BreakableShootTutorialComplete,
                                true
                            );
                            FungusHelper.ExecuteBlock(flowchart, "BreakableShootTutorialField");
                            isEventTriggered = true;
                        }
                        break;

                    case FieldName.BeforeFirstBossField:
                        if (
                            !flagManager.GetBoolFlag(PrologueTriggeredEvent.BeforeFirstBoss)
                            && flagManager.IsDoorUnlocked(4)
                        )
                        {
                            flagManager.SetBoolFlag(PrologueTriggeredEvent.BeforeFirstBoss, true);
                            FungusHelper.ExecuteBlock(flowchart, "BeforeFirstBossField");
                            isEventTriggered = true;
                        }
                        break;

                    case FieldName.FirstBossAppearField:
                        if (!flagManager.GetBoolFlag(PrologueTriggeredEvent.FirstBossAppear))
                        {
                            flagManager.SetBoolFlag(PrologueTriggeredEvent.FirstBossAppear, true);
                            FungusHelper.ExecuteBlock(flowchart, "FirstBossAppearField");
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
                            FungusHelper.ExecuteBlock(flowchart, "WomanAppearField");
                            isEventTriggered = true;
                        }
                        break;

                    case FieldName.SecondPrologueStartField:
                        if (
                            !flagManager.GetBoolFlag(PrologueTriggeredEvent.SecondPrologueStart)
                            && flagManager.GetBoolFlag(PrologueTriggeredEvent.WomanEventStart)
                        )
                        {
                            flagManager.SetBoolFlag(
                                PrologueTriggeredEvent.SecondPrologueStart,
                                true
                            );
                            flagManager.SetBoolFlag(PrologueTriggeredEvent.PrologueEndStart, true);
                            FungusHelper.ExecuteBlock(flowchart, "SecondPrologueStartField");
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
                            flagManager.SetBoolFlag(
                                PrologueTriggeredEvent.SecondPrologueEndStart,
                                true
                            );
                            FungusHelper.ExecuteBlock(flowchart, "SecondPrologueEndField");
                            isEventTriggered = true;
                        }
                        break;
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Color fillColor = new Color(0f, 1f, 1f, 0.2f); // 半透明のシアン
        Color borderColor = Color.cyan; // 枠線は明るいシアン

        BoxCollider2D box2D = GetComponent<BoxCollider2D>();
        if (box2D == null)
            return;

        // ワールド座標での位置とサイズを計算

        Gizmos.matrix = Matrix4x4.TRS(
            transform.position + (Vector3)box2D.offset,
            transform.rotation,
            transform.lossyScale
        );
        Gizmos.color = fillColor;
        Gizmos.DrawCube(Vector3.zero, (Vector3)box2D.size);
        Gizmos.color = borderColor;
        Gizmos.DrawWireCube(Vector3.zero, (Vector3)box2D.size);
    }
}

using System;
using System.Linq;
using Fungus;
using UnityEngine;

[CommandInfo("Custom", "Talk Start", "会話が始まる前のコマンド")]
public class TalkStartCommand : Command
{
    public static event Action OnTalkStartExecuted; // このコマンドが実行されたことを通知するイベント
    private Flowchart globalFlowchart = null;
    private BlockType parentBlockType;

    public override void OnEnter()
    {
        Debug.Log($"[TalkStateDebug] TalkStart entered. Block: {ParentBlock?.BlockName}", this);

        // 会話開始イベントを発行し、購読しているクラスに通知する
        OnTalkStartExecuted?.Invoke();

        if (globalFlowchart == null)
        {
            globalFlowchart = GameObject.Find("GlobalFlowchart").GetComponent<Flowchart>();
            if (globalFlowchart == null)
            {
                Debug.LogError("GlobalFlowchartが見つかりません！");
                Continue();
                return;
            }
        }

        // 必要なマネージャーが一つでも欠けていたら、エラーを出して処理を中断
        if (
            GameManager.instance == null
            || TimeManager.instance == null
            || PlayerBodyManager.instance == null
            || globalFlowchart == null
        )
        {
            if (GameManager.instance == null)
                Debug.LogError("GameManagerのインスタンスが存在しません");
            if (TimeManager.instance == null)
                Debug.LogError("TimeManagerのインスタンスが存在しません");
            if (PlayerBodyManager.instance == null)
                Debug.LogError("PlayerBodyManagerのインスタンスが存在しません");
            if (globalFlowchart == null)
                Debug.LogError("GlobalFlowchartがインスペクターから設定されていません");

            Continue();
            return;
        }

        // --- ここから先のコードは、全てのインスタンスが存在することが保証されている ---

        // 会話中のフラグをONにし、敵の動きを停止する
        GameManager.instance.StartTalk();
        TimeManager.instance.SetEnemyMovePaused(true);

        // 1. このコマンドが所属しているブロックを取得
        Block currentBlock = ParentBlock;
        if (currentBlock == null)
        {
            Debug.LogError("コマンドが所属するブロックが見つかりませんでした。", this);
            Continue();
            return;
        }
        else
        {
            parentBlockType = currentBlock.TypeOfBlock; // BlockTypeを取得
            FungusCustomSignals.DoTalkBlockStart(parentBlockType); // BlockTypeをHeroinPortraitControllerに通知する
            FungusCustomSignals.DoTalkBlockIsSkippable(currentBlock.IsSkippable); // Blockのスキップ可能情報をFungusSkipControllerに通知する

            if (parentBlockType == BlockType.Story)
            {
                BGMManager.instance.SetDucking(true); // ストーリーブロックならBGMをダッキングする
            }
        }

        // 2. 同じブロック内にある If, ElseIf, Else コマンドの合計数をLINQで数える
        int conditionalCommandCount = currentBlock.CommandList.Count(command =>
            command is IfDialogueSeedCommand
            || command is ElseIfDialogueSeedCommand
            || command is Else
        );

        // 3. 条件分岐コマンドが1つ以上存在する場合のみ、乱数を生成して変数を設定
        if (conditionalCommandCount > 0)
        {
            // 4. 0から「合計数 - 1」までの範囲でランダムな整数を生成
            int randomState = UnityEngine.Random.Range(0, conditionalCommandCount);

            // 5. GlobalFlowchart内の "DialogueSeed" 変数を探す
            IntegerVariable dialogueSeedVariable = globalFlowchart.GetVariable<IntegerVariable>(
                "DialogueSeed"
            );
            if (dialogueSeedVariable != null)
            {
                // 6. 変数にランダムな値を設定
                dialogueSeedVariable.Value = randomState;
            }
            else
            {
                Debug.LogError(
                    $"Flowchart '{globalFlowchart.name}' 内に 'DialogueSeed' という名前のInt変数が見つかりません！"
                );
            }
        }

        // PlayerManagerではなく、PlayerBodyManagerからBodyStateを取得
        int bodyStateValue = PlayerBodyManager.instance.BodyState;

        // Flowchart内の"BodyState"変数を探す
        IntegerVariable outputVariable = globalFlowchart.GetVariable<IntegerVariable>("BodyState");
        if (outputVariable != null)
        {
            outputVariable.Value = bodyStateValue;
        }
        else
        {
            Debug.LogError(
                $"Flowchart '{globalFlowchart.name}' 内に 'BodyState' という名前のInt変数が見つかりません！"
            );
        }

        Continue();
    }

    public override string GetSummary()
    {
        return "会話開始処理（時間停止・状態設定）";
    }

    public override Color GetButtonColor()
    {
        return new Color32(255, 255, 255, 255);
    }
}

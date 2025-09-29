using System.Linq;
using Fungus;
using UnityEngine;

/// <summary>
/// DialogueSeed変数を循環させるFungusコマンド
/// </summary>
[CommandInfo(
    "Custom", // カテゴリ名
    "Cycle Dialogue Seed", // コマンド名
    "同じブロック内にあるIf/ElseIf/Elseの数に応じて、GlobalFlowchartの'DialogueSeed'変数の値を循環させます。"
)] // コマンドの説明
public class CycleDialogueSeed : Command
{
    // GlobalFlowchartをキャッシュするための変数
    private Flowchart globalFlowchart = null;

    // ==========【ここから追加】==========
    /// <summary>
    /// このコマンドインスタンス内でSeed値を独自に保持する変数。
    /// 初期値を-1にすることで、初回実行かどうかを判定するフラグとして利用します。
    /// </summary>
    private int ownSeed = -1;
    // ============================

    public override void OnEnter()
    {
        // --- 初期設定・オブジェクト取得 ---

        // globalFlowchartが未取得の場合、シーンから探す
        if (globalFlowchart == null)
        {
            GameObject flowchartObj = GameObject.Find("GlobalFlowchart");
            if (flowchartObj != null)
            {
                globalFlowchart = flowchartObj.GetComponent<Flowchart>();
            }
        }

        // それでもFlowchartが見つからなかった場合はエラーを出して終了
        if (globalFlowchart == null)
        {
            Debug.LogError(
                "シーン内に 'GlobalFlowchart' という名前のゲームオブジェクトが見つかりません！"
            );
            Continue();
            return;
        }

        // このコマンドが所属しているブロックを取得
        Block currentBlock = ParentBlock;
        if (currentBlock == null)
        {
            Debug.LogError("コマンドが所属するブロックが見つかりませんでした。", this);
            Continue();
            return;
        }

        // --- メインロジック ---

        // 同じブロック内にある If, ElseIf, Else コマンドの合計数を数える
        int conditionalCommandCount = currentBlock.CommandList.Count(command =>
            command is IfDialogueSeed || command is ElseIfDialogueSeed || command is Else
        );

        // 分岐が2つ未満の場合、循環させる意味がないので処理を終了
        if (conditionalCommandCount < 2)
        {
            Continue();
            return;
        }

        // GlobalFlowchartから "DialogueSeed" という名前のInteger変数を探す
        IntegerVariable dialogueSeedVariable = globalFlowchart.GetVariable<IntegerVariable>(
            "DialogueSeed"
        );
        if (dialogueSeedVariable == null)
        {
            Debug.LogError(
                $"Flowchart '{globalFlowchart.name}' 内に 'DialogueSeed' という名前のInteger変数が見つかりません！"
            );
            Continue();
            return;
        }

        // このコマンドの独自Seedが未初期化(-1)の場合、Flowchartの現在の値を取得して初期化する
        if (ownSeed == -1)
        {
            ownSeed = dialogueSeedVariable.Value;
        }

        // 独自に保持しているownSeedの値を元に、次の値を計算する
        int nextSeed = ownSeed + 1;

        // 計算後の値が分岐の合計数以上なら、0に戻す（循環させる）
        if (nextSeed >= conditionalCommandCount)
        {
            nextSeed = 0;
        }
        
        // 更新前の値をデバッグログ用に保持
        int previousSeed = ownSeed;

        // 計算結果をこのコマンドの独自変数と、Flowchartの変数の両方に書き戻す
        ownSeed = nextSeed; // 自分の値を更新
        dialogueSeedVariable.Value = nextSeed; // Flowchartの値を更新

        Debug.Log($"DialogueSeedを {previousSeed} -> {nextSeed} に更新しました。");

        // ============================


        // 処理が完了したので、次のコマンドへ進む
        Continue();
    }

    public override string GetSummary()
    {
        // Fungusのブロックエディタに表示されるコマンドの概要
        return "DialogueSeedの値を循環させます";
    }

    public override Color GetButtonColor()
    {
        // コマンドの色を少し変えて見やすくします（任意）
        return new Color32(235, 191, 217, 255);
    }
}
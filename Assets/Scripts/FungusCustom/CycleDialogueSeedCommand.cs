using System.Linq;
using Fungus;
using UnityEngine;

/// <summary>
/// DialogueSeed変数を循環させるFungusコマンド
/// </summary>
[CommandInfo(
    "Custom", 
    "Cycle Dialogue Seed",
    "同じブロック内にあるIf/ElseIf/Elseの数に応じて、GlobalFlowchartの'DialogueSeed'変数の値を循環させます。"
)] // コマンドの説明
public class CycleDialogueSeedCommand : Command
{
    // GlobalFlowchartをキャッシュするための変数
    private Flowchart globalFlowchart = null;

    /// <summary>
    /// 次回実行時に使用するSeed値。
    /// Flowchart上のコマンドごとに保持され、初回実行では0が使用されます。
    /// </summary>
    [SerializeField]
    private int currentSeed = 0;

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
            command is IfDialogueSeedCommand || command is ElseIfDialogueSeedCommand || command is Else
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

        // Inspectorで設定された現在の値を出力する。
        // 分岐数の変更などで値が範囲外になっていた場合は、循環範囲内へ補正する。
        currentSeed = ((currentSeed % conditionalCommandCount) + conditionalCommandCount)
            % conditionalCommandCount;
        dialogueSeedVariable.Value = currentSeed;

        // 次回実行時の値を進める。分岐数に達したら0へ戻す。
        currentSeed = (currentSeed + 1) % conditionalCommandCount;

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

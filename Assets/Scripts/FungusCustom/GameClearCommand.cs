using Fungus;
using UnityEngine;

/// <summary>
/// ゲームクリア時にGlobalFlowchartの指定したブロックを実行するコマンド
/// </summary>
[CommandInfo("Custom", "Game Clear", "ゲームクリア時にGlobalFlowchartの指定ブロックを実行します")]
public class GameClearCommand : Command
{
    private string targetBlockName = "GameClear"; //GlobalFlowchart内で実行したいブロックの名前

    public override void OnEnter()
    {
        // GlobalFlowchartControllerのインスタンスとその内部のFlowchartが存在するか確認
        if (
            GlobalFlowchartController.instance == null
            || GlobalFlowchartController.instance.globalFlowchart == null
        )
        {
            Debug.LogError("GlobalFlowchartController、または対象のFlowchartが見つかりません。");
            Continue();
            return;
        }

        Flowchart globalFlowchart = GlobalFlowchartController.instance.globalFlowchart;

        // 指定されたブロックが存在するか確認してから実行する
        if (globalFlowchart.HasBlock(targetBlockName))
        {
            globalFlowchart.ExecuteBlock(targetBlockName);
        }
        else
        {
            Debug.LogError(
                $"GlobalFlowchart内に '{targetBlockName}' という名前のブロックが見つかりません。"
            );
        }

        Continue();
    }

    public override string GetSummary()
    {
        if (string.IsNullOrEmpty(targetBlockName))
        {
            return "エラー：ブロック名が設定されていません";
        }
        return $"GlobalFlowchartの '{targetBlockName}' を実行";
    }
}

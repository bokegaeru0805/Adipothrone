using Fungus;
using UnityEngine;

/// <summary>
/// Fungusコマンド：BGM停止
/// </summary>
[CommandInfo("BGM", "StopBGM", "現在流れているBGMを停止します")]
public class StopBGMCommand : Command
{
    public override void OnEnter()
    {
        if (BGMManager.instance != null)
        {
            BGMManager.instance.Stop();
        }
        else
        {
            Debug.LogError("BGMManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override string GetSummary()
    {
        return $"BGMを停止します";
    }
}
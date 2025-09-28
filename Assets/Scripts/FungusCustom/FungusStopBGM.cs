using Fungus;
using UnityEngine;

// --------------------------------
// 現在流れているBGMを停止するコマンド
// --------------------------------
[CommandInfo("BGM", "StopBGM", "現在流れているBGMを停止します")]
public class FungusStopBGM : Command
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
using Fungus;
using UnityEngine;

/// <summary>
/// システムに関するSEを停止するコマンド
/// </summary>
[CommandInfo("SE", "Stop SystemEventSE", "システムに関するSEを停止します")]
public class StopSystemEventSECommand : Command
{
    [Tooltip("止めるSE")]
    public SE_SystemEvent SystemEventSE;

    public override void OnEnter()
    {
        if (SEManager.instance != null)
        {
            SEManager.instance.StopSystemEventSE(SystemEventSE);
        }
        else
        {
            Debug.LogError("SEManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override string GetSummary()
    {
        return $"止めるSEは {SystemEventSE}";
    }
}

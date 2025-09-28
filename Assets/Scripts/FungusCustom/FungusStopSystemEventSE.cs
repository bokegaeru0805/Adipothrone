using Fungus;
using UnityEngine;
// --------------------------------
// システムに関するSEを停止するコマンド
// --------------------------------
[CommandInfo("SE", "StopSystemEventSE", "システムに関するSEを停止します")]
public class FungusStopSystemEventSE : Command
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

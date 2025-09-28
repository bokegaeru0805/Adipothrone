using Fungus;
using UnityEngine;
// --------------------------------
// システムに関するSEを再生するコマンド
// --------------------------------
[CommandInfo("SE", "PlaySystemEventSE", "システムに関するSEを再生します")]
public class FungusPlaySystemEventSE : Command
{
    [Tooltip("流すSE")]
    public SE_SystemEvent SystemEventSE;

    public override void OnEnter()
    {
        if (SEManager.instance != null)
        {
            SEManager.instance.PlaySystemEventSE(SystemEventSE);
        }
        else
        {
            Debug.LogError("SEManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override string GetSummary()
    {
        return $"流すSEは {SystemEventSE}";
    }
}

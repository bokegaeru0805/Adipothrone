using Fungus;
using UnityEngine;

/// <summary>
/// システムに関するSEを再生するコマンド
/// </summary>
[CommandInfo("SE", "Play SystemEventSE", "システムに関するSEを再生します")]
public class PlaySystemEventSECommand : Command
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

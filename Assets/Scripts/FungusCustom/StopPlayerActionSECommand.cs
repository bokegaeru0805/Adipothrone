using Fungus;
using UnityEngine;

/// <summary>
/// プレイヤーに関するSEを停止するコマンド
/// </summary>
[CommandInfo("SE", "Stop PlayerActionSE", "プレイヤーに関するSEを停止します")]
public class StopPlayerActionSECommand : Command
{
    [Tooltip("止めるSE")]
    public SE_PlayerAction PlayerActionSE;

    public override void OnEnter()
    {
        if (SEManager.instance != null)
        {
            SEManager.instance.StopPlayerActionSE(PlayerActionSE);
        }
        else
        {
            Debug.LogError("SEManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override string GetSummary()
    {
        return $"止めるSEは {PlayerActionSE}";
    }
}

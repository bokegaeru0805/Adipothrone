using Fungus;
using UnityEngine;
// --------------------------------
// プレイヤーに関するSEを停止するコマンド
// --------------------------------
[CommandInfo("SE", "Stop PlayerActionSE", "プレイヤーに関するSEを停止します")]
public class FungusStopPlayerActionSE : Command
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

using Fungus;
using UnityEngine;
// --------------------------------
// プレイヤーに関するSEを再生するコマンド
// --------------------------------
[CommandInfo("SE", "Play PlayerActionSE", "プレイヤーに関するSEを再生します")]
public class FungusPlayPlayerActionSE : Command
{
    [Tooltip("流すSE")]
    public SE_PlayerAction PlayerActionSE;

    public override void OnEnter()
    {
        if (SEManager.instance != null)
        {
            SEManager.instance.PlayPlayerActionSE(PlayerActionSE);
        }
        else
        {
            Debug.LogError("SEManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override string GetSummary()
    {
        return $"流すSEは {PlayerActionSE}";
    }
}

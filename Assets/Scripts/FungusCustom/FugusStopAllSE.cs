using Fungus;
using UnityEngine;

// --------------------------------
// 流れているSEを全て停止するコマンド
// --------------------------------
[CommandInfo("SE", "StopAllSE", "流れているSEを全て停止します")]
public class FungusStopAllSE : Command
{

    public override void OnEnter()
    {
        if (SEManager.instance != null)
        {
            SEManager.instance.StopAllSE();
        }
        else
        {
            Debug.LogError("SEManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override string GetSummary()
    {
        return $"流れているSEを全て停止";
    }
}

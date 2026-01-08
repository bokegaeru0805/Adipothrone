using Fungus;
using UnityEngine;


/// <summary>
/// 流れているSEを全て停止するコマンド
/// </summary>
[CommandInfo("SE", "StopAllSE", "流れているSEを全て停止します")]
public class StopAllSECommand : Command
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

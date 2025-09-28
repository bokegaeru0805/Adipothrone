using Fungus;
using UnityEngine;

// --------------------------------
// プレイヤーのBodyStateを一定数だけ変更するコマンド
// --------------------------------
[CommandInfo("Player", "Step BodyState", "プレイヤーのBodyStateを一定数だけ変更します")]
public class FungusStepBodyState : Command
{
    [Tooltip("変更する数")]
    public int StepValue = 1;

    [Tooltip("加えるかどうか")]
    public bool isPlus = true;

    public override void OnEnter()
    {
        var playerBodyManager = PlayerBodyManager.instance;
        if (playerBodyManager != null)
        {
            if (isPlus)
            {
                playerBodyManager.StepBodyState(StepValue, true);
            }
            else
            {
                playerBodyManager.StepBodyState(StepValue, false);
            }
        }
        else
        {
            Debug.LogError("PlayerBodyManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override string GetSummary()
    {
        if (isPlus)
        {
            return $"BodyState + {StepValue}";
        }
        else
        {
            return $"BodyState - {StepValue}";
        }
    }
}

using UnityEngine;
using UnityEngine.Playables;

public class BoolFlagControlMixerBehaviour : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (FlagManager.instance == null)
            return;

        int inputCount = playable.GetInputCount();

        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);
            var inputPlayable = (ScriptPlayable<BoolFlagControlBehaviour>)playable.GetInput(i);
            var input = inputPlayable.GetBehaviour();

            // クリップに入った瞬間（Weight > 0）かつ、未実行の場合に処理を行う
            if (inputWeight > 0f && !input.hasExecuted)
            {
                ExecuteFlagChange(input);
                input.hasExecuted = true;
            }
            // クリップから出たらフラグを戻す（ループ再生時などのため）
            else if (inputWeight <= 0f && input.hasExecuted)
            {
                input.hasExecuted = false;
            }
        }
    }

    private void ExecuteFlagChange(BoolFlagControlBehaviour input)
    {
        // Debug.Log($"[Timeline Flag] Setting {input.category} Flag to {input.valueToSet}");

        switch (input.category)
        {
            case SetGameBoolFlagCommand.FlagCategory.Tutorial:
                FlagManager.instance.SetBoolFlag(input.tutorialFlag, input.valueToSet);
                break;
            case SetGameBoolFlagCommand.FlagCategory.Prologue:
                FlagManager.instance.SetBoolFlag(input.prologueFlag, input.valueToSet);
                break;
            case SetGameBoolFlagCommand.FlagCategory.Chapter1:
                FlagManager.instance.SetBoolFlag(input.chapter1Flag, input.valueToSet);
                break;
            case SetGameBoolFlagCommand.FlagCategory.Chapter2:
                FlagManager.instance.SetBoolFlag(input.chapter2Flag, input.valueToSet);
                break;
        }
    }
}

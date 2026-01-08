using UnityEngine;
using UnityEngine.Playables;

public class BGMMixerBehaviour : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (BGMManager.instance == null) return;

        int inputCount = playable.GetInputCount();

        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);
            var inputPlayable = (ScriptPlayable<BGMPlayableBehaviour>)playable.GetInput(i);
            var input = inputPlayable.GetBehaviour();

            // クリップに入った瞬間（Weight > 0）かつ、未実行の場合に処理を行う
            if (inputWeight > 0f && !input.hasExecuted)
            {
                ExecuteBGMAction(input);
                input.hasExecuted = true;
            }
            // クリップから出たらフラグを戻す（巻き戻し対応など）
            else if (inputWeight <= 0f && input.hasExecuted)
            {
                input.hasExecuted = false;
            }
        }
    }

    private void ExecuteBGMAction(BGMPlayableBehaviour input)
    {
        switch (input.actionType)
        {
            case BGMClip.BGMActionType.PlayImmediate:
                BGMManager.instance.Play(input.bgmCategory);
                break;

            case BGMClip.BGMActionType.Crossfade:
                BGMManager.instance.Crossfade(input.bgmCategory, input.fadeDuration);
                break;

            case BGMClip.BGMActionType.FadeIn:
                BGMManager.instance.FadeIn(input.bgmCategory, input.fadeDuration);
                break;

            case BGMClip.BGMActionType.FadeOut:
                BGMManager.instance.FadeOut(input.fadeDuration);
                break;

            case BGMClip.BGMActionType.Stop:
                BGMManager.instance.Stop();
                break;
        }
    }
}
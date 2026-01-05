using UnityEngine;
using UnityEngine.Playables;
using CriWare.Assets;

public class LocalSeMixerBehaviour : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        CriAtomSePlayer player = playerData as CriAtomSePlayer;
        if (player == null) return;

        int inputCount = playable.GetInputCount();

        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);
            var inputPlayable = (ScriptPlayable<SePlayableBehaviour>)playable.GetInput(i);
            SePlayableBehaviour input = inputPlayable.GetBehaviour();

            if (inputWeight > 0f && !input.hasPlayed)
            {
                if (input.cue != null)
                {
                    // PlayEx を使用してパラメータ付きで再生
                    player.PlayEx(
                        input.cue,
                        input.overrideVolume,
                        input.volume,
                        input.overridePitch,
                        input.pitch
                    );
                }
                input.hasPlayed = true;
            }
            else if (inputWeight <= 0f && input.hasPlayed)
            {
                input.hasPlayed = false;
            }
        }
    }
}
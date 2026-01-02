using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0, 0, 0)]
[TrackClipType(typeof(FadeClip))]
public class FadeTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<FadeMixerBehaviour>.Create(graph, inputCount);
    }
}
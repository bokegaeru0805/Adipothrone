using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(1f, 0.4f, 0.4f)] // 目立つ赤色系
[TrackClipType(typeof(BoolFlagControlClip))]
// FlagManagerはシングルトンなのでBindingは不要
public class BoolFlagControlTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<BoolFlagControlMixerBehaviour>.Create(graph, inputCount);
    }
}
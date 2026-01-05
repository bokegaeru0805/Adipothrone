using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.2f, 0.8f, 0.2f)] // 薄緑色
[TrackClipType(typeof(SeClip))]
// Bindingは不要なのでTrackBindingTypeは指定しません
public class GlobalSeTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<GlobalSeMixerBehaviour>.Create(graph, inputCount);
    }
}
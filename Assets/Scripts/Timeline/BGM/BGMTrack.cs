using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(1f, 0.8f, 0.4f)] // オレンジ/黄色系
[TrackClipType(typeof(BGMClip))]
// BGMManager.instanceを使うため、Bindingは不要
public class BGMTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<BGMMixerBehaviour>.Create(graph, inputCount);
    }
}
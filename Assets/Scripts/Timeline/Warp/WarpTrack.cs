using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0f, 1f, 0f)] // 緑色
[TrackBindingType(typeof(Transform))] // 操作対象はTransform（キャラ）
[TrackClipType(typeof(WarpClip))]
public class WarpTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<WarpMixerBehaviour>.Create(graph, inputCount);
    }
}
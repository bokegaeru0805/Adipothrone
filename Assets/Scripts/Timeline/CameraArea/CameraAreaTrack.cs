using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(1f, 0.5f, 0f)]
[TrackClipType(typeof(CameraAreaClip))]
// バインド不要 (Clip自体が参照を持つため)
public class CameraAreaTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CameraAreaMixerBehaviour>.Create(graph, inputCount);
    }
}
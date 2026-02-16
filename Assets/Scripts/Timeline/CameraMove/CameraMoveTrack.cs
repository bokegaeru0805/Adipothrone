using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.5f, 0f, 0.5f)]
[TrackClipType(typeof(CameraMoveClip))]
public class CameraMoveTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CameraMoveMixerBehaviour>.Create(graph, inputCount);
    }
}
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using MyGame.CameraControl;

[TrackColor(1f, 0.5f, 0f)] // オレンジ色
[TrackClipType(typeof(CameraShakeClip))]
// CameraManagerをバインド対象とせず、シングルトンを使用するためBinding不要
public class CameraShakeTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CameraShakeMixerBehaviour>.Create(graph, inputCount);
    }
}
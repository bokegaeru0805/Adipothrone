using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using CriWare.Assets; // CriAtomSePlayer用

[TrackColor(0.2f, 0.6f, 1f)] // 水色
[TrackClipType(typeof(SeClip))]
[TrackBindingType(typeof(CriAtomSePlayer))] // 再生したいPlayerをバインド
public class LocalSeTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<LocalSeMixerBehaviour>.Create(graph, inputCount);
    }
}
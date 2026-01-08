using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.85f, 0.3f, 0.85f)]
[TrackClipType(typeof(HeroineClip))]
[TrackBindingType(typeof(Heroin_move))] // Heroin_moveがついているオブジェクトをバインド
public class HeroineAnimationTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<HeroineMixerBehaviour>.Create(graph, inputCount);
    }
}
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class FadeClip : PlayableAsset, ITimelineClipAsset
{
    [Range(0, 1)] public float startAlpha = 0f;
    [Range(0, 1)] public float endAlpha = 1f;

    // Extrapolation（Holdなど）を有効にする設定
    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<FadePlayableBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.startAlpha = startAlpha;
        behaviour.endAlpha = endAlpha;
        return playable;
    }
}
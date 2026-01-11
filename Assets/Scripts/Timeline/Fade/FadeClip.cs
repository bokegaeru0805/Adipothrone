using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using NaughtyAttributes;

[System.Serializable]
public class FadeClip : PlayableAsset, ITimelineClipAsset
{
    public enum FadeColorType
    {
        [InspectorName("黒 (暗転)")]
        Black,
        [InspectorName("白 (発光)")]
        White
    }

    [Header("Fade Settings")]
    [Tooltip("フェードの色を選択します")]
    public FadeColorType colorType = FadeColorType.Black;

    [Range(0f, 1f)]
    [Tooltip("最大時の不透明度（基本は1.0でOK）")]
    public float targetAlpha = 1.0f;

    // ブレンド機能を有効化（これでクリップのEase In/Outが使えるようになります）
    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<FadePlayableBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        behaviour.colorType = colorType;
        behaviour.targetAlpha = targetAlpha;

        return playable;
    }
}
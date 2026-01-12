using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class CameraShakeClip : PlayableAsset, ITimelineClipAsset
{
    [Header("Shake Settings")]
    [Tooltip("揺れの強さ (Amplitude)")]
    public float amplitude = 0.0f;

    [Tooltip("揺れの速さ (Frequency)")]
    public float frequency = 0.0f;

    [Tooltip("クリップ内での強さの変化カーブ (1.0 = そのまま, 0.0 = 揺れなし)")]
    public AnimationCurve intensityCurve = AnimationCurve.Constant(0, 1, 1);

    // ブレンド機能を有効化（クリップ同士を重ねた時の滑らかな移行用）
    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<CameraShakeBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        behaviour.amplitude = amplitude;
        behaviour.frequency = frequency;
        behaviour.intensityCurve = intensityCurve;

        return playable;
    }
}

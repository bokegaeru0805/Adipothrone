using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class CameraMoveClip : PlayableAsset, ITimelineClipAsset
{
    public Vector2 targetPosition; // 移動先

    // ブレンド（パン）とHold（停止）を有効化
    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<CameraMovePlayableBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.targetPosition = targetPosition;
        return playable;
    }
}
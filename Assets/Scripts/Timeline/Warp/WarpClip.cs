using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class WarpClip : PlayableAsset, ITimelineClipAsset
{
    // Inspectorで指定したい座標
    public Vector2 targetPosition;
    
    // クリップの機能（今回はブレンドなし、重なったら上書き）
    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<WarpPlayableBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.targetPosition = targetPosition;
        return playable;
    }
}
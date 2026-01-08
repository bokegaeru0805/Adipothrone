using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class CameraAreaClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip("この期間中に有効にしたいCameraMoveArea")]
    public ExposedReference<CameraMoveArea> cameraArea;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<CameraAreaPlayableBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        // シーン上のオブジェクト参照を解決してBehaviourに渡す
        behaviour.targetArea = cameraArea.Resolve(graph.GetResolver());

        return playable;
    }
}
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using NaughtyAttributes;

[System.Serializable]
public class BGMClip : PlayableAsset, ITimelineClipAsset
{
    public enum BGMActionType
    {
        [InspectorName("即時再生 (Play)")] PlayImmediate,
        [InspectorName("クロスフェード (Crossfade)")] Crossfade,
        [InspectorName("フェードイン (Fade In)")] FadeIn,
        [InspectorName("フェードアウト (Fade Out)")] FadeOut,
        [InspectorName("停止 (Stop)")] Stop
    }

    [Header("BGM Settings")]
    [Tooltip("実行したいアクションの種類")]
    public BGMActionType actionType = BGMActionType.Crossfade;

    [Tooltip("再生するBGMのカテゴリ")]
    // StopとFadeOutの時はBGM指定は不要なので隠す
    [HideIf("IsStopOrFadeOut")]
    public BGMCategory bgmCategory;

    [Tooltip("フェードにかける時間（秒）")]
    // 即時再生と停止の時は時間は不要なので隠す
    [ShowIf("IsFadeAction")]
    public float fadeDuration = 1.0f;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<BGMPlayableBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        // Behaviourに設定値を渡す
        behaviour.actionType = actionType;
        behaviour.bgmCategory = bgmCategory;
        behaviour.fadeDuration = fadeDuration;

        return playable;
    }

    // --- NaughtyAttributes用の条件判定プロパティ ---

    private bool IsStopOrFadeOut()
    {
        return actionType == BGMActionType.Stop || actionType == BGMActionType.FadeOut;
    }

    private bool IsFadeAction()
    {
        return actionType == BGMActionType.Crossfade || 
               actionType == BGMActionType.FadeIn || 
               actionType == BGMActionType.FadeOut;
    }
}
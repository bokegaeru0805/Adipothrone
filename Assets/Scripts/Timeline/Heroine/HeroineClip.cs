using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using NaughtyAttributes;

[System.Serializable]
public class HeroineClip : PlayableAsset, ITimelineClipAsset
{
    // GameConstantsの定義に合わせて値を設定
    public enum BodyStateType
    {
        [InspectorName("現在の状態 (Use Current)")]
        UseCurrent = -1,

        [InspectorName("通常 (Normal)")]
        Normal = GameConstants.ANIM_BODY_STATE_NORMAL, // 1

        [InspectorName("体形1 (Armed 1)")]
        Armed1 = GameConstants.ANIM_BODY_STATE_ARMED_1, // 2

        [InspectorName("体形2 (Armed 2)")]
        Armed2 = GameConstants.ANIM_BODY_STATE_ARMED_2, // 3
        
        // 将来的にGameConstantsに追加されたらここにも追記可能
        // Armed3 = GameConstants.ANIM_BODY_STATE_ARMED_3
    }

    public enum AnimStateType
    {
        [InspectorName("立ち (Idle)")]
        Idle = 0,
        
        [InspectorName("歩き (Walk)")]
        Walk = 1,
        
        // 必要ならダッシュなどを追加
        // Dash = 2
    }

    public enum FacingType
    {
        [InspectorName("維持 (Keep)")]
        Keep,
        [InspectorName("右 (Right)")]
        Right,
        [InspectorName("左 (Left)")]
        Left
    }

    [Header("Heroine Animation Settings")]
    [Tooltip("体形（変身状態）を指定します。「UseCurrent」なら現在の体形状態を維持します。")]
    public BodyStateType bodyState = BodyStateType.UseCurrent;

    [Tooltip("アニメーションの状態（立ち/歩き）")]
    public AnimStateType animState = AnimStateType.Idle;

    [Tooltip("向いている方向")]
    public FacingType facing = FacingType.Keep;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<HeroinePlayableBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        behaviour.bodyState = (int)bodyState;
        behaviour.animState = (int)animState;
        behaviour.facing = facing;

        return playable;
    }
}
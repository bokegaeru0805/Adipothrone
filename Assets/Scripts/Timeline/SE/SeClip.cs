using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using NaughtyAttributes;

[System.Serializable]
public class SeClip : PlayableAsset, ITimelineClipAsset
{
    [Header("Basic Settings")]
    [Tooltip("再生したいSEのカテゴリを選択")]
    public SECategory category;

    // --- 各カテゴリのEnum (ShowIfで切り替え) ---
    [ShowIf("category", SECategory.UI)] public SE_UI uiCue;
    [ShowIf("category", SECategory.PlayerAction)] public SE_PlayerAction playerActionCue;
    [ShowIf("category", SECategory.EnemyAction)] public SE_EnemyAction enemyActionCue;
    [ShowIf("category", SECategory.Field)] public SE_Field fieldCue;
    [ShowIf("category", SECategory.SystemEvent)] public SE_SystemEvent systemEventCue;

    // --- オプション設定 (BoxGroupでまとめる) ---
    [BoxGroup("Optional Settings")]
    [Tooltip("音量を個別に設定するかどうか")]
    public bool overrideVolume = false;

    [BoxGroup("Optional Settings")]
    [ShowIf("overrideVolume")]
    [Range(0f, 1f)]
    public float volume = 1.0f;

    [BoxGroup("Optional Settings")]
    [Tooltip("ピッチ（音の高さ）を変更するかどうか")]
    public bool overridePitch = false;

    [BoxGroup("Optional Settings")]
    [ShowIf("overridePitch")]
    [Tooltip("ピッチ（セント単位）。100で半音、1200で1オクターブ変化")]
    [Range(-1200f, 1200f)]
    public float pitch = 0f;

    public ClipCaps clipCaps => ClipCaps.None;

    // Enum取得ロジック（変更なし）
    public System.Enum SelectedCue
    {
        get
        {
            switch (category)
            {
                case SECategory.UI: return uiCue;
                case SECategory.PlayerAction: return playerActionCue;
                case SECategory.EnemyAction: return enemyActionCue;
                case SECategory.Field: return fieldCue;
                case SECategory.SystemEvent: return systemEventCue;
                default: return null;
            }
        }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<SePlayableBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        behaviour.cue = SelectedCue;
        
        // オプション設定をBehaviourに渡す
        behaviour.overrideVolume = overrideVolume;
        behaviour.volume = volume;
        behaviour.overridePitch = overridePitch;
        behaviour.pitch = pitch;

        return playable;
    }
}
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using NaughtyAttributes; // コマンド同様、表示切り替えに使用

[System.Serializable]
public class BoolFlagControlClip : PlayableAsset, ITimelineClipAsset
{
    // SetGameBoolFlagCommand.cs からEnum定義を流用
    // (FlagData.cs等で定義されているはずなので、ここではCommandと同じ型を使います)
    
    [Header("Flag Settings")]
    [Tooltip("操作するフラグのカテゴリ（章）")]
    public SetGameBoolFlagCommand.FlagCategory category = SetGameBoolFlagCommand.FlagCategory.Tutorial;

    // --- 各章ごとのフラグ変数 (NaughtyAttributesで分岐表示) ---

    [AllowNesting]
    [ShowIf("category", SetGameBoolFlagCommand.FlagCategory.Tutorial)]
    [Label("Flag Name")]
    public TutorialEvent tutorialFlag;

    [AllowNesting]
    [ShowIf("category", SetGameBoolFlagCommand.FlagCategory.Prologue)]
    [Label("Flag Name")]
    public PrologueTriggeredEvent prologueFlag;

    [AllowNesting]
    [ShowIf("category", SetGameBoolFlagCommand.FlagCategory.Chapter1)]
    [Label("Flag Name")]
    public Chapter1TriggeredEvent chapter1Flag;

    [AllowNesting]
    [ShowIf("category", SetGameBoolFlagCommand.FlagCategory.Chapter2)]
    [Label("Flag Name")]
    public Chapter2TriggeredEvent chapter2Flag;

    // ---------------------------

    [Tooltip("フラグに設定したい値 (true/false)")]
    public bool valueToSet = true;

    // クリップの機能（ブレンドなどは不要）
    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<BoolFlagControlBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        // Behaviourに設定値を渡す
        behaviour.category = category;
        behaviour.tutorialFlag = tutorialFlag;
        behaviour.prologueFlag = prologueFlag;
        behaviour.chapter1Flag = chapter1Flag;
        behaviour.chapter2Flag = chapter2Flag;
        behaviour.valueToSet = valueToSet;

        return playable;
    }
}
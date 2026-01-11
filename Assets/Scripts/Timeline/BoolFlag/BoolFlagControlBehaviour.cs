using UnityEngine;
using UnityEngine.Playables;

public class BoolFlagControlBehaviour : PlayableBehaviour
{
    public SetGameBoolFlagCommand.FlagCategory category;
    
    public TutorialEvent tutorialFlag;
    public PrologueTriggeredEvent prologueFlag;
    public Chapter1TriggeredEvent chapter1Flag;
    public Chapter2TriggeredEvent chapter2Flag;

    public bool valueToSet;

    // クリップ通過時に1回だけ実行するためのフラグ
    public bool hasExecuted = false;

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        // グラフ停止時や巻き戻し時にフラグをリセットすることで、再度通過したときに実行可能にする
        hasExecuted = false;
    }
}
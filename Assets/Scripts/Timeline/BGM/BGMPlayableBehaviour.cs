using UnityEngine;
using UnityEngine.Playables;

public class BGMPlayableBehaviour : PlayableBehaviour
{
    public BGMClip.BGMActionType actionType;
    public BGMCategory bgmCategory;
    public float fadeDuration;

    // クリップ通過時に1回だけ実行するためのフラグ
    public bool hasExecuted = false;

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        // グラフ停止時やポーズ時にフラグをリセット
        hasExecuted = false;
    }
}
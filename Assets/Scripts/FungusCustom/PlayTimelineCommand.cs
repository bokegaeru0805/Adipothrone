using Fungus;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// PlayableDirectorを再生し、終了（Hold状態含む）を待機するFungusコマンド。
/// Wrap ModeがHoldでも、時間がDurationに達した時点で「完了」とみなして次に進みます。
/// </summary>
[CommandInfo(
    "Timeline",
    "Play Timeline",
    "Timelineを再生します。Wrap ModeがHoldの場合でも、終了時間まで到達すればWaitを解除して次に進みます"
)]
[AddComponentMenu("")]
public class PlayTimeline : Command
{
    [Tooltip("再生させるPlayable Director")]
    [SerializeField]
    protected PlayableDirector director;

    [Tooltip("再生終了まで待機するか")]
    [SerializeField]
    protected bool waitUntilFinished = true;

    // 内部フラグ
    private bool isWaiting = false;

    public override void OnEnter()
    {
        if (director == null)
        {
            Debug.LogError("PlayTimeline: Directorが設定されていません。", this);
            Continue();
            return;
        }

        // 再生開始
        // ※CutsceneHookがついている場合、ここで自動的にSkipManagerに登録されます
        director.Play();

        if (waitUntilFinished)
        {
            isWaiting = true;
        }
        else
        {
            Continue();
        }
    }

    protected virtual void Update()
    {
        if (isWaiting)
        {
            if (director == null)
            {
                isWaiting = false;
                Continue();
                return;
            }

            // 終了判定ロジック
            // 1. Directorが停止している（Noneモードなどで自然停止した場合）
            bool isStopped = (director.state != PlayState.Playing);

            // 2. 時間が最後まで到達している（Holdモードやスキップ機能で飛ばされた場合）
            //    ※浮動小数の誤差を考慮して少し余裕を持たせるか、>= で判定
            bool isTimeUp = (director.time >= director.duration - 0.001); // 誤差対策

            if (isStopped || isTimeUp)
            {
                isWaiting = false;

                // 次のブロックへ進む
                Continue();
            }
        }
    }

    public override string GetSummary()
    {
        if (director == null)
            return "Error: No Director selected";
        return director.name + (waitUntilFinished ? " (Wait)" : "");
    }

    public override Color GetButtonColor()
    {
        return new Color32(235, 191, 128, 255); // 薄いオレンジ（Timelineっぽい色）
    }
}
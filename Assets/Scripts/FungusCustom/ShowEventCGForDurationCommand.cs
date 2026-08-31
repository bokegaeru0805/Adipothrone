using Fungus;
using NaughtyAttributes;
using UnityEngine;

[CommandInfo(
    "Custom",
    "Show Event CG For Duration",
    "一枚絵を指定時間だけ表示し、自動的に非表示にします。"
)]
[AddComponentMenu("")]
public class ShowEventCGForDurationCommand : EventCGCommandBase
{
    [BoxGroup("Timing")]
    [MinValue(0f)]
    [Tooltip("表示にかける時間（秒）")]
    [SerializeField]
    private float fadeInDuration = 0.5f;

    [BoxGroup("Timing")]
    [MinValue(0f)]
    [Tooltip("完全に表示した状態を維持する時間（秒）")]
    [SerializeField]
    private float displayDuration = 2f;

    [BoxGroup("Timing")]
    [MinValue(0f)]
    [Tooltip("非表示にかける時間（秒）")]
    [SerializeField]
    private float fadeOutDuration = 0.2f;

    [BoxGroup("Flowchart")]
    [Tooltip("有効な場合、一枚絵が消えるまで次のコマンドへ進みません")]
    [SerializeField]
    private bool waitUntilFinished = true;

    private bool _hasContinued;

    public override void OnEnter()
    {
        _hasContinued = false;
        if (eventCG == null)
        {
            Debug.LogWarning(
                "ShowEventCGForDurationCommand: EventCGが設定されていません。",
                this
            );
            ContinueOnce();
            return;
        }

        if (TimelineSkipManager.instance != null && TimelineSkipManager.instance.IsSkipping)
        {
            EventCGController.EnsureInstance().HideImmediate();
            ContinueOnce();
            return;
        }

        float durationScale = 1f;
        if (
            TimelineSkipManager.instance != null
            && TimelineSkipManager.instance.IsFastForwarding
        )
        {
            durationScale = TimelineSkipManager.instance.FastForwardSpeed;
        }

        EventCGController.EnsureInstance().ShowForDuration(
            eventCG,
            GetDisplayOptions(),
            fadeInDuration / durationScale,
            displayDuration / durationScale,
            fadeOutDuration / durationScale,
            waitUntilFinished ? ContinueOnce : null
        );

        if (!waitUntilFinished)
        {
            ContinueOnce();
        }
    }

    public override void OnStopExecuting()
    {
        EventCGController.EnsureInstance().HideImmediate();
        base.OnStopExecuting();
    }

    private void ContinueOnce()
    {
        if (_hasContinued)
        {
            return;
        }

        _hasContinued = true;
        Continue();
    }

    public override string GetSummary()
    {
        string waitSummary = waitUntilFinished ? "待機" : "非待機";
        return $"{GetEventCGSummary("時間表示")} {displayDuration:0.##}秒 / {waitSummary}";
    }
}

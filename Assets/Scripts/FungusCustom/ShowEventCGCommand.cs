using Fungus;
using NaughtyAttributes;
using UnityEngine;

[CommandInfo("Custom", "Show Event CG", "一枚絵を表示します。")]
[AddComponentMenu("")]
public class ShowEventCGCommand : EventCGCommandBase
{
    [BoxGroup("Fade")]
    [MinValue(0f)]
    [Tooltip("表示にかける時間（秒）")]
    [SerializeField]
    private float fadeInDuration = 0.5f;

    [BoxGroup("Flowchart")]
    [Tooltip("有効な場合、表示フェードが完了するまで次のコマンドへ進みません")]
    [SerializeField]
    private bool waitUntilFinished = false;

    private bool _hasContinued;

    public override void OnEnter()
    {
        _hasContinued = false;
        if (eventCG == null)
        {
            Debug.LogWarning("ShowEventCGCommand: EventCGが設定されていません。", this);
            ContinueOnce();
            return;
        }

        EventCGController.EnsureInstance().Show(
            eventCG,
            GetDisplayOptions(),
            fadeInDuration,
            waitUntilFinished ? ContinueOnce : null
        );

        if (!waitUntilFinished)
        {
            ContinueOnce();
        }
    }

    public override void OnStopExecuting()
    {
        _hasContinued = true;
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
        return $"{GetEventCGSummary("表示")} / {waitSummary}";
    }
}

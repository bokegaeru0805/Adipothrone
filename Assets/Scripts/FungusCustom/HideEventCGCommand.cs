using Fungus;
using NaughtyAttributes;
using UnityEngine;

[CommandInfo("Custom", "Hide Event CG", "表示中の一枚絵を非表示にします。")]
[AddComponentMenu("")]
public class HideEventCGCommand : Command
{
    [BoxGroup("Fade")]
    [MinValue(0f)]
    [Tooltip("非表示にかける時間（秒）")]
    [SerializeField]
    private float fadeOutDuration = 0.5f;

    [BoxGroup("Flowchart")]
    [Tooltip("有効な場合、一枚絵が消えるまで次のコマンドへ進みません")]
    [SerializeField]
    private bool waitUntilFinished = true;

    private bool _hasContinued;

    public override void OnEnter()
    {
        _hasContinued = false;
        EventCGController.EnsureInstance().Hide(
            fadeOutDuration,
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
        return $"非表示: {fadeOutDuration:0.##}秒 {(waitUntilFinished ? "待機" : "非待機")}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(95, 115, 185, 255);
    }
}

using DG.Tweening;
using Fungus;
using NaughtyAttributes;
using UnityEngine;

[CommandInfo(
    "Custom",
    "Set Talk UI Position",
    "会話ウィンドウ全体を上側または下側へ移動します。"
)]
[AddComponentMenu("")]
public class SetTalkUIPositionCommand : Command
{
    [BoxGroup("Position")]
    [Tooltip("会話ウィンドウの移動先")]
    [SerializeField]
    private TalkUIPosition position = TalkUIPosition.Top;

    [BoxGroup("Animation")]
    [MinValue(0f)]
    [Tooltip("移動にかける時間（秒）")]
    [SerializeField]
    private float duration = 0.5f;

    [BoxGroup("Animation")]
    [Tooltip("移動時のイージング")]
    [SerializeField]
    private Ease ease = Ease.OutCubic;

    [BoxGroup("Flowchart")]
    [Tooltip("有効な場合、移動完了まで次のコマンドへ進みません")]
    [SerializeField]
    private bool waitUntilFinished = true;

    private bool _hasContinued;
    private TalkUIPositionController _positionController;

    public override void OnEnter()
    {
        _hasContinued = false;
        SayDialog sayDialog = SayDialog.GetSayDialog();
        if (sayDialog == null)
        {
            Debug.LogError("SetTalkUIPositionCommand: SayDialogが見つかりません。", this);
            ContinueOnce();
            return;
        }

        _positionController = sayDialog.GetComponent<TalkUIPositionController>();
        if (_positionController == null)
        {
            _positionController =
                sayDialog.gameObject.AddComponent<TalkUIPositionController>();
        }

        _positionController.MoveTo(
            position,
            duration,
            ease,
            waitUntilFinished ? ContinueOnce : null
        );

        if (!waitUntilFinished)
        {
            ContinueOnce();
        }
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

    public override void OnStopExecuting()
    {
        _hasContinued = true;
        _positionController?.CancelMove();
        _positionController = null;
        base.OnStopExecuting();
    }

    public override string GetSummary()
    {
        string waitSummary = waitUntilFinished ? "待機" : "非待機";
        return $"{position} / {duration:0.##}秒 / {waitSummary}";
    }

    public override Color GetButtonColor()
    {
        return position == TalkUIPosition.Bottom
            ? new Color32(105, 155, 215, 255)
            : new Color32(145, 185, 230, 255);
    }
}

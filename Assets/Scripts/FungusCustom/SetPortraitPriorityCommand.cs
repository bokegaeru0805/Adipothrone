using Fungus;
using NaughtyAttributes;
using UnityEngine;

[CommandInfo(
    "Custom",
    "Set Character Portrait Priority",
    "この会話中の指定キャラクターの画像表示方式を強制的に切り替えます。次のTalk Startでリセットされます。"
)]
[AddComponentMenu("")]
public class SetPortraitPriorityCommand : Command
{
    public enum PriorityOperation
    {
        [InspectorName("全体／Block設定に従う")]
        UseBlockSetting = 0,

        [InspectorName("顔グラフィック優先")]
        FaceGraphicFirst = 1,

        [InspectorName("立ち絵優先")]
        StandingPortraitFirst = 2,
    }

    [Header("対象")]
    [Tooltip("画像表示方式を変更するキャラクター")]
    [SerializeField]
    private Character targetCharacter;

    [Header("強制する表示方式")]
    [Tooltip("この会話中に使用する画像表示方式。解除時は全体設定、次にBlock設定を参照します。")]
    [SerializeField]
    private PriorityOperation priority = PriorityOperation.UseBlockSetting;

    [Header("立ち絵を閉じる演出")]
    [ShowIf("CanFadeOutStandingPortrait")]
    [MinValue(0f)]
    [Tooltip("顔グラフィックへ切り替える際、表示中の立ち絵を消す時間（秒）")]
    [SerializeField]
    private float fadeOutDuration = 0.2f;

    [Header("Flowchart")]
    [ShowIf("CanFadeOutStandingPortrait")]
    [Tooltip("有効な場合、対象の立ち絵が消えるまで次のコマンドへ進みません")]
    [SerializeField]
    private bool waitUntilFinished = false;

    private bool _hasContinued;

    public override void OnEnter()
    {
        _hasContinued = false;
        if (targetCharacter == null)
        {
            Debug.LogWarning("SetPortraitPriorityCommand: 対象キャラクターが未設定です。", this);
            ContinueOnce();
            return;
        }

        if (priority == PriorityOperation.UseBlockSetting)
        {
            PortraitDisplayPriorityState.ClearOverride(targetCharacter);
        }
        else
        {
            PortraitDisplayPriorityState.SetOverride(
                targetCharacter,
                priority == PriorityOperation.StandingPortraitFirst
                    ? PortraitDisplayPriority.StandingPortraitFirst
                    : PortraitDisplayPriority.FaceGraphicFirst
            );
        }

        PortraitDisplayPriority resolvedPriority = PortraitDisplayPriorityState.Resolve(
            ParentBlock,
            targetCharacter
        );
        if (resolvedPriority == PortraitDisplayPriority.FaceGraphicFirst)
        {
            BasePortraitController controller =
                PortraitDisplayPriorityState.FindDynamicPortraitController(targetCharacter);
            if (controller != null)
            {
                controller.FadeOutPortrait(
                    fadeOutDuration,
                    waitUntilFinished ? ContinueOnce : null
                );

                if (!waitUntilFinished)
                {
                    ContinueOnce();
                }
                return;
            }
        }

        ContinueOnce();
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

    private bool CanFadeOutStandingPortrait()
    {
        return priority != PriorityOperation.StandingPortraitFirst;
    }

    public override string GetSummary()
    {
        if (targetCharacter == null)
        {
            return "Error: キャラクター未設定";
        }

        string operationName = priority switch
        {
            PriorityOperation.FaceGraphicFirst => "顔グラ",
            PriorityOperation.StandingPortraitFirst => "立ち絵",
            _ => "全体／Block設定",
        };
        string waitSummary = waitUntilFinished ? "待機" : "非待機";
        return $"{targetCharacter.name} → {operationName} / {waitSummary}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(240, 160, 190, 255);
    }
}

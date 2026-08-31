using System.Collections.Generic;
using Fungus;
using NaughtyAttributes;
using UnityEngine;

[CommandInfo(
    "Custom",
    "Set All Portrait Priority",
    "この会話中の全キャラクターの画像表示方式を強制的に切り替えます。次のTalk Startでリセットされます。"
)]
[AddComponentMenu("")]
public class SetAllPortraitPriorityCommand : Command
{
    public enum PriorityOperation
    {
        [InspectorName("全強制設定を解除してBlock設定に従う")]
        UseBlockSetting = 0,

        [InspectorName("顔グラフィック優先")]
        FaceGraphicFirst = 1,

        [InspectorName("立ち絵優先")]
        StandingPortraitFirst = 2,
    }

    [Header("全体へ強制する表示方式")]
    [Tooltip("全キャラクターへ適用します。既存のキャラクター別設定は解除されます。")]
    [SerializeField]
    private PriorityOperation priority = PriorityOperation.UseBlockSetting;

    [Header("立ち絵を閉じる演出")]
    [ShowIf("CanFadeOutStandingPortrait")]
    [MinValue(0f)]
    [Tooltip("顔グラフィックへ切り替える際、表示中の立ち絵を消す時間（秒）")]
    [SerializeField]
    private float fadeOutDuration = 0.5f;

    [Header("Flowchart")]
    [ShowIf("CanFadeOutStandingPortrait")]
    [Tooltip("有効な場合、対象の立ち絵がすべて消えるまで次のコマンドへ進みません")]
    [SerializeField]
    private bool waitUntilFinished = false;

    private bool _hasContinued;
    private int _remainingFadeOutCount;

    public override void OnEnter()
    {
        _hasContinued = false;
        if (priority == PriorityOperation.UseBlockSetting)
        {
            PortraitDisplayPriorityState.ClearAllOverrides();
        }
        else
        {
            PortraitDisplayPriorityState.SetGlobalOverride(
                priority == PriorityOperation.StandingPortraitFirst
                    ? PortraitDisplayPriority.StandingPortraitFirst
                    : PortraitDisplayPriority.FaceGraphicFirst
            );
        }

        List<BasePortraitController> controllersToFadeOut =
            new List<BasePortraitController>();
        foreach (BasePortraitController controller in BasePortraitController.ActiveControllers)
        {
            if (
                controller != null
                && PortraitDisplayPriorityState.Resolve(ParentBlock, controller.character)
                    == PortraitDisplayPriority.FaceGraphicFirst
            )
            {
                controllersToFadeOut.Add(controller);
            }
        }

        if (!waitUntilFinished)
        {
            foreach (BasePortraitController controller in controllersToFadeOut)
            {
                controller.FadeOutPortrait(fadeOutDuration);
            }

            ContinueOnce();
            return;
        }

        _remainingFadeOutCount = controllersToFadeOut.Count;
        if (_remainingFadeOutCount == 0)
        {
            ContinueOnce();
            return;
        }

        foreach (BasePortraitController controller in controllersToFadeOut)
        {
            controller.FadeOutPortrait(fadeOutDuration, OnFadeOutCompleted);
        }
    }

    public override void OnStopExecuting()
    {
        _hasContinued = true;
        base.OnStopExecuting();
    }

    private void OnFadeOutCompleted()
    {
        _remainingFadeOutCount--;
        if (_remainingFadeOutCount <= 0)
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

    private bool CanFadeOutStandingPortrait()
    {
        return priority != PriorityOperation.StandingPortraitFirst;
    }

    public override string GetSummary()
    {
        string operationName = priority switch
        {
            PriorityOperation.FaceGraphicFirst => "顔グラ",
            PriorityOperation.StandingPortraitFirst => "立ち絵",
            _ => "Block設定",
        };
        string waitSummary = waitUntilFinished ? "待機" : "非待機";
        return $"[全体] → {operationName} / {waitSummary}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(185, 135, 230, 255);
    }
}

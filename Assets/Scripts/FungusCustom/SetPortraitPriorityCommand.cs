using Fungus;
using UnityEngine;

[CommandInfo(
    "Custom",
    "Set Portrait Priority",
    "この会話中の指定キャラクターの画像表示優先度を切り替えます。次のTalk Startでリセットされます。"
)]
[AddComponentMenu("")]
public class SetPortraitPriorityCommand : Command
{
    public enum PriorityOperation
    {
        [InspectorName("Block設定に従う")]
        UseBlockSetting = 0,

        [InspectorName("顔グラフィック優先")]
        FaceGraphicFirst = 1,

        [InspectorName("立ち絵優先")]
        StandingPortraitFirst = 2,
    }

    [Tooltip("画像表示優先度を変更するキャラクター")]
    [SerializeField]
    private Character targetCharacter;

    [Tooltip("この会話中に使用する画像表示優先度")]
    [SerializeField]
    private PriorityOperation priority = PriorityOperation.UseBlockSetting;

    public override void OnEnter()
    {
        if (targetCharacter == null)
        {
            Debug.LogWarning("SetPortraitPriorityCommand: 対象キャラクターが未設定です。", this);
            Continue();
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

        Continue();
    }

    public override string GetSummary()
    {
        return targetCharacter == null
            ? "Error: キャラクター未設定"
            : $"{targetCharacter.name}: {priority}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(240, 160, 190, 255);
    }
}

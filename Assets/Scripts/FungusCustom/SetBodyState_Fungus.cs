using Fungus;
using UnityEngine;

// --------------------------------
// プレイヤーのBodyStateを変更するコマンド
// --------------------------------
[CommandInfo("Player", "Set BodyState", "プレイヤーのBodyStateを変更します")]
public class SetBodyState_Fungus : Command
{
    [Tooltip("変更する体形")]
    [SerializeField]
    private BodyStateEnum bodyState = BodyStateEnum.BodyState_Normal;

    private enum BodyStateEnum
    {
        None = 0,
        BodyState_Normal = 1,
        BodyState_Armed1 = 10,
        BodyState_Armed2 = 20,
        BodyState_Armed3 = 30,
        BodyState_Immobile = 40,
    };

    public override void OnEnter()
    {
        var playerBodyManager = PlayerBodyManager.instance;
        if (playerBodyManager != null)
        {
            switch (bodyState)
            {
                case BodyStateEnum.BodyState_Normal:
                    playerBodyManager.SetBodyStateFromWP(
                        GameConstants.BodyStateEnum.BodyState_Normal
                    );
                    break;
                case BodyStateEnum.BodyState_Armed1:
                    playerBodyManager.SetBodyStateFromWP(
                        GameConstants.BodyStateEnum.BodyState_Armed1
                    );
                    break;
                case BodyStateEnum.BodyState_Armed2:
                    playerBodyManager.SetBodyStateFromWP(
                        GameConstants.BodyStateEnum.BodyState_Armed2
                    );
                    break;
                case BodyStateEnum.BodyState_Armed3:
                    playerBodyManager.SetBodyStateFromWP(
                        GameConstants.BodyStateEnum.BodyState_Armed3
                    );
                    break;
                case BodyStateEnum.BodyState_Immobile:
                    playerBodyManager.SetBodyStateFromWP(
                        GameConstants.BodyStateEnum.BodyState_Immobile
                    );
                    break;
                case BodyStateEnum.None:
                    Debug.LogWarning("BodyStateが設定されていません。");
                    break;
            }
        }
        else
        {
            Debug.LogError("PlayerBodyManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override Color GetButtonColor()
    {
        return new Color32(251, 207, 153, 255);
    }

    public override string GetSummary()
    {
        return $"BodyState = {bodyState}";
    }
}

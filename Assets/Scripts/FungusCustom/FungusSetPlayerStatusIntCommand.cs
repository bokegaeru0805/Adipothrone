using Fungus;
using UnityEngine;

// --------------------------------
// プレイヤーのステータス（int型）設定コマンド
// --------------------------------
[CommandInfo("Player", "Set Player Status Int", "プレイヤーのステータス（int型）を設定します")]
public class FungusSetPlayerStatusIntCommand : Command
{
    [Tooltip("変更したいステータス名")]
    public PlayerStatusIntName statusName;

    [Tooltip("設定したい値")]
    public int statusValue;

    public override void OnEnter()
    {
        var playerManager = PlayerManager.instance;
        if (playerManager != null)
        {
            playerManager.SetPlayerIntStatus(statusName, statusValue);
        }
        else
        {
            Debug.LogError("PlayerManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override Color GetButtonColor()
    {
        return new Color32(251, 207, 153, 255);
    }

    public override string GetSummary()
    {
        return $"{statusName} = {statusValue}";
    }
}

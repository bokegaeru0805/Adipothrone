using Fungus;
using UnityEngine;


/// <summary>
/// プレイヤーのステータス（bool型）設定コマンド
/// </summary>
[CommandInfo("Player", "Set Player Status Bool", "プレイヤーのステータス（bool型）を設定します")]
public class SetPlayerStatusBoolCommand : Command
{
    [Tooltip("変更したいステータス名")]
    public PlayerStatusBoolName statusName;

    [Tooltip("設定したい値")]
    public bool statusValue;

    public override void OnEnter()
    {
        var playerManager = PlayerManager.instance;
        if (playerManager != null)
        {
            playerManager.SetPlayerBoolStatus(statusName, statusValue);
        }
        else
        {
            Debug.LogError("GameManagerのインスタンスが見つかりません！");
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

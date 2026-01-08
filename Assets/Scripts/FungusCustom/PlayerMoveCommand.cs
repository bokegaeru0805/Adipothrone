using Fungus;
using UnityEngine;

/// <summary>
/// FungusのコマンドでPlayerを特定の座標に移動させるクラス
/// </summary>
[CommandInfo("Player", "PlayerMove", "Playerを特定の座標に移動させます")]
public class PlayerMoveCommand : Command
{
    [Tooltip("移動させる座標")]
    public Vector2 targetPoint = new Vector2(0, 0);

    public override void OnEnter()
    {
        PlayerManager playerManager = PlayerManager.instance;
        if (playerManager != null)
        {
            playerManager.StartCoroutine(playerManager.PlayerMove(targetPoint)); // Playerを指定した座標に移動させる
        }
        else
        {
            Debug.LogError("PlayerManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override string GetSummary()
    {
        return $"Playerを{targetPoint}に移動させる";
    }
}

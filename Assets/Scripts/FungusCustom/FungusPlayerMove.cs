using Fungus;
using Shapes2D;
using UnityEngine;

// --------------------------------
// Playerを特定の座標に移動させるコマンド
// --------------------------------
[CommandInfo("Player", "PlayerMove", "Playerを特定の座標に移動させます")]
public class FungusPlayerMove : Command
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

using Fungus;
using UnityEngine;

/// <summary>
/// Enemyに関するSEを停止するFungusコマンド
/// </summary>
[CommandInfo("SE", "Stop EnemyActionSE", "Enemyに関するSEを停止します")]
public class StopEnemyActionSECommand : Command
{
    [Tooltip("止めるSE")]
    public SE_EnemyAction EnemyActionSE;

    public override void OnEnter()
    {
        if (SEManager.instance != null)
        {
            SEManager.instance.StopEnemyActionSE(EnemyActionSE);
        }
        else
        {
            Debug.LogError("SEManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override string GetSummary()
    {
        return $"止めるSEは {EnemyActionSE}";
    }
}

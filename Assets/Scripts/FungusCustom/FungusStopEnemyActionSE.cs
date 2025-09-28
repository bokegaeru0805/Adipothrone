using Fungus;
using UnityEngine;
// --------------------------------
// Enemyに関するSEを停止するコマンド
// --------------------------------
[CommandInfo("SE", "Stop EnemyActionSE", "Enemyに関するSEを停止します")]
public class FungusStopEnemyActionSE : Command
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

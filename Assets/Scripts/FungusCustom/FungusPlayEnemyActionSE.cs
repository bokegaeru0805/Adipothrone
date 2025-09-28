using Fungus;
using UnityEngine;
// --------------------------------
// Enemyに関するSEを再生するコマンド
// --------------------------------
[CommandInfo("SE", "Start EnemyActionSE", "Enemyに関するSEを再生します")]
public class FungusPlayEnemyActionSE : Command
{
    [Tooltip("流すSE")]
    public SE_EnemyAction EnemyActionSE;

    public override void OnEnter()
    {
        if (SEManager.instance != null)
        {
            SEManager.instance.PlayEnemyActionSE(EnemyActionSE);
        }
        else
        {
            Debug.LogError("SEManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override string GetSummary()
    {
        return $"流すSEは {EnemyActionSE}";
    }
}

using Fungus;
using UnityEngine;

/// <summary>
/// Fungus用のEnemyActionSE再生コマンド
/// </summary>
[CommandInfo("SE", "Start EnemyActionSE", "Enemyに関するSEを再生します")]
public class PlayEnemyActionSECommand : Command
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

using Fungus;
using UnityEngine;

// --------------------------------
// ゲームオーバーコマンド
// --------------------------------
[CommandInfo("Custom", "GameOver", "ゲームオーバー")]
public class FungusGameOver : Command
{
    public override void OnEnter()
    {
        if (GameOverUIManager.instance != null)
        {
            GameOverUIManager.instance.StartGameOver(); // ゲームオーバー処理を呼び出す
        }
        else
        {
            Debug.LogError("GameOverUIManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override string GetSummary()
    {
        return $"ゲームオーバー";
    }
}

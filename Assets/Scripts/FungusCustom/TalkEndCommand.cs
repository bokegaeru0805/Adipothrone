using System;
using Fungus;
using UnityEngine;

[CommandInfo("Custom", "TalkEnd", "会話が終わった後のコマンド")]
public class TalkEndCommand : Command
{
    public static event Action OnTalkEndExecuted; // このコマンドが実行されたことを通知するイベント

    public override void OnEnter()
    {
        // スキップ中にここに到達したら、スキップ終了を通知
        OnTalkEndExecuted?.Invoke();

        // このコマンドが所属しているブロックを取得
        Block currentBlock = ParentBlock;
        if (currentBlock == null)
        {
            Debug.LogError("コマンドが所属するブロックが見つかりませんでした。", this);
            Continue();
            return;
        }
        else
        {
            FungusCustomSignals.DoTalkBlockIsSkippable(false); // このブロックにより、スキップ不可を通知
        }

        if (GameManager.instance != null)
        {
            GameManager.instance.StartCoroutine(GameManager.instance.DialogEnd()); //会話中のフラグをOFFにする
        }
        else
        {
            Debug.LogError("GameManagerが存在しません");
            Continue();
            return;
        }

        // BGMのダッキングを解除する
        BGMManager.instance?.SetDucking(false);

        if (HeroinPortraitController.instance != null)
        {
            HeroinPortraitController.instance.HidePortrait(); // 立ち絵を非表示にする
        }
        else
        {
            Debug.LogError("HeroinPortraitControllerが存在しません");
            Continue();
            return;
        }

        // 会話が終わったら敵の動きを再開する
        TimeManager.instance.SetEnemyMovePaused(false);

        Continue();
    }

    public override string GetSummary()
    {
        return $"会話が終わった後に時間を再開します";
    }
}

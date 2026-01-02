using Fungus;
using UnityEngine;
using UnityEngine.Playables;

// Fungusのコマンドとして登録するための属性
[CommandInfo("Timeline", "Play Timeline", "Unity Timelineを再生し、終了するまで待機します")]
[AddComponentMenu("")]
public class FungusPlayTimeline : Command
{
    [Tooltip("再生したいTimelineを持つPlayable Directorを指定してください")]
    [SerializeField]
    protected PlayableDirector director;

    [Tooltip("Timelineの再生終了を待ってから次のコマンドに進むか")]
    [SerializeField]
    protected bool waitUntilFinished = true;

    public override void OnEnter()
    {
        if (director == null)
        {
            // Directorが設定されていない場合はエラーを出して次へ
            Debug.LogError("PlayTimeline: Playable Directorが設定されていません。");
            Continue();
            return;
        }

        // Timelineを再生
        director.Play();

        if (waitUntilFinished)
        {
            // Timelineの長さ（秒）を取得し、その時間だけ待ってからFinishCommandを呼ぶ
            // ※Time.timeScaleの影響を受ける場合は調整が必要ですが、基本はこれで動作します
            Invoke("FinishCommand", (float)director.duration);
        }
        else
        {
            // 待機しない場合はすぐに次のコマンドへ
            Continue();
        }
    }

    // 待機完了後に呼ばれる処理
    protected void FinishCommand()
    {
        Continue();
    }

    public override string GetSummary()
    {
        if (director == null)
        {
            return "Error: No Director selected";
        }
        return director.name;
    }

    public override Color GetButtonColor()
    {
        // Timelineっぽい色にしておく
        return new Color32(235, 191, 128, 255);
    }
}

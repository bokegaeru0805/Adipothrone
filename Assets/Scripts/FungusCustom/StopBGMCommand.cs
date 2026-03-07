using System.Collections;
using Fungus;
using UnityEngine;

/// <summary>
/// Fungusコマンド：BGM停止
/// フェードアウト機能と、完了待機機能、および早送り/スキップ機能に対応しています。
/// </summary>
[CommandInfo("BGM", "StopBGM", "現在流れているBGMを停止、またはフェードアウトさせます")]
[AddComponentMenu("")]
public class StopBGMCommand : Command
{
    #region Settings

    [Header("Fade Settings")]
    [Tooltip("フェードアウトにかける時間（秒）。0の場合は即座に停止します。")]
    [SerializeField]
    protected float fadeOutDuration = 1.0f;

    [Tooltip("フェードアウトが完了するまで次のコマンドに進まないか")]
    [SerializeField]
    protected bool waitUntilFinished = false;

    #endregion

    #region Public Methods

    public override void OnEnter()
    {
        if (BGMManager.instance == null)
        {
            Debug.LogError("BGMManagerのインスタンスが見つかりません！");
            Continue();
            return;
        }

        // フェード時間が0以下の場合は即座に停止
        if (fadeOutDuration <= 0f)
        {
            BGMManager.instance.Stop();
            Continue();
            return;
        }

        // フェードアウト処理をコルーチンで開始
        StartCoroutine(FadeOutRoutine());

        // 待機しない設定なら、フェード処理は裏で回したまま即座に次のコマンドへ
        if (!waitUntilFinished)
        {
            Continue();
        }
    }

    public override string GetSummary()
    {
        if (fadeOutDuration <= 0f)
        {
            return "即座に停止";
        }

        string waitStr = waitUntilFinished ? " (待機あり)" : " (待機なし)";
        return $"{fadeOutDuration}秒かけてフェードアウト{waitStr}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(116, 200, 200, 255);
    }

    #endregion

    #region Internal Logic

    /// <summary>
    /// BGMの音量を徐々に下げてから停止するコルーチン。
    /// TimelineSkipManagerの状態を監視し、早送りやスキップに追従します。
    /// </summary>
    private IEnumerator FadeOutRoutine()
    {
        float startVolume = BGMManager.instance.GetAllVolume();
        float timer = 0f;

        while (timer < fadeOutDuration)
        {
            float dt = Time.unscaledDeltaTime;

            // TimelineSkipManagerが存在する場合、早送り/スキップの処理を適用
            if (TimelineSkipManager.instance != null)
            {
                if (TimelineSkipManager.instance.IsSkipping)
                {
                    // 全スキップ中は即座にループを抜けて完了させる
                    break;
                }
                else if (TimelineSkipManager.instance.IsFastForwarding)
                {
                    // 早送り中は経過時間に倍率をかける
                    dt *= TimelineSkipManager.instance.FastForwardSpeed;
                }
            }

            timer += dt;

            // 進捗率 (0.0 ～ 1.0)
            float progress = Mathf.Clamp01(timer / fadeOutDuration);

            // 音量を徐々に0へ近づける
            float currentVolume = Mathf.Lerp(startVolume, 0f, progress);
            BGMManager.instance.AdjustAllVolume(currentVolume);

            yield return null;
        }

        // 完全に停止処理を行い、次回再生時のためにカテゴリの音量を元の値に戻しておく
        BGMManager.instance.Stop();
        BGMManager.instance.AdjustAllVolume(startVolume);

        // 待機設定が有効な場合のみ、ここで次のコマンドへ進める
        if (waitUntilFinished)
        {
            Continue();
        }
    }

    #endregion
}

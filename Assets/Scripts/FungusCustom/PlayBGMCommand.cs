using System.Collections;
using Fungus;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Fungusコマンド：BGM再生
/// フェードイン、クロスフェード、完了待機、および早送り/スキップ機能に対応しています。
/// </summary>
[CommandInfo("BGM", "PlayBGM", "指定したBGMを再生、またはフェードイン・クロスフェードさせます")]
[AddComponentMenu("")]
public class PlayBGMCommand : Command
{
    public enum BGMPlayMode
    {
        PlayImmediate, // 即時再生
        FadeIn, // 停止状態からのフェードイン
        Crossfade // 現在のBGMからのクロスフェード
        ,
    }

    #region Settings

    [Header("BGM Settings")]
    [Tooltip("流すBGM")]
    [FormerlySerializedAs("BGM")] // 以前の "BGM" 変数で設定されていたデータを引き継ぎます
    [SerializeField]
    protected BGMCategory bgmCategory = BGMCategory.None;

    [Tooltip("再生方法（即時、フェードイン、クロスフェード）")]
    [SerializeField]
    protected BGMPlayMode playMode = BGMPlayMode.PlayImmediate;

    [Header("Fade Settings")]
    [Tooltip("フェードにかける時間（秒）。0の場合は即座に再生します。")]
    [SerializeField]
    protected float fadeDuration = 1.0f;

    [Tooltip("フェードが完了するまで次のコマンドに進まないか")]
    [SerializeField]
    protected bool waitUntilFinished = false;

    #endregion

    #region Public Methods

    public override void OnEnter()
    {
        if (BGMManager.instance == null)
        {
            Debug.LogError("BGMManagerのインスタンスが見つかりません！BGMを再生できません。");
            Continue();
            return;
        }

        // 同じBGMが既に再生中の場合は何もしない
        if (BGMManager.instance.IsPlayingCategory(bgmCategory))
        {
            Continue();
            return;
        }

        // フェード時間が0以下、または即時再生モードの場合は即座に再生
        if (fadeDuration <= 0f || playMode == BGMPlayMode.PlayImmediate)
        {
            // Playメソッド内では再生中ならクロスフェード扱いになりますが、停止中なら即時再生されます
            BGMManager.instance.Play(bgmCategory);
            Continue();
            return;
        }

        // フェード処理の呼び出し
        if (playMode == BGMPlayMode.FadeIn)
        {
            BGMManager.instance.FadeIn(bgmCategory, fadeDuration);
        }
        else if (playMode == BGMPlayMode.Crossfade)
        {
            BGMManager.instance.Crossfade(bgmCategory, fadeDuration);
        }

        // 待機設定が有効な場合はコルーチンで待機、無効なら即座に次へ
        if (waitUntilFinished)
        {
            StartCoroutine(WaitRoutine());
        }
        else
        {
            Continue();
        }
    }

    public override string GetSummary()
    {
        if (bgmCategory == BGMCategory.None)
        {
            return "Error: BGMが設定されていません";
        }

        if (playMode == BGMPlayMode.PlayImmediate || fadeDuration <= 0f)
        {
            return $"{bgmCategory} を即時再生";
        }

        string modeStr = (playMode == BGMPlayMode.FadeIn) ? "フェードイン" : "クロスフェード";
        string waitStr = waitUntilFinished ? " (待機あり)" : " (待機なし)";

        return $"{bgmCategory} を {fadeDuration}秒で{modeStr}{waitStr}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(140, 220, 220, 255);
    }

    #endregion

    #region Internal Logic

    /// <summary>
    /// BGMのフェード完了を待つコルーチン。
    /// TimelineSkipManagerの状態を監視し、早送りやスキップに追従して待機時間を短縮します。
    /// </summary>
    private IEnumerator WaitRoutine()
    {
        float timer = 0f;

        while (timer < fadeDuration)
        {
            float dt = Time.unscaledDeltaTime;

            // TimelineSkipManagerが存在する場合、早送り/スキップの処理を適用
            if (TimelineSkipManager.instance != null)
            {
                if (TimelineSkipManager.instance.IsSkipping)
                {
                    // 全スキップ中は即座に待機を終了させる
                    break;
                }
                else if (TimelineSkipManager.instance.IsFastForwarding)
                {
                    // 早送り中は経過時間に倍率をかける
                    dt *= TimelineSkipManager.instance.FastForwardSpeed;
                }
            }

            timer += dt;
            yield return null;
        }

        Continue();
    }

    #endregion
}

using System.Collections;
using Fungus;
using UnityEngine;

/// <summary>
/// Fungus用のフェードイン・フェードアウトコマンド
/// </summary>
// ▼ Fungusのメニュー候補に表示させないため、CommandInfoをコメントアウト
// [CommandInfo("Custom", "FadeTime", "フェードアウト(正)・イン(負)時間を設定し、指定秒数待機します")]
[AddComponentMenu("")]
public class FadeTimeCommand : Command
{
    [Tooltip("フェードアウト(正)・イン(負)の時間（秒）")]
    public float FadeTime;

    [Tooltip("Trueにすると白フェード（フラッシュ）、Falseなら通常の黒フェードを行います")]
    public bool useWhiteFade = false;

    public override void OnEnter()
    {
        if (FadeCanvas.instance != null)
        {
            float duration = Mathf.Abs(FadeTime);

            if (useWhiteFade)
            {
                // 白フェード（フラッシュ）処理
                if (FadeTime > 0)
                {
                    FadeCanvas.instance.FlashOut(duration); // 画面を白く飛ばす
                }
                else
                {
                    FadeCanvas.instance.FlashIn(duration); // 白い画面から戻る
                }
            }
            else
            {
                // 黒フェード処理
                if (FadeTime > 0)
                {
                    FadeCanvas.instance.FadeOut(duration); // 画面を暗転させる
                }
                else
                {
                    FadeCanvas.instance.FadeIn(duration); // 画面を明転させる
                }
            }
        }
        else
        {
            Debug.LogError("FadeCanvasのインスタンスが見つかりません！");
        }

        // 指定時間だけ待ってから続行
        StartCoroutine(WaitAndContinue());
    }

    private IEnumerator WaitAndContinue()
    {
        // FadeTimeが負の値の場合も考慮して、絶対値で待機
        yield return new WaitForSecondsRealtime(Mathf.Abs(FadeTime));
        Continue();
    }

    public override string GetSummary()
    {
        string colorText = useWhiteFade ? " (白)" : " (黒)";

        if (FadeTime > 0)
        {
            return $"フェードアウト: {FadeTime}秒{colorText}";
        }
        else
        {
            return $"フェードイン: {Mathf.Abs(FadeTime)}秒{colorText}";
        }
    }

    public override Color GetButtonColor()
    {
        // 視認性を上げるため、白フェード設定時はボタン色を少し変える（任意）
        return useWhiteFade ? new Color32(220, 220, 220, 255) : new Color32(235, 191, 217, 255);
    }
}

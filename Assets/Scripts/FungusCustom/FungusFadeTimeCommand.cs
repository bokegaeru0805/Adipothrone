using System.Collections;
using Fungus;
using UnityEngine;

// --------------------------------
// 画面をフェードアウト・インさせるコマンド
// --------------------------------
[CommandInfo("Custom", "FadeTime", "フェードアウト(正)・イン(負)時間を設定し、指定秒数待機します")]
public class FungusFadeTimeCommand : Command
{
    [Tooltip("フェードアウト(正)・イン(負)の時間（秒）")]
    public float FadeTime;

    public override void OnEnter()
    {
        if (FadeCanvas.instance != null)
        {
            if (FadeTime > 0)
            {
                FadeCanvas.instance.FadeOut(FadeTime); //画面を暗転させる
            }
            else
            {
                FadeCanvas.instance.FadeIn(Mathf.Abs(FadeTime)); //画面を明転させる
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
        if (FadeTime > 0)
        {
            return $"フェードアウト時間: {FadeTime}秒";
        }
        else
        {
            return $"フェードイン時間: {Mathf.Abs(FadeTime)}秒";
        }
    }
}

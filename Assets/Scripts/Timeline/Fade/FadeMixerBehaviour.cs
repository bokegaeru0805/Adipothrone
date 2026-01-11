using UnityEngine;
using UnityEngine.Playables;

public class FadeMixerBehaviour : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (FadeCanvas.instance == null) return;

        int inputCount = playable.GetInputCount();

        float totalBlackAlpha = 0f;
        float totalWhiteAlpha = 0f;

        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);
            
            // ウェイトが0より大きいクリップのみ計算
            if (inputWeight > 0f)
            {
                var inputPlayable = (ScriptPlayable<FadePlayableBehaviour>)playable.GetInput(i);
                var input = inputPlayable.GetBehaviour();

                // クリップごとのアルファ値 = ウェイト * 設定最大値
                float alpha = inputWeight * input.targetAlpha;

                if (input.colorType == FadeClip.FadeColorType.Black)
                {
                    // 黒フェードは最大値を採用するか加算するか選べますが、
                    // フェードの重ね合わせを考慮して「一番濃い値を採用」または「加算」します。
                    // ここでは加算し、1.0でキャップします。
                    totalBlackAlpha += alpha;
                }
                else
                {
                    totalWhiteAlpha += alpha;
                }
            }
        }

        // --- 適用 ---
        
        // 1.0を超えないようにクランプ
        totalBlackAlpha = Mathf.Clamp01(totalBlackAlpha);
        totalWhiteAlpha = Mathf.Clamp01(totalWhiteAlpha);

        // FadeCanvasにそれぞれの値を送る
        FadeCanvas.instance.SetAlpha(totalBlackAlpha);
        FadeCanvas.instance.SetFlashAlpha(totalWhiteAlpha);
    }

    public override void OnGraphStop(Playable playable)
    {
        // Timeline終了時はフェードを解除（安全策）
        if (FadeCanvas.instance != null)
        {
            // ここで0に戻すかどうかは仕様次第ですが、
            // 演出として「黒いまま終わる」こともあるので、
            // 強制解除はせず、Timelineの設定（Extrapolation: Holdなど）に委ねるのが基本です。
            
            // ただし、エディタでのプレビュー終了時に画面が真っ暗なままにならないように
            // アプリケーション再生中でなければリセットする手もあります。
            if (!Application.isPlaying)
            {
                FadeCanvas.instance.SetAlpha(0f);
                FadeCanvas.instance.SetFlashAlpha(0f);
            }
        }
    }
}
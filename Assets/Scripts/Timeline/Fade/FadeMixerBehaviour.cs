using UnityEngine;
using UnityEngine.Playables;

// 複数のクリップを混ぜ合わせて計算するクラス
public class FadeMixerBehaviour : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (FadeCanvas.instance == null)
            return;

        int inputCount = playable.GetInputCount();
        float finalAlpha = 0f;
        float totalWeight = 0f;

        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);
            var inputPlayable = (ScriptPlayable<FadePlayableBehaviour>)playable.GetInput(i);
            FadePlayableBehaviour input = inputPlayable.GetBehaviour();

            double time = inputPlayable.GetTime();
            double duration = inputPlayable.GetDuration();

            // 進行度計算
            float progress = 0f;
            if (duration > 0)
                progress = (float)(time / duration);

            // 進行度を確実に0～1にクランプ
            progress = Mathf.Clamp01(progress);

            // ここにあった「Ease Out事故防止」の強制ウェイト変更処理を削除しました
            // ウェイトの計算を歪めると、正規化（割り算）の計算がおかしくなります。

            float currentAlpha = Mathf.Lerp(input.startAlpha, input.endAlpha, progress);

            finalAlpha += currentAlpha * inputWeight;
            totalWeight += inputWeight;
        }

        // 正規化処理（加重平均）
        if (totalWeight > 0.001f) // 0除算防止
        {
            finalAlpha /= totalWeight;
        }
        else
        {
            // 何も再生されていない時は、最後に設定された値を維持するか、
            // 明示的に0にするか等の仕様によりますが、ここでは影響を与えないようにします
            // 必要であれば FadeCanvas.instance.GetAlpha() を使うなどしてください
            return;
        }

        FadeCanvas.instance.SetAlpha(finalAlpha);
    }
}

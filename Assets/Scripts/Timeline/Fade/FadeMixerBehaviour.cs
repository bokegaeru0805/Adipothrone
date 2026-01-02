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

            // ★修正点1: 進行度を確実に0～1にクランプ
            progress = Mathf.Clamp01(progress);

            // ★修正点2: もしHold状態で時間がDurationを超えている、かつWeightが下がってきているなら
            // Weightを強制的に1とみなして計算に参加させる（Ease Out事故防止）
            if (progress >= 1.0f && inputWeight < 1.0f)
            {
                // ここで強制的にウェイトを戻すことで、EaseOutによる消失を防ぐ
                // ただし、クロスフェード中は邪魔になる可能性があるので、
                // 「単一クリップ」の時などに有効な安全策です。
                inputWeight = 1.0f;
            }

            float currentAlpha = Mathf.Lerp(input.startAlpha, input.endAlpha, progress);

            if (inputWeight > 0f)
            {
                finalAlpha += currentAlpha * inputWeight;
                totalWeight += inputWeight;
            }
        }

        // 正規化処理
        if (totalWeight > 0f)
        {
            finalAlpha /= totalWeight;
        }

        FadeCanvas.instance.SetAlpha(finalAlpha);
    }
}

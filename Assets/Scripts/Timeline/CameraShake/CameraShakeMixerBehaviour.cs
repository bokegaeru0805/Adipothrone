using UnityEngine;
using UnityEngine.Playables;
using MyGame.CameraControl;

public class CameraShakeMixerBehaviour : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (CameraManager.instance == null) return;

        int inputCount = playable.GetInputCount();
        float totalAmplitude = 0f;
        float totalFrequency = 0f;
        float totalWeight = 0f;

        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);
            
            if (inputWeight > 0f)
            {
                var inputPlayable = (ScriptPlayable<CameraShakeBehaviour>)playable.GetInput(i);
                var input = inputPlayable.GetBehaviour();

                // クリップの進行度 (0.0 ～ 1.0)
                double time = inputPlayable.GetTime();
                double duration = inputPlayable.GetDuration();
                float progress = (duration > 0) ? (float)(time / duration) : 0f;

                // カーブによる倍率を取得
                float curveValue = input.intensityCurve.Evaluate(progress);

                // 加算合成
                // Amplitudeは重なると強くなる
                totalAmplitude += input.amplitude * inputWeight * curveValue;
                
                // Frequencyは重なった場合、高い方を採用するか平均するかですが、
                // ここでは「重み付き平均」にします
                totalFrequency += input.frequency * inputWeight;

                totalWeight += inputWeight;
            }
        }

        // Frequencyの正規化（重み合計で割る）
        if (totalWeight > 0.001f)
        {
            totalFrequency /= totalWeight;
        }

        // CameraManagerに適用
        CameraManager.instance.SetTimelineShake(totalAmplitude, totalFrequency);
    }

    public override void OnGraphStop(Playable playable)
    {
        // 停止時（Hold以外で止まった時）は揺れをゼロにする
        
        // ただし、HoldモードでPauseされている場合はここでのリセットをスキップする必要がある
        // （CameraMoveと同じロジック）
        var director = playable.GetGraph().GetResolver() as PlayableDirector;
        if (director != null && 
            director.extrapolationMode == DirectorWrapMode.Hold && 
            director.state == PlayState.Paused)
        {
            return; // 揺れを維持したまま抜ける
        }

        if (CameraManager.instance != null)
        {
            CameraManager.instance.SetTimelineShake(0f, 0f);
        }
    }
}
using UnityEngine;
using UnityEngine.Playables;

public class CameraAreaMixerBehaviour : PlayableBehaviour
{
    // 現在アクティブにしているエリアを記憶（毎フレーム呼び出し防止）
    private CameraMoveArea currentActiveArea = null;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        int inputCount = playable.GetInputCount();
        CameraMoveArea targetArea = null;
        float maxWeight = 0f;

        // 最もウェイトが高い（＝現在再生されている）クリップを探す
        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);
            
            if (inputWeight > 0.5f) // 半分以上重なっていたら有効とみなす
            {
                var inputPlayable = (ScriptPlayable<CameraAreaPlayableBehaviour>)playable.GetInput(i);
                var behaviour = inputPlayable.GetBehaviour();
                
                if (behaviour.targetArea != null)
                {
                    targetArea = behaviour.targetArea;
                    maxWeight = inputWeight;
                }
            }
        }

        // エリアが切り替わったタイミングで ActivateFromTimeline を呼ぶ
        if (targetArea != null && targetArea != currentActiveArea)
        {
            targetArea.ActivateFromTimeline();
            currentActiveArea = targetArea;
        }
    }
}
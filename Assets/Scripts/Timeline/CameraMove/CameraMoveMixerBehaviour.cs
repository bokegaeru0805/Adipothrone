using UnityEngine;
using UnityEngine.Playables;
using MyGame.CameraControl;

public class CameraMoveMixerBehaviour : PlayableBehaviour
{
    private CameraManager trackBinding;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        // 1. まずシングルトン経由で取得を試みる
        if (trackBinding == null)
        {
            trackBinding = CameraManager.instance;
        }

        // 2. 【重要】Edit Mode（プレビュー中）はinstanceがnullの場合が多いので、シーン内検索で無理やり見つける
        // これがないと、再生ボタンを押すまでTimelineのプレビューが動きません
        if (trackBinding == null && !Application.isPlaying)
        {
            trackBinding = Object.FindObjectOfType<CameraManager>();
        }

        // それでも見つからなければ何もしない
        if (trackBinding == null) return;

        int inputCount = playable.GetInputCount();
        Vector2 finalPosition = Vector2.zero;
        float totalWeight = 0f;

        // クリップの計算
        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);
            
            if (inputWeight > 0f)
            {
                var inputPlayable = (ScriptPlayable<CameraMovePlayableBehaviour>)playable.GetInput(i);
                var input = inputPlayable.GetBehaviour();

                finalPosition += input.targetPosition * inputWeight;
                totalWeight += inputWeight;
            }
        }

        // 制御適用
        if (totalWeight > 0f)
        {
            finalPosition /= totalWeight;
            
            // Timeline操作中はBrainをOFFにして座標を適用
            trackBinding.SetTimelineControlMode(true);
            trackBinding.SetCameraPosition(finalPosition);
        }
        else
        {
            // クリップがない区間は通常制御（Brain ON）に戻す
            trackBinding.SetTimelineControlMode(false);
        }
    }
    
    public override void OnGraphStop(Playable playable)
    {
        // 停止時も念のため再取得を試みてからリセット
        if (trackBinding == null) trackBinding = CameraManager.instance;
        if (trackBinding == null && !Application.isPlaying) trackBinding = Object.FindObjectOfType<CameraManager>();

        if (trackBinding != null)
        {
            trackBinding.SetTimelineControlMode(false);
        }
    }
}
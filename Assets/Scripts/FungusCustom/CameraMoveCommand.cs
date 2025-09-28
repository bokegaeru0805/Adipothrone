using System.Collections;
using Fungus;
using UnityEngine;

// --------------------------------
// カメラ移動コマンド
// --------------------------------
[CommandInfo("Custom", "Camera Move", "指定した座標にカメラを移動させ、完了後に次に進みます")]
public class FungusCameraMove : Command
{
    [Tooltip("目標座標")]
    public Vector2 targetPosition;

    [Tooltip("到達時間(秒)")]
    public float reachTime = 1.0f;

    public override void OnEnter()
    {
        if (MyGame.CameraControl.CameraManager.instance != null)
        {
            StartCoroutine(WaitForCameraMove());
        }
        else
        {
            Continue();
        }
    }

    private IEnumerator WaitForCameraMove()
    {
        yield return MyGame.CameraControl.CameraManager.instance.StartCoroutine(
            MyGame.CameraControl.CameraManager.instance.CameraMoveByTween(targetPosition, reachTime)
        );
        Continue();
    }

    public override string GetSummary()
    {
        return $"カメラを {targetPosition} へ {reachTime} 秒で移動";
    }
}

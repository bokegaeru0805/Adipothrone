using System.Collections;
using Fungus;
using UnityEngine;

/// <summary>
/// カメラを揺らし、揺れ終わったら次に進みます
/// </summary>
[CommandInfo("Custom", "Camera Shake", "カメラを揺らし、揺れ終わったら次に進みます")]
public class CameraShakeCommand : Command
{
    [Tooltip("揺れの強さ")]
    public Vector3 strength = new Vector3(1, 1, 0);

    [Tooltip("揺れる時間(秒)")]
    public float duration = 0.5f;

    public override void OnEnter()
    {
        if (MyGame.CameraControl.CameraManager.instance != null)
        {
            StartCoroutine(WaitForCameraShake());
        }
        else
        {
            Continue();
        }
    }

    private IEnumerator WaitForCameraShake()
    {
        yield return MyGame.CameraControl.CameraManager.instance.StartCoroutine(
            MyGame.CameraControl.CameraManager.instance.CameraShake(strength, duration)
        );
        Continue();
    }

    public override string GetSummary()
    {
        return $"カメラを {duration} 秒間 {strength} の強さで振動させます";
    }
}

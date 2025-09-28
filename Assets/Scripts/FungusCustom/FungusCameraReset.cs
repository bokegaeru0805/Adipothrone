using System.Collections;
using Fungus;
using UnityEngine;

// --------------------------------
// カメラ初期化コマンド
// --------------------------------
[CommandInfo("Custom", "Camera Reset", "カメラの位置を初期化します")]
public class FungusCameraReset : Command
{    public override void OnEnter()
    {
        if (MyGame.CameraControl.CameraManager.instance != null)
        {
            MyGame.CameraControl.CameraManager.instance.CameraReset();
        }
        else
        {
            Debug.LogError("CameraManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override string GetSummary()
    {
        return $"カメラを初期化します";
    }
}
using System.Collections;
using Fungus;
using UnityEngine;

namespace Fungus
{
    [CommandInfo(
        "Camera",
        "Stop Camera Shake",
        "持続中のカメラ振動を停止・フェードアウトさせます。"
    )]
    [AddComponentMenu("")]
    public class StopCameraShakeCommand : Command
    {
        [Tooltip("停止までにかけるフェードアウト時間（0で即座に停止）")]
        [SerializeField]
        protected float fadeOutDuration = 0.5f;

        [Tooltip("フェードアウトが完全に終了するまで、次のFungusコマンドへ進むのを待機するか")]
        [SerializeField]
        protected bool waitUntilFinished = false;

        public override void OnEnter()
        {
            // アクティブな持続シェイクがあれば停止命令を出す
            if (
                MyGame.CameraControl.CameraManager.instance != null
                && MyGame.CameraControl.CameraManager.instance.IsContinuousShakeActive
            )
            {
                MyGame.CameraControl.CameraManager.instance.StopContinuousShake(fadeOutDuration);
            }

            // フェードアウト時間が設定されており、完了を待つ場合
            if (waitUntilFinished && fadeOutDuration > 0f)
            {
                StartCoroutine(WaitCoroutine());
            }
            else
            {
                // 即時停止、または待機不要の場合はすぐに次へ
                Continue();
            }
        }

        private IEnumerator WaitCoroutine()
        {
            yield return new WaitForSeconds(fadeOutDuration);
            Continue();
        }

        public override string GetSummary()
        {
            if (fadeOutDuration > 0f)
            {
                return $"FadeOut: {fadeOutDuration}秒かけて停止";
            }
            return "即時停止";
        }

        public override Color GetButtonColor()
        {
            return new Color32(210, 170, 190, 255); // Startより少し暗いピンク色
        }
    }
}

using Fungus;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fungus
{
    [CommandInfo(
        "Camera",
        "Change Global Volume Profile",
        "GlobalVolumeManagerを使用して、シーンのVolume Profileを滑らかに変更します。"
    )]
    [AddComponentMenu("")]
    public class ChangeGlobalVolumeProfileCommand : Command
    {
        [Tooltip("変更後のVolume Profile")]
        [SerializeField]
        protected VolumeProfile targetProfile;

        [Tooltip("遷移にかける時間（秒）")]
        [SerializeField]
        protected float duration = 1.0f;

        [Tooltip("遷移が完了するまで待機するかどうか")]
        [SerializeField]
        protected bool waitUntilFinished = true;

        public override void OnEnter()
        {
            if (GlobalVolumeManager.instance == null)
            {
                Debug.LogWarning("GlobalVolumeManagerのインスタンスが見つかりません。");
                Continue();
                return;
            }

            if (targetProfile == null)
            {
                Debug.LogWarning("Target Profileが設定されていません。");
                Continue();
                return;
            }

            // GlobalVolumeManagerにプロファイル変更を依頼
            GlobalVolumeManager.instance.ChangeProfile(targetProfile, duration);

            if (waitUntilFinished && duration > 0f)
            {
                // 待機する場合は、指定時間後にContinueを呼ぶ
                Invoke(nameof(OnComplete), duration);
            }
            else
            {
                // 待機しない場合は即座に次へ
                Continue();
            }
        }

        private void OnComplete()
        {
            Continue();
        }

        public override string GetSummary()
        {
            if (targetProfile == null)
            {
                return "Error: No profile selected";
            }

            return $"{targetProfile.name} over {duration} seconds";
        }

        public override Color GetButtonColor()
        {
            return new Color32(235, 191, 217, 255); // 薄いピンク色（カメラ系コマンドのイメージ）
        }
    }
}

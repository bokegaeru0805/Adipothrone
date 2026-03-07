using Fungus;
using NaughtyAttributes;
using UnityEngine;

[CommandInfo(
    "BGM",
    "Set Area BGM Lock",
    "エリア進入時の自動BGM切り替えをロック/解除します。イベント中のBGMを固定する際に使用します。"
)]
public class SetAreaBgmLockCommand : Command
{
    [Tooltip(
        "trueでBGMをロック（自動切り替え無効化）、falseでロック解除（現在のエリアのBGMを再生）します。"
    )]
    [SerializeField]
    private bool isLocked = true;

    [Tooltip("ロックを解除(false)した際、元のエリアBGMを再生し直すときのフェード時間（秒）")]
    [SerializeField, HideIf(nameof(isLocked))]
    [AllowNesting]
    private float fadeDuration = 1.0f;

    public override void OnEnter()
    {
        // 先ほど CameraMoveArea に追加した静的メソッドを呼び出す
        CameraMoveArea.SetAreaBgmLocked(isLocked, fadeDuration);

        // 次のコマンドへ進む
        Continue();
    }

    public override Color GetButtonColor()
    {
        return new Color32(100, 190, 200, 255);
    }

    public override string GetSummary()
    {
        // Flowchart上で表示されるサマリーテキスト
        if (isLocked)
        {
            return "エリアBGMをロックする";
        }
        else
        {
            return $"ロック解除し、{fadeDuration}秒で元に戻す";
        }
    }
}

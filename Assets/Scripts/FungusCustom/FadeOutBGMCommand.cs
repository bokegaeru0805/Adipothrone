using Fungus;
using UnityEngine;

/// <summary>
/// Fungus用のBGMフェードアウトコマンド
/// </summary>
[CommandInfo("BGM", "FadeOutBGM", "現在流れているBGMをフェードアウトします")]
public class FadeOutBGMCommand : Command
{
    [Tooltip("フェードアウトさせる時間")]
    public float FadeOutTime = 1.0f;

    public override void OnEnter()
    {
        if (BGMManager.instance != null)
        {
            BGMManager.instance.FadeOut(FadeOutTime); // BGMをフェードアウト
        }
        else
        {
            Debug.LogError("BGMManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override string GetSummary()
    {
        return $"{FadeOutTime}秒でBGMをフェードアウト";
    }
}

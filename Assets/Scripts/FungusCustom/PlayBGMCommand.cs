using Fungus;
using UnityEngine;

/// <summary>
/// Fungus用のBGM再生コマンド
/// </summary>
[CommandInfo("BGM", "PlayBGM", "BGMを流します")]
public class PlayBGMCommand : Command
{
    [Tooltip("流すBGM")]
    public BGMCategory BGM;

    public override void OnEnter()
    {
        if (BGMManager.instance != null)
        {
            BGMManager.instance.Play(BGM); // BGMを流す
        }
        else
        {
            Debug.LogWarning("BGMManagerが存在しません。BGMを流すことができません。");
        }
        Continue();
    }

    public override string GetSummary()
    {
        return $"{BGM}を流す";
    }

    public override Color GetButtonColor()
    {
        return new Color32(140, 220, 220, 255);
    }
}

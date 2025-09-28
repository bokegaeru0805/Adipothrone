using Fungus;
using UnityEngine;

// --------------------------------
// BGMを流すコマンド
// --------------------------------
[CommandInfo("BGM", "PlayBGM", "BGMを流します")]
public class FungusPlayBGM : Command
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
}

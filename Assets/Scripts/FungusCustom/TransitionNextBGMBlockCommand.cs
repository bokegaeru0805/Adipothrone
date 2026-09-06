using Fungus;
using UnityEngine;

/// <summary>
/// Fungusコマンド：現在再生中のBGMを次のBlockへ遷移させます。
/// </summary>
[CommandInfo("BGM", "TransitionNextBlock", "現在再生中のBGMを次のBlockへ遷移させます")]
[AddComponentMenu("")]
public class TransitionNextBGMBlockCommand : Command
{
    public override void OnEnter()
    {
        if (BGMManager.instance == null)
        {
            Debug.LogError("BGMManagerのインスタンスが見つかりません！Block遷移を実行できません。");
            Continue();
            return;
        }

        BGMManager.instance.TryTransitionToNextBlock();
        Continue();
    }

    public override string GetSummary()
    {
        return "現在のBGMを次のBlockへ遷移";
    }

    public override Color GetButtonColor()
    {
        return new Color32(140, 220, 220, 255);
    }
}

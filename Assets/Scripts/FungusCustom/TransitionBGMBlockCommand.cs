using Fungus;
using UnityEngine;

/// <summary>
/// Fungusコマンド：現在再生中のBGMを指定したBlockへ遷移させます。
/// </summary>
[CommandInfo("BGM", "TransitionBlock", "現在再生中のBGMを指定したBlockへ遷移させます")]
[AddComponentMenu("")]
public class TransitionBGMBlockCommand : Command
{
    [Tooltip("遷移先のBlock Index")]
    [SerializeField]
    [Min(0)]
    protected int blockIndex;

    public override void OnEnter()
    {
        if (BGMManager.instance == null)
        {
            Debug.LogError("BGMManagerのインスタンスが見つかりません！Block遷移を実行できません。");
            Continue();
            return;
        }

        BGMManager.instance.TryTransitionToBlock(blockIndex);
        Continue();
    }

    public override string GetSummary()
    {
        return $"現在のBGMをBlock {blockIndex}へ遷移";
    }

    public override Color GetButtonColor()
    {
        return new Color32(140, 220, 220, 255);
    }
}

using Fungus;
using UnityEngine;

/// <summary>
/// FungusのFlowchartから、PrologueTriggeredEventのboolフラグを設定するためのカスタムコマンド
/// </summary>
[CommandInfo(
    "Flag", // コマンドのカテゴリ名
    "Set Chapter1 Bool Flag", // コマンド名
    "指定した第一章のboolフラグの値を変更します"
)] // コマンドの説明
public class SetChapter1BoolFlag : Command
{
    [Tooltip("値を変更したいフラグの名前")]
    [SerializeField]
    private Chapter1TriggeredEvent flagToSet;

    [Tooltip("フラグに設定したい値 (true/false)")]
    [SerializeField]
    private bool valueToSet = true;

    // このコマンドが実行されたときに呼ばれる処理
    public override void OnEnter()
    {
        // FlagManagerのインスタンスが存在するか確認
        if (FlagManager.instance != null)
        {
            // 指定されたフラグに、指定された値を設定
            FlagManager.instance.SetBoolFlag(flagToSet, valueToSet);
        }
        else
        {
            Debug.LogError("FlagManagerが見つかりません！");
        }

        // 次のコマンドへ処理を続ける
        Continue();
    }

    public override Color GetButtonColor()
    {
        return new Color32(251, 207, 153, 255);
    }

    // Inspectorに表示されるコマンドの概要
    public override string GetSummary()
    {
        return $"Set {flagToSet} to {valueToSet}";
    }
}

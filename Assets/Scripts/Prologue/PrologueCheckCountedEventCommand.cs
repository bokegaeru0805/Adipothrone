using Fungus;
using UnityEngine;

[CommandInfo(
    "Flag",
    "PrologueCheckCountedEvent",
    "Prologueステージの特定イベント数を取得し、Flowchart変数に渡します"
)]
public class PrologueCheckCountedEventCommand : Command
{
    [Tooltip("取得したいイベント名（Dictionaryのキー）")]
    public PrologueCountedEvent FlagName;

    [Tooltip("取得結果を入れるFlowchartのInt型変数")]
    [VariableProperty(typeof(IntegerVariable))]
    public IntegerVariable outputVariable;

    public override void OnEnter()
    {
        if (FlagManager.instance == null)
        {
            Debug.LogWarning("[Fungus] FlagManager が null です。");
            outputVariable.Value = 0;
            Continue();
            return;
        }

        // FlagManagerから直接値を取得する
        int value = FlagManager.instance.GetIntFlag(FlagName);
        outputVariable.Value = value;

        Continue();
    }

    public override string GetSummary()
    {
        // outputVariableがnullの場合も考慮して、より安全な記述に
        return $"{FlagName} → {outputVariable?.Key ?? "なし"}";
    }
}

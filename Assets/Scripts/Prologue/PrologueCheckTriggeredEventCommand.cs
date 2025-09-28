using Fungus;
using UnityEngine;

[CommandInfo(
    "Flag",
    "PrologueCheckTriggeredEvent",
    "bool型のプロローグステージのフラグを取得し、Flowchart変数に渡します"
)]
public class PrologueCheckTriggeredEventCommand : Command
{
    [Tooltip("取得したいイベント名（Dictionaryのキー）")]
    public PrologueTriggeredEvent flagName;

    [Tooltip("取得結果を入れるFlowchartのBool型変数")]
    [VariableProperty(typeof(BooleanVariable))]
    public BooleanVariable outputVariable;

    public override void OnEnter()
    {
        //FlagManagerのインスタンスを直接チェックする
        if (FlagManager.instance == null)
        {
            Debug.LogWarning("[Fungus] FlagManager が null です。");
            outputVariable.Value = false;
            Continue();
            return;
        }

        // FlagManagerから直接値を取得する
        bool value = FlagManager.instance.GetBoolFlag(flagName);
        outputVariable.Value = value;

        Continue();
    }

    public override string GetSummary()
    {
        return $"{flagName} → {outputVariable?.Key ?? "なし"}";
    }
}

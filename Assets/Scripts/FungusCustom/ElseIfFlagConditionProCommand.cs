using Fungus;
using UnityEngine;

/// <summary>
/// 直前のIfまたはElse Ifが偽で、複数のフラグ条件を満たす場合に、次のコマンドブロックを実行します。
/// </summary>
[CommandInfo(
    "Custom",
    "Else If Flag Condition Pro",
    "直前のIfまたはElse Ifが偽で、複数のFlagConditionProによる条件を満たす場合に次のコマンドブロックを実行します。"
)]
[AddComponentMenu("")]
public class ElseIfFlagConditionProCommand : CheckFlagConditionPro
{
    protected override bool IsElseIf { get { return true; } }

    public override bool CloseBlock()
    {
        return true;
    }
}

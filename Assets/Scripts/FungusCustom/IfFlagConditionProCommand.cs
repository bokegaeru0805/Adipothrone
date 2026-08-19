using Fungus;
using UnityEngine;

/// <summary>
/// 複数のフラグ条件を満たす場合に、次のコマンドブロックを実行します。
/// </summary>
[CommandInfo(
    "Custom",
    "If Flag Condition Pro",
    "複数のFlagConditionProをANDまたはORで評価し、条件を満たす場合に次のコマンドブロックを実行します。"
)]
[AddComponentMenu("")]
public class IfFlagConditionProCommand : CheckFlagConditionPro
{
}

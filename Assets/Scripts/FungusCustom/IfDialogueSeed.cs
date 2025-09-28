using Fungus;
using UnityEngine;

/// <summary>
/// もしDialogueSeed変数が条件を満たすなら、次のコマンドブロックを実行します。
/// </summary>
[CommandInfo("Custom",
             "If Dialogue Seed",
             "もしGlobalFlowchartのDialogueSeed変数が条件を満たすなら、次のコマンドブロックを実行します。")]
[AddComponentMenu("")]
public class IfDialogueSeed : CheckDialogueSeed
{
    // ロジックは全て基底クラスのCheckDialogueSeedが担当します
}
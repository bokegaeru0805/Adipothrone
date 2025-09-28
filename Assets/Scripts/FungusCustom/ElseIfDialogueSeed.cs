using Fungus;
using UnityEngine;

/// <summary>
/// 直前のIf(またはElse If)が偽で、かつこのコマンドの条件が真の場合に、次のコマンドブロックを実行します。
/// </summary>
[CommandInfo("Custom",
             "Else If Dialogue Seed",
             "直前のIf/ElseIfが偽で、かつこのコマンドの条件が真の場合に、次のコマンドブロックを実行します。")]
[AddComponentMenu("")]
public class ElseIfDialogueSeed : CheckDialogueSeed
{
    /// <summary>
    /// このコマンドがElse IfであることをFungusに伝えます。
    /// </summary>
    protected override bool IsElseIf { get { return true; } }

    /// <summary>
    /// ブロックを閉じる役割を持つことをFungusに伝えます。
    /// </summary>
    public override bool CloseBlock()
    {
        return true;
    }
}
using Fungus;
using UnityEngine;

/// <summary>
/// FungusのFlowchartから、店での会話を開始するためのカスタムコマンド
/// </summary>
[CommandInfo("Shop", "Start Shop Conversation", "店での会話を開始するコマンド")]
public class StartShopConversationCommand : Command
{
    public override void OnEnter()
    {
        if (ShopUIManager.instance != null)
        {
            ShopUIManager.instance.StartShopConversation();
        }
        else
        {
            Debug.LogError("ShopUIManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override string GetSummary()
    {
        return "店での会話を開始する";
    }
}

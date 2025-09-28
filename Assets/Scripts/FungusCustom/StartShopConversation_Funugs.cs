using Fungus;
using UnityEngine;

// --------------------------------
// 店での会話を開始するコマンド
// --------------------------------
[CommandInfo("Shop", "StartShopConversation", "店での会話を開始するコマンド")]
public class StartShopConversation_Fungus : Command
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

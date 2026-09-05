using Fungus;
using UnityEngine;

/// <summary>ショップを終了し、現在の店員に設定された会話を開始する。</summary>
[CommandInfo("Shop", "Start Shop Conversation", "ショップを終了し、現在の店員に設定された会話を開始します。")]
public class StartShopConversationCommand : Command
{
    public override void OnEnter()
    {
        var shop = ShopUIManager.instance;
        if (shop == null)
        {
            Debug.LogError("ShopUIManagerのインスタンスが見つかりません。", this);
            Continue();
            return;
        }

        if (!shop.TryStartShopConversationAndClose())
        {
            Debug.LogWarning("現在の店で会話を開始できませんでした。", this);
        }

        Continue();
    }

    public override string GetSummary()
    {
        return "ショップを終了 → 現在の店員と会話";
    }
}

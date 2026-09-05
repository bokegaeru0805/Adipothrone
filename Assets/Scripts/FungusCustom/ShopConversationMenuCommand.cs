using Fungus;

/// <summary>現在の店に実行可能な店内会話がある場合だけ、会話の選択肢を表示する。</summary>
[CommandInfo("Shop", "Shop Conversation Menu", "店内会話がある店だけに表示する「会話」選択肢。接続先にはStart Shop Conversationのブロックを指定します。")]
public class ShopConversationMenuCommand : Menu
{
    public override void OnEnter()
    {
        if (ShopUIManager.instance == null || !ShopUIManager.instance.HasShopConversation)
        {
            Continue();
            return;
        }

        base.OnEnter();
    }

    public override string GetSummary()
    {
        return "店内会話がある場合のみ表示: " + base.GetSummary();
    }
}

/// <summary>現在の店で利用できる店内会話。</summary>
public interface IShopConversation
{
    bool IsAvailable { get; }
    bool TryStartConversation();
}

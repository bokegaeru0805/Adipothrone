using Fungus;
using UnityEngine;

/// <summary>
/// 購入ショップUIを開くコマンド
/// </summary>
[CommandInfo("Shop", "Open Buy Shop UI", "購入ショップUIを開くコマンド")]
public class OpenBuyShopUICommand : Command
{
    public override void OnEnter()
    {
        if (ShopUIManager.instance != null)
        {
            ShopUIManager.instance.OpenBuyShop();
        }
        else
        {
            Debug.LogError("ShopUIManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override string GetSummary()
    {
        return "購入時のショップUIを開く";
    }
}

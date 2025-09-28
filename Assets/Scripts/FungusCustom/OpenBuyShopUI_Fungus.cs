using Fungus;
using UnityEngine;

// --------------------------------
// 購入ショップUIを開くコマンド
// --------------------------------
[CommandInfo("Shop", "OpenBuyShopUI", "購入ショップUIを開くコマンド")]
public class OpenBuyShopUI_Fungus : Command
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

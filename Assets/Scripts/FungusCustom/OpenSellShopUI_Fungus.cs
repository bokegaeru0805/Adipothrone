using Fungus;
using UnityEngine;

// --------------------------------
// 売却ショップUIを開くコマンド
// --------------------------------
[CommandInfo("Shop", "OpenSellShopUI", "売却ショップUIを開くコマンド")]
public class OpenSellShopUI_Fungus : Command
{
    public override void OnEnter()
    {
        if (ShopUIManager.instance != null)
        {
            ShopUIManager.instance.OpenSellShop();
        }
        else
        {
            Debug.LogError("ShopUIManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override string GetSummary()
    {
        return "売却時のショップUIを開く";
    }
}

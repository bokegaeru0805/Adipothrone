using Fungus;
using UnityEngine;

/// <summary>
/// 売却ショップUIを開くコマンド
/// </summary>
[CommandInfo("Shop", "Open Sell Shop UI", "売却ショップUIを開くコマンド")]
public class OpenSellShopUICommand : Command
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

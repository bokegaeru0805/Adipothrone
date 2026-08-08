using UnityEngine;

public class ShopInteractionTrigger : MonoBehaviour
{
    [Header("店の名前")]
    [SerializeField]
    private ShopName shopName = ShopName.None; // 店の名前を指定

    private void Awake()
    {
        if (shopName == ShopName.None)
        {
            Debug.LogError(
                "ShopNameがNoneに設定されています。適切なShopNameを設定してください。",
                this
            );
            return;
        }
    }

    public void ShopTrigger()
    {
        if (ShopUIManager.instance != null)
        {
            switch (shopName)
            {
                case ShopName.VillageGirl_Shop:
                    ShopUIManager.instance.SetShopID(shopName);
                    break;
                case ShopName.Desert_Shop:
                    ShopUIManager.instance.SetShopID(shopName);
                    break;
            }
        }
        else
        {
            Debug.LogError("ShopUIManagerが見つかりません。");
            return;
        }
    }
}

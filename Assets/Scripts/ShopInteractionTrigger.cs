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
            Debug.LogError("ShopNameが設定されていません。" + "オブジェクト名: " + gameObject.name);
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
            }
        }
        else
        {
            Debug.LogError("ShopUIManagerが見つかりません。");
            return;
        }
    }
}

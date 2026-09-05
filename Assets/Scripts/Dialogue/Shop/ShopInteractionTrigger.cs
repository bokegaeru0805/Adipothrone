using UnityEngine;

public class ShopInteractionTrigger : MonoBehaviour
{
    [Header("店舗データ")]
    [Tooltip("開く店のShopDataを指定します。ShopDataBaseへの登録は不要です。")]
    [SerializeField]
    private ShopData shopData;

    private void Awake()
    {
        if (shopData == null)
        {
            Debug.LogError("店舗データを設定してください。", this);
        }
    }

    /// <summary>指定された店舗データで店を開きます。</summary>
    public void ShopTrigger()
    {
        if (shopData == null)
        {
            Debug.LogError("店舗データを設定してください。", this);
            return;
        }

        if (ShopUIManager.instance == null)
        {
            Debug.LogError("ShopUIManagerが見つかりません。", this);
            return;
        }

        ShopUIManager.instance.OpenShop(shopData, GetComponent<ShopConversation>());
    }
}

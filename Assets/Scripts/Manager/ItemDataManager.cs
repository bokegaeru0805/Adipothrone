using System;
using UnityEngine;

/// <summary>
/// 各アイテムデータベースへのアクセスを仲介し、
/// IDに基づいてアイテム情報を取得するためのシングルトンクラス。
/// </summary>
public class ItemDataManager : MonoBehaviour
{
    public static ItemDataManager instance { get; private set; }

    [Header("アイテムデータベース")]
    [SerializeField]
    private WeaponItemDatabase weaponItemDatabase;

    [SerializeField]
    private HealItemDatabase healItemDatabase;

    [SerializeField]
    private TipsInfoDatabase tipsInfoDatabase;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(gameObject); //親オブジェクトがシーンが変わっても廃棄されないので不要
        }
        else
        {
            Destroy(gameObject);
        }

        if (weaponItemDatabase == null)
        {
            Debug.LogError("ItemDataManagerにWeaponItemDatabaseが設定されていません");
            return;
        }

        if (healItemDatabase == null)
        {
            Debug.LogError("ItemDataManagerにHealItemDatabaseが設定されていません");
            return;
        }

        if (tipsInfoDatabase == null)
        {
            Debug.LogError("ItemDataManagerにTipsInfoDatabaseが設定されていません");
            return;
        }
    }

    /// <summary>
    /// 指定されたIDに対応するBaseItemDataを取得します。
    /// /// </summary>
    /// <remarks>
    /// このメソッドは、IDに基づいてアイテムのタイプを判別し、対応するデータベースからアイテムデータを取得します。
    /// </remarks>
    /// <param name="ID">アイテムのID</param>
    public BaseItemData GetBaseItemDataByID(Enum ID)
    {
        // Enumから、タイプを判別する数に変更
        int typeNumber = EnumIDUtility.ExtractTypeID(EnumIDUtility.ToID(ID));
        BaseItemData itemData = null;

        switch (typeNumber)
        {
            case (int)TypeID.Blade:
                itemData = weaponItemDatabase.GetBladeByID(ID);
                break;
            case (int)TypeID.Shoot:
                itemData = weaponItemDatabase.GetShootByID(ID);
                break;
            case (int)TypeID.HealItem:
                itemData = healItemDatabase.GetItemByID(ID);
                break;
            default:
                Debug.LogWarning($"このID {ID} はBaseItemDataを持ちません");
                break;
        }
        return itemData;
    }

    /// <summary>
    /// 指定されたIDに対応するアイテムの名前を取得します。
    /// </summary>
    public string GetItemNameByID(Enum ID)
    {
        BaseItemData data = GetBaseItemDataByID(ID);
        return data != null ? data.itemName : "null";
    }

    /// <summary>
    /// 指定されたIDに対応するアイテムのスプライトを取得します。
    /// </summary>
    public Sprite GetItemSpriteByID(Enum ID)
    {
        BaseItemData data = GetBaseItemDataByID(ID);
        return data != null ? data.itemSprite : null;
    }

    /// <summary>
    /// 指定されたIDに対応するアイテムのランクを取得します。
    /// </summary>
    public ItemRank GetItemRankByID(Enum ID)
    {
        BaseItemData data = GetBaseItemDataByID(ID);
        return data != null ? data.itemRank : ItemRank.None;
    }

    /// <summary>
    /// 指定されたIDに対応するアイテムの売却価格を取得します。
    /// </summary>
    public int GetItemSellPriceByID(Enum ID)
    {
        BaseItemData data = GetBaseItemDataByID(ID);
        return data != null ? data.sellPrice : 0;
    }

    /// <summary>
    /// 指定されたIDのアイテムが売却可能かどうかを判定します。
    /// </summary>
    public bool IsItemSellable(Enum ID)
    {
        BaseItemData data = GetBaseItemDataByID(ID);
        return data != null && data.isSellable;
    }
}

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
    private StatusEnhanceItemDatabase statusEnhanceItemDatabase;

    [SerializeField]
    private KeyItemDatabase keyItemDatabase;

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

        if (
            weaponItemDatabase == null
            || healItemDatabase == null
            || tipsInfoDatabase == null
            || keyItemDatabase == null
            || statusEnhanceItemDatabase == null
        )
        {
            Debug.LogError("ItemDataManagerに必要なデータベースが設定されていません");
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
    /// <returns>対応するBaseItemData。存在しない場合はnull。</returns>
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
            case (int)TypeID.StatusEnhanceItem:
                itemData = statusEnhanceItemDatabase.GetItemByID(ID);
                break;
            case (int)TypeID.KeyItem:
                itemData = keyItemDatabase.GetItemByID(ID);
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
    /// <param name="ID">アイテムのID</param>
    /// <returns>アイテム名。存在しない場合は "null"。</returns>
    public string GetItemNameByID(Enum ID)
    {
        BaseItemData data = GetBaseItemDataByID(ID);
        return data != null ? data.itemName : "null";
    }

    /// <summary>
    /// 指定されたIDに対応するアイテムのスプライトを取得します。
    /// </summary>
    /// <param name="ID">アイテムのID</param>
    /// <returns>アイテムのスプライト。存在しない場合は null。</returns>
    public Sprite GetItemSpriteByID(Enum ID)
    {
        BaseItemData data = GetBaseItemDataByID(ID);
        return data != null ? data.itemSprite : null;
    }

    /// <summary>
    /// 指定されたIDに対応するアイテムのランクを取得します。
    /// </summary>
    /// <param name="ID">アイテムのID</param>
    /// <returns>アイテムのランク。存在しない場合は ItemRank.None。</returns>
    public ItemRank GetItemRankByID(Enum ID)
    {
        BaseItemData data = GetBaseItemDataByID(ID);
        return data != null ? data.itemRank : ItemRank.None;
    }

    /// <summary>
    /// 指定されたIDに対応するアイテムの売却価格を取得します。
    /// </summary>
    /// <param name="ID">アイテムのID</param>
    /// <returns>アイテムの売却価格。存在しない場合は 0。</returns>
    public int GetItemSellPriceByID(Enum ID)
    {
        BaseItemData data = GetBaseItemDataByID(ID);
        return data != null ? data.sellPrice : 0;
    }

    /// <summary>
    /// 指定されたIDのアイテムが売却可能かどうかを判定します。
    /// </summary>
    /// <param name="ID">アイテムのID</param>
    /// <returns>売却可能な場合は true、そうでない場合は false。</returns>
    public bool IsItemSellable(Enum ID)
    {
        BaseItemData data = GetBaseItemDataByID(ID);
        return data != null && data.isSellable;
    }
}

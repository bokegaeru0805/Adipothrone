using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class ItemEntry
{
    public int itemID;
    public int count;
    public int totalCount;

    public ItemEntry(int id, int amount)
    {
        itemID = id;
        count = amount;
        totalCount = amount;
    }
}

[System.Serializable]
public class InventoryItemData
{
    public List<ItemEntry> ownedItems = new();

    public enum ItemType
    {
        HealItem = 8,
        StatusEnhanceItem = 9,
        KeyItem = 12,
    }

    private readonly Dictionary<ItemType, int> itemTypeDigits =
        new()
        {
            { ItemType.HealItem, (int)TypeID.HealItem },
            { ItemType.StatusEnhanceItem, (int)TypeID.StatusEnhanceItem },
            { ItemType.KeyItem, (int)TypeID.KeyItem },
        };

    /// <summary>
    /// アイテムを追加
    /// </summary>
    /// <param name="itemID">追加したいアイテムのID（Enum）</param>
    /// <param name="amount">追加する数量</param>
    public void AddItem(Enum itemID, int amount = 1)
    {
        int itemIDNumber = EnumIDUtility.ToID(itemID);
        var entry = ownedItems.Find(e => e.itemID == itemIDNumber);
        if (entry != null)
        {
            entry.count += amount;
            entry.totalCount += amount; // 総所持数も更新
        }
        else
        {
            ownedItems.Add(new ItemEntry(itemIDNumber, amount));
            var playerManager = PlayerManager.instance;
            if (playerManager != null)
            {
                playerManager.SortOwnedItems(); // アイテム追加後に並び替え
            }
            else
            {
                Debug.LogError("PlayerManagerが見つかりません。アイテムの並び替えができません。");
            }
        }
        GameManager.instance?.NotifyInventoryUpdated(); // アイテム追加後に更新を通知
    }

    /// <summary>
    /// アイテムを使用（削除を含む）
    /// アイテムの効果は別のクラスで実装することを想定
    /// 具体的には、PlayerManagerのUseHealItemメソッドなどで使用される
    /// ここでは所持数を減らすだけ
    /// </summary>
    /// <param name="itemID">使用したいアイテムのID（Enum）</param>
    /// <param name="amount">使用する数量</param>
    /// <returns>使用に成功したかどうか</returns>
    public bool UseItem(Enum itemID, int amount = 1)
    {
        int itemIDNumber = EnumIDUtility.ToID(itemID);
        var entry = ownedItems.Find(e => e.itemID == itemIDNumber);
        if (entry != null && entry.count >= amount)
        {
            entry.count -= amount;
            //クイックリストの参照のために排除しない
            // if (entry.count <= 0)
            //     ownedItems.Remove(entry);
            GameManager.instance?.NotifyInventoryUpdated(); // アイテム使用後に更新を通知
            return true;
        }
        return false;
    }

    /// <summary>
    /// アイテムを使用（BaseItemData版）
    /// </summary>
    /// <param name="itemData">使用したいアイテムのデータ</param>
    /// <param name="amount">使用する数量</param>
    /// <returns>使用に成功したかどうか</returns>
    public bool UseItem(BaseItemData itemData, int amount = 1)
    {
        if (itemData == null)
        {
            Debug.LogWarning("UseItem: itemDataがnullです。");
            return false;
        }
        return UseItem(itemData.GetItemID(), amount);
    }

    /// <summary>
    /// 指定されたアイテムの所持数を取得します。
    /// </summary>
    /// <param name="itemID"> 取得したいアイテムのID（Enum）</param>
    /// <returns>指定されたアイテムの所持数</returns>
    public int GetItemAmount(Enum itemID)
    {
        int itemIDNumber = EnumIDUtility.ToID(itemID);
        var entry = ownedItems.Find(e => e.itemID == itemIDNumber);
        return entry?.count ?? 0;
    }

    /// <summary>
    /// 指定されたアイテムの所持数を取得（BaseItemData版）
    /// </summary>
    /// <param name="itemData">取得したいアイテムのデータ</param>
    /// <returns>指定されたアイテムの所持数</returns>
    public int GetItemAmount(BaseItemData itemData)
    {
        if (itemData == null)
        {
            Debug.LogWarning("GetItemAmount: itemDataがnullです。");
            return 0;
        }
        return GetItemAmount(itemData.GetItemID());
    }

    /// <summary>
    /// 指定されたタイプの全アイテムを取得します。
    /// </summary>
    /// <param name="type">取得したいアイテムのタイプ</param>
    /// <returns>指定されたタイプの全アイテムのリスト</returns>
    public List<ItemEntry> GetAllItemByType(ItemType type)
    {
        // タイプに対応する桁番号を取得
        int typeDigit = itemTypeDigits[type];
        // 所持アイテムの中から、指定タイプのものだけを抽出して ItemEntry に変換する
        return ownedItems.Where(e => EnumIDUtility.ExtractTypeID(e.itemID) == typeDigit).ToList();
    }

    /// <summary>
    /// アイテムリストをID順にソートします。
    /// </summary>
    public void SortByID()
    {
        ownedItems = ownedItems.OrderBy(e => e.itemID).ToList();
    }
}

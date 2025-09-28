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
    }

    private readonly Dictionary<ItemType, int> itemTypeDigits =
        new() { { ItemType.HealItem, (int)TypeID.HealItem } };
    public event Action OnItemCountChanged; //アイテムの所持数が変更されたときのイベント

    // アイテムを追加
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
        OnItemCountChanged?.Invoke();
    }

    // アイテムを使用（削除を含む）
    // アイテムの効果は別のクラスで実装することを想定
    // 具体的には、PlayerManagerのUseHealItemメソッドなどで使用される
    // ここでは所持数を減らすだけ
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
            OnItemCountChanged?.Invoke();
            return true;
        }
        return false;
    }

    // 所持数を取得
    public int GetItemAmount(Enum itemID)
    {
        int itemIDNumber = EnumIDUtility.ToID(itemID);
        var entry = ownedItems.Find(e => e.itemID == itemIDNumber);
        return entry?.count ?? 0;
    }

    // 所持中の特定タイプのアイテムを順番付きで取得
    public List<ItemEntry> GetAllItemByType(ItemType type)
    {
        // タイプに対応する桁番号を取得
        int typeDigit = itemTypeDigits[type];
        // 所持アイテムの中から、指定タイプのものだけを抽出して ItemEntry に変換する
        return ownedItems.Where(e => EnumIDUtility.ExtractTypeID(e.itemID) == typeDigit).ToList();
    }

    // ID順に並び替え
    public void SortByID()
    {
        ownedItems = ownedItems.OrderBy(e => e.itemID).ToList();
    }
}
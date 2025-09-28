using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HealItemDatabase", menuName = "Items/HealItem Database")]
public class HealItemDatabase : ScriptableObject
{
    public List<HealItemData> healItems = new List<HealItemData>();

    // IDからアイテムを取得（存在しなければnull）
    public HealItemData GetItemByID(Enum id)
    {
        if (id is HealItemName healItemID)
        {
            return healItems.Find(item => item.itemID == healItemID);
        }

        return null;
    }
}
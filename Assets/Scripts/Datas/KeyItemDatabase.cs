using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "KeyItemDatabase", menuName = "Items/KeyItem Database")]
public class KeyItemDatabase : ScriptableObject
{
    public List<KeyItemData> keyItems = new List<KeyItemData>();

    // IDからアイテムを取得（存在しなければnull）
    public KeyItemData GetItemByID(Enum id)
    {
        if (id is KeyItemName keyItemID)
        {
            return keyItems.Find(item => item.itemID == keyItemID);
        }

        Debug.LogWarning($"KeyItemDatabase: 指定されたID '{id}' はKeyItemName型ではありません。");
        return null;
    }
}

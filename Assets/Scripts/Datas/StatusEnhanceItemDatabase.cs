using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StatusEnhanceItemDatabase",
    menuName = "Items/StatusEnhanceItem Database"
)]
public class StatusEnhanceItemDatabase : ScriptableObject
{
    public List<StatusEnhanceItemData> statusEnhanceItems = new List<StatusEnhanceItemData>();

    // IDからアイテムを取得（存在しなければnull）
    public StatusEnhanceItemData GetItemByID(Enum id)
    {
        if (id is EnhanceItemName statusEnhanceItemID)
        {
            return statusEnhanceItems.Find(item => item.itemID == statusEnhanceItemID);
        }

        Debug.LogWarning(
            $"StatusEnhanceItemDatabase: 指定されたID '{id}' はStatusEnhanceItemName型ではありません。"
        );
        return null;
    }
}

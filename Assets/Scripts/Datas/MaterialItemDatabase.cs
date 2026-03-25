using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MaterialItemDatabase", menuName = "Items/MaterialItem Database")]
public class MaterialItemDatabase : ScriptableObject
{
    public List<MaterialItemData> materialItems = new List<MaterialItemData>();

    // IDからアイテムを取得（存在しなければnull）
    public MaterialItemData GetItemByID(Enum id)
    {
        if (id is MaterialItemName materialItemID)
        {
            return materialItems.Find(item => item.itemID == materialItemID);
        }

        Debug.LogWarning(
            $"MaterialItemDatabase: 指定されたID '{id}' はMaterialItemName型ではありません。"
        );
        return null;
    }
}

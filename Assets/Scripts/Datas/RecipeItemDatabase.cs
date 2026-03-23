using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RecipeItemDatabase", menuName = "Items/RecipeItem Database")]
public class RecipeItemDatabase : ScriptableObject
{
    public List<RecipeItemData> recipeItems = new List<RecipeItemData>();

    // IDからアイテムを取得（存在しなければnull）
    public RecipeItemData GetItemByID(Enum id)
    {
        if (id is RecipeItemName recipeItemID)
        {
            return recipeItems.Find(item => item.itemID == recipeItemID);
        }

        Debug.LogWarning(
            $"RecipeItemDatabase: 指定されたID '{id}' はRecipeItemName型ではありません。"
        );
        return null;
    }
}

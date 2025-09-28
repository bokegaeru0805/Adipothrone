using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ShopDataBase", menuName = "Shops/Shop DataBase")]
public class ShopDataBase : ScriptableObject
{
    public List<ShopData> shopList = new List<ShopData>(); // 店のリスト

    // IDから店を取得（存在しなければnull）
    public ShopData GetShopByID(Enum id)
    {
        if (id is ShopName shopID)
        {
            return shopList.Find(shop => shop.shopID == shopID);
        }

        return null;
    }
}

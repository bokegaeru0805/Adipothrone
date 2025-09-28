using System;
using UnityEngine;

public class BaseItemManager : MonoBehaviour
{
    public static BaseItemManager instance;

    private void Awake()
    {
        instance = this;
    }

    //<summary>
    // 任意のアイテムデータのIDを取得するメソッド
    // アイテムデータの型に応じて、適切なIDを返す
    // </summary>
    public Enum GetItemIDFromData(BaseItemData item)
    {
        if (item == null)
        {
            Debug.LogWarning("アイテムデータがnullです。");
            return null;
        }

        if (item is HealItemData heal)
        {
            return heal.itemID;
        }
        else if (item is ShootWeaponData shoot)
        {
            return shoot.weaponID;
        }
        else if (item is BladeWeaponData blade)
        {
            return blade.weaponID;
        }

        Debug.LogWarning("対応していないItemDataの型です: " + item.GetType().Name);
        return null;
    }
}

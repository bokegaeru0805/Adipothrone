using System.Collections.Generic;
using UnityEngine;

//注意
//新しい要素を追加したらItemDataEditor.csも修正すること

[CreateAssetMenu(fileName = "NewHealItem", menuName = "Items/HealItem")]
public class HealItemData : BaseItemData
{
    public HealItemName itemID; //ID
    public int hpHealAmount; // HP回復量
    public int wpHealAmount; // WP回復量

    // バフ効果をスクリプトで定義したクラスから選ぶようにする
    public List<PlayerBuffDebuffEffect> buffEffects = new List<PlayerBuffDebuffEffect>();

    public override System.Enum GetItemID()
    {
        return itemID;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

//注意
//新しい要素を追加したらItemDataEditor.csも修正すること

[System.Serializable]
public class ItemData : BaseItemData { }

[CreateAssetMenu(fileName = "NewHealItem", menuName = "Items/HealItem")]
public class HealItemData : ItemData
{
    public HealItemName itemID; // 名前(IDも兼ねる)
    public int hpHealAmount; // HP回復量
    public int wpHealAmount; // WP回復量

    // バフ効果をスクリプトで定義したクラスから選ぶようにする
    public List<PlayerBuffDebuffEffect> buffEffects = new List<PlayerBuffDebuffEffect>();
}
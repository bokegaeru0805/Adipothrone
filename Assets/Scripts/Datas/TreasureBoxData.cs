using System;
using UnityEngine;

[CreateAssetMenu(fileName = "TreasureBoxData", menuName = "Game/Treasure Box Data")]
public class TreasureBoxData : ScriptableObject
{
    public TreasureBoxName treasureBoxID; // 宝箱のID
    public BaseItemData baseItemData; // 中身のアイテムIDデータ
    public int itemAmount = 1; // アイテムの個数
}
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 宝箱の開封状態をセーブ・ロードするためにシリアライズ可能な形式で保持するクラス
/// </summary>
[System.Serializable]
public class TreasureStatus
{
    public int treasureID;
    public bool isOpened;

    // データの変換を容易にするためのコンストラクタ
    public TreasureStatus(int id, bool status)
    {
        treasureID = id;
        isOpened = status;
    }
}


[System.Serializable]
public class TreasureData
{
    /// <summary>
    /// 【セーブデータ用】宝箱の開封状態を保存するためのリスト。
    /// こちらにデータを移して保存します。
    /// </summary>
    [SerializeField]
    private List<TreasureStatus> openedTreasures = new List<TreasureStatus>();

    public event Action<int, bool> OnTreasureStatusChanged;

    public void SetTreasureOpened(TreasureBoxName id, bool isOpened = true)
    {
        int treasureIdInt = (int)id;
        if (GetTreasureOpened(id) == isOpened) return;
        openedTreasures.Add(new TreasureStatus(treasureIdInt, isOpened));
        OnTreasureStatusChanged?.Invoke(treasureIdInt, isOpened);
    }

    public bool GetTreasureOpened(TreasureBoxName id)
    {
        int treasureIdInt = (int)id;
        return openedTreasures.Any(t => t.treasureID == treasureIdInt && t.isOpened);
    }
    
    public List<TreasureBoxName> GetAllOpenedTreasureIDs()
    {
        return openedTreasures
            .Where(t => t.isOpened)
            .Select(t => (TreasureBoxName)t.treasureID)
            .ToList();
    }

    public void Reset()
    {
        openedTreasures.Clear();
    }
}
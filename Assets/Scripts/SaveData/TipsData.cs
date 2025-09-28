using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class TipsDataEntry
{
    public int TipsID; //TipsのID
    public bool isNew; // このTipsが新規入手（まだ選択されていない）状態かどうかを示すフラグ。
    public bool isPinned; // ユーザーによるお気に入り設定など（任意）

    public TipsDataEntry(int id)
    {
        TipsID = id;
        isNew = true; // 初期状態では新規入手
        isPinned = false; // 初期値は未ピン留め
    }
}

[System.Serializable]
public class TipsData
{
    public List<TipsDataEntry> unlockedTips = new List<TipsDataEntry>();

    /// <summary>
    /// 指定された TipsName のデータが未登録であれば追加します。
    /// すでに存在している場合は何もしません。
    /// </summary>
    public void RegisterTipsData(TipsName tipsName)
    {
        int tipsID = (int)tipsName;

        // 既に同じIDが登録されているかチェック
        if (unlockedTips.Any(t => t.TipsID == tipsID))
            return; // すでに登録済み → 何もしない

        // 未登録なら新規追加
        unlockedTips.Add(new TipsDataEntry(tipsID));

        var gameManager = GameManager.instance;
        if (gameManager != null)
        {
            // ゲームマネージャーが存在する場合、Tipsのソートを行う
            gameManager.SortUnlockedTips();
        }
        else
        {
            // ゲームマネージャーが存在しない場合、ログに警告を出力
            Debug.LogWarning("GameManagerが見つからないため、Tipsのソートが行われませんでした。");
        }
    }

    /// <summary>
    /// 指定されたTipsIDの新規フラグをfalseにします。
    /// </summary>
    public void MarkAsRead(int tipsID)
    {
        var tip = unlockedTips.FirstOrDefault(t => t.TipsID == tipsID);
        if (tip != null)
        {
            tip.isNew = false;
        }
    }
}

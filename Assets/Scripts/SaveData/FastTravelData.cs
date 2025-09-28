using System;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class FastTravelEntry
{
    public int FastTravelID; //ファストトラベルのID

    public FastTravelEntry(int id)
    {
        FastTravelID = id;
    }
}

[System.Serializable]
public class FastTravelData
{
    public List<FastTravelEntry> unlockedFastTravels = new List<FastTravelEntry>();

    // 最後に使用したファストトラベルのIDを保存する変数 (-1は未使用を表す)
    public int LastUsedFastTravelID = -1;

    /// <summary>
    /// 指定された FastTravelName のデータが未登録であれば追加します。
    /// すでに存在している場合は何もしません。
    /// </summary>
    public void RegisterFastTravelData(FastTravelName fastTravelName)
    {
        int fastTravelID = (int)fastTravelName;

        // 既に同じIDが登録されているかチェック
        if (unlockedFastTravels.Any(t => t.FastTravelID == fastTravelID))
            return; // すでに登録済み → 何もしない

        // 未登録なら新規追加
        unlockedFastTravels.Add(new FastTravelEntry(fastTravelID));
        //ファストトラベルデータをIDでソート
        SortByFastTravelID();
    }

    /// <summary>
    /// 指定された FastTravelName のデータをリストから削除します。
    /// </summary>
    /// <param name="fastTravelName">削除するファストトラベル地点</param>
    public void RemoveFastTravelData(FastTravelName fastTravelName)
    {
        int fastTravelID = (int)fastTravelName;

        // リストから指定されたIDを持つ要素をすべて削除
        unlockedFastTravels.RemoveAll(entry => entry.FastTravelID == fastTravelID);

        // もし削除した地点が「最後に使用した地点」だった場合、その記録もリセットする
        if (LastUsedFastTravelID == fastTravelID)
        {
            LastUsedFastTravelID = -1;
        }
    }

    /// <summary>
    /// 指定された FastTravelName のデータが登録済みか調べます。
    /// /// 登録済みなら true、未登録なら false を返します。
    /// /// </summary>
    public bool IsFastTravelDataRegistered(FastTravelName fastTravelName)
    {
        int fastTravelID = (int)fastTravelName;

        // 登録済みかどうかをチェック
        return unlockedFastTravels.Any(t => t.FastTravelID == fastTravelID);
    }

    /// <summary>
    /// 最後に使用したファストトラベル地点を設定します。
    /// </summary>
    /// <param name="fastTravelName">使用したファストトラベル地点</param>
    public void SetLastUsedFastTravel(FastTravelName fastTravelName)
    {
        LastUsedFastTravelID = (int)fastTravelName;
    }

    /// <summary>
    /// FastTravelIDの昇順でunlockedFastTravelsリストを並べ替えます。
    /// </summary>
    public void SortByFastTravelID()
    {
        unlockedFastTravels = unlockedFastTravels.OrderBy(t => t.FastTravelID).ToList();
    }
}

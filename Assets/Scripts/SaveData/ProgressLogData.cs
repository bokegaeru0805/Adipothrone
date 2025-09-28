using System.Collections.Generic;

[System.Serializable]
public class ProgressLogDataEntry
{
    public int progressID; //ゲーム全体の進行度ID

    public ProgressLogDataEntry(int id)
    {
        progressID = id;
    }
}

[System.Serializable]
public class ProgressLogData
{
    public List<ProgressLogDataEntry> progressRecords = new List<ProgressLogDataEntry>();

    public void RegisterProgressData(ProgressLogName progressLogName)
    {
        int progressID = (int)progressLogName;

        // 一度すべての要素を削除（常に1つだけ保持するため）
        progressRecords.Clear();

        // 新しいデータを追加
        progressRecords.Add(new ProgressLogDataEntry(progressID));
    }
}

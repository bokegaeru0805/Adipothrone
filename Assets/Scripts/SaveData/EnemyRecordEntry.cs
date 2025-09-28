using System;
using System.Collections.Generic;

/// <summary>
/// 個々の敵に関する記録を保持するエントリークラス
/// </summary>
[Serializable]
public class EnemyRecordEntry
{
    public int enemyIdValue;
    public int killCount;
    public bool isNew = true; //新規討伐フラグ（デフォルトはtrue）

    // 今後、初めて遭遇した日時などの新しい記録をここに追加できます
    // public bool hasEncountered = false;
    // public int maxDamageDealt = 0;

    // コンストラクタ
    public EnemyRecordEntry(int idValue, int amount)
    {
        enemyIdValue = idValue;
        killCount = amount;
        isNew = true; // 新規登録時は必ずNew
    }
}

/// <summary>
/// 全ての敵関連のセーブデータを統括するクラス
/// </summary>
[Serializable]
public class EnemyRecordData
{
    public List<EnemyRecordEntry> enemyRecords = new();

    /// <summary>
    /// 指定された敵の討伐数を加算する
    /// </summary>
    public void AddKillCount(EnemyName enemyID, int amount = 1)
    {
        int targetIdValue = (int)enemyID;

        // [変更点] 変数名とリスト名の変更を反映
        var entry = enemyRecords.Find(e => e.enemyIdValue == targetIdValue);
        if (entry != null)
        {
            entry.killCount += amount;
        }
        else
        {
            enemyRecords.Add(new EnemyRecordEntry(targetIdValue, amount));
        }
    }

    /// <summary>
    /// 指定された敵の討伐数を取得する
    /// </summary>
    public int GetKillCount(EnemyName enemyID)
    {
        int targetIdValue = (int)enemyID;
        // [変更点] 変数名とリスト名の変更を反映
        var entry = enemyRecords.Find(e => e.enemyIdValue == targetIdValue);
        return entry?.killCount ?? 0;
    }

    /// <summary>
    /// 図鑑登録済みか（一度でも倒したか）を判定する
    /// </summary>
    public bool IsUnlocked(EnemyName enemyID)
    {
        return GetKillCount(enemyID) > 0;
    }

    /// <summary>
    /// 図鑑に登録済みのすべての敵IDのリストを取得する
    /// </summary>
    public List<EnemyName> GetUnlockedEnemies()
    {
        List<EnemyName> unlockedList = new List<EnemyName>();
        // [変更点] リスト名の変更を反映
        foreach (var entry in enemyRecords)
        {
            if (entry.killCount > 0)
            {
                // [変更点] 変数名の変更を反映
                if (Enum.IsDefined(typeof(EnemyName), entry.enemyIdValue))
                {
                    unlockedList.Add((EnemyName)entry.enemyIdValue);
                }
            }
        }
        return unlockedList;
    }

    /// <summary>
    /// 指定した敵を「確認済み」としてマークする
    /// </summary>
    public void MarkAsSeen(int enemyIdValue)
    {
        var entry = enemyRecords.Find(e => e.enemyIdValue == enemyIdValue);
        if (entry != null)
        {
            entry.isNew = false;
        }
    }

    /// <summary>
    /// isNewフラグを取得するためのヘルパーメソッド
    /// </summary>
    public bool IsNew(int enemyIdValue)
    {
        var entry = enemyRecords.Find(e => e.enemyIdValue == enemyIdValue);
        return entry?.isNew ?? false;
    }
}
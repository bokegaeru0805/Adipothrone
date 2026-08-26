using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

#region Entry Class

/// <summary>
/// 個々の敵に関する討伐記録やドロップアイテムの解放状況を保持するエントリークラス。
/// </summary>
[Serializable]
public class EnemyRecordEntry
{
    #region Fields

    /// <summary>
    /// 敵のID（EnemyName Enumの整数値）
    /// </summary>
    public int enemyIdValue;

    /// <summary>
    /// 総討伐数
    /// </summary>
    public int killCount;

    /// <summary>
    /// 新規討伐フラグ（図鑑で「NEW」を表示するため）。デフォルトはtrue。
    /// </summary>
    public bool isNew = true;

    /// <summary>
    /// 遭遇済みフラグ（図鑑で敵のシルエットを表示するため）。
    /// </summary>
    public bool hasEncountered = false;

    /// <summary>
    /// この敵からスキルクリスタルを取得済みか。
    /// </summary>
    public bool hasObtainedSkillCrystal = false;

    // 内部リスト（nullチェック用のプロパティ経由でアクセス推奨）
    private List<int> _unlockedDropItemIds; // 解除済みドロップアイテムID
    private List<int> _unlockedConditionItemIds; // 解除済み条件付きドロップアイテムID
    #endregion

    #region Properties (Safe Access)

    /// <summary>
    /// 解除済みのドロップアイテムIDリスト（nullなら自動生成）
    /// </summary>
    public List<int> UnlockedDropItemIds
    {
        get
        {
            if (_unlockedDropItemIds == null)
            {
                _unlockedDropItemIds = new List<int>();
            }
            return _unlockedDropItemIds;
        }
    }

    /// <summary>
    /// 解除済みの条件付きドロップアイテムIDリスト（nullなら自動生成）
    /// </summary>
    public List<int> UnlockedConditionItemIds
    {
        get
        {
            if (_unlockedConditionItemIds == null)
            {
                _unlockedConditionItemIds = new List<int>();
            }
            return _unlockedConditionItemIds;
        }
    }

    #endregion

    #region Constructor

    public EnemyRecordEntry(int idValue, int amount)
    {
        enemyIdValue = idValue;
        killCount = amount;
        isNew = true;
        hasEncountered = true;
        hasObtainedSkillCrystal = false;
        _unlockedDropItemIds = new List<int>();
        _unlockedConditionItemIds = new List<int>();
    }

    #endregion
}

#endregion

#region Manager Class (Data Container)

/// <summary>
/// 全ての敵関連のセーブデータを統括するクラス。
/// 討伐数の加算や、ドロップアイテムの解禁情報の管理を行います。
/// </summary>
[Serializable]
public class EnemyRecordData
{
    // 全敵のレコードリスト
    public List<EnemyRecordEntry> enemyRecords = new();

    // 入手済みユニークアイテムIDリスト
    public List<int> ObtainedUniqueItemIds = new List<int>();

    #region Modification Methods (Write)

    /// <summary>
    /// 指定された敵の討伐数を加算します。
    /// レコードが存在しない場合は新規作成します。
    /// </summary>
    public void AddKillCount(EnemyName enemyID, int amount = 1)
    {
        int targetIdValue = (int)enemyID;
        var entry = enemyRecords.Find(e => e.enemyIdValue == targetIdValue);

        if (entry != null)
        {
            entry.killCount += amount;
            // 念のためここでもtrueに
            entry.hasEncountered = true;
        }
        else
        {
            // 初回討伐時は新規エントリーを作成
            enemyRecords.Add(new EnemyRecordEntry(targetIdValue, amount));
        }
    }

    /// <summary>
    /// 指定された敵のドロップアイテムを「確認済み（解禁）」として記録します。
    /// </summary>
    public void UnlockDropItem(EnemyName enemyID, int itemID)
    {
        var entry = GetEntry(enemyID);
        if (entry != null)
        {
            // 重複チェックを行ってから追加
            if (!entry.UnlockedDropItemIds.Contains(itemID))
            {
                entry.UnlockedDropItemIds.Add(itemID);
            }
        }
    }

    /// <summary>
    /// 指定された敵のマスターデータ(EnemyData)を元に、その敵が持つすべてのドロップアイテムを解禁済みとして記録します。
    /// デバッグや図鑑の全開放機能などに使用します。
    /// </summary>
    /// <param name="enemyData">全開放したい敵のマスターデータ</param>
    public void UnlockAllDropItems(EnemyData enemyData)
    {
        if (enemyData == null || enemyData.dropItems == null)
        {
            return;
        }

        // 該当の敵のエントリーを取得（存在しない場合は自動的に新規作成されます）
        var entry = GetOrCreateEntry(enemyData.enemyID);

        foreach (var dropItem in enemyData.dropItems)
        {
            if (dropItem.baseItemData != null)
            {
                // BaseItemDataからEnum型のIDを取得し、保存用のint型に変換
                int itemID = Convert.ToInt32(dropItem.baseItemData.GetItemID());

                // 通常のドロップアイテムとして図鑑に登録（重複チェック）
                if (!entry.UnlockedDropItemIds.Contains(itemID))
                {
                    entry.UnlockedDropItemIds.Add(itemID);
                }

                // 条件付きドロップ（特定レベル以下での討伐など）に設定されている場合は、その条件も解禁済みにする
                if (dropItem.hasCondition && !entry.UnlockedConditionItemIds.Contains(itemID))
                {
                    entry.UnlockedConditionItemIds.Add(itemID);
                }
            }
        }
    }

    /// <summary>
    /// 指定された敵の「条件付きドロップ」の条件を「解禁済み」として記録します。
    /// </summary>
    public void UnlockItemCondition(EnemyName enemyID, int itemID)
    {
        var entry = GetEntry(enemyID);
        if (entry != null)
        {
            if (!entry.UnlockedConditionItemIds.Contains(itemID))
            {
                entry.UnlockedConditionItemIds.Add(itemID);
            }
        }
    }

    /// <summary>
    /// 指定された敵のスキルクリスタルを取得済みとして記録します。
    /// </summary>
    public void UnlockSkillCrystal(EnemyName enemyID)
    {
        GetOrCreateEntry(enemyID).hasObtainedSkillCrystal = true;
    }

    /// <summary>
    /// 指定した敵を「確認済み（NEWフラグ解除）」としてマークします。
    /// 図鑑を開いた際などに呼び出します。
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
    /// 指定された敵のエントリーを取得し、存在しない場合は新規作成して返します。
    /// 確実にエントリーが必要な場合に使用します。
    /// </summary>
    public EnemyRecordEntry GetOrCreateEntry(EnemyName enemyID)
    {
        int targetIdValue = (int)enemyID;
        var entry = enemyRecords.Find(e => e.enemyIdValue == targetIdValue);

        if (entry == null)
        {
            entry = new EnemyRecordEntry(targetIdValue, 0);
            enemyRecords.Add(entry);
        }
        return entry;
    }

    /// <summary>
    /// 指定された敵を「遭遇済み」として登録します。
    /// </summary>
    /// <param name="enemyID">敵の識別子</param>
    public void RegisterEncounter(EnemyName enemyID)
    {
        int targetIdValue = (int)enemyID;
        var entry = enemyRecords.Find(e => e.enemyIdValue == targetIdValue);

        if (entry == null)
        {
            // 討伐数0でエントリを作成
            entry = new EnemyRecordEntry(targetIdValue, 0);
            enemyRecords.Add(entry);
        }
        // 遭遇フラグをON
        entry.hasEncountered = true;
    }

    /// <summary>
    /// ユニークアイテムを入手済みとして即座に記録する
    /// </summary>
    public void MarkUniqueItemAsObtained(int itemID)
    {
        if (!ObtainedUniqueItemIds.Contains(itemID))
        {
            ObtainedUniqueItemIds.Add(itemID);
            // 必要に応じてここでSaveLoadManager.Save()などを呼ぶことも検討してください
        }
    }

    #endregion

    #region Query Methods (Read)

    /// <summary>
    /// 指定された敵の討伐数を取得します。
    /// </summary>
    public int GetKillCount(EnemyName enemyID)
    {
        var entry = GetEntry(enemyID);
        return entry?.killCount ?? 0;
    }

    /// <summary>
    /// 図鑑登録済みか（一度でも倒したか）を判定します。
    /// </summary>
    public bool IsUnlocked(EnemyName enemyID)
    {
        return GetKillCount(enemyID) > 0;
    }

    /// <summary>
    /// 指定された敵の特定のアイテムがドロップ解禁済みかを判定します。
    /// </summary>
    public bool IsDropUnlocked(EnemyName enemyID, int itemID)
    {
        var entry = GetEntry(enemyID);
        if (entry != null)
        {
            return entry.UnlockedDropItemIds.Contains(itemID);
        }
        return false;
    }

    /// <summary>
    /// 指定された敵の特定の条件付きドロップが解禁済みかを判定します。
    /// </summary>
    public bool IsItemConditionUnlocked(EnemyName enemyID, int itemID)
    {
        var entry = GetEntry(enemyID);
        if (entry != null)
        {
            return entry.UnlockedConditionItemIds.Contains(itemID);
        }
        return false;
    }

    /// <summary>
    /// 指定された敵のスキルクリスタルを取得済みか判定します。
    /// </summary>
    public bool IsSkillCrystalUnlocked(EnemyName enemyID)
    {
        var entry = GetEntry(enemyID);
        return entry != null && entry.hasObtainedSkillCrystal;
    }

    /// <summary>
    /// 指定された敵が「NEW（新規討伐）」状態かを取得します。
    /// </summary>
    public bool IsNew(int enemyIdValue)
    {
        var entry = enemyRecords.Find(e => e.enemyIdValue == enemyIdValue);
        return entry?.isNew ?? false;
    }

    /// <summary>
    /// 指定された敵が「遭遇済み」かを取得します。
    /// </summary>
    public bool IsEncountered(EnemyName enemyID)
    {
        int targetIdValue = (int)enemyID;
        var entry = enemyRecords.Find(e => e.enemyIdValue == targetIdValue);
        // エントリが存在し、かつ遭遇フラグがtrueならOK
        return entry != null && entry.hasEncountered;
    }

    /// <summary>
    /// 図鑑に登録済み（討伐数 > 0）のすべての敵IDのリストを取得します。
    /// </summary>
    public List<EnemyName> GetUnlockedEnemies()
    {
        List<EnemyName> unlockedList = new List<EnemyName>();

        foreach (var entry in enemyRecords)
        {
            if (entry.killCount > 0)
            {
                // int値がEnemyName Enumとして定義されているか確認して追加
                if (Enum.IsDefined(typeof(EnemyName), entry.enemyIdValue))
                {
                    unlockedList.Add((EnemyName)entry.enemyIdValue);
                }
            }
        }
        return unlockedList;
    }

    /// <summary>
    /// 指定したアイテムが既に入手済みのユニークアイテムか判定する
    /// </summary>
    public bool IsUniqueItemObtained(int itemID)
    {
        return ObtainedUniqueItemIds.Contains(itemID);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 指定された敵IDに対応するエントリーを検索して返します。
    /// 見つからない場合はnullを返します。
    /// </summary>
    private EnemyRecordEntry GetEntry(EnemyName enemyID)
    {
        int targetIdValue = (int)enemyID;
        return enemyRecords.Find(e => e.enemyIdValue == targetIdValue);
    }

    #endregion
}

#endregion

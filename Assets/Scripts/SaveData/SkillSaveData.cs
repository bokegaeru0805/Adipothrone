using System;
using System.Collections.Generic;

/// <summary>
/// 1つのスキルの保存データ
/// </summary>
[Serializable]
public class SkillEntry
{
    public int skillID; // スキルのID (enumのint値)
    public bool isUnlocked; // 解放(認知/所持)されているか（UIに表示されるか）
    public bool isEquipped; // 現在装備(有効化)されているか
    public bool isNew; // UIで「NEW!」を表示するためのフラグ
    public int level; // スキルのレベル（必要に応じて）

    public SkillEntry(int id, bool unlocked = true)
    {
        skillID = id;
        isUnlocked = unlocked;
        isEquipped = false; // 初期状態は未装備
        isNew = unlocked;
        level = 1;
    }
}

/// <summary>
/// スキルシステム全体のセーブデータをまとめたクラス
/// </summary>
[Serializable]
public class SkillSaveData
{
    // これまでにプレイヤーが獲得したスキルポイントの「総計」
    public int totalEarnedSkillPoints = 0;

    // プレイヤーが認知・所持しているスキルのリスト
    public List<SkillEntry> knownSkills = new List<SkillEntry>();

    // ※「現在の残りスキルポイント」はここには保存せず、ゲーム実行中に計算します

    public void Validate()
    {
        if (knownSkills == null)
            knownSkills = new List<SkillEntry>();

        totalEarnedSkillPoints = Math.Max(0, totalEarnedSkillPoints);

        Dictionary<int, SkillEntry> entriesByID = new Dictionary<int, SkillEntry>();

        for (int i = knownSkills.Count - 1; i >= 0; i--)
        {
            SkillEntry entry = knownSkills[i];
            if (entry == null)
            {
                knownSkills.RemoveAt(i);
                continue;
            }

            entry.level = Math.Max(1, entry.level);
            if (!entry.isUnlocked)
                entry.isEquipped = false;

            if (entriesByID.TryGetValue(entry.skillID, out SkillEntry existingEntry))
            {
                existingEntry.isUnlocked |= entry.isUnlocked;
                existingEntry.isEquipped |= entry.isEquipped;
                existingEntry.isNew |= entry.isNew;
                existingEntry.level = Math.Max(existingEntry.level, entry.level);
                knownSkills.RemoveAt(i);
            }
            else
            {
                entriesByID.Add(entry.skillID, entry);
            }
        }
    }
}

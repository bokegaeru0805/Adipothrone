using System;
using System.Collections.Generic;
using UnityEngine;

public class SkillManager : MonoBehaviour
{
    [Header("データベース参照")]
    [Tooltip("全スキルデータが登録されたデータベースをアタッチしてください")]
    public SkillDatabase skillDatabase;

    public static SkillManager instance { get; private set; }

    // スキルのマスターデータをID検索するための辞書
    private Dictionary<int, SkillData> skillDataCache = new Dictionary<int, SkillData>();

    // 高速アクセス用のキャッシュ（装備中のスキルIDのみを保持）
    private HashSet<int> equippedSkillsCache = new HashSet<int>();

    // 高速アクセス用のキャッシュ（所持・解放済みのスキルIDのみを保持）
    private HashSet<int> unlockedSkillsCache = new HashSet<int>();

    // スキル状態やポイントが変化したときにUIを更新するためのイベント
    public event Action OnSkillStateChanged;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeSkillDatabaseCache();
    }

    private void Start()
    {
        // ゲーム開始時やロード完了時にキャッシュを構築する
        RebuildSkillCache();
    }

    /// <summary>
    /// 起動時に1回だけ呼ばれ、リスト型のデータベースを高速検索用のDictionaryに変換する
    /// </summary>
    private void InitializeSkillDatabaseCache()
    {
        skillDataCache.Clear();
        if (skillDatabase == null || skillDatabase.skills == null)
        {
            Debug.LogError("SkillManager に SkillDatabase が設定されていません！");
            return;
        }

        foreach (var skillData in skillDatabase.skills)
        {
            if (skillData != null)
            {
                int id = EnumIDUtility.ToID(skillData.skillID);
                if (!skillDataCache.ContainsKey(id))
                {
                    skillDataCache.Add(id, skillData);
                }
            }
        }
    }

    /// <summary>
    /// セーブデータからキャッシュを構築する
    /// ロード時や、スキルの着脱が行われた際に呼び出します
    /// </summary>
    public void RebuildSkillCache()
    {
        equippedSkillsCache.Clear();
        unlockedSkillsCache.Clear();

        if (GameManager.instance == null || GameManager.instance.savedata == null)
        {
            Debug.LogWarning("セーブデータが存在しないため、スキルキャッシュを構築できません。");
            return;
        }

        var savedSkills = GameManager.instance.savedata.SkillData.knownSkills;
        foreach (var skill in savedSkills)
        {
            if (skill.isUnlocked)
            {
                unlockedSkillsCache.Add(skill.skillID);
            }

            if (skill.isEquipped)
            {
                equippedSkillsCache.Add(skill.skillID);
            }
        }

        OnSkillStateChanged?.Invoke();
    }

    /// <summary>
    /// 指定したスキルを解放（取得）する処理
    /// 宝箱やイベント、ボス討伐時などに呼び出します
    /// </summary>
    /// <param name="skillID">解放するスキルのID</param>
    public void UnlockSkill(Enum skillID)
    {
        if (GameManager.instance == null || GameManager.instance.savedata == null)
        {
            Debug.LogWarning("セーブデータが存在しないため、スキルを解放できません。");
            return;
        }

        int id = EnumIDUtility.ToID(skillID);
        var skillData = GameManager.instance.savedata.SkillData;
        var skillEntry = skillData.knownSkills.Find(s => s.skillID == id);

        if (skillEntry != null)
        {
            // 既にリストに存在する場合（未解放状態で認知だけしていた場合など）
            if (!skillEntry.isUnlocked)
            {
                skillEntry.isUnlocked = true;
                skillEntry.isNew = true; // 新規取得フラグを立てる
                RebuildSkillCache(); // キャッシュを更新
                // Debug.Log($"スキルID:{skillID} を解放しました！");
            }
        }
        else
        {
            // リストにない場合は新規追加して解放
            skillData.knownSkills.Add(new SkillEntry(id, true));
            RebuildSkillCache(); // キャッシュを更新
            // Debug.Log($"スキルID:{skillID} を新規取得・解放しました！");
        }
    }

    /// <summary>
    /// 他のスクリプト(移動や攻撃など)から、指定したスキルが有効か判定する
    /// HashSetを使用しているため、毎フレーム呼ばれても負荷は極小です
    /// </summary>
    /// <param name="skillID">判定するスキルのID</param>
    /// <returns>装備中であればtrue</returns>
    public bool IsSkillActive(Enum skillID)
    {
        int id = EnumIDUtility.ToID(skillID);
        return equippedSkillsCache.Contains(id);
    }

    /// <summary>
    /// 指定したスキルを既に取得（解放）しているかどうかを判定する
    /// HashSetを使用しているため、毎フレーム呼ばれても負荷は極小です
    /// </summary>
    /// <param name="skillID">判定するスキルのID</param>
    /// <returns>取得済みであればtrue</returns>
    public bool IsSkillUnlocked(Enum skillID)
    {
        int id = EnumIDUtility.ToID(skillID);
        return unlockedSkillsCache.Contains(id);
    }

    /// <summary>
    /// スキルポイント(クリスタル)を獲得した際に呼び出す
    /// </summary>
    /// <param name="amount">獲得したポイント数</param>
    public void AddSkillPoint(int amount = 1)
    {
        if (GameManager.instance.savedata != null)
        {
            GameManager.instance.savedata.SkillData.totalEarnedSkillPoints += amount;
            Debug.Log(
                $"スキルポイントを {amount} 獲得しました。合計: {GameManager.instance.savedata.SkillData.totalEarnedSkillPoints}"
            );
            OnSkillStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// 現在使用可能な（余っている）スキルポイントを計算して返す
    /// </summary>
    public int GetAvailableSkillPoints()
    {
        if (GameManager.instance.savedata == null)
            return 0;

        int totalEarned = GameManager.instance.savedata.SkillData.totalEarnedSkillPoints;
        int usedPoints = 0;

        // HashSet を使って、装備中のスキルIDのみを高速ループ
        foreach (int equippedID in equippedSkillsCache)
        {
            // Dictionary を使ってコストを O(1) で取得
            if (skillDataCache.TryGetValue(equippedID, out SkillData data))
            {
                usedPoints += data.requiredPoints;
            }
        }

        return totalEarned - usedPoints;
    }

    /// <summary>
    /// スキルを装備する処理
    /// </summary>
    public bool EquipSkill(Enum skillID)
    {
        int id = EnumIDUtility.ToID(skillID);

        // 1. 取得済みかどうかのチェック（HashSetで一瞬で判定）
        if (!unlockedSkillsCache.Contains(id))
        {
            Debug.LogWarning("未解放のスキルは装備できません。");
            return false;
        }

        // 2. データベースからスキル情報を取得
        if (!skillDataCache.TryGetValue(id, out SkillData data))
        {
            Debug.LogError($"スキルID:{id} のデータがデータベースに存在しません。");
            return false;
        }

        // 3. コストが足りているかチェック
        if (GetAvailableSkillPoints() >= data.requiredPoints)
        {
            var skillEntry = GameManager.instance.savedata.SkillData.knownSkills.Find(s =>
                s.skillID == id
            );
            if (skillEntry != null)
            {
                skillEntry.isEquipped = true;
                RebuildSkillCache(); // キャッシュを再構築
                return true;
            }
        }
        else
        {
            Debug.Log("スキルポイントが足りません。");
        }

        return false;
    }

    /// <summary>
    /// スキルを外す処理
    /// </summary>
    public void UnequipSkill(Enum skillID)
    {
        int id = EnumIDUtility.ToID(skillID);
        var skillEntry = GameManager.instance.savedata.SkillData.knownSkills.Find(s =>
            s.skillID == id
        );

        if (skillEntry != null && skillEntry.isEquipped)
        {
            skillEntry.isEquipped = false;
            RebuildSkillCache();
        }
    }

    /// <summary>
    /// データベースからスキル情報を検索し、そのスキルカテゴリーを取得する
    /// </summary>
    /// <param name="skillID">判定するスキルのID</param>
    /// <returns>スキルカテゴリー</returns>
    public SkillCategory GetSkillCategory(SkillName skillID)
    {
        // SkillDatabaseから直接SkillDataを取得
        SkillData data = skillDatabase.GetSkillByID(skillID);

        if (data != null)
        {
            // SkillDataに定義されているカテゴリーを返す
            return data.category;
        }

        Debug.LogWarning($"スキルID:{skillID} のデータがデータベースに存在しません。");
        return SkillCategory.None;
    }

    /// <summary>
    /// データベースからスキル情報を検索し、そのスキルの表示名（日本語名）を取得する
    /// </summary>
    /// <param name="skillID">判定するスキルのID</param>
    /// <returns>スキルの表示名</returns>
    public string GetSkillDisplayName(SkillName skillID)
    {
        SkillData data = skillDatabase.GetSkillByID(skillID);

        if (data != null)
        {
            // SkillDataに定義されている日本語名を返す
            return data.skillName;
        }

        Debug.LogWarning($"スキルID:{skillID} のデータがデータベースに存在しません。");
        return skillID.ToString(); // 万が一取得できなかった場合は列挙型名を返す
    }
}

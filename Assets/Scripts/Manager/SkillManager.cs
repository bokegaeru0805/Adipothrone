using System;
using System.Collections.Generic;
using UnityEngine;

public enum SkillEquipResult
{
    Success = 0,
    InvalidSkill = 10,
    NotUnlocked = 20,
    AlreadyEquipped = 30,
    NotEnoughPoints = 40,
    PrerequisiteNotMet = 50,
    ExclusiveSkillEquipped = 60,
    SaveDataUnavailable = 70,
}

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
                else
                {
                    Debug.LogError($"SkillDatabase に重複したスキルIDがあります: {id}");
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

        GameManager.instance.savedata.SkillData.Validate();
        var savedSkills = GameManager.instance.savedata.SkillData.knownSkills;
        foreach (var skill in savedSkills)
        {
            if (skill == null || !skillDataCache.ContainsKey(skill.skillID))
            {
                if (skill != null)
                    Debug.LogWarning($"SkillDatabase に存在しないスキルIDを検出しました: {skill.skillID}");
                continue;
            }

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
    public void UnlockSkill(SkillName skillID)
    {
        if (GameManager.instance == null || GameManager.instance.savedata == null)
        {
            Debug.LogWarning("セーブデータが存在しないため、スキルを解放できません。");
            return;
        }

        int id = EnumIDUtility.ToID(skillID);
        if (!skillDataCache.ContainsKey(id))
        {
            Debug.LogError($"スキルID:{id} のデータがデータベースに存在しません。");
            return;
        }
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

    [Obsolete("UnlockSkill(SkillName) を使用してください。")]
    public void UnlockSkill(Enum skillID)
    {
        if (skillID is SkillName typedSkillID)
            UnlockSkill(typedSkillID);
        else
            Debug.LogError($"SkillName 以外のEnumはスキルIDとして使用できません: {skillID}");
    }

    /// <summary>
    /// 他のスクリプト(移動や攻撃など)から、指定したスキルが有効か判定する
    /// HashSetを使用しているため、毎フレーム呼ばれても負荷は極小です
    /// </summary>
    /// <param name="skillID">判定するスキルのID</param>
    /// <returns>装備中であればtrue</returns>
    public bool IsSkillActive(SkillName skillID)
    {
        int id = EnumIDUtility.ToID(skillID);
        return equippedSkillsCache.Contains(id);
    }

    [Obsolete("IsSkillActive(SkillName) を使用してください。")]
    public bool IsSkillActive(Enum skillID)
    {
        return skillID is SkillName typedSkillID && IsSkillActive(typedSkillID);
    }

    /// <summary>
    /// 指定したスキルを既に取得（解放）しているかどうかを判定する
    /// HashSetを使用しているため、毎フレーム呼ばれても負荷は極小です
    /// </summary>
    /// <param name="skillID">判定するスキルのID</param>
    /// <returns>取得済みであればtrue</returns>
    public bool IsSkillUnlocked(SkillName skillID)
    {
        int id = EnumIDUtility.ToID(skillID);
        return unlockedSkillsCache.Contains(id);
    }

    [Obsolete("IsSkillUnlocked(SkillName) を使用してください。")]
    public bool IsSkillUnlocked(Enum skillID)
    {
        return skillID is SkillName typedSkillID && IsSkillUnlocked(typedSkillID);
    }

    /// <summary>
    /// スキルポイント(クリスタル)を獲得した際に呼び出す
    /// </summary>
    /// <param name="amount">獲得したポイント数</param>
    public void AddSkillPoint(int amount = 1)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("追加するスキルポイントは1以上を指定してください。");
            return;
        }

        if (GameManager.instance != null && GameManager.instance.savedata != null)
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
        if (GameManager.instance == null || GameManager.instance.savedata == null)
            return 0;

        int totalEarned = GameManager.instance.savedata.SkillData.totalEarnedSkillPoints;
        int usedPoints = 0;

        // HashSet を使って、装備中のスキルIDのみを高速ループ
        foreach (int equippedID in equippedSkillsCache)
        {
            // Dictionary を使ってコストを O(1) で取得
            if (skillDataCache.TryGetValue(equippedID, out SkillData data))
            {
                usedPoints += Mathf.Max(0, data.requiredPoints);
            }
        }

        return Mathf.Max(0, totalEarned - usedPoints);
    }

    /// <summary>
    /// スキルを装備する処理
    /// </summary>
    public bool EquipSkill(SkillName skillID)
    {
        return TryEquipSkill(skillID, out _);
    }

    /// <summary>
    /// 装備条件を検証してスキルを装備し、失敗理由を返します。
    /// </summary>
    public bool TryEquipSkill(SkillName skillID, out SkillEquipResult result)
    {
        int id = EnumIDUtility.ToID(skillID);

        if (GameManager.instance == null || GameManager.instance.savedata == null)
        {
            result = SkillEquipResult.SaveDataUnavailable;
            return false;
        }

        // 1. 取得済みかどうかのチェック（HashSetで一瞬で判定）
        if (!unlockedSkillsCache.Contains(id))
        {
            Debug.LogWarning("未解放のスキルは装備できません。");
            result = SkillEquipResult.NotUnlocked;
            return false;
        }

        if (equippedSkillsCache.Contains(id))
        {
            result = SkillEquipResult.AlreadyEquipped;
            return false;
        }

        // 2. データベースからスキル情報を取得
        if (!skillDataCache.TryGetValue(id, out SkillData data))
        {
            Debug.LogError($"スキルID:{id} のデータがデータベースに存在しません。");
            result = SkillEquipResult.InvalidSkill;
            return false;
        }

        if (!ArePrerequisitesMet(data))
        {
            result = SkillEquipResult.PrerequisiteNotMet;
            return false;
        }

        if (HasExclusiveSkillEquipped(data))
        {
            result = SkillEquipResult.ExclusiveSkillEquipped;
            return false;
        }

        // 3. コストが足りているかチェック
        int requiredPoints = Mathf.Max(0, data.requiredPoints);
        if (GetAvailableSkillPoints() >= requiredPoints)
        {
            var skillEntry = GameManager.instance.savedata.SkillData.knownSkills.Find(s =>
                s.skillID == id
            );
            if (skillEntry != null)
            {
                skillEntry.isEquipped = true;
                RebuildSkillCache(); // キャッシュを再構築
                result = SkillEquipResult.Success;
                return true;
            }
        }
        else
        {
            Debug.Log("スキルポイントが足りません。");
            result = SkillEquipResult.NotEnoughPoints;
            return false;
        }

        result = SkillEquipResult.InvalidSkill;
        return false;
    }

    [Obsolete("EquipSkill(SkillName) を使用してください。")]
    public bool EquipSkill(Enum skillID)
    {
        return skillID is SkillName typedSkillID && EquipSkill(typedSkillID);
    }

    /// <summary>
    /// スキルを外す処理
    /// </summary>
    public bool UnequipSkill(SkillName skillID)
    {
        if (GameManager.instance == null || GameManager.instance.savedata == null)
            return false;

        int id = EnumIDUtility.ToID(skillID);
        var skillEntry = GameManager.instance.savedata.SkillData.knownSkills.Find(s =>
            s.skillID == id
        );

        if (skillEntry != null && skillEntry.isEquipped)
        {
            skillEntry.isEquipped = false;
            RebuildSkillCache();
            return true;
        }

        return false;
    }

    [Obsolete("UnequipSkill(SkillName) を使用してください。")]
    public void UnequipSkill(Enum skillID)
    {
        if (skillID is SkillName typedSkillID)
            UnequipSkill(typedSkillID);
    }

    /// <summary>
    /// 解放済みスキルの現在レベルを返します。未解放の場合は0を返します。
    /// </summary>
    public int GetSkillLevel(SkillName skillID)
    {
        if (GameManager.instance == null || GameManager.instance.savedata == null)
            return 0;

        int id = EnumIDUtility.ToID(skillID);
        SkillEntry entry = GameManager.instance.savedata.SkillData.knownSkills.Find(skill =>
            skill != null && skill.skillID == id
        );
        if (entry == null || !entry.isUnlocked)
            return 0;

        SkillData data = GetSkillData(skillID);
        int maxLevel = data != null ? Mathf.Max(1, data.maxLevel) : entry.level;
        return Mathf.Clamp(entry.level, 1, maxLevel);
    }

    /// <summary>
    /// 指定したスキルのマスターデータを返します。
    /// </summary>
    public SkillData GetSkillData(SkillName skillID)
    {
        skillDataCache.TryGetValue(EnumIDUtility.ToID(skillID), out SkillData data);
        return data;
    }

    /// <summary>
    /// 指定したスキルを既読にします。
    /// </summary>
    public bool MarkSkillAsSeen(SkillName skillID)
    {
        if (GameManager.instance == null || GameManager.instance.savedata == null)
            return false;

        int id = EnumIDUtility.ToID(skillID);
        SkillEntry entry = GameManager.instance.savedata.SkillData.knownSkills.Find(skill =>
            skill != null && skill.skillID == id
        );
        if (entry == null || !entry.isNew)
            return false;

        entry.isNew = false;
        OnSkillStateChanged?.Invoke();
        return true;
    }

    private bool ArePrerequisitesMet(SkillData data)
    {
        if (data.prerequisiteSkills == null)
            return true;

        foreach (SkillName prerequisite in data.prerequisiteSkills)
        {
            if (prerequisite != SkillName.None && !IsSkillUnlocked(prerequisite))
                return false;
        }

        return true;
    }

    private bool HasExclusiveSkillEquipped(SkillData data)
    {
        if (data.exclusiveGroupID <= 0)
            return false;

        foreach (int equippedID in equippedSkillsCache)
        {
            if (
                skillDataCache.TryGetValue(equippedID, out SkillData equippedData)
                && equippedData.exclusiveGroupID == data.exclusiveGroupID
            )
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// データベースからスキル情報を検索し、そのスキルカテゴリーを取得する
    /// </summary>
    /// <param name="skillID">判定するスキルのID</param>
    /// <returns>スキルカテゴリー</returns>
    public SkillCategory GetSkillCategory(SkillName skillID)
    {
        if (skillDatabase == null)
            return SkillCategory.None;

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
        if (skillDatabase == null)
            return skillID.ToString();

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

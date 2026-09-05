#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>手動ダウンロードしたCSVを検証し、既存のEnemyDataへ反映します。</summary>
public sealed class EnemyDataImporterWindow : EditorWindow
{
    private const string DefaultCsvPath = "Assets/敵の名称・攻撃・HPデータ - Unity Export.csv";
    private const string EnemyAssetFolder = "Assets/EnemyData";
    private static readonly string[] Headers =
    {
        "EnemyID", "EnemyName", "HP", "RequiredLevel", "EXP", "Coin", "DropType",
        "TargetID", "TargetName", "Chance", "Count", "HasCondition", "ConditionType",
        "ConditionValue", "IsUnique",
    };

    [SerializeField] private TextAsset csvAsset;
    private readonly List<ResultMessage> messages = new List<ResultMessage>();
    private readonly List<Change> changes = new List<Change>();
    private Vector2 scroll;
    private bool isValidated;
    private bool hasErrors;

    [MenuItem("Tools/Adipothrone/Enemy Data Importer")]
    private static void Open() => GetWindow<EnemyDataImporterWindow>("Enemy Data CSV Importer");

    private void OnEnable()
    {
        if (csvAsset == null)
            csvAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(DefaultCsvPath);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Enemy Data CSV Importer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "手動ダウンロードした「Unity Export」CSVを検証し、既存EnemyDataだけを更新します。"
                + " 新規アセットは作成しません。画像・説明・演出スケール・図鑑表示設定は変更しません。",
            MessageType.Info
        );
        EditorGUI.BeginChangeCheck();
        csvAsset = (TextAsset)EditorGUILayout.ObjectField("CSV", csvAsset, typeof(TextAsset), false);
        if (EditorGUI.EndChangeCheck())
            ClearResult();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("検証"))
                Validate();
            using (new EditorGUI.DisabledScope(!isValidated || hasErrors))
            {
                if (GUILayout.Button("反映"))
                    Apply();
            }
        }
        if (!isValidated)
            return;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(hasErrors ? "検証結果: エラーあり（反映不可）" : "検証結果: 反映可能", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (ResultMessage message in messages)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox(message.Text, message.Type);
            if (message.Context != null && GUILayout.Button("選択", GUILayout.Width(48)))
                Selection.activeObject = message.Context;
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField($"変更予定: {changes.Count}項目", EditorStyles.boldLabel);
        foreach (Change change in changes)
            EditorGUILayout.LabelField($"ID {change.ID} / {change.Field}", $"{change.Before} → {change.After}");
        EditorGUILayout.EndScrollView();
    }

    private void ClearResult()
    {
        isValidated = false;
        hasErrors = false;
        messages.Clear();
        changes.Clear();
    }

    private void Validate()
    {
        Plan plan = BuildPlan();
        ShowPlan(plan);
        if (!hasErrors)
            messages.Insert(0, new ResultMessage($"検証成功: {plan.Entries.Count}件のEnemyDataを反映できます。", MessageType.Info));
    }

    private void Apply()
    {
        Plan plan = BuildPlan();
        ShowPlan(plan);
        if (plan.HasErrors)
        {
            Debug.LogError("敵データCSVの再検証でエラーが見つかったため、反映を中止しました。");
            return;
        }

        EnemyData[] targets = plan.Entries.Select(entry => entry.Asset).ToArray();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Import Enemy Data CSV");
        Undo.RecordObjects(targets, "Import Enemy Data CSV");
        foreach (Entry entry in plan.Entries)
        {
            EnemyData asset = entry.Asset;
            EnemyRow row = entry.Row;
            asset.enemyName = row.Name;
            asset.enemyHP = row.HP;
            asset.requiredLevel = row.RequiredLevel;
            asset.rewardExp = row.Exp;
            asset.dropMoney = row.Coin;
            asset.dropItems = row.Items.Select(item => new DropItemData
            {
                baseItemData = item.Item,
                dropChance = item.Chance,
                maxDropCount = item.Count,
                hasCondition = item.HasCondition,
                conditionType = item.ConditionType,
                conditionValue = item.ConditionValue,
                isUnique = item.IsUnique,
            }).ToList();
            asset.dropSkills = row.Skills.Select(skill => new DropSkillData
            {
                skillID = skill.SkillID,
                isSkillCrystal = skill.IsSkillCrystal,
                dropChance = skill.Chance,
            }).ToList();
            EditorUtility.SetDirty(asset);
        }
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undoGroup);
        messages.Insert(0, new ResultMessage($"敵データCSV反映完了: {plan.Entries.Count}件。", MessageType.Info));
        changes.Clear();
    }

    private void ShowPlan(Plan plan)
    {
        isValidated = true;
        hasErrors = plan.HasErrors;
        messages.Clear();
        messages.AddRange(plan.Messages);
        changes.Clear();
        changes.AddRange(plan.Changes);
        foreach (ResultMessage message in plan.Messages)
        {
            if (message.Type == MessageType.Error)
                Debug.LogError(message.Text, message.Context);
            else if (message.Type == MessageType.Warning)
                Debug.LogWarning(message.Text, message.Context);
        }
    }

    private Plan BuildPlan()
    {
        Plan plan = new Plan();
        if (csvAsset == null)
        {
            plan.Error("CSVが設定されていません。");
            return plan;
        }
        List<string[]> rows = ParseCsv(csvAsset.text, out bool isValid);
        if (!isValid || rows.Count == 0)
        {
            plan.Error(isValid ? "CSVが空です。" : "CSVのダブルクォーテーションが閉じられていません。");
            return plan;
        }

        Dictionary<string, int> columns = CreateColumns(rows[0], plan);
        foreach (string header in Headers)
        {
            if (!columns.ContainsKey(header))
                plan.Error($"CSV: 必須ヘッダー '{header}' がありません。");
        }
        foreach (string header in columns.Keys.Where(header => !Headers.Contains(header)))
            plan.Warning($"CSV: 未使用列 '{header}' はUnityへ反映されません。");
        if (plan.HasErrors)
            return plan;

        Dictionary<int, List<EnemyData>> enemies = FindAssets<EnemyData>(EnemyAssetFolder)
            .GroupBy(asset => Convert.ToInt32(asset.enemyID)).ToDictionary(group => group.Key, group => group.ToList());
        foreach (KeyValuePair<int, List<EnemyData>> pair in enemies)
        {
            if (pair.Value.Count > 1)
                plan.Error($"EnemyDataのID {pair.Key} が重複しています: " + string.Join(", ", pair.Value.Select(AssetDatabase.GetAssetPath)));
        }

        Dictionary<int, List<BaseItemData>> items = FindAssets<BaseItemData>("Assets")
            .Select(asset => new { Asset = asset, ID = TryGetItemID(asset) })
            .Where(value => value.ID.HasValue)
            .GroupBy(value => value.ID.Value)
            .ToDictionary(group => group.Key, group => group.Select(value => value.Asset).ToList());

        Dictionary<int, EnemyRow> parsed = new Dictionary<int, EnemyRow>();
        for (int index = 1; index < rows.Count; index++)
        {
            string[] cells = rows[index];
            if (cells.All(string.IsNullOrWhiteSpace))
                continue;
            int line = index + 1;
            int errors = plan.ErrorCount;
            int id = ReadInt(cells, columns, "EnemyID", line, 1, plan);
            string name = RequiredText(cells, columns, "EnemyName", line, plan);
            int hp = ReadInt(cells, columns, "HP", line, 1, plan);
            int level = ReadInt(cells, columns, "RequiredLevel", line, 0, plan);
            int exp = ReadInt(cells, columns, "EXP", line, 0, plan);
            int coin = ReadInt(cells, columns, "Coin", line, 0, plan);
            if (!Enum.IsDefined(typeof(EnemyName), id) || id == 0)
                plan.Error($"CSV {line}行目: EnemyID {id} はEnemyNameに存在しません。");
            if (!enemies.TryGetValue(id, out List<EnemyData> matches))
                plan.Error($"CSV {line}行目: ID {id} の既存EnemyDataがありません。新規作成は行いません。");
            else if (matches.Count != 1)
                plan.Error($"CSV {line}行目: ID {id} のEnemyDataを一意に特定できません。");

            if (!parsed.TryGetValue(id, out EnemyRow enemyRow))
            {
                enemyRow = new EnemyRow { ID = id, Name = name, HP = hp, RequiredLevel = level, Exp = exp, Coin = coin };
                parsed[id] = enemyRow;
            }
            else if (enemyRow.Name != name || enemyRow.HP != hp || enemyRow.RequiredLevel != level || enemyRow.Exp != exp || enemyRow.Coin != coin)
                plan.Error($"CSV {line}行目: ID {id} の基本情報が他の行と一致しません。");

            string dropType = Cell(cells, columns, "DropType").Trim();
            if (dropType == "None") { }
            else if (dropType == "Item")
                ReadItemDrop(cells, columns, line, enemyRow, items, plan);
            else if (dropType == "Skill" || dropType == "SkillCrystal")
                ReadSkillDrop(cells, columns, line, enemyRow, dropType == "SkillCrystal", plan);
            else
                plan.Error($"CSV {line}行目: DropTypeはNone、Item、Skill、SkillCrystalのいずれかです: {dropType}");
            if (plan.ErrorCount != errors)
                continue;
        }

        foreach (EnemyRow row in parsed.Values)
        {
            if (!enemies.TryGetValue(row.ID, out List<EnemyData> matches) || matches.Count != 1)
                continue;
            if (row.Skills.Count(skill => skill.IsSkillCrystal) > 1)
            {
                plan.Error($"ID {row.ID}: SkillCrystalは1体につき1件だけ設定できます。");
                continue;
            }
            if (row.Items.GroupBy(item => TryGetItemID(item.Item)).Any(group => group.Count() > 1))
            {
                plan.Error($"ID {row.ID}: 同じアイテムのドロップが重複しています。");
                continue;
            }
            Entry entry = new Entry { Asset = matches[0], Row = row };
            plan.Entries.Add(entry);
            AddChanges(entry, plan);
        }
        foreach (KeyValuePair<int, List<EnemyData>> pair in enemies.Where(pair => !parsed.ContainsKey(pair.Key)))
            foreach (EnemyData asset in pair.Value)
                plan.Warning($"EnemyData '{asset.name}' (ID {pair.Key}) はCSVにないため変更しません。", asset);
        return plan;
    }

    private static void ReadItemDrop(string[] cells, Dictionary<string, int> columns, int line, EnemyRow row,
        Dictionary<int, List<BaseItemData>> items, Plan plan)
    {
        int targetID = ReadInt(cells, columns, "TargetID", line, 1, plan);
        string targetName = RequiredText(cells, columns, "TargetName", line, plan);
        float chance = ReadFloat(cells, columns, "Chance", line, 0f, 100f, plan);
        int count = ReadInt(cells, columns, "Count", line, 1, plan);
        bool hasCondition = ReadBool(cells, columns, "HasCondition", line, plan);
        bool isUnique = ReadBool(cells, columns, "IsUnique", line, plan);
        DropConditionType conditionType = ReadCondition(cells, columns, line, hasCondition, plan);
        int conditionValue = ReadOptionalInt(cells, columns, "ConditionValue", line, plan);
        if (hasCondition && (conditionType == DropConditionType.KillCountOver || conditionType == DropConditionType.PlayerLevelUnder) && conditionValue < 1)
            plan.Error($"CSV {line}行目: {conditionType}のConditionValueは1以上です。");
        if (conditionType == DropConditionType.NoDamage)
            plan.Error($"CSV {line}行目: NoDamageは実判定が未実装のため使用できません。");
        if (!items.TryGetValue(targetID, out List<BaseItemData> matches) || matches.Count != 1)
        {
            plan.Error($"CSV {line}行目: TargetID {targetID} のアイテムを一意に特定できません。");
            return;
        }
        if (!string.Equals(matches[0].itemName, targetName, StringComparison.Ordinal))
            plan.Error($"CSV {line}行目: TargetID {targetID} の表示名は'{matches[0].itemName}'です: {targetName}");
        row.Items.Add(new ItemDrop { Item = matches[0], Chance = chance, Count = count, HasCondition = hasCondition,
            ConditionType = conditionType, ConditionValue = conditionValue, IsUnique = isUnique });
    }

    private static void ReadSkillDrop(string[] cells, Dictionary<string, int> columns, int line, EnemyRow row, bool isCrystal, Plan plan)
    {
        int targetID = ReadOptionalInt(cells, columns, "TargetID", line, plan);
        float chance = ReadFloat(cells, columns, "Chance", line, 0f, 100f, plan);
        SkillName skillID = SkillName.None;
        if (!isCrystal)
        {
            if (!Enum.IsDefined(typeof(SkillName), targetID) || targetID == 0)
                plan.Error($"CSV {line}行目: TargetID {targetID} は有効なSkillNameではありません。");
            else
                skillID = (SkillName)targetID;
            string targetName = RequiredText(cells, columns, "TargetName", line, plan);
            if (skillID != SkillName.None && !string.Equals(skillID.ToString(), targetName, StringComparison.Ordinal))
                plan.Error($"CSV {line}行目: SkillのTargetNameはenum名'{skillID}'にしてください: {targetName}");
        }
        else if (targetID != 0)
            plan.Error($"CSV {line}行目: SkillCrystalのTargetIDは0です。");
        row.Skills.Add(new SkillDrop { SkillID = skillID, IsSkillCrystal = isCrystal, Chance = chance });
    }

    private static DropConditionType ReadCondition(string[] cells, Dictionary<string, int> columns, int line, bool hasCondition, Plan plan)
    {
        string raw = Cell(cells, columns, "ConditionType").Trim();
        if (string.IsNullOrEmpty(raw)) raw = "None";
        if (!Enum.TryParse(raw, false, out DropConditionType value) || !Enum.IsDefined(typeof(DropConditionType), value))
        {
            plan.Error($"CSV {line}行目: ConditionTypeが不正です: {raw}");
            return DropConditionType.None;
        }
        if (hasCondition == (value == DropConditionType.None))
            plan.Error($"CSV {line}行目: HasConditionとConditionTypeの組み合わせが不正です。");
        return value;
    }

    private static IEnumerable<T> FindAssets<T>(string folder) where T : UnityEngine.Object =>
        AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>).Where(asset => asset != null);

    private static int? TryGetItemID(BaseItemData asset)
    {
        try { Enum id = asset.GetItemID(); return id == null ? null : (int?)Convert.ToInt32(id); }
        catch { return null; }
    }

    private static Dictionary<string, int> CreateColumns(string[] headers, Plan plan)
    {
        Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < headers.Length; i++)
        {
            string header = headers[i].Trim().TrimStart('﻿');
            if (string.IsNullOrEmpty(header)) continue;
            if (result.ContainsKey(header)) plan.Error($"CSV: ヘッダー'{header}'が重複しています。");
            else result.Add(header, i);
        }
        return result;
    }

    private static string Cell(string[] cells, Dictionary<string, int> columns, string header) =>
        columns[header] < cells.Length ? cells[columns[header]] : string.Empty;
    private static string RequiredText(string[] cells, Dictionary<string, int> columns, string header, int line, Plan plan)
    {
        string value = Cell(cells, columns, header).Trim();
        if (string.IsNullOrEmpty(value)) plan.Error($"CSV {line}行目: '{header}'は必須です。");
        return value;
    }
    private static int ReadInt(string[] cells, Dictionary<string, int> columns, string header, int line, int min, Plan plan)
    {
        string raw = Cell(cells, columns, header).Trim();
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < min)
            plan.Error($"CSV {line}行目: '{header}'は{min}以上の整数です: {raw}");
        return value;
    }
    private static int ReadOptionalInt(string[] cells, Dictionary<string, int> columns, string header, int line, Plan plan)
    {
        string raw = Cell(cells, columns, header).Trim();
        if (string.IsNullOrEmpty(raw)) return 0;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < 0)
            plan.Error($"CSV {line}行目: '{header}'は0以上の整数です: {raw}");
        return value;
    }
    private static float ReadFloat(string[] cells, Dictionary<string, int> columns, string header, int line, float min, float max, Plan plan)
    {
        string raw = Cell(cells, columns, header).Trim();
        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || value < min || value > max)
            plan.Error($"CSV {line}行目: '{header}'は{min}～{max}の数値です: {raw}");
        return value;
    }
    private static bool ReadBool(string[] cells, Dictionary<string, int> columns, string header, int line, Plan plan)
    {
        string raw = Cell(cells, columns, header).Trim();
        if (bool.TryParse(raw, out bool value)) return value;
        plan.Error($"CSV {line}行目: '{header}'はTRUEまたはFALSEです: {raw}");
        return false;
    }

    private static void AddChanges(Entry entry, Plan plan)
    {
        EnemyData asset = entry.Asset; EnemyRow row = entry.Row;
        AddChange(plan, row.ID, "敵名", asset.enemyName, row.Name);
        AddChange(plan, row.ID, "HP", asset.enemyHP, row.HP);
        AddChange(plan, row.ID, "推奨レベル", asset.requiredLevel, row.RequiredLevel);
        AddChange(plan, row.ID, "EXP", asset.rewardExp, row.Exp);
        AddChange(plan, row.ID, "コイン", asset.dropMoney, row.Coin);
        AddChange(plan, row.ID, "アイテムドロップ数", asset.dropItems?.Count ?? 0, row.Items.Count);
        AddChange(plan, row.ID, "スキルドロップ数", asset.dropSkills?.Count ?? 0, row.Skills.Count);
    }
    private static void AddChange<T>(Plan plan, int id, string field, T before, T after)
    {
        if (!EqualityComparer<T>.Default.Equals(before, after))
            plan.Changes.Add(new Change
            {
                ID = id,
                Field = field,
                Before = object.Equals(before, null) ? "(なし)" : before.ToString(),
                After = object.Equals(after, null) ? "(なし)" : after.ToString(),
            });
    }

    private static List<string[]> ParseCsv(string text, out bool isValid)
    {
        List<string[]> rows = new List<string[]>(); List<string> row = new List<string>();
        StringBuilder value = new StringBuilder(); bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { value.Append('"'); i++; }
                else if (ch == '"') quoted = false; else value.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == ',') { row.Add(value.ToString()); value.Clear(); }
            else if (ch == '\r' || ch == '\n')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(value.ToString()); rows.Add(row.ToArray()); row = new List<string>(); value.Clear();
            }
            else value.Append(ch);
        }
        if (value.Length > 0 || row.Count > 0) { row.Add(value.ToString()); rows.Add(row.ToArray()); }
        isValid = !quoted; return rows;
    }

    private sealed class Plan
    {
        public readonly List<Entry> Entries = new List<Entry>();
        public readonly List<ResultMessage> Messages = new List<ResultMessage>();
        public readonly List<Change> Changes = new List<Change>();
        public int ErrorCount => Messages.Count(message => message.Type == MessageType.Error);
        public bool HasErrors => ErrorCount > 0;
        public void Error(string text, UnityEngine.Object context = null) => Messages.Add(new ResultMessage(text, MessageType.Error, context));
        public void Warning(string text, UnityEngine.Object context = null) => Messages.Add(new ResultMessage(text, MessageType.Warning, context));
    }
    private sealed class ResultMessage
    {
        public readonly string Text; public readonly MessageType Type; public readonly UnityEngine.Object Context;
        public ResultMessage(string text, MessageType type, UnityEngine.Object context = null) { Text = text; Type = type; Context = context; }
    }
    private sealed class Change { public int ID; public string Field; public string Before; public string After; }
    private sealed class Entry { public EnemyData Asset; public EnemyRow Row; }
    private sealed class EnemyRow
    {
        public int ID; public string Name; public int HP; public int RequiredLevel; public int Exp; public int Coin;
        public readonly List<ItemDrop> Items = new List<ItemDrop>();
        public readonly List<SkillDrop> Skills = new List<SkillDrop>();
    }
    private sealed class ItemDrop
    {
        public BaseItemData Item; public float Chance; public int Count; public bool HasCondition;
        public DropConditionType ConditionType; public int ConditionValue; public bool IsUnique;
    }
    private sealed class SkillDrop { public SkillName SkillID; public bool IsSkillCrystal; public float Chance; }
}
#endif

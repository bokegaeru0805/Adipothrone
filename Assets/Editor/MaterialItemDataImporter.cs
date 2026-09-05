#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 手動配置した素材アイテムCSVを検証し、既存のMaterialItemDataだけへ反映するEditorWindow。
/// </summary>
public sealed class MaterialItemDataImporterWindow : EditorWindow
{
    private const string DefaultCsvPath = "Assets/アイテムデータ - 素材アイテム.csv";
    private const string AssetFolderPath = "Assets/ItemData/MaterialItemData";
    private static readonly string[] ExpectedHeaders =
    {
        "ID", "表示名", "レア度", "購入価格", "売却価格", "説明文",
    };

    [SerializeField] private TextAsset csvAsset;
    private readonly List<Message> messages = new List<Message>();
    private readonly List<Change> changes = new List<Change>();
    private Vector2 scroll;
    private bool isValidated;
    private bool hasErrors;

    [MenuItem("Tools/Adipothrone/Material Item Data Importer")]
    private static void Open() => GetWindow<MaterialItemDataImporterWindow>("Material Item CSV Importer");

    private void OnEnable()
    {
        if (csvAsset == null)
            csvAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(DefaultCsvPath);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Material Item Data CSV Importer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "手動ダウンロードしたCSVを検証し、既存のMaterialItemDataだけを更新します。"
                + " CSVを配置しただけでは反映されず、新規アセットも作成しません。"
                + " 売却可能フラグは常に既存値を維持します。",
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
        EditorGUILayout.LabelField(
            hasErrors ? "検証結果: エラーあり（反映不可）" : "検証結果: 反映可能",
            EditorStyles.boldLabel
        );
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (Message message in messages)
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
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"ID {change.ID} / {change.Asset.name}");
            EditorGUILayout.LabelField(change.Field, $"{change.Before}  →  {change.After}");
            if (GUILayout.Button("アセットを選択", GUILayout.Width(110)))
                Selection.activeObject = change.Asset;
            EditorGUILayout.EndVertical();
        }
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
            messages.Insert(0, new Message($"検証成功: {plan.Entries.Count}件を反映できます。", MessageType.Info));
    }

    private void Apply()
    {
        Plan plan = BuildPlan();
        ShowPlan(plan);
        if (hasErrors)
        {
            Debug.LogError("素材アイテムCSVの再検証でエラーが見つかったため、反映を中止しました。");
            return;
        }

        MaterialItemData[] targets = plan.Entries.Select(entry => entry.Asset).ToArray();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Import Material Item Data CSV");
        Undo.RecordObjects(targets, "Import Material Item Data CSV");
        foreach (Entry entry in plan.Entries)
        {
            MaterialItemData asset = entry.Asset;
            Row row = entry.Row;
            asset.itemName = row.Name;
            asset.itemRank = row.Rank;
            asset.buyPrice = row.Buy;
            asset.sellPrice = row.Sell;
            asset.description = row.Description;
            EditorUtility.SetDirty(asset);
        }
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undoGroup);
        messages.Insert(0, new Message($"素材アイテムCSV反映完了: {plan.Entries.Count}件、変更 {plan.Changes.Count}項目。", MessageType.Info));
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
        foreach (Message message in plan.Messages)
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

        List<string[]> csvRows = ParseCsv(csvAsset.text, out bool isCsvValid);
        if (!isCsvValid)
        {
            plan.Error("CSV: ダブルクォーテーションが閉じられていません。");
            return plan;
        }
        if (csvRows.Count == 0)
        {
            plan.Error("CSVが空です。");
            return plan;
        }

        Dictionary<string, int> columns = CreateColumns(csvRows[0], plan);
        foreach (string header in ExpectedHeaders)
        {
            if (!columns.ContainsKey(header))
                plan.Error($"CSV: 必須ヘッダー '{header}' がありません。");
        }
        foreach (string header in columns.Keys.Where(header => !ExpectedHeaders.Contains(header)))
            plan.Warning($"CSV: 未使用列 '{header}' はUnityへ反映されません。");
        if (plan.HasErrors)
            return plan;

        Dictionary<int, List<MaterialItemData>> assetsById = AssetDatabase
            .FindAssets("t:MaterialItemData", new[] { AssetFolderPath })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<MaterialItemData>)
            .Where(asset => asset != null)
            .GroupBy(asset => Convert.ToInt32(asset.itemID))
            .ToDictionary(group => group.Key, group => group.ToList());
        ValidateAssets(assetsById, plan);

        HashSet<int> csvIds = new HashSet<int>();
        for (int index = 1; index < csvRows.Count; index++)
        {
            string[] cells = csvRows[index];
            if (cells.All(string.IsNullOrWhiteSpace))
                continue;

            int line = index + 1;
            if (!TryReadID(cells, columns, line, csvIds, plan, out int id))
                continue;
            if (!Enum.IsDefined(typeof(MaterialItemName), id) || id == (int)MaterialItemName.None)
            {
                plan.Error($"CSV {line}行目: ID {id} はMaterialItemNameに存在しません。");
                continue;
            }
            if (!assetsById.TryGetValue(id, out List<MaterialItemData> matchingAssets))
            {
                plan.Error($"CSV {line}行目: ID {id} の既存MaterialItemDataがありません。新規作成は行いません。");
                continue;
            }
            if (matchingAssets.Count != 1)
                continue;

            int errorCount = plan.ErrorCount;
            Row row = new Row
            {
                Name = RequiredText(cells, columns, "表示名", line, plan),
                Rank = ReadEnum<ItemRank>(cells, columns, "レア度", line, plan),
                Buy = ReadNonNegativeInt(cells, columns, "購入価格", line, plan),
                Sell = ReadNonNegativeInt(cells, columns, "売却価格", line, plan),
                Description = Cell(cells, columns, "説明文").Trim(),
            };
            if (plan.ErrorCount != errorCount)
                continue;

            Entry entry = new Entry { Asset = matchingAssets[0], Row = row };
            plan.Entries.Add(entry);
            AddChanges(entry, plan);
        }

        foreach (KeyValuePair<int, List<MaterialItemData>> pair in assetsById)
        {
            if (csvIds.Contains(pair.Key))
                continue;
            foreach (MaterialItemData asset in pair.Value)
                plan.Warning($"MaterialItemData '{asset.name}' (ID {pair.Key}) はCSVにないため変更しません。", asset);
        }
        return plan;
    }

    private static Dictionary<string, int> CreateColumns(string[] headers, Plan plan)
    {
        Dictionary<string, int> columns = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < headers.Length; index++)
        {
            string header = headers[index].Trim().TrimStart('﻿');
            if (string.IsNullOrEmpty(header))
                continue;
            if (columns.ContainsKey(header))
                plan.Error($"CSV: ヘッダー '{header}' が重複しています。");
            else
                columns.Add(header, index);
        }
        return columns;
    }

    private static bool TryReadID(string[] cells, Dictionary<string, int> columns, int line, HashSet<int> csvIds, Plan plan, out int id)
    {
        string raw = Cell(cells, columns, "ID").Trim();
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
        {
            plan.Error($"CSV {line}行目: ID '{raw}' は整数ではありません。");
            return false;
        }
        if (!csvIds.Add(id))
        {
            plan.Error($"CSV {line}行目: ID {id} がCSV内で重複しています。");
            return false;
        }
        return true;
    }

    private static void ValidateAssets(Dictionary<int, List<MaterialItemData>> assetsById, Plan plan)
    {
        foreach (KeyValuePair<int, List<MaterialItemData>> pair in assetsById)
        {
            if (pair.Value.Count > 1)
                plan.Error($"MaterialItemDataのID {pair.Key} が重複しています: " + string.Join(", ", pair.Value.Select(AssetDatabase.GetAssetPath)));
            if (!Enum.IsDefined(typeof(MaterialItemName), pair.Key) || pair.Key == 0)
            {
                foreach (MaterialItemData asset in pair.Value)
                    plan.Error($"MaterialItemData '{asset.name}' のID {pair.Key} はMaterialItemNameに存在しません。", asset);
            }
        }
    }

    private static string RequiredText(string[] cells, Dictionary<string, int> columns, string header, int line, Plan plan)
    {
        string value = Cell(cells, columns, header).Trim();
        if (string.IsNullOrEmpty(value))
            plan.Error($"CSV {line}行目: '{header}' は必須です。");
        return value;
    }

    private static int ReadNonNegativeInt(string[] cells, Dictionary<string, int> columns, string header, int line, Plan plan)
    {
        string raw = Cell(cells, columns, header).Trim();
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < 0)
            plan.Error($"CSV {line}行目: '{header}' は0以上の整数で入力してください: {raw}");
        return value;
    }

    private static T ReadEnum<T>(string[] cells, Dictionary<string, int> columns, string header, int line, Plan plan) where T : struct, Enum
    {
        string raw = Cell(cells, columns, header).Trim();
        if (!Enum.TryParse(raw, false, out T value) || !Enum.IsDefined(typeof(T), value)
            || !string.Equals(Enum.GetName(typeof(T), value), raw, StringComparison.Ordinal) || Convert.ToInt32(value) == 0)
            plan.Error($"CSV {line}行目: '{header}' の値が不正です: {raw}");
        return value;
    }

    private static string Cell(string[] cells, Dictionary<string, int> columns, string header)
    {
        int index = columns[header];
        return index < cells.Length ? cells[index] : string.Empty;
    }

    private static void AddChanges(Entry entry, Plan plan)
    {
        MaterialItemData asset = entry.Asset;
        Row row = entry.Row;
        AddChange(plan, asset, "表示名", asset.itemName, row.Name);
        AddChange(plan, asset, "レア度", asset.itemRank, row.Rank);
        AddChange(plan, asset, "購入価格", asset.buyPrice, row.Buy);
        AddChange(plan, asset, "売却価格", asset.sellPrice, row.Sell);
        AddChange(plan, asset, "説明文", asset.description, row.Description);
    }

    private static void AddChange<T>(Plan plan, MaterialItemData asset, string field, T before, T after)
    {
        if (EqualityComparer<T>.Default.Equals(before, after))
            return;
        plan.Changes.Add(new Change
        {
            ID = Convert.ToInt32(asset.itemID), Asset = asset, Field = field,
            Before = object.Equals(before, null) ? "(なし)" : before.ToString(),
            After = object.Equals(after, null) ? "(なし)" : after.ToString(),
        });
    }

    private static List<string[]> ParseCsv(string text, out bool isValid)
    {
        List<string[]> rows = new List<string[]>();
        List<string> row = new List<string>();
        StringBuilder value = new StringBuilder();
        bool inQuotes = false;
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (inQuotes)
            {
                if (character == '"' && index + 1 < text.Length && text[index + 1] == '"') { value.Append('"'); index++; }
                else if (character == '"') inQuotes = false;
                else value.Append(character);
            }
            else if (character == '"') inQuotes = true;
            else if (character == ',') { row.Add(value.ToString()); value.Length = 0; }
            else if (character == '\r' || character == '\n')
            {
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                row.Add(value.ToString()); rows.Add(row.ToArray()); row = new List<string>(); value.Length = 0;
            }
            else value.Append(character);
        }
        if (value.Length > 0 || row.Count > 0) { row.Add(value.ToString()); rows.Add(row.ToArray()); }
        isValid = !inQuotes;
        return rows;
    }

    private sealed class Plan
    {
        public readonly List<Entry> Entries = new List<Entry>();
        public readonly List<Message> Messages = new List<Message>();
        public readonly List<Change> Changes = new List<Change>();
        public int ErrorCount => Messages.Count(message => message.Type == MessageType.Error);
        public bool HasErrors => ErrorCount > 0;
        public void Error(string text, UnityEngine.Object context = null) => Messages.Add(new Message(text, MessageType.Error, context));
        public void Warning(string text, UnityEngine.Object context = null) => Messages.Add(new Message(text, MessageType.Warning, context));
    }

    private sealed class Message
    {
        public readonly string Text;
        public readonly MessageType Type;
        public readonly UnityEngine.Object Context;
        public Message(string text, MessageType type, UnityEngine.Object context = null) { Text = text; Type = type; Context = context; }
    }

    private sealed class Change
    {
        public int ID;
        public MaterialItemData Asset;
        public string Field;
        public string Before;
        public string After;
    }

    private sealed class Entry { public MaterialItemData Asset; public Row Row; }
    private sealed class Row
    {
        public string Name;
        public ItemRank Rank;
        public int Buy;
        public int Sell;
        public string Description;
    }
}
#endif

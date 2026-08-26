#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CSVを正として、作成済みのSkillDataだけを検証・更新するEditorWindow。
/// 新しいSkillDataやSkillName enumは作成しません。
/// </summary>
public class SkillDataCsvImporterWindow : EditorWindow
{
    private const string SkillRootPath = "Assets/SkillData";
    private const string DefaultCsvPath = "Assets/スキルデータ - UnityExport.csv";
    private const string DatabasePath = "Assets/Database/SkillDatabase.asset";
    private const int ExpectedColumnCount = 9;

    [SerializeField]
    private TextAsset csvAsset;

    private readonly List<ImportMessage> messages = new List<ImportMessage>();
    private Vector2 scrollPosition;
    private bool isValidated;
    private bool hasErrors;

    [MenuItem("Tools/Adipothrone/Skill Data CSV Importer")]
    private static void OpenWindow()
    {
        GetWindow<SkillDataCsvImporterWindow>("Skill CSV Importer");
    }

    private void OnEnable()
    {
        if (csvAsset == null)
            csvAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(DefaultCsvPath);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("SkillData CSV Importer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "CSVを正として既存SkillDataのみを更新します。新規SkillDataとenumは作成しません。"
                + " 検証成功後に、カテゴリ別移動・リネーム・Database再構築をまとめて実行します。",
            MessageType.Info
        );

        EditorGUI.BeginChangeCheck();
        csvAsset = (TextAsset)
            EditorGUILayout.ObjectField("CSV", csvAsset, typeof(TextAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            isValidated = false;
            messages.Clear();
        }

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

        if (messages.Count == 0)
            return;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(
            hasErrors ? "検証結果: エラーあり（反映不可）" : "検証結果: 反映可能",
            EditorStyles.boldLabel
        );

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        foreach (ImportMessage message in messages)
        {
            EditorGUILayout.HelpBox(message.Text, message.Type);
            if (message.Context != null && GUILayout.Button($"選択: {message.Context.name}"))
                Selection.activeObject = message.Context;
        }
        EditorGUILayout.EndScrollView();
    }

    private void Validate()
    {
        messages.Clear();
        isValidated = true;

        ImportPlan plan = BuildPlan();
        hasErrors = plan.HasErrors;
        messages.AddRange(plan.Messages);
        LogMessages(plan.Messages);

        if (!hasErrors)
        {
            messages.Add(
                new ImportMessage(
                    $"既存SkillData {plan.AssetEntries.Count}件を更新できます。"
                        + $" CSV内の未作成SkillData {plan.IgnoredCsvCount}件は対象外です。",
                    MessageType.Info
                )
            );
        }
    }

    private void Apply()
    {
        ImportPlan plan = BuildPlan();
        messages.Clear();
        messages.AddRange(plan.Messages);
        hasErrors = plan.HasErrors;
        isValidated = true;

        if (hasErrors)
        {
            Debug.LogError("SkillData CSVの再検証でエラーが見つかったため、反映を中止しました。");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Import SkillData CSV");

        int updatedCount = 0;
        int movedCount = 0;

        try
        {
            foreach (AssetEntry entry in plan.AssetEntries)
            {
                string currentPath = AssetDatabase.GetAssetPath(entry.Asset);
                EnsureFolder(entry.TargetFolder);
                if (!string.Equals(currentPath, entry.TargetPath, StringComparison.Ordinal))
                {
                    string moveError = AssetDatabase.MoveAsset(currentPath, entry.TargetPath);
                    if (!string.IsNullOrEmpty(moveError))
                        throw new InvalidOperationException(
                            $"SkillDataの移動に失敗しました: {currentPath} -> {entry.TargetPath}\n{moveError}"
                        );
                    movedCount++;
                }

                Undo.RecordObject(entry.Asset, "Update SkillData from CSV");
                ApplyRow(entry.Asset, entry.Row);
                EditorUtility.SetDirty(entry.Asset);
                updatedCount++;
            }

            RebuildDatabase();
        }
        catch (Exception exception)
        {
            hasErrors = true;
            messages.Add(new ImportMessage(exception.Message, MessageType.Error));
            Debug.LogException(exception);
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Undo.CollapseUndoOperations(undoGroup);
        }

        if (!hasErrors)
        {
            string result =
                $"SkillData CSV反映完了: 更新 {updatedCount}件、移動/リネーム {movedCount}件。"
                + $" 未作成 {plan.IgnoredCsvCount}件は変更していません。";
            messages.Add(new ImportMessage(result, MessageType.Info));
            Debug.Log(result);
        }
    }

    private ImportPlan BuildPlan()
    {
        ImportPlan plan = new ImportPlan();
        if (csvAsset == null)
        {
            plan.AddError("CSVが設定されていません。");
            return plan;
        }

        if (AssetDatabase.LoadAssetAtPath<SkillDatabase>(DatabasePath) == null)
        {
            plan.AddError($"SkillDatabaseが見つかりません: {DatabasePath}");
            return plan;
        }

        List<CsvRow> rows = ParseAndValidateCsv(csvAsset.text, plan);
        Dictionary<int, CsvRow> rowsById = rows.GroupBy(row => row.ID)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.First());

        List<SkillData> assets = LoadSkillAssets();
        Dictionary<int, List<SkillData>> assetsById = assets
            .GroupBy(GetID)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (SkillData asset in assets)
        {
            int id = GetID(asset);
            string assetPath = AssetDatabase.GetAssetPath(asset);

            if (asset.skillID == SkillName.None)
            {
                plan.AddError($"SkillIDが未設定(None)です: {assetPath}", asset);
                continue;
            }

            if (assetsById[id].Count > 1)
            {
                string duplicatePaths = string.Join(
                    ", ",
                    assetsById[id].Select(AssetDatabase.GetAssetPath)
                );
                plan.AddError($"SkillID {id} が重複しています: {duplicatePaths}", asset);
                continue;
            }

            if (!rowsById.TryGetValue(id, out CsvRow row))
            {
                plan.AddWarning($"CSVにSkillID {id} がないため変更しません: {assetPath}", asset);
                continue;
            }

            if (!Enum.TryParse(row.EnumName, out SkillName enumValue) || GetID(enumValue) != row.ID)
            {
                plan.AddError(
                    $"CSV {row.LineNumber}行目のEnumName/IDが既存SkillNameと一致しません: "
                        + $"{row.EnumName} / {row.ID}",
                    asset
                );
                continue;
            }

            string targetFolder = $"{SkillRootPath}/{row.Category}";
            string targetPath = $"{targetFolder}/{row.EnumName}.asset";
            UnityEngine.Object pathAsset = AssetDatabase.LoadMainAssetAtPath(targetPath);
            if (pathAsset != null && pathAsset != asset)
            {
                plan.AddError($"移動先に別のAssetが存在します: {targetPath}", asset);
                continue;
            }

            plan.AssetEntries.Add(new AssetEntry(asset, row, targetFolder, targetPath));
        }

        HashSet<int> existingIds = new HashSet<int>(assets.Select(GetID));
        plan.IgnoredCsvCount = rows.Count(row => !existingIds.Contains(row.ID));
        return plan;
    }

    private static List<CsvRow> ParseAndValidateCsv(string text, ImportPlan plan)
    {
        List<string[]> parsedRows = ParseCsv(text);
        List<CsvRow> rows = new List<CsvRow>();
        if (parsedRows.Count == 0)
        {
            plan.AddError("CSVが空です。");
            return rows;
        }

        string[] expectedHeaders =
        {
            "ID",
            "EnumName",
            "Name",
            "Category",
            "Description",
            "RequiredPoints",
            "MaxLevel",
            "PrerequisiteSkills",
            "ExclusiveGroupID",
        };
        if (
            parsedRows[0].Length < ExpectedColumnCount
            || !expectedHeaders.SequenceEqual(
                parsedRows[0].Take(ExpectedColumnCount).Select(value => value.Trim())
            )
        )
        {
            plan.AddError("CSVヘッダーが想定形式と一致しません。");
            return rows;
        }

        for (int index = 1; index < parsedRows.Count; index++)
        {
            string[] columns = parsedRows[index];
            int lineNumber = index + 1;
            if (columns.All(string.IsNullOrWhiteSpace))
                continue;
            if (columns.Length < ExpectedColumnCount)
            {
                plan.AddError($"CSV {lineNumber}行目の列数が不足しています。");
                continue;
            }
            if (!int.TryParse(columns[0].Trim(), out int id))
            {
                plan.AddError($"CSV {lineNumber}行目のIDが数値ではありません: {columns[0]}");
                continue;
            }
            if (
                !Enum.TryParse(columns[3].Trim(), out SkillCategory category)
                || category == SkillCategory.None
            )
            {
                plan.AddError($"CSV {lineNumber}行目のCategoryが不正です: {columns[3]}");
                continue;
            }
            if (!int.TryParse(columns[5].Trim(), out int requiredPoints) || requiredPoints < 0)
            {
                plan.AddError($"CSV {lineNumber}行目のRequiredPointsが不正です: {columns[5]}");
                continue;
            }
            if (!int.TryParse(columns[6].Trim(), out int maxLevel) || maxLevel < 1)
            {
                plan.AddError($"CSV {lineNumber}行目のMaxLevelが不正です: {columns[6]}");
                continue;
            }
            if (!int.TryParse(columns[8].Trim(), out int exclusiveGroupID) || exclusiveGroupID < 0)
            {
                plan.AddError($"CSV {lineNumber}行目のExclusiveGroupIDが不正です: {columns[8]}");
                continue;
            }

            List<SkillName> prerequisites = new List<SkillName>();
            string prerequisiteText = columns[7].Trim();
            if (!string.IsNullOrEmpty(prerequisiteText))
            {
                foreach (
                    string name in prerequisiteText.Split(
                        new[] { '|', ';' },
                        StringSplitOptions.RemoveEmptyEntries
                    )
                )
                {
                    if (
                        !Enum.TryParse(name.Trim(), out SkillName prerequisite)
                        || prerequisite == SkillName.None
                    )
                        plan.AddError(
                            $"CSV {lineNumber}行目のPrerequisiteSkillsが不正です: {name}"
                        );
                    else
                        prerequisites.Add(prerequisite);
                }
            }

            rows.Add(
                new CsvRow(
                    lineNumber,
                    id,
                    columns[1].Trim(),
                    columns[2],
                    category,
                    columns[4],
                    requiredPoints,
                    maxLevel,
                    prerequisites,
                    exclusiveGroupID
                )
            );
        }

        foreach (
            IGrouping<int, CsvRow> duplicate in rows.GroupBy(row => row.ID)
                .Where(group => group.Count() > 1)
        )
            plan.AddError(
                $"CSV内でID {duplicate.Key} が重複しています: {string.Join(", ", duplicate.Select(row => row.LineNumber + "行目"))}"
            );
        foreach (
            IGrouping<string, CsvRow> duplicate in rows.GroupBy(row => row.EnumName)
                .Where(group => group.Count() > 1)
        )
            plan.AddError(
                $"CSV内でEnumName {duplicate.Key} が重複しています: {string.Join(", ", duplicate.Select(row => row.LineNumber + "行目"))}"
            );

        return rows;
    }

    private static void ApplyRow(SkillData asset, CsvRow row)
    {
        asset.skillID = (SkillName)row.ID;
        asset.skillName = row.Name;
        asset.category = row.Category;
        asset.description = row.Description;
        asset.requiredPoints = row.RequiredPoints;
        asset.maxLevel = row.MaxLevel;
        asset.prerequisiteSkills = new List<SkillName>(row.Prerequisites);
        asset.exclusiveGroupID = row.ExclusiveGroupID;
    }

    private static void RebuildDatabase()
    {
        SkillDatabase database = AssetDatabase.LoadAssetAtPath<SkillDatabase>(DatabasePath);
        if (database == null)
            throw new InvalidOperationException($"SkillDatabaseが見つかりません: {DatabasePath}");

        List<SkillData> skills = LoadSkillAssets()
            .Where(skill => skill.skillID != SkillName.None)
            .OrderBy(skill => (int)skill.category)
            .ThenBy(GetID)
            .ToList();

        Undo.RecordObject(database, "Rebuild SkillDatabase");
        database.skills = skills;
        EditorUtility.SetDirty(database);
    }

    private static List<SkillData> LoadSkillAssets()
    {
        return AssetDatabase
            .FindAssets("t:SkillData", new[] { SkillRootPath })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<SkillData>)
            .Where(asset => asset != null)
            .ToList();
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static int GetID(SkillData data) => GetID(data.skillID);

    private static int GetID(SkillName id) => Convert.ToInt32(id);

    private static void LogMessages(IEnumerable<ImportMessage> importMessages)
    {
        foreach (ImportMessage message in importMessages)
        {
            if (message.Type == MessageType.Error)
                Debug.LogError(message.Text, message.Context);
            else if (message.Type == MessageType.Warning)
                Debug.LogWarning(message.Text, message.Context);
        }
    }

    private static List<string[]> ParseCsv(string text)
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
                if (character == '"' && index + 1 < text.Length && text[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else if (character == '"')
                    inQuotes = false;
                else
                    value.Append(character);
            }
            else if (character == '"')
                inQuotes = true;
            else if (character == ',')
            {
                row.Add(value.ToString());
                value.Length = 0;
            }
            else if (character == '\r' || character == '\n')
            {
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                    index++;
                row.Add(value.ToString());
                rows.Add(row.ToArray());
                row = new List<string>();
                value.Length = 0;
            }
            else
                value.Append(character);
        }

        if (value.Length > 0 || row.Count > 0)
        {
            row.Add(value.ToString());
            rows.Add(row.ToArray());
        }
        return rows;
    }

    private sealed class ImportPlan
    {
        public readonly List<AssetEntry> AssetEntries = new List<AssetEntry>();
        public readonly List<ImportMessage> Messages = new List<ImportMessage>();
        public int IgnoredCsvCount;
        public bool HasErrors => Messages.Any(message => message.Type == MessageType.Error);

        public void AddError(string text, UnityEngine.Object context = null) =>
            Messages.Add(new ImportMessage(text, MessageType.Error, context));

        public void AddWarning(string text, UnityEngine.Object context = null) =>
            Messages.Add(new ImportMessage(text, MessageType.Warning, context));
    }

    private sealed class AssetEntry
    {
        public readonly SkillData Asset;
        public readonly CsvRow Row;
        public readonly string TargetFolder;
        public readonly string TargetPath;

        public AssetEntry(SkillData asset, CsvRow row, string targetFolder, string targetPath)
        {
            Asset = asset;
            Row = row;
            TargetFolder = targetFolder;
            TargetPath = targetPath;
        }
    }

    private sealed class CsvRow
    {
        public readonly int LineNumber;
        public readonly int ID;
        public readonly string EnumName;
        public readonly string Name;
        public readonly SkillCategory Category;
        public readonly string Description;
        public readonly int RequiredPoints;
        public readonly int MaxLevel;
        public readonly List<SkillName> Prerequisites;
        public readonly int ExclusiveGroupID;

        public CsvRow(
            int lineNumber,
            int id,
            string enumName,
            string name,
            SkillCategory category,
            string description,
            int requiredPoints,
            int maxLevel,
            List<SkillName> prerequisites,
            int exclusiveGroupID
        )
        {
            LineNumber = lineNumber;
            ID = id;
            EnumName = enumName;
            Name = name;
            Category = category;
            Description = description;
            RequiredPoints = requiredPoints;
            MaxLevel = maxLevel;
            Prerequisites = prerequisites;
            ExclusiveGroupID = exclusiveGroupID;
        }
    }

    private sealed class ImportMessage
    {
        public readonly string Text;
        public readonly MessageType Type;
        public readonly UnityEngine.Object Context;

        public ImportMessage(string text, MessageType type, UnityEngine.Object context = null)
        {
            Text = text;
            Type = type;
            Context = context;
        }
    }
}
#endif

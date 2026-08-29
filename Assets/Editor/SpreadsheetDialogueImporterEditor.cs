using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Fungus;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpreadsheetDialogueImporter))]
public class SpreadsheetDialogueImporterEditor : Editor
{
    private const int ExportColumnCount = 24;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        if (GUILayout.Button("CSVを検証", GUILayout.Height(30)))
            Validate((SpreadsheetDialogueImporter)target, false);

        if (GUILayout.Button("CSVからFungusを同期", GUILayout.Height(40)))
        {
            if (
                EditorUtility.DisplayDialog(
                    "Fungus会話の同期",
                    "CSVに記載された同名Block内のCommandを全て再構築します。CSVにないBlockは変更しません。続行しますか？",
                    "同期する",
                    "キャンセル"
                )
            )
            {
                Validate((SpreadsheetDialogueImporter)target, true);
            }
        }
    }

    private static void Validate(SpreadsheetDialogueImporter importer, bool apply)
    {
        var errors = new List<string>();
        if (importer.targetFlowchart == null)
            errors.Add("同期先Flowchartが設定されていません。");
        if (importer.unityExportCsv == null)
            errors.Add("UnityExport CSVが設定されていません。");

        if (errors.Count > 0)
        {
            Report(errors);
            return;
        }

        ParseExport(
            importer.unityExportCsv.text,
            errors,
            out List<DialogueRow> dialogueRows,
            out List<ConditionRow> conditionRows,
            out Dictionary<string, FlagMasterRow> flagMaster
        );
        ValidateRows(importer.targetFlowchart, dialogueRows, conditionRows, flagMaster, errors);

        if (errors.Count > 0)
        {
            Report(errors);
            return;
        }

        if (!apply)
        {
            Debug.Log(
                $"CSV検証成功: {flagMaster.Count}フラグ、{dialogueRows.Count}会話行、{conditionRows.Count}条件行"
            );
            return;
        }

        Undo.SetCurrentGroupName("Sync spreadsheet dialogues");
        int undoGroup = Undo.GetCurrentGroup();
        try
        {
            int blockCount = Synchronize(importer.targetFlowchart, dialogueRows, conditionRows);
            EditorUtility.SetDirty(importer.targetFlowchart);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"Fungus同期完了: {blockCount} Block、{dialogueRows.Count}会話行");
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(undoGroup);
            Debug.LogException(exception);
        }
    }

    private static int Synchronize(
        Flowchart flowchart,
        List<DialogueRow> dialogues,
        List<ConditionRow> conditions
    )
    {
        var dialogueByBlock = dialogues.GroupBy(row => row.blockName).ToList();
        var conditionLookup = conditions
            .GroupBy(row => new BranchKey(row.blockName, row.priority))
            .ToDictionary(group => group.Key, group => group.OrderBy(row => row.order).ToList());

        int positionIndex = 0;
        foreach (IGrouping<string, DialogueRow> blockGroup in dialogueByBlock)
        {
            Block block = flowchart.FindBlock(blockGroup.Key);
            if (block == null)
            {
                block = flowchart.CreateBlock(
                    new Vector2(positionIndex % 5 * 280, positionIndex / 5 * 180)
                );
                block.BlockName = blockGroup.Key;
                Undo.RegisterCreatedObjectUndo(block, "Create dialogue block");
            }

            RebuildBlock(flowchart, block, blockGroup.ToList(), conditionLookup);
            positionIndex++;
        }

        return dialogueByBlock.Count;
    }

    private static void RebuildBlock(
        Flowchart flowchart,
        Block block,
        List<DialogueRow> rows,
        Dictionary<BranchKey, List<ConditionRow>> conditionLookup
    )
    {
        Undo.RecordObject(block, "Rebuild dialogue block");
        foreach (Command command in block.CommandList.Where(command => command != null).ToArray())
            Undo.DestroyObjectImmediate(command);
        block.CommandList.Clear();

        List<IGrouping<int, DialogueRow>> branches = rows.GroupBy(row => row.priority)
            .OrderByDescending(group => group.Key)
            .ToList();

        // すべての自動生成会話Blockは、会話状態の初期化を必ず最初に行う。
        AddCommand<TalkStartCommand>(flowchart, block);

        bool hasConditionalBranch = branches.Any(group => !group.Any(row => row.isDefault));
        bool emittedCondition = false;

        foreach (IGrouping<int, DialogueRow> branch in branches)
        {
            bool isDefault = branch.Any(row => row.isDefault);
            if (isDefault)
            {
                if (hasConditionalBranch)
                    AddCommand<Fungus.Else>(flowchart, block);
            }
            else
            {
                BranchKey key = new BranchKey(block.BlockName, branch.Key);
                CheckFlagConditionPro conditionCommand = emittedCondition
                    ? AddCommand<ElseIfFlagConditionProCommand>(flowchart, block)
                    : AddCommand<IfFlagConditionProCommand>(flowchart, block);
                SetConditions(conditionCommand, conditionLookup[key]);
                emittedCondition = true;
            }

            AddDialogueCommands(flowchart, block, branch.OrderBy(row => row.lineOrder).ToList());
        }

        if (hasConditionalBranch)
            AddCommand<Fungus.End>(flowchart, block);

        // 条件分岐の終了後を含め、会話Blockの最後に必ず終了処理を置く。
        AddCommand<TalkEndCommand>(flowchart, block);

        block.UpdateIndentLevels();
        EditorUtility.SetDirty(block);
    }

    private static void AddDialogueCommands(
        Flowchart flowchart,
        Block block,
        List<DialogueRow> rows
    )
    {
        for (int index = 0; index < rows.Count; index++)
        {
            DialogueRow row = rows[index];
            if (row.commandType == DialogueCommandType.RandomSay)
            {
                string groupId = row.groupId;
                List<DialogueRow> options = rows.Where(candidate =>
                        candidate.commandType == DialogueCommandType.RandomSay
                        && candidate.groupId == groupId
                    )
                    .ToList();
                if (options[0] != row)
                    continue;

                RandomSayCommand command = AddCommand<RandomSayCommand>(flowchart, block);
                ConfigureSay(command, row);
                command.SetDialogueOptions(
                    options
                        .GroupBy(option =>
                            string.IsNullOrEmpty(option.randomOptionId)
                                ? $"__ROW_{option.sourceLine}"
                                : option.randomOptionId
                        )
                        .OrderBy(option => option.Min(row => row.lineOrder))
                        .Select(option =>
                            string.Join(
                                "{wc}",
                                option.OrderBy(row => row.lineOrder).Select(row => row.dialogue)
                            )
                        )
                );
                EditorUtility.SetDirty(command);
            }
            else if (row.commandType == DialogueCommandType.Choice)
            {
                Block targetBlock = flowchart.FindBlock(row.choiceTargetBlock);
                if (targetBlock == null)
                {
                    targetBlock = flowchart.CreateBlock(
                        block._NodeRect.position + Vector2.right * 320
                    );
                    targetBlock.BlockName = row.choiceTargetBlock;
                    Undo.RegisterCreatedObjectUndo(targetBlock, "Create choice target block");
                }

                Fungus.Menu menu = AddCommand<Fungus.Menu>(flowchart, block);
                SerializedObject serializedMenu = new SerializedObject(menu);
                serializedMenu.FindProperty("text").stringValue = row.dialogue;
                serializedMenu.FindProperty("targetBlock").objectReferenceValue = targetBlock;
                serializedMenu.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Say say = AddCommand<Say>(flowchart, block);
                ConfigureSay(say, row);
            }
        }
    }

    private static void ConfigureSay(Say say, DialogueRow row)
    {
        say.SetStandardText(row.dialogue);
        Character character = row.showName ? FindCharacter(row.characterName) : null;
        say.SetCharacter(character);
        say.SetCustomCharacterName(row.showName ? row.displayName : "");

        if (character != null && !string.IsNullOrEmpty(row.expression))
        {
            if (row.characterName == "Heroin" || row.characterName == "Fill")
                say.SetPortraitString(row.expression);
            else
                say.SetPortrait(character.GetPortrait(row.expression));
        }

        SerializedObject serializedSay = new SerializedObject(say);
        serializedSay.FindProperty("extendPrevious").boolValue = row.extendPrevious;
        serializedSay.FindProperty("waitForClick").boolValue = row.waitForClick;
        serializedSay.FindProperty("fadeWhenDone").boolValue = row.fadeWhenDone;
        serializedSay.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(say);
    }

    private static T AddCommand<T>(Flowchart flowchart, Block block)
        where T : Command
    {
        T command = Undo.AddComponent<T>(block.gameObject);
        command.ParentBlock = block;
        command.ItemId = flowchart.NextItemId();
        command.OnCommandAdded(block);
        block.CommandList.Add(command);
        return command;
    }

    private static void SetConditions(CheckFlagConditionPro command, List<ConditionRow> rows)
    {
        SerializedObject serializedCommand = new SerializedObject(command);
        serializedCommand.FindProperty("logicalOperator").enumValueIndex = (int)
            rows[0].logicalOperator;
        SerializedProperty conditions = serializedCommand.FindProperty("flagConditions");
        conditions.arraySize = rows.Count;

        for (int index = 0; index < rows.Count; index++)
        {
            ConditionRow row = rows[index];
            SerializedProperty condition = conditions.GetArrayElementAtIndex(index);
            condition.FindPropertyRelative("conditionType").enumValueIndex = (int)row.type;
            condition.FindPropertyRelative("enumTypeName").stringValue = row.enumTypeName;
            condition.FindPropertyRelative("enumValueName").stringValue = row.flagName;
            condition.FindPropertyRelative("requiredBoolValue").boolValue = row.boolValue;
            condition.FindPropertyRelative("intComparison").enumValueIndex = (int)row.intComparison;
            condition.FindPropertyRelative("requiredIntValue").intValue = row.intValue;
            condition.FindPropertyRelative("doorId").intValue = row.doorId;
        }

        serializedCommand.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Character FindCharacter(string objectName)
    {
        return UnityEngine
            .Object.FindObjectsOfType<Character>(true)
            .FirstOrDefault(character => character.gameObject.name == objectName);
    }

    private static void ParseExport(
        string csv,
        List<string> errors,
        out List<DialogueRow> dialogues,
        out List<ConditionRow> conditions,
        out Dictionary<string, FlagMasterRow> flagMaster
    )
    {
        dialogues = new List<DialogueRow>();
        conditions = new List<ConditionRow>();
        flagMaster = new Dictionary<string, FlagMasterRow>();
        List<string[]> records = CsvParser.Parse(csv);

        if (
            records.Count == 0
            || records[0].Length < ExportColumnCount
            || records[0][0].Trim().TrimStart('\uFEFF') != "RecordType"
            || records[0][23].Trim() != "EnumValue"
            || (records[0].Length > 24 && records[0][24].Trim() != "RandomOptionId")
        )
        {
            errors.Add("CSV形式が不正です。Google SheetsのUnityExportタブをCSV出力してください。");
            return;
        }

        for (int index = 1; index < records.Count; index++)
        {
            string[] values = records[index];
            if (values.All(string.IsNullOrWhiteSpace))
                continue;
            if (values.Length < ExportColumnCount)
            {
                errors.Add($"UnityExport CSV {index + 1}行目: 列数が不足しています。");
                continue;
            }

            switch (values[0].Trim().ToUpperInvariant())
            {
                case "FLAG":
                    ParseFlagRecord(values, index + 1, flagMaster, errors);
                    break;
                case "DIALOGUE":
                    ParseDialogueRecord(values, index + 1, dialogues, errors);
                    break;
                case "CONDITION":
                    ParseConditionRecord(values, index + 1, conditions, errors);
                    break;
                default:
                    errors.Add($"UnityExport CSV {index + 1}行目: RecordTypeが不正です。");
                    break;
            }
        }
    }

    private static void ParseFlagRecord(
        string[] values,
        int sourceLine,
        Dictionary<string, FlagMasterRow> flagMaster,
        List<string> errors
    )
    {
        string flagId = values[16].Trim();
        if (string.IsNullOrEmpty(flagId))
        {
            errors.Add($"UnityExport CSV {sourceLine}行目: FLAGのFlagIdが空です。");
            return;
        }
        if (!int.TryParse(values[23], out int enumValue))
        {
            errors.Add($"UnityExport CSV {sourceLine}行目: FLAGのEnumValueが整数ではありません。");
            return;
        }
        if (flagMaster.ContainsKey(flagId))
        {
            errors.Add($"UnityExport CSV {sourceLine}行目: FlagId '{flagId}' が重複しています。");
            return;
        }

        flagMaster.Add(
            flagId,
            new FlagMasterRow
            {
                sourceLine = sourceLine,
                flagId = flagId,
                flagType = values[18].Trim(),
                enumTypeName = values[19].Trim(),
                flagName = values[20].Trim(),
                enumValue = enumValue,
            }
        );
    }

    private static void ParseDialogueRecord(
        string[] values,
        int sourceLine,
        List<DialogueRow> result,
        List<string> errors
    )
    {
        if (
            !int.TryParse(values[2], out int priority)
            || !int.TryParse(values[3], out int lineOrder)
        )
        {
            errors.Add(
                $"UnityExport CSV {sourceLine}行目: 分岐優先度または行順が整数ではありません。"
            );
            return;
        }
        if (!TryParseCommandType(values[13], out DialogueCommandType commandType))
        {
            errors.Add($"UnityExport CSV {sourceLine}行目: CommandTypeが不正です。");
            return;
        }

        result.Add(
            new DialogueRow
            {
                sourceLine = sourceLine,
                blockName = values[1].Trim(),
                priority = priority,
                lineOrder = lineOrder,
                characterName = values[4].Trim(),
                displayName = values[5].Trim(),
                showName = ParseBool(values[6]),
                dialogue = values[7],
                expression = values[8].Trim(),
                extendPrevious = ParseBool(values[9]),
                waitForClick = ParseBool(values[10], true),
                fadeWhenDone = ParseBool(values[11], true),
                isDefault = ParseBool(values[12]),
                commandType = commandType,
                groupId = values[14].Trim(),
                choiceTargetBlock = values[15].Trim(),
                randomOptionId = values.Length > 24 ? values[24].Trim() : "",
            }
        );
    }

    private static void ParseConditionRecord(
        string[] values,
        int sourceLine,
        List<ConditionRow> result,
        List<string> errors
    )
    {
        if (!int.TryParse(values[2], out int priority) || !int.TryParse(values[3], out int order))
        {
            errors.Add(
                $"UnityExport CSV {sourceLine}行目: 分岐優先度または条件順が整数ではありません。"
            );
            return;
        }

        var row = new ConditionRow
        {
            sourceLine = sourceLine,
            blockName = values[1].Trim(),
            priority = priority,
            order = order,
            flagId = values[16].Trim(),
            logicalOperator = values[17].Trim().Equals("OR", StringComparison.OrdinalIgnoreCase)
                ? CheckFlagConditionPro.LogicalOperator.Or
                : CheckFlagConditionPro.LogicalOperator.And,
            enumTypeName = values[19].Trim(),
            flagName = values[20].Trim(),
            intComparison = ParseComparison(values[21]),
        };

        if (values[18].Trim().Equals("Bool", StringComparison.OrdinalIgnoreCase))
        {
            row.type = FlagConditionPro.ConditionType.Bool;
            row.boolValue = ParseBool(values[22]);
        }
        else if (values[18].Trim().Equals("Int", StringComparison.OrdinalIgnoreCase))
        {
            row.type = FlagConditionPro.ConditionType.Int;
            if (!int.TryParse(values[22], out row.intValue))
                errors.Add(
                    $"UnityExport CSV {sourceLine}行目: Int条件のValueが整数ではありません。"
                );
        }
        else
        {
            errors.Add($"UnityExport CSV {sourceLine}行目: FlagTypeはBoolまたはIntです。");
            return;
        }
        result.Add(row);
    }

    private static void ValidateRows(
        Flowchart flowchart,
        List<DialogueRow> dialogues,
        List<ConditionRow> conditions,
        Dictionary<string, FlagMasterRow> flagMaster,
        List<string> errors
    )
    {
        foreach (DialogueRow row in dialogues)
        {
            if (string.IsNullOrEmpty(row.blockName))
                errors.Add($"会話CSV {row.sourceLine}行目: 入口Block名が空です。");
            if (string.IsNullOrEmpty(row.dialogue))
                errors.Add($"会話CSV {row.sourceLine}行目: セリフ／選択肢文が空です。");
            if (!row.showName && !string.IsNullOrEmpty(row.characterName))
                errors.Add(
                    $"会話CSV {row.sourceLine}行目: 名前を表示がFALSEの場合、Unity Character名は空欄にしてください。"
                );
            if (!row.showName && !string.IsNullOrEmpty(row.displayName))
                errors.Add(
                    $"会話CSV {row.sourceLine}行目: 名前を表示がFALSEの場合、表示名は空欄にしてください。"
                );
            if (row.showName && string.IsNullOrEmpty(row.displayName))
                errors.Add(
                    $"会話CSV {row.sourceLine}行目: 名前を表示がTRUEの場合、表示名が必要です。"
                );
            if (
                row.showName
                && !string.IsNullOrEmpty(row.characterName)
                && FindCharacter(row.characterName) == null
            )
                errors.Add(
                    $"会話CSV {row.sourceLine}行目: Character '{row.characterName}' がシーンにありません。"
                );
            if (row.expression == "__SETTING_CHARACTER_NOT_FOUND__")
                errors.Add(
                    $"会話CSV {row.sourceLine}行目: Character名が設定ページに存在しません。"
                );
            if (row.expression == "__SETTING_PORTRAIT_INCOMPLETE__")
                errors.Add(
                    $"会話CSV {row.sourceLine}行目: 設定ページの体形・表情候補が片方だけ設定されています。"
                );
            if (row.expression == "__PORTRAIT_SELECTION_REQUIRED__")
                errors.Add(
                    $"会話CSV {row.sourceLine}行目: 体形または表情に複数候補があるため、会話シートで指定してください。"
                );
            if (
                row.commandType == DialogueCommandType.RandomSay
                && string.IsNullOrEmpty(row.groupId)
            )
                errors.Add($"会話CSV {row.sourceLine}行目: RandomSayにはグループIDが必要です。");
            if (
                row.commandType == DialogueCommandType.Choice
                && string.IsNullOrEmpty(row.choiceTargetBlock)
            )
                errors.Add(
                    $"会話CSV {row.sourceLine}行目: Choiceには選択肢移動先Blockが必要です。"
                );
        }

        foreach (
            IGrouping<string, DialogueRow> randomGroup in dialogues
                .Where(row => row.commandType == DialogueCommandType.RandomSay)
                .GroupBy(row => $"{row.blockName}\u001f{row.priority}\u001f{row.groupId}")
        )
        {
            foreach (
                IGrouping<string, DialogueRow> option in randomGroup
                    .Where(row => !string.IsNullOrEmpty(row.randomOptionId))
                    .GroupBy(row => row.randomOptionId)
            )
            {
                DialogueRow first = option.OrderBy(row => row.lineOrder).First();
                if (
                    option.Any(row =>
                        row.characterName != first.characterName
                        || row.displayName != first.displayName
                        || row.showName != first.showName
                        || row.expression != first.expression
                    )
                )
                {
                    errors.Add(
                        $"Block '{first.blockName}' のランダム候補 '{option.Key}': "
                            + "候補内では話者名・Character・表情を統一してください。"
                    );
                }
            }
        }

        var conditionKeys = conditions
            .Select(row => new BranchKey(row.blockName, row.priority))
            .ToHashSet();
        foreach (
            IGrouping<BranchKey, DialogueRow> branch in dialogues
                .Where(row => !row.isDefault)
                .GroupBy(row => new BranchKey(row.blockName, row.priority))
        )
        {
            if (!conditionKeys.Contains(branch.Key))
                errors.Add(
                    $"Block '{branch.Key.blockName}' 優先度 {branch.Key.priority}: 条件行がありません。"
                );
        }

        ValidateFlagMaster(flagMaster, errors);

        foreach (ConditionRow row in conditions)
        {
            if (!flagMaster.TryGetValue(row.flagId, out FlagMasterRow master))
            {
                errors.Add(
                    $"UnityExport CSV {row.sourceLine}行目: FlagId '{row.flagId}' はマスター未登録です。"
                );
                continue;
            }
            if (row.enumTypeName != master.enumTypeName || row.flagName != master.flagName)
            {
                errors.Add(
                    $"UnityExport CSV {row.sourceLine}行目: FlagId '{row.flagId}' の展開値がマスターと不一致です。"
                );
                continue;
            }
            bool expectsBool = master.flagType.Equals("Bool", StringComparison.OrdinalIgnoreCase);
            if (expectsBool != (row.type == FlagConditionPro.ConditionType.Bool))
                errors.Add(
                    $"UnityExport CSV {row.sourceLine}行目: FlagId '{row.flagId}' の型が不一致です。"
                );

            Type enumType = ResolveEnumType(row.enumTypeName);
            if (enumType != null && Enum.GetNames(enumType).Contains(row.flagName))
                row.enumTypeName = enumType.AssemblyQualifiedName;
        }

        foreach (
            IGrouping<BranchKey, ConditionRow> branch in conditions.GroupBy(row => new BranchKey(
                row.blockName,
                row.priority
            ))
        )
        {
            if (branch.Select(row => row.logicalOperator).Distinct().Count() > 1)
                errors.Add(
                    $"Block '{branch.Key.blockName}' 優先度 {branch.Key.priority}: ANDとORを混在できません。"
                );
        }

        foreach (IGrouping<string, DialogueRow> block in dialogues.GroupBy(row => row.blockName))
        {
            if (
                block
                    .Select(row => row.priority)
                    .Distinct()
                    .Count(priority => block.Any(row => row.priority == priority && row.isDefault))
                > 1
            )
                errors.Add($"Block '{block.Key}': デフォルト分岐は一つだけ指定できます。");
        }
    }

    private static void ValidateFlagMaster(
        Dictionary<string, FlagMasterRow> flagMaster,
        List<string> errors
    )
    {
        foreach (FlagMasterRow row in flagMaster.Values)
        {
            Type enumType = ResolveEnumType(row.enumTypeName);
            if (enumType == null || !enumType.IsEnum)
            {
                errors.Add(
                    $"FlagId '{row.flagId}': EnumType '{row.enumTypeName}' が見つかりません。"
                );
                continue;
            }
            if (!TryGetExpectedFlagType(enumType, out string expectedFlagType))
            {
                errors.Add(
                    $"FlagId '{row.flagId}': EnumType '{row.enumTypeName}' は会話条件の対象外です。"
                );
                continue;
            }
            if (!Enum.GetNames(enumType).Contains(row.flagName))
            {
                errors.Add($"FlagId '{row.flagId}': FlagName '{row.flagName}' が存在しません。");
                continue;
            }
            if (row.flagId != row.enumTypeName + "." + row.flagName)
                errors.Add(
                    $"FlagId '{row.flagId}': IDは 'EnumType.FlagName' 形式と一致する必要があります。"
                );
            if (!row.flagType.Equals(expectedFlagType, StringComparison.OrdinalIgnoreCase))
                errors.Add(
                    $"FlagId '{row.flagId}': Typeは '{expectedFlagType}' である必要があります。"
                );

            int actualValue = Convert.ToInt32(
                Enum.Parse(enumType, row.flagName),
                CultureInfo.InvariantCulture
            );
            if (row.enumValue != actualValue)
                errors.Add(
                    $"FlagId '{row.flagId}': Enum数値不一致 Sheet={row.enumValue}, Unity={actualValue}"
                );
        }
    }

    private static bool TryGetExpectedFlagType(Type enumType, out string flagType)
    {
        if (
            enumType.Name == nameof(TutorialEvent)
            || enumType.Name.EndsWith("TriggeredEvent", StringComparison.Ordinal)
        )
        {
            flagType = "Bool";
            return true;
        }
        if (enumType.Name.EndsWith("CountedEvent", StringComparison.Ordinal))
        {
            flagType = "Int";
            return true;
        }

        flagType = null;
        return false;
    }

    private static Type ResolveEnumType(string name)
    {
        Type type = Type.GetType(name);
        if (type != null)
            return type;
        return AppDomain
            .CurrentDomain.GetAssemblies()
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch
                {
                    return Array.Empty<Type>();
                }
            })
            .FirstOrDefault(candidate => candidate.IsEnum && candidate.Name == name);
    }

    private static bool TryParseCommandType(string value, out DialogueCommandType result)
    {
        if (
            string.IsNullOrWhiteSpace(value)
            || value.Equals("Say", StringComparison.OrdinalIgnoreCase)
        )
        {
            result = DialogueCommandType.Say;
            return true;
        }
        return Enum.TryParse(value, true, out result);
    }

    private static FlagConditionPro.IntComparison ParseComparison(string value)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "notequal":
                return FlagConditionPro.IntComparison.NotEqualTo;
            case "greaterthan":
                return FlagConditionPro.IntComparison.GreaterThan;
            case "lessthan":
                return FlagConditionPro.IntComparison.LessThan;
            default:
                return FlagConditionPro.IntComparison.EqualTo;
        }
    }

    private static bool ParseBool(string value, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;
        return value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Trim() == "1"
            || value.Trim() == "TRUE";
    }

    private static void Report(List<string> errors)
    {
        Debug.LogError("CSVの検証に失敗しました:\n- " + string.Join("\n- ", errors));
    }

    private enum DialogueCommandType
    {
        Say,
        RandomSay,
        Choice,
    }

    private sealed class DialogueRow
    {
        public int sourceLine;
        public string blockName;
        public int priority;
        public int lineOrder;
        public string characterName;
        public string displayName;
        public bool showName;
        public string dialogue;
        public string expression;
        public bool extendPrevious;
        public bool waitForClick;
        public bool fadeWhenDone;
        public bool isDefault;
        public DialogueCommandType commandType;
        public string groupId;
        public string choiceTargetBlock;
        public string randomOptionId;
    }

    private sealed class ConditionRow
    {
        public int sourceLine;
        public string blockName;
        public int priority;
        public int order;
        public string flagId;
        public CheckFlagConditionPro.LogicalOperator logicalOperator;
        public FlagConditionPro.ConditionType type;
        public string enumTypeName;
        public string flagName;
        public bool boolValue;
        public FlagConditionPro.IntComparison intComparison;
        public int intValue;
        public int doorId;
    }

    private sealed class FlagMasterRow
    {
        public int sourceLine;
        public string flagId;
        public string flagType;
        public string enumTypeName;
        public string flagName;
        public int enumValue;
    }

    private readonly struct BranchKey : IEquatable<BranchKey>
    {
        public readonly string blockName;
        public readonly int priority;

        public BranchKey(string blockName, int priority)
        {
            this.blockName = blockName;
            this.priority = priority;
        }

        public bool Equals(BranchKey other) =>
            blockName == other.blockName && priority == other.priority;

        public override bool Equals(object obj) => obj is BranchKey other && Equals(other);

        public override int GetHashCode() =>
            (blockName != null ? blockName.GetHashCode() : 0) * 397 ^ priority;
    }

    private static class CsvParser
    {
        public static List<string[]> Parse(string text)
        {
            var rows = new List<string[]>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            for (int index = 0; index < text.Length; index++)
            {
                char current = text[index];
                if (current == '"')
                {
                    if (quoted && index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                        quoted = !quoted;
                }
                else if (current == ',' && !quoted)
                {
                    row.Add(field.ToString());
                    field.Clear();
                }
                else if ((current == '\n' || current == '\r') && !quoted)
                {
                    if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                        index++;
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row.ToArray());
                    row.Clear();
                }
                else
                    field.Append(current);
            }
            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row.ToArray());
            }
            return rows;
        }
    }
}

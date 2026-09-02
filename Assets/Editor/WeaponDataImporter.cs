#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 手動配置した武器CSVを検証し、既存の武器ScriptableObjectだけへ反映するEditorWindow。
/// </summary>
public sealed class WeaponDataImporterWindow : EditorWindow
{
    private const string BladeCsvPath = "Assets/武器データ(剣) - 剣のデータ.csv";
    private const string ShootCsvPath = "Assets/武器データ(弾) - 弾のデータ.csv";
    private const string BladeAssetPath = "Assets/WeaponData/Blade";
    private const string ShootAssetPath = "Assets/WeaponData/Shoot";
    private const string MotionAssetPath = "Assets/WeaponData/BladeAttackData";

    private static readonly string[] BladeHeaders =
    {
        "ID", "武器名", "攻撃力", "WP消費量", "クールタイム", "sizeX", "sizeY",
        "offsetX", "offsetY", "購入", "売却", "レア度", "モーションデータ",
    };

    private static readonly string[] ShootHeaders =
    {
        "ID", "武器名", "攻撃力", "WP消費量", "クールタイム", "速度", "消滅時間",
        "発射間隔", "貫通限界数", "移動タイプ", "radius", "offsetX", "offsetY",
        "購入", "売却", "レア度",
    };

    private static readonly HashSet<string> ShootReferenceHeaders =
        new HashSet<string>(StringComparer.Ordinal) { "距離", "入手方法" };

    [SerializeField] private TextAsset bladeCsv;
    [SerializeField] private TextAsset shootCsv;

    private readonly List<ResultMessage> messages = new List<ResultMessage>();
    private readonly List<Change> changes = new List<Change>();
    private Vector2 scroll;
    private bool isValidated;
    private bool hasErrors;

    [MenuItem("Tools/Adipothrone/Weapon Data Importer")]
    private static void Open() => GetWindow<WeaponDataImporterWindow>("Weapon CSV Importer");

    private void OnEnable()
    {
        if (bladeCsv == null)
            bladeCsv = AssetDatabase.LoadAssetAtPath<TextAsset>(BladeCsvPath);
        if (shootCsv == null)
            shootCsv = AssetDatabase.LoadAssetAtPath<TextAsset>(ShootCsvPath);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Weapon Data CSV Importer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "手動ダウンロードしたCSVを検証し、既存アセットだけを更新します。"
                + " CSVを配置しただけでは反映されず、新規アセットも作成しません。",
            MessageType.Info
        );

        EditorGUI.BeginChangeCheck();
        bladeCsv = (TextAsset)EditorGUILayout.ObjectField("剣CSV", bladeCsv, typeof(TextAsset), false);
        shootCsv = (TextAsset)EditorGUILayout.ObjectField("弾CSV", shootCsv, typeof(TextAsset), false);
        if (EditorGUI.EndChangeCheck())
            ResetResult();

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
        if (changes.Count == 0)
            EditorGUILayout.HelpBox("変更される項目はありません。", MessageType.Info);
        foreach (Change change in changes)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"{change.Kind} / ID {change.ID} / {change.Asset.name}");
            EditorGUILayout.LabelField(change.Field, $"{change.Before}  →  {change.After}");
            if (GUILayout.Button("アセットを選択", GUILayout.Width(110)))
                Selection.activeObject = change.Asset;
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
    }

    private void ResetResult()
    {
        isValidated = false;
        hasErrors = false;
        messages.Clear();
        changes.Clear();
    }

    private void Validate()
    {
        ImportPlan plan = BuildPlan();
        ShowPlan(plan);
        if (!hasErrors)
            messages.Insert(
                0,
                new ResultMessage(
                    $"検証成功: 剣 {plan.Blades.Count}件、弾 {plan.Shoots.Count}件を反映できます。",
                    MessageType.Info
                )
            );
    }

    private void Apply()
    {
        ImportPlan plan = BuildPlan();
        ShowPlan(plan);
        if (hasErrors)
        {
            Debug.LogError("武器CSVの再検証でエラーが見つかったため、反映を中止しました。");
            return;
        }

        UnityEngine.Object[] targets = plan.Blades
            .Select(x => (UnityEngine.Object)x.Asset)
            .Concat(plan.Shoots.Select(x => (UnityEngine.Object)x.Asset))
            .Distinct()
            .ToArray();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Import Weapon Data CSV");
        Undo.RecordObjects(targets, "Import Weapon Data CSV");

        foreach (BladeEntry entry in plan.Blades)
        {
            BladeWeaponData asset = entry.Asset;
            BladeRow row = entry.Row;
            asset.itemName = row.Name;
            asset.power = row.Power;
            asset.wpCost = row.WpCost;
            asset.cooldownTime = row.Cooldown;
            asset.ColliderSize = row.Size;
            asset.ColliderOffset = row.Offset;
            asset.buyPrice = row.BuyPrice;
            asset.sellPrice = row.SellPrice;
            asset.itemRank = row.Rank;
            asset.attackActionData = entry.Motion;
            EditorUtility.SetDirty(asset);
        }
        foreach (ShootEntry entry in plan.Shoots)
        {
            ShootWeaponData asset = entry.Asset;
            ShootRow row = entry.Row;
            asset.itemName = row.Name;
            asset.power = row.Power;
            asset.wpCost = row.WpCost;
            asset.cooldownTime = row.Cooldown;
            asset.shootSpeed = row.Speed;
            asset.vanishTime = row.VanishTime;
            asset.shotInterval = row.Interval;
            asset.penetrationLimitCount = row.Penetration;
            asset.moveType = row.MoveType;
            asset.colliderRadius = row.Radius;
            asset.colliderOffset = row.Offset;
            asset.buyPrice = row.BuyPrice;
            asset.sellPrice = row.SellPrice;
            asset.itemRank = row.Rank;
            EditorUtility.SetDirty(asset);
        }
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undoGroup);

        string result =
            $"武器CSV反映完了: 剣 {plan.Blades.Count}件、弾 {plan.Shoots.Count}件、変更 {plan.Changes.Count}項目。";
        messages.Insert(0, new ResultMessage(result, MessageType.Info));
        changes.Clear();
        Debug.Log(result);
    }

    private void ShowPlan(ImportPlan plan)
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

    private ImportPlan BuildPlan()
    {
        ImportPlan plan = new ImportPlan();
        if (bladeCsv == null)
            plan.Error("剣CSVが設定されていません。");
        if (shootCsv == null)
            plan.Error("弾CSVが設定されていません。");
        if (plan.HasErrors)
            return plan;
        BuildBladePlan(bladeCsv.text, plan);
        BuildShootPlan(shootCsv.text, plan);
        return plan;
    }

    private static void BuildBladePlan(string text, ImportPlan plan)
    {
        ParsedCsv csv = ParseAndValidate(text, BladeHeaders, null, "剣", plan);
        if (csv == null)
            return;

        Dictionary<int, List<BladeWeaponData>> assets = Load<BladeWeaponData>(BladeAssetPath)
            .Where(asset => !IsDebugAsset(asset))
            .GroupBy(x => Convert.ToInt32(x.weaponID))
            .ToDictionary(x => x.Key, x => x.ToList());
        ValidateAssetIds(assets, typeof(BladeName), "剣", plan);
        Dictionary<string, List<BladeAttackActionData>> motions = Load<BladeAttackActionData>(
                MotionAssetPath
            )
            .GroupBy(x => x.name, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);
        HashSet<int> csvIds = new HashSet<int>();

        ForEachDataRow(csv, (row, line) =>
        {
            if (!TryID(row, csv, line, "剣", csvIds, plan, out int id))
                return;
            if (!ValidateEnumId<BladeName>(id, line, "剣", plan))
                return;
            if (!TryAsset(assets, id, line, "剣", plan, out BladeWeaponData asset))
                return;
            int errors = plan.ErrorCount;
            BladeRow data = new BladeRow
            {
                Name = Text(row, csv, "武器名", line, "剣", plan),
                Power = NonNegativeInt(row, csv, "攻撃力", line, "剣", plan),
                WpCost = NonNegativeFloat(row, csv, "WP消費量", line, "剣", plan),
                Cooldown = NonNegativeFloat(row, csv, "クールタイム", line, "剣", plan),
                Size = new Vector2(
                    PositiveFloat(row, csv, "sizeX", line, "剣", plan),
                    PositiveFloat(row, csv, "sizeY", line, "剣", plan)
                ),
                Offset = new Vector2(
                    AnyFloat(row, csv, "offsetX", line, "剣", plan),
                    AnyFloat(row, csv, "offsetY", line, "剣", plan)
                ),
                BuyPrice = NonNegativeInt(row, csv, "購入", line, "剣", plan),
                SellPrice = NonNegativeInt(row, csv, "売却", line, "剣", plan),
                Rank = EnumCell<ItemRank>(row, csv, "レア度", line, "剣", plan),
                MotionName = Text(row, csv, "モーションデータ", line, "剣", plan),
            };
            if (plan.ErrorCount != errors)
                return;
            if (!motions.TryGetValue(data.MotionName, out List<BladeAttackActionData> found))
            {
                plan.Error($"剣CSV {line}行目: モーション '{data.MotionName}' が見つかりません。");
                return;
            }
            if (found.Count != 1)
            {
                plan.Error($"剣CSV {line}行目: モーション '{data.MotionName}' が複数存在します。");
                return;
            }
            BladeEntry entry = new BladeEntry { Asset = asset, Row = data, Motion = found[0] };
            plan.Blades.Add(entry);
            BladeChanges(entry, plan);
        });
        MissingWarnings(assets, csvIds, "剣", plan);
    }

    private static void BuildShootPlan(string text, ImportPlan plan)
    {
        ParsedCsv csv = ParseAndValidate(text, ShootHeaders, ShootReferenceHeaders, "弾", plan);
        if (csv == null)
            return;
        Dictionary<int, List<ShootWeaponData>> assets = Load<ShootWeaponData>(ShootAssetPath)
            .Where(asset => !IsDebugAsset(asset))
            .GroupBy(x => Convert.ToInt32(x.weaponID))
            .ToDictionary(x => x.Key, x => x.ToList());
        ValidateAssetIds(assets, typeof(ShootName), "弾", plan);
        HashSet<int> csvIds = new HashSet<int>();

        ForEachDataRow(csv, (row, line) =>
        {
            if (!TryID(row, csv, line, "弾", csvIds, plan, out int id))
                return;
            if (!ValidateEnumId<ShootName>(id, line, "弾", plan))
                return;
            if (!TryAsset(assets, id, line, "弾", plan, out ShootWeaponData asset))
                return;
            int errors = plan.ErrorCount;
            ShootRow data = new ShootRow
            {
                Name = Text(row, csv, "武器名", line, "弾", plan),
                Power = NonNegativeInt(row, csv, "攻撃力", line, "弾", plan),
                WpCost = NonNegativeFloat(row, csv, "WP消費量", line, "弾", plan),
                Cooldown = NonNegativeFloat(row, csv, "クールタイム", line, "弾", plan),
                Speed = PositiveFloat(row, csv, "速度", line, "弾", plan),
                VanishTime = PositiveFloat(row, csv, "消滅時間", line, "弾", plan),
                Interval = NonNegativeFloat(row, csv, "発射間隔", line, "弾", plan),
                Penetration = NonNegativeInt(row, csv, "貫通限界数", line, "弾", plan),
                MoveType = EnumCell<ShootWeaponData.ShootMoveType>(
                    row, csv, "移動タイプ", line, "弾", plan
                ),
                Radius = PositiveFloat(row, csv, "radius", line, "弾", plan),
                Offset = new Vector2(
                    AnyFloat(row, csv, "offsetX", line, "弾", plan),
                    AnyFloat(row, csv, "offsetY", line, "弾", plan)
                ),
                BuyPrice = NonNegativeInt(row, csv, "購入", line, "弾", plan),
                SellPrice = NonNegativeInt(row, csv, "売却", line, "弾", plan),
                Rank = EnumCell<ItemRank>(row, csv, "レア度", line, "弾", plan),
            };
            if (plan.ErrorCount != errors)
                return;
            ShootEntry entry = new ShootEntry { Asset = asset, Row = data };
            plan.Shoots.Add(entry);
            ShootChanges(entry, plan);
        });
        MissingWarnings(assets, csvIds, "弾", plan);
    }

    private static ParsedCsv ParseAndValidate(
        string text,
        IEnumerable<string> required,
        HashSet<string> knownReference,
        string kind,
        ImportPlan plan
    )
    {
        int initialErrorCount = plan.ErrorCount;
        List<string[]> rows = ParseCsv(text, out bool isValid);
        if (!isValid)
        {
            plan.Error($"{kind}CSV: ダブルクォーテーションが閉じられていません。");
            return null;
        }
        if (rows.Count == 0)
        {
            plan.Error($"{kind}CSVが空です。");
            return null;
        }
        string[] headers = rows[0].Select(x => x.Trim().TrimStart('﻿')).ToArray();
        Dictionary<string, int> columns = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < headers.Length; index++)
        {
            if (string.IsNullOrEmpty(headers[index]))
                continue;
            if (columns.ContainsKey(headers[index]))
                plan.Error($"{kind}CSV: ヘッダー '{headers[index]}' が重複しています。");
            else
                columns.Add(headers[index], index);
        }
        foreach (string header in required)
            if (!columns.ContainsKey(header))
                plan.Error($"{kind}CSV: 必須ヘッダー '{header}' がありません。");

        HashSet<string> allowed = new HashSet<string>(required, StringComparer.Ordinal);
        if (knownReference != null)
            allowed.UnionWith(knownReference);
        foreach (string header in columns.Keys.Where(x => !allowed.Contains(x)))
            plan.Warning($"{kind}CSV: 未使用列 '{header}' はUnityへ反映されません。");
        return plan.ErrorCount != initialErrorCount
            ? null
            : new ParsedCsv { Rows = rows, Columns = columns };
    }

    private static void ForEachDataRow(ParsedCsv csv, Action<string[], int> action)
    {
        for (int index = 1; index < csv.Rows.Count; index++)
        {
            string[] row = csv.Rows[index];
            if (!row.All(string.IsNullOrWhiteSpace))
                action(row, index + 1);
        }
    }

    private static bool TryID(
        string[] row, ParsedCsv csv, int line, string kind, HashSet<int> ids,
        ImportPlan plan, out int id
    )
    {
        string raw = Get(row, csv, "ID").Trim();
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
        {
            plan.Error($"{kind}CSV {line}行目: ID '{raw}' は整数ではありません。");
            return false;
        }
        if (!ids.Add(id))
        {
            plan.Error($"{kind}CSV {line}行目: ID {id} がCSV内で重複しています。");
            return false;
        }
        return true;
    }

    private static bool ValidateEnumId<T>(int id, int line, string kind, ImportPlan plan)
        where T : struct, Enum
    {
        if (id != 0 && Enum.IsDefined(typeof(T), id))
            return true;
        plan.Error($"{kind}CSV {line}行目: ID {id} は{typeof(T).Name}に存在しません。");
        return false;
    }

    private static bool TryAsset<T>(
        Dictionary<int, List<T>> assets, int id, int line, string kind,
        ImportPlan plan, out T asset
    ) where T : UnityEngine.Object
    {
        asset = null;
        if (!assets.TryGetValue(id, out List<T> found))
        {
            plan.Error($"{kind}CSV {line}行目: ID {id} の既存アセットがありません。新規作成は行いません。");
            return false;
        }
        if (found.Count != 1)
            return false;
        asset = found[0];
        return true;
    }

    private static void ValidateAssetIds<T>(
        Dictionary<int, List<T>> assets, Type enumType, string kind, ImportPlan plan
    ) where T : UnityEngine.Object
    {
        foreach (KeyValuePair<int, List<T>> pair in assets)
        {
            if (pair.Value.Count > 1)
                plan.Error(
                    $"{kind}アセットのID {pair.Key} が重複しています: "
                        + string.Join(", ", pair.Value.Select(AssetDatabase.GetAssetPath))
                );
            if (!Enum.IsDefined(enumType, pair.Key) || pair.Key == 0)
                foreach (T asset in pair.Value)
                    plan.Error(
                        $"{kind}アセット '{asset.name}' のID {pair.Key} は{enumType.Name}に存在しません。",
                        asset
                    );
        }
    }

    private static void MissingWarnings<T>(
        Dictionary<int, List<T>> assets, HashSet<int> csvIds, string kind, ImportPlan plan
    ) where T : UnityEngine.Object
    {
        foreach (KeyValuePair<int, List<T>> pair in assets)
            if (!csvIds.Contains(pair.Key))
                foreach (T asset in pair.Value)
                    plan.Warning(
                        $"{kind}アセット '{asset.name}' (ID {pair.Key}) はCSVにないため変更しません。",
                        asset
                    );
    }

    private static List<T> Load<T>(string folder) where T : UnityEngine.Object =>
        AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(x => x != null)
            .ToList();

    private static bool IsDebugAsset(BladeWeaponData asset) =>
        asset.weaponID == BladeName.Blade_Debug || asset.name == nameof(BladeName.Blade_Debug);

    private static bool IsDebugAsset(ShootWeaponData asset) =>
        asset.weaponID == ShootName.Shoot_Debug || asset.name == nameof(ShootName.Shoot_Debug);

    private static string Text(
        string[] row, ParsedCsv csv, string header, int line, string kind, ImportPlan plan
    )
    {
        string value = Get(row, csv, header).Trim();
        if (string.IsNullOrEmpty(value))
            plan.Error($"{kind}CSV {line}行目: '{header}' は必須です。");
        return value;
    }

    private static int NonNegativeInt(
        string[] row, ParsedCsv csv, string header, int line, string kind, ImportPlan plan
    )
    {
        string raw = Get(row, csv, header).Trim();
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            || value < 0)
            plan.Error($"{kind}CSV {line}行目: '{header}' は0以上の整数で入力してください: {raw}");
        return value;
    }

    private static float AnyFloat(
        string[] row, ParsedCsv csv, string header, int line, string kind, ImportPlan plan
    ) => Float(row, csv, header, line, kind, plan, float.NegativeInfinity);

    private static float NonNegativeFloat(
        string[] row, ParsedCsv csv, string header, int line, string kind, ImportPlan plan
    ) => Float(row, csv, header, line, kind, plan, 0f);

    private static float PositiveFloat(
        string[] row, ParsedCsv csv, string header, int line, string kind, ImportPlan plan
    ) => Float(row, csv, header, line, kind, plan, float.Epsilon);

    private static float Float(
        string[] row, ParsedCsv csv, string header, int line, string kind,
        ImportPlan plan, float minimum
    )
    {
        string raw = Get(row, csv, header).Trim();
        bool parsed = float.TryParse(
            raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value
        );
        if (!parsed || float.IsNaN(value) || float.IsInfinity(value) || value < minimum)
        {
            string rule = minimum > 0f ? "0より大きい数値" : minimum == 0f ? "0以上の数値" : "数値";
            plan.Error($"{kind}CSV {line}行目: '{header}' は{rule}で入力してください: {raw}");
        }
        return value;
    }

    private static T EnumCell<T>(
        string[] row, ParsedCsv csv, string header, int line, string kind, ImportPlan plan
    ) where T : struct, Enum
    {
        string raw = Get(row, csv, header).Trim();
        if (!Enum.TryParse(raw, false, out T value)
            || !Enum.IsDefined(typeof(T), value)
            || !string.Equals(Enum.GetName(typeof(T), value), raw, StringComparison.Ordinal)
            || Convert.ToInt32(value) == 0)
            plan.Error($"{kind}CSV {line}行目: '{header}' の値が不正です: {raw}");
        return value;
    }

    private static string Get(string[] row, ParsedCsv csv, string header)
    {
        int index = csv.Columns[header];
        return index < row.Length ? row[index] : string.Empty;
    }

    private static void BladeChanges(BladeEntry e, ImportPlan p)
    {
        AddChange(p, "剣", e.Asset, "武器名", e.Asset.itemName, e.Row.Name);
        AddChange(p, "剣", e.Asset, "攻撃力", e.Asset.power, e.Row.Power);
        AddChange(p, "剣", e.Asset, "WP消費量", e.Asset.wpCost, e.Row.WpCost);
        AddChange(p, "剣", e.Asset, "クールタイム", e.Asset.cooldownTime, e.Row.Cooldown);
        AddChange(p, "剣", e.Asset, "ColliderSize", e.Asset.ColliderSize, e.Row.Size);
        AddChange(p, "剣", e.Asset, "ColliderOffset", e.Asset.ColliderOffset, e.Row.Offset);
        AddChange(p, "剣", e.Asset, "購入", e.Asset.buyPrice, e.Row.BuyPrice);
        AddChange(p, "剣", e.Asset, "売却", e.Asset.sellPrice, e.Row.SellPrice);
        AddChange(p, "剣", e.Asset, "レア度", e.Asset.itemRank, e.Row.Rank);
        AddChange(p, "剣", e.Asset, "モーションデータ", e.Asset.attackActionData, e.Motion);
    }

    private static void ShootChanges(ShootEntry e, ImportPlan p)
    {
        AddChange(p, "弾", e.Asset, "武器名", e.Asset.itemName, e.Row.Name);
        AddChange(p, "弾", e.Asset, "攻撃力", e.Asset.power, e.Row.Power);
        AddChange(p, "弾", e.Asset, "WP消費量", e.Asset.wpCost, e.Row.WpCost);
        AddChange(p, "弾", e.Asset, "クールタイム", e.Asset.cooldownTime, e.Row.Cooldown);
        AddChange(p, "弾", e.Asset, "速度", e.Asset.shootSpeed, e.Row.Speed);
        AddChange(p, "弾", e.Asset, "消滅時間", e.Asset.vanishTime, e.Row.VanishTime);
        AddChange(p, "弾", e.Asset, "発射間隔", e.Asset.shotInterval, e.Row.Interval);
        AddChange(p, "弾", e.Asset, "貫通限界数", e.Asset.penetrationLimitCount, e.Row.Penetration);
        AddChange(p, "弾", e.Asset, "移動タイプ", e.Asset.moveType, e.Row.MoveType);
        AddChange(p, "弾", e.Asset, "radius", e.Asset.colliderRadius, e.Row.Radius);
        AddChange(p, "弾", e.Asset, "ColliderOffset", e.Asset.colliderOffset, e.Row.Offset);
        AddChange(p, "弾", e.Asset, "購入", e.Asset.buyPrice, e.Row.BuyPrice);
        AddChange(p, "弾", e.Asset, "売却", e.Asset.sellPrice, e.Row.SellPrice);
        AddChange(p, "弾", e.Asset, "レア度", e.Asset.itemRank, e.Row.Rank);
    }

    private static void AddChange<T>(
        ImportPlan plan, string kind, UnityEngine.Object asset, string field, T before, T after
    )
    {
        if (EqualityComparer<T>.Default.Equals(before, after))
            return;
        int id = asset is BladeWeaponData blade
            ? Convert.ToInt32(blade.weaponID)
            : Convert.ToInt32(((ShootWeaponData)asset).weaponID);
        plan.Changes.Add(
            new Change
            {
                Kind = kind,
                ID = id,
                Asset = asset,
                Field = field,
                Before = Display(before),
                After = Display(after),
            }
        );
    }

    private static string Display<T>(T value)
    {
        if (value == null)
            return "(なし)";
        if (value is UnityEngine.Object obj)
            return obj == null ? "(なし)" : obj.name;
        if (value is float number)
            return number.ToString("0.###", CultureInfo.InvariantCulture);
        return value.ToString();
    }

    private static List<string[]> ParseCsv(string text, out bool isValid)
    {
        List<string[]> rows = new List<string[]>();
        List<string> row = new List<string>();
        StringBuilder value = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    value.Append('"');
                    i++;
                }
                else if (c == '"')
                    quoted = false;
                else
                    value.Append(c);
            }
            else if (c == '"')
                quoted = true;
            else if (c == ',')
            {
                row.Add(value.ToString());
                value.Length = 0;
            }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                row.Add(value.ToString());
                rows.Add(row.ToArray());
                row = new List<string>();
                value.Length = 0;
            }
            else
                value.Append(c);
        }
        if (value.Length > 0 || row.Count > 0)
        {
            row.Add(value.ToString());
            rows.Add(row.ToArray());
        }
        isValid = !quoted;
        return rows;
    }

    private sealed class ParsedCsv
    {
        public List<string[]> Rows;
        public Dictionary<string, int> Columns;
    }

    private sealed class ImportPlan
    {
        public readonly List<BladeEntry> Blades = new List<BladeEntry>();
        public readonly List<ShootEntry> Shoots = new List<ShootEntry>();
        public readonly List<ResultMessage> Messages = new List<ResultMessage>();
        public readonly List<Change> Changes = new List<Change>();
        public int ErrorCount => Messages.Count(x => x.Type == MessageType.Error);
        public bool HasErrors => ErrorCount > 0;
        public void Error(string text, UnityEngine.Object context = null) =>
            Messages.Add(new ResultMessage(text, MessageType.Error, context));
        public void Warning(string text, UnityEngine.Object context = null) =>
            Messages.Add(new ResultMessage(text, MessageType.Warning, context));
    }

    private sealed class ResultMessage
    {
        public readonly string Text;
        public readonly MessageType Type;
        public readonly UnityEngine.Object Context;
        public ResultMessage(string text, MessageType type, UnityEngine.Object context = null)
        {
            Text = text;
            Type = type;
            Context = context;
        }
    }

    private sealed class Change
    {
        public string Kind;
        public int ID;
        public UnityEngine.Object Asset;
        public string Field;
        public string Before;
        public string After;
    }

    private sealed class BladeEntry
    {
        public BladeWeaponData Asset;
        public BladeRow Row;
        public BladeAttackActionData Motion;
    }

    private sealed class ShootEntry
    {
        public ShootWeaponData Asset;
        public ShootRow Row;
    }

    private sealed class BladeRow
    {
        public string Name;
        public int Power;
        public float WpCost;
        public float Cooldown;
        public Vector2 Size;
        public Vector2 Offset;
        public int BuyPrice;
        public int SellPrice;
        public ItemRank Rank;
        public string MotionName;
    }

    private sealed class ShootRow
    {
        public string Name;
        public int Power;
        public float WpCost;
        public float Cooldown;
        public float Speed;
        public float VanishTime;
        public float Interval;
        public int Penetration;
        public ShootWeaponData.ShootMoveType MoveType;
        public float Radius;
        public Vector2 Offset;
        public int BuyPrice;
        public int SellPrice;
        public ItemRank Rank;
    }
}
#endif

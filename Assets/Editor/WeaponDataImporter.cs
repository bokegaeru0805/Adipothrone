#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 剣（Blade）と弾（Shoot）のCSVファイルの更新を検知し、
/// 既存のScriptableObjectを自動で更新・上書きするエディタ拡張。
/// 堅牢なCSVパーサーとJSON比較による差分更新機能を備えています。
/// </summary>
public class WeaponDataUpdater : AssetPostprocessor
{
    #region ▼ 定数・パス・列番号の設定

    // =================================================================
    // 監視対象のCSVファイル名
    // =================================================================
    private const string TargetBladeCsvFileName = "武器データ(剣) - 剣のデータ.csv";
    private const string TargetShootCsvFileName = "武器データ(弾) - 弾のデータ.csv";

    // =================================================================
    // アセットの保存先フォルダパス
    // =================================================================
    private const string BladeDataPath = "Assets/WeaponData/blade/";
    private const string BladeAttackDataPath = "Assets/WeaponData/BladeAttackData/";
    private const string ShootDataPath = "Assets/WeaponData/shoot/";

    // =================================================================
    // スプレッドシート（CSV）の列番号定義：剣（Blade）
    // =================================================================
    private const int BladeCol_ID = 0; // ID
    private const int BladeCol_Name = 1; // 武器名
    private const int BladeCol_Power = 2; // 攻撃力
    private const int BladeCol_WpCost = 3; // WP消費量
    private const int BladeCol_Cooldown = 4; // クールタイム
    private const int BladeCol_SizeX = 5; // sizeX
    private const int BladeCol_SizeY = 6; // sizeY
    private const int BladeCol_OffsetX = 7; // offsetX
    private const int BladeCol_OffsetY = 8; // offsetY
    private const int BladeCol_BuyPrice = 9; // 購入
    private const int BladeCol_SellPrice = 10; // 売却
    private const int BladeCol_Rank = 11; // レア度
    private const int BladeCol_MotionData = 12; // モーションデータ

    // =================================================================
    // スプレッドシート（CSV）の列番号定義：弾（Shoot）
    // =================================================================
    private const int ShootCol_ID = 0; // ID
    private const int ShootCol_Name = 1; // 武器名
    private const int ShootCol_Power = 2; // 攻撃力
    private const int ShootCol_WpCost = 3; // WP消費量
    private const int ShootCol_Cooldown = 4; // クールタイム
    private const int ShootCol_Speed = 5; // 速度
    private const int ShootCol_VanishTime = 6; // 消滅時間
    private const int ShootCol_Distance = 7; // 距離 (※要件通り、この列のデータは使用しません)
    private const int ShootCol_Interval = 8; // 発射間隔
    private const int ShootCol_Penetration = 9; // 貫通限界数
    private const int ShootCol_MoveType = 10; // 移動タイプ
    private const int ShootCol_Radius = 11; // radius
    private const int ShootCol_OffsetX = 12; // offsetX
    private const int ShootCol_OffsetY = 13; // offsetY
    private const int ShootCol_BuyPrice = 14; // 購入
    private const int ShootCol_SellPrice = 15; // 売却
    private const int ShootCol_Rank = 16; // レア度
    #endregion

    #region ▼ 自動検知処理 (AssetPostprocessor)

    /// <summary>
    /// プロジェクト内のアセットが更新・追加された際に自動的に呼ばれるコールバック
    /// </summary>
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths
    )
    {
        foreach (string assetPath in importedAssets)
        {
            string fileName = Path.GetFileName(assetPath);

            // 剣のCSV更新検知
            if (fileName == TargetBladeCsvFileName)
            {
                Debug.Log(
                    $"<color=#00FFFF>[{TargetBladeCsvFileName}] の更新を検知しました。剣データの自動更新を開始します...</color>"
                );
                UpdateBladeDataFromCsv(assetPath);
            }
            // 弾のCSV更新検知
            else if (fileName == TargetShootCsvFileName)
            {
                Debug.Log(
                    $"<color=#00FFFF>[{TargetShootCsvFileName}] の更新を検知しました。弾データの自動更新を開始します...</color>"
                );
                UpdateShootDataFromCsv(assetPath);
            }
        }
    }

    #endregion

    #region ▼ 剣(Blade)データ更新ロジック

    private static void UpdateBladeDataFromCsv(string csvPath)
    {
        string csvText = File.ReadAllText(csvPath);
        List<string[]> rows = ParseCSV(csvText);

        if (rows.Count <= 1)
        {
            Debug.LogWarning(
                $"<color=yellow>剣のCSVデータが空か、ヘッダーしかありません。</color>"
            );
            return;
        }

        var existingDataDict = LoadAllExistingBladeData();
        int updateCount = 0;

        for (int i = 1; i < rows.Count; i++)
        {
            string[] row = rows[i];

            // 列数不足の行はスキップ
            if (row.Length <= BladeCol_MotionData)
                continue;

            if (!int.TryParse(row[BladeCol_ID], out int id))
            {
                Debug.LogWarning(
                    $"<color=yellow>[剣 CSV行 {i + 1}] IDが数値に変換できませんでした: {row[BladeCol_ID]}</color>"
                );
                continue;
            }

            if (!existingDataDict.TryGetValue(id, out BladeWeaponData data))
            {
                Debug.LogWarning(
                    $"<color=orange>[剣 スキップ] ID: {id} ({row[BladeCol_Name]}) のアセットが見つかりません。新規作成は行いません。</color>"
                );
                continue;
            }

            // --- 差分比較用のJSON化(更新前) ---
            string beforeJson = EditorJsonUtility.ToJson(data);

            // データの適用
            data.itemName = row[BladeCol_Name];

            if (int.TryParse(row[BladeCol_Power], out int power))
                data.power = power;
            if (float.TryParse(row[BladeCol_WpCost], out float wp))
                data.wpCost = wp;
            if (float.TryParse(row[BladeCol_Cooldown], out float cd))
                data.cooldownTime = cd;

            if (
                float.TryParse(row[BladeCol_SizeX], out float sx)
                && float.TryParse(row[BladeCol_SizeY], out float sy)
            )
                data.ColliderSize = new Vector2(sx, sy);

            if (
                float.TryParse(row[BladeCol_OffsetX], out float ox)
                && float.TryParse(row[BladeCol_OffsetY], out float oy)
            )
                data.ColliderOffset = new Vector2(ox, oy);

            if (int.TryParse(row[BladeCol_BuyPrice], out int bp))
                data.buyPrice = bp;
            if (int.TryParse(row[BladeCol_SellPrice], out int sp))
                data.sellPrice = sp;

            if (Enum.TryParse(row[BladeCol_Rank], out ItemRank rank))
                data.itemRank = rank;

            // モーションデータの適用
            string motionKeyword = row[BladeCol_MotionData].Trim();
            if (!string.IsNullOrEmpty(motionKeyword))
            {
                BladeAttackActionData motionData = FindBladeAttackActionData(motionKeyword);
                if (motionData != null)
                {
                    data.attackActionData = motionData;
                }
                else
                {
                    Debug.LogWarning(
                        $"<color=yellow>[剣 警告] 武器 '{data.itemName}' に指定されたモーションデータ '{motionKeyword}' が見つかりません。</color>"
                    );
                }
            }

            // --- 差分比較用のJSON化(更新後) ---
            string afterJson = EditorJsonUtility.ToJson(data);

            // 変更があった場合のみアセットを更新対象にする
            if (beforeJson != afterJson)
            {
                EditorUtility.SetDirty(data);
                updateCount++;
            }
        }

        // 保存と結果ログ出力
        if (updateCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"<color=#00FF00>✓ 剣（Blade）データの自動更新が完了しました！ ({updateCount} 件のアセットを上書きしました)</color>"
            );
        }
        else
        {
            Debug.Log("変更された剣（Blade）アセットはありませんでした。");
        }
    }

    #endregion

    #region ▼ 弾(Shoot)データ更新ロジック

    private static void UpdateShootDataFromCsv(string csvPath)
    {
        string csvText = File.ReadAllText(csvPath);
        List<string[]> rows = ParseCSV(csvText);

        if (rows.Count <= 1)
        {
            Debug.LogWarning(
                $"<color=yellow>弾のCSVデータが空か、ヘッダーしかありません。</color>"
            );
            return;
        }

        var existingDataDict = LoadAllExistingShootData();
        int updateCount = 0;

        for (int i = 1; i < rows.Count; i++)
        {
            string[] row = rows[i];

            // 列数不足の行はスキップ
            if (row.Length <= ShootCol_Rank)
                continue;

            if (!int.TryParse(row[ShootCol_ID], out int id))
            {
                Debug.LogWarning(
                    $"<color=yellow>[弾 CSV行 {i + 1}] IDが数値に変換できませんでした: {row[ShootCol_ID]}</color>"
                );
                continue;
            }

            if (!existingDataDict.TryGetValue(id, out ShootWeaponData data))
            {
                Debug.LogWarning(
                    $"<color=orange>[弾 スキップ] ID: {id} ({row[ShootCol_Name]}) のアセットが見つかりません。新規作成は行いません。</color>"
                );
                continue;
            }

            // --- 差分比較用のJSON化(更新前) ---
            string beforeJson = EditorJsonUtility.ToJson(data);

            // データの適用
            data.itemName = row[ShootCol_Name];

            if (int.TryParse(row[ShootCol_Power], out int power))
                data.power = power;
            if (float.TryParse(row[ShootCol_WpCost], out float wp))
                data.wpCost = wp;
            if (float.TryParse(row[ShootCol_Cooldown], out float cd))
                data.cooldownTime = cd;
            if (float.TryParse(row[ShootCol_Speed], out float spd))
                data.shootSpeed = spd;
            if (float.TryParse(row[ShootCol_VanishTime], out float vt))
                data.vanishTime = vt;

            // ※7列目(Distance)は無視する仕様

            if (float.TryParse(row[ShootCol_Interval], out float interval))
                data.shotInterval = interval;
            if (int.TryParse(row[ShootCol_Penetration], out int pen))
                data.penetrationLimitCount = pen;

            if (Enum.TryParse(row[ShootCol_MoveType], out ShootWeaponData.ShootMoveType moveType))
                data.moveType = moveType;

            if (float.TryParse(row[ShootCol_Radius], out float radius))
                data.colliderRadius = radius;

            if (
                float.TryParse(row[ShootCol_OffsetX], out float ox)
                && float.TryParse(row[ShootCol_OffsetY], out float oy)
            )
                data.colliderOffset = new Vector2(ox, oy);

            if (int.TryParse(row[ShootCol_BuyPrice], out int bp))
                data.buyPrice = bp;
            if (int.TryParse(row[ShootCol_SellPrice], out int sp))
                data.sellPrice = sp;

            if (Enum.TryParse(row[ShootCol_Rank], out ItemRank rank))
                data.itemRank = rank;

            // --- 差分比較用のJSON化(更新後) ---
            string afterJson = EditorJsonUtility.ToJson(data);

            // 変更があった場合のみアセットを更新対象にする
            if (beforeJson != afterJson)
            {
                EditorUtility.SetDirty(data);
                updateCount++;
            }
        }

        // 保存と結果ログ出力
        if (updateCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"<color=#00FF00>✓ 弾（Shoot）データの自動更新が完了しました！ ({updateCount} 件のアセットを上書きしました)</color>"
            );
        }
        else
        {
            Debug.Log("変更された弾（Shoot）アセットはありませんでした。");
        }
    }

    #endregion

    #region ▼ ユーティリティ (CSVパース・データ検索)

    /// <summary>
    /// ダブルクォーテーションやセル内の改行に対応した堅牢なCSVパース処理。
    /// </summary>
    private static List<string[]> ParseCSV(string csvText)
    {
        List<string[]> rows = new List<string[]>();
        List<string> currentRow = new List<string>();
        string currentValue = "";
        bool inQuotes = false;

        for (int i = 0; i < csvText.Length; i++)
        {
            char c = csvText[i];

            if (inQuotes)
            {
                if (c == '\"')
                {
                    if (i + 1 < csvText.Length && csvText[i + 1] == '\"')
                    {
                        currentValue += '\"';
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    currentValue += c;
                }
            }
            else
            {
                if (c == '\"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    currentRow.Add(currentValue);
                    currentValue = "";
                }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < csvText.Length && csvText[i + 1] == '\n')
                    {
                        i++;
                    }

                    currentRow.Add(currentValue);
                    rows.Add(currentRow.ToArray());

                    currentRow = new List<string>();
                    currentValue = "";
                }
                else
                {
                    currentValue += c;
                }
            }
        }

        if (!string.IsNullOrEmpty(currentValue) || csvText.EndsWith(","))
        {
            currentRow.Add(currentValue);
        }

        if (currentRow.Count > 0)
        {
            rows.Add(currentRow.ToArray());
        }

        return rows;
    }

    // --- 以下、既存のデータロード・検索用メソッド ---

    private static Dictionary<int, BladeWeaponData> LoadAllExistingBladeData()
    {
        var dict = new Dictionary<int, BladeWeaponData>();
        string[] guids = AssetDatabase.FindAssets("t:BladeWeaponData", new[] { BladeDataPath });

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<BladeWeaponData>(path);
            if (asset != null)
            {
                int id = Convert.ToInt32(asset.weaponID);
                if (!dict.ContainsKey(id))
                {
                    dict.Add(id, asset);
                }
            }
        }
        return dict;
    }

    private static Dictionary<int, ShootWeaponData> LoadAllExistingShootData()
    {
        var dict = new Dictionary<int, ShootWeaponData>();
        string[] guids = AssetDatabase.FindAssets("t:ShootWeaponData", new[] { ShootDataPath });

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<ShootWeaponData>(path);
            if (asset != null)
            {
                int id = Convert.ToInt32(asset.weaponID);
                if (!dict.ContainsKey(id))
                {
                    dict.Add(id, asset);
                }
            }
        }
        return dict;
    }

    private static BladeAttackActionData FindBladeAttackActionData(string keyword)
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:BladeAttackActionData",
            new[] { BladeAttackDataPath }
        );
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains(keyword))
            {
                return AssetDatabase.LoadAssetAtPath<BladeAttackActionData>(path);
            }
        }
        return null;
    }

    #endregion
}
#endif

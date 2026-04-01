#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;

#region 武器データインポーター
/// <summary>
/// 剣（Blade）と弾（Shoot）のCSVデータを読み込み、既存のScriptableObjectを更新するエディタ拡張。
/// 新規作成は行わず、データが見つからない場合はエラーを出力します。
/// 実際のデータに変更があったものだけを上書きし、更新件数をコンソールに表示します。
/// </summary>
public class WeaponDataImporter : EditorWindow
{
    #region パスと列番号の定義

    // =================================================================
    // フォルダパスの定義
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

    #region GUI描画用変数

    private TextAsset bladeCsvFile;
    private TextAsset shootCsvFile;

    private const string SaveKey_BladeCsvGuid = "WeaponImporter_BladeCsvGuid";
    private const string SaveKey_ShootCsvGuid = "WeaponImporter_ShootCsvGuid";

    #endregion

    #region ウィンドウ初期化・描画

    [MenuItem("Tools/武器データインポーター (CSV)")]
    public static void ShowWindow()
    {
        GetWindow<WeaponDataImporter>("武器インポーター");
    }

    private void OnEnable()
    {
        // 以前設定したCSVファイルのGUIDをロードして復元
        string bladeGuid = EditorPrefs.GetString(SaveKey_BladeCsvGuid, "");
        if (!string.IsNullOrEmpty(bladeGuid))
        {
            string path = AssetDatabase.GUIDToAssetPath(bladeGuid);
            bladeCsvFile = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        }

        string shootGuid = EditorPrefs.GetString(SaveKey_ShootCsvGuid, "");
        if (!string.IsNullOrEmpty(shootGuid))
        {
            string path = AssetDatabase.GUIDToAssetPath(shootGuid);
            shootCsvFile = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("剣（Blade）データのインポート", EditorStyles.boldLabel);
        bladeCsvFile = (TextAsset)
            EditorGUILayout.ObjectField("剣のCSVファイル", bladeCsvFile, typeof(TextAsset), false);

        if (GUILayout.Button("剣のデータを更新"))
        {
            if (bladeCsvFile != null)
            {
                SavePrefs(SaveKey_BladeCsvGuid, bladeCsvFile);
                ImportBladeData();
            }
            else
            {
                Debug.LogWarning("剣のCSVファイルを選択してください。");
            }
        }

        GUILayout.Space(20);

        GUILayout.Label("弾（Shoot）データのインポート", EditorStyles.boldLabel);
        shootCsvFile = (TextAsset)
            EditorGUILayout.ObjectField("弾のCSVファイル", shootCsvFile, typeof(TextAsset), false);

        if (GUILayout.Button("弾のデータを更新"))
        {
            if (shootCsvFile != null)
            {
                SavePrefs(SaveKey_ShootCsvGuid, shootCsvFile);
                ImportShootData();
            }
            else
            {
                Debug.LogWarning("弾のCSVファイルを選択してください。");
            }
        }

        GUILayout.Space(30);

        // --- 両方一括更新ボタン ---
        if (GUILayout.Button("剣と弾のデータを両方とも更新", GUILayout.Height(30)))
        {
            bool hasBlade = bladeCsvFile != null;
            bool hasShoot = shootCsvFile != null;

            if (!hasBlade && !hasShoot)
            {
                Debug.LogWarning("更新するCSVファイルが選択されていません。");
                return;
            }

            if (hasBlade)
            {
                SavePrefs(SaveKey_BladeCsvGuid, bladeCsvFile);
                ImportBladeData();
            }
            else
            {
                Debug.LogWarning(
                    "剣のCSVファイルが選択されていないため、剣のインポートはスキップされました。"
                );
            }

            if (hasShoot)
            {
                SavePrefs(SaveKey_ShootCsvGuid, shootCsvFile);
                ImportShootData();
            }
            else
            {
                Debug.LogWarning(
                    "弾のCSVファイルが選択されていないため、弾のインポートはスキップされました。"
                );
            }
        }
    }

    private void SavePrefs(string key, TextAsset asset)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        string guid = AssetDatabase.AssetPathToGUID(path);
        EditorPrefs.SetString(key, guid);
    }

    #endregion

    #region インポート処理（Blade）

    /// <summary>
    /// 剣のCSVデータを読み込み、既存のBladeWeaponDataを更新します。
    /// 変更があったデータのみを更新対象とします。
    /// </summary>
    private void ImportBladeData()
    {
        List<string[]> rows = ParseCSV(bladeCsvFile.text);
        if (rows.Count <= 1)
        {
            Debug.LogWarning("剣のCSVデータが空か、ヘッダーしかありません。");
            return;
        }

        var existingDataDict = LoadAllExistingBladeData();
        int updateCount = 0;

        for (int i = 1; i < rows.Count; i++)
        {
            string[] row = rows[i];
            if (row.Length <= BladeCol_MotionData)
                continue;

            if (!int.TryParse(row[BladeCol_ID], out int id))
            {
                Debug.LogWarning(
                    $"[剣 CSV行 {i + 1}] IDが数値に変換できませんでした: {row[BladeCol_ID]}"
                );
                continue;
            }

            if (!existingDataDict.TryGetValue(id, out BladeWeaponData data))
            {
                Debug.LogWarning(
                    $"[剣インポートエラー] ID: {id} ({row[BladeCol_Name]}) のアセットが '{BladeDataPath}' に見つかりません。新規作成はスキップされました。"
                );
                continue;
            }

            // --- 更新前の状態をJSON化して記憶 ---
            string beforeJson = EditorJsonUtility.ToJson(data);

            // データの代入
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
                        $"[剣インポート警告] 武器 '{data.itemName}' に指定されたモーションデータ '{motionKeyword}' が '{BladeAttackDataPath}' に見つかりません。"
                    );
                }
            }

            // --- 更新後の状態をJSON化して比較 ---
            string afterJson = EditorJsonUtility.ToJson(data);

            // 値に変化があった場合のみアセットを更新（SetDirty）してカウント
            if (beforeJson != afterJson)
            {
                EditorUtility.SetDirty(data);
                updateCount++;
                // Debug.Log($"[剣データ更新] ID:{id} '{data.itemName}' のデータが変更されました。");
            }
        }

        AssetDatabase.SaveAssets();

        // 完了結果をDebug.Logで出力
        if (updateCount > 0)
        {
            Debug.Log(
                $"剣（Blade）データのインポートが完了しました。実際に変更・上書きされたアセット数: {updateCount} 件"
            );
        }
        else
        {
            Debug.Log(
                "剣（Blade）データのインポートを完了しましたが、変更されたアセットはありませんでした。"
            );
        }
    }

    private Dictionary<int, BladeWeaponData> LoadAllExistingBladeData()
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

    private BladeAttackActionData FindBladeAttackActionData(string keyword)
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

    #region インポート処理（Shoot）

    /// <summary>
    /// 弾のCSVデータを読み込み、既存のShootWeaponDataを更新します。
    /// 変更があったデータのみを更新対象とします。
    /// </summary>
    private void ImportShootData()
    {
        List<string[]> rows = ParseCSV(shootCsvFile.text);
        if (rows.Count <= 1)
        {
            Debug.LogWarning("弾のCSVデータが空か、ヘッダーしかありません。");
            return;
        }

        var existingDataDict = LoadAllExistingShootData();
        int updateCount = 0;

        for (int i = 1; i < rows.Count; i++)
        {
            string[] row = rows[i];
            if (row.Length <= ShootCol_Rank)
                continue;

            if (!int.TryParse(row[ShootCol_ID], out int id))
            {
                Debug.LogWarning(
                    $"[弾 CSV行 {i + 1}] IDが数値に変換できませんでした: {row[ShootCol_ID]}"
                );
                continue;
            }

            if (!existingDataDict.TryGetValue(id, out ShootWeaponData data))
            {
                Debug.LogWarning(
                    $"[弾インポートエラー] ID: {id} ({row[ShootCol_Name]}) のアセットが '{ShootDataPath}' に見つかりません。新規作成はスキップされました。"
                );
                continue;
            }

            // --- 更新前の状態をJSON化して記憶 ---
            string beforeJson = EditorJsonUtility.ToJson(data);

            // データの代入
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

            // 7列目「距離」は要件通り使用しないため無視します

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

            // --- 更新後の状態をJSON化して比較 ---
            string afterJson = EditorJsonUtility.ToJson(data);

            // 値に変化があった場合のみアセットを更新（SetDirty）してカウント
            if (beforeJson != afterJson)
            {
                EditorUtility.SetDirty(data);
                updateCount++;
                // Debug.Log($"[弾データ更新] ID:{id} '{data.itemName}' のデータが変更されました。");
            }
        }

        AssetDatabase.SaveAssets();

        // 完了結果をDebug.Logで出力
        if (updateCount > 0)
        {
            Debug.Log(
                $"弾（Shoot）データのインポートが完了しました。実際に変更・上書きされたアセット数: {updateCount} 件"
            );
        }
        else
        {
            Debug.Log(
                "弾（Shoot）データのインポートを完了しましたが、変更されたアセットはありませんでした。"
            );
        }
    }

    private Dictionary<int, ShootWeaponData> LoadAllExistingShootData()
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

    #endregion

    #region CSVパーサー（共通機能）

    /// <summary>
    /// ダブルクォーテーションやセル内の改行に対応した堅牢なCSVパース処理。
    /// </summary>
    private List<string[]> ParseCSV(string csvText)
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

    #endregion
}
#endregion
#endif

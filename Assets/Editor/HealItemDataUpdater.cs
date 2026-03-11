using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CSVファイルの更新を検知し、ScriptableObject(HealItemData)を自動更新するエディタ拡張
/// </summary>
public class HealItemDataUpdater : AssetPostprocessor
{
    // =========================================================
    // ▼ 基本設定
    // =========================================================

    // 監視するCSVのファイル名（拡張子含む）
    private const string TargetCsvFileName = "アイテムデータ - 回復アイテム.csv";

    // 更新対象のHealItemDataアセットが保存されているフォルダのパス
    // ※プロジェクトの構成に合わせて適宜変更してください
    private const string TargetAssetFolderPath = "Assets/ItemData/HealItemData";

    // =========================================================
    // ▼ CSVの列インデックス定義（0からスタート）
    // =========================================================

    // CSVの列構成が変わった場合は、ここの数値を変更するだけで対応できます
    private const int Col_ID = 0; // ID
    private const int Col_ItemName = 1; // 表示名
    private const int Col_ItemRank = 2; // レア度
    private const int Col_HpHealAmount = 3; // HP回復量
    private const int Col_WpHealAmount = 4; // WP回復量
    private const int Col_BuyPrice = 10; // 購入価格
    private const int Col_SellPrice = 11; // 売却価格

    // =========================================================
    // ▼ 自動検知処理 (AssetPostprocessorの標準機能)
    // =========================================================

    /// <summary>
    /// アセットがインポート、削除、移動などされた後に自動で呼ばれるコールバック
    /// </summary>
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths
    )
    {
        // インポート（更新や上書き保存）されたファイルの中に、対象のCSVがあるかチェック
        foreach (string str in importedAssets)
        {
            if (Path.GetFileName(str) == TargetCsvFileName)
            {
                Debug.Log(
                    $"[{TargetCsvFileName}] の更新を検知しました。HealItemDataの自動更新を開始します..."
                );
                UpdateHealItemDataFromCsv(str);
                break; // 1回実行すれば十分なのでループを抜ける
            }
        }
    }

    // =========================================================
    // ▼ メイン更新処理
    // =========================================================

    private static void UpdateHealItemDataFromCsv(string csvPath)
    {
        // 1. CSVデータの読み込み
        string[] csvLines = File.ReadAllLines(csvPath);
        if (csvLines.Length <= 1)
        {
            Debug.LogWarning("CSVファイルが空、またはヘッダー行しかありません。");
            return;
        }

        // CSVのデータを「ID」をキーにして検索しやすいようにDictionaryへ格納
        Dictionary<int, string[]> csvDataDict = new Dictionary<int, string[]>();

        // 1行目（ヘッダー）を飛ばして2行目から読み込む
        for (int i = 1; i < csvLines.Length; i++)
        {
            string line = csvLines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue; // 空行はスキップ

            string[] columns = line.Split(',');

            // 最低限必要な列数があるかチェック（設定した最大インデックスの数だけ列が必要）
            if (columns.Length <= Col_SellPrice)
            {
                Debug.LogWarning($"CSVの {i + 1} 行目のフォーマットが不正です。列数が足りません。");
                continue;
            }

            // IDを取得して辞書に登録（数値変換に失敗した行はスキップ）
            if (int.TryParse(columns[Col_ID], out int id))
            {
                csvDataDict[id] = columns;
            }
            else
            {
                Debug.LogWarning($"CSVの {i + 1} 行目のIDが数値ではありません: {columns[Col_ID]}");
            }
        }

        // 2. 指定フォルダ内のHealItemDataアセットをすべて取得
        // "t:HealItemData" というフィルターで型を指定して検索します
        string[] guids = AssetDatabase.FindAssets(
            "t:HealItemData",
            new[] { TargetAssetFolderPath }
        );

        if (guids.Length == 0)
        {
            Debug.LogWarning(
                $"指定されたフォルダ '{TargetAssetFolderPath}' 内に HealItemData アセットが見つかりませんでした。"
            );
            return;
        }

        int updatedCount = 0;

        // 3. 取得した各アセットに対するデータ上書き処理
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            HealItemData asset = AssetDatabase.LoadAssetAtPath<HealItemData>(assetPath);

            if (asset == null)
                continue;

            // アセットに設定されている列挙型(Enum)のIDをintに変換
            int assetId = (int)asset.itemID;

            // アセットのIDと一致するデータがCSV側にあれば、上書き処理を行う
            if (csvDataDict.TryGetValue(assetId, out string[] csvRow))
            {
                // 文字列の代入
                asset.itemName = csvRow[Col_ItemName];

                // ItemRank(レア度)の文字列(E, Dなど)をEnumに変換して代入
                if (Enum.TryParse(csvRow[Col_ItemRank], out ItemRank rank))
                {
                    asset.itemRank = rank;
                }
                else
                {
                    Debug.LogWarning(
                        $"アセット '{asset.name}' (ID:{assetId}): レア度 '{csvRow[Col_ItemRank]}' は不正な値のため変換をスキップしました。"
                    );
                }

                // 数値データの変換と代入
                if (int.TryParse(csvRow[Col_HpHealAmount], out int hpHeal))
                    asset.hpHealAmount = hpHeal;
                if (int.TryParse(csvRow[Col_WpHealAmount], out int wpHeal))
                    asset.wpHealAmount = wpHeal;
                if (int.TryParse(csvRow[Col_BuyPrice], out int buyPrice))
                    asset.buyPrice = buyPrice;
                if (int.TryParse(csvRow[Col_SellPrice], out int sellPrice))
                    asset.sellPrice = sellPrice;

                // 変更があったことをUnityエディタに通知（保存対象マークを付ける）
                EditorUtility.SetDirty(asset);
                updatedCount++;
            }
        }

        // 4. 変更されたすべてのアセットをディスクに一括保存
        if (updatedCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"<color=green>✓ HealItemDataの自動更新が完了しました！ ({updatedCount} 個のアイテム情報を書き換えました)</color>"
            );
        }
        else
        {
            Debug.Log("更新対象のHealItemDataアセットはありませんでした（IDの一致なし）。");
        }
    }
}

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 進行度ログデータ(CSV)の更新を検知し、
/// 既存のProgressLogInfoDataを自動で更新・上書きするエディタ拡張。
/// フラグ条件(conditions)を保持したまま、テキストのみを安全に上書きします。
/// </summary>
public class ProgressLogDataUpdater : AssetPostprocessor
{
    #region ▼ 定数・パス・列番号の設定

    // =================================================================
    // 監視対象のCSVファイル名
    // =================================================================
    private const string TargetCsvFileName = "ゲームシステム会話 - ProgressLog.csv";

    // =================================================================
    // スプレッドシート（CSV）の列番号定義
    // ※ 0始まりで列番号を指定します。構成が変わった場合はここを変更してください。
    // =================================================================
    private const int ColumnIndex_ProgressID = 0; // 進行度ID（int値、例: 16001）
    private const int ColumnIndex_TextType = 2; // テキストの種類（"Base" または "Additional"）
    private const int ColumnIndex_SectionIndex = 3; // セクション番号（Additionalの場合のみ使用）
    private const int ColumnIndex_LogIndex = 4; // ログ番号（Additionalの場合のみ使用）

    // private const int ColumnIndex_Memo = 4;      // 管理用メモ（読み飛ばす用）
    private const int ColumnIndex_Text = 5; // 実際にゲームで表示・追記されるテキスト
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
            if (Path.GetFileName(assetPath) == TargetCsvFileName)
            {
                Debug.Log(
                    $"<color=#00FFFF>[{TargetCsvFileName}] の更新を検知しました。進行度ログの自動更新を開始します...</color>"
                );
                UpdateProgressLogDataFromCsv(assetPath);
                break; // 1度実行すれば全行処理されるため抜ける
            }
        }
    }

    #endregion

    #region ▼ データ更新ロジック

    /// <summary>
    /// CSVのテキストデータを受け取り、プロジェクト内のアセットを更新します。
    /// </summary>
    private static void UpdateProgressLogDataFromCsv(string csvPath)
    {
        string csvText = File.ReadAllText(csvPath);
        List<string[]> rows = ParseCsv(csvText);

        if (rows.Count <= 1)
        {
            Debug.LogWarning(
                $"<color=yellow>進行度ログのCSVデータが空か、ヘッダーしかありません。</color>"
            );
            return;
        }

        // プロジェクト内のすべての ProgressLogInfoData を取得し、IDで検索しやすいよう辞書化
        string[] guids = AssetDatabase.FindAssets("t:ProgressLogInfoData");
        Dictionary<int, ProgressLogInfoData> allLogDataDict =
            new Dictionary<int, ProgressLogInfoData>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ProgressLogInfoData data = AssetDatabase.LoadAssetAtPath<ProgressLogInfoData>(path);
            if (data != null)
            {
                int id = (int)data.logName;
                if (!allLogDataDict.ContainsKey(id))
                {
                    allLogDataDict.Add(id, data);
                }
            }
        }

        int updateCount = 0;
        int notFoundCount = 0;

        // 差分チェックのため、更新されたアセットを記録するハッシュセット
        HashSet<ProgressLogInfoData> modifiedAssets = new HashSet<ProgressLogInfoData>();

        // 見出し行をスキップするため i = 1 から開始
        for (int i = 1; i < rows.Count; i++)
        {
            string[] columns = rows[i];

            // 列数が足りない場合はスキップ
            if (columns.Length <= ColumnIndex_Text)
                continue;

            // 1. CSVからIDをint型として読み込む
            string progressIdStr = columns[ColumnIndex_ProgressID].Trim();
            if (int.TryParse(progressIdStr, out int progressID))
            {
                if (allLogDataDict.TryGetValue(progressID, out ProgressLogInfoData targetData))
                {
                    // --- 差分比較用のJSON化(更新前) ---
                    // ※複数行にわたって同一アセットを更新する可能性があるため、
                    // 最初の1回目の更新前状態だけを比較対象にする工夫も可能ですが、
                    // ここではシンプルに毎行ごとの変更を検知してHashSetに放り込みます。
                    string beforeJson = EditorJsonUtility.ToJson(targetData);

                    // テキストのパース
                    string textType = columns[ColumnIndex_TextType].Trim();
                    string textContent = columns[ColumnIndex_Text]; // 本文は意図的な空白の可能性があるのでTrimしない
                    textContent = textContent.Replace("\\n", "\n"); // "\n" を実際の改行に変換

                    // 実際のデータ書き換え
                    UpdateLogData(targetData, textType, columns, textContent);

                    // --- 差分比較用のJSON化(更新後) ---
                    string afterJson = EditorJsonUtility.ToJson(targetData);

                    if (beforeJson != afterJson)
                    {
                        modifiedAssets.Add(targetData);
                    }
                }
                else
                {
                    notFoundCount++;
                    Debug.LogWarning(
                        $"<color=orange>行 {i + 1}: 進行度ID [{progressID}] のアセットが見つかりません。事前にCreateメニューから作成し、Enumを設定してください。</color>"
                    );
                }
            }
            else if (!string.IsNullOrEmpty(progressIdStr))
            {
                Debug.LogWarning(
                    $"<color=yellow>行 {i + 1}: 進行度IDの読み込みに失敗しました。数値ではありません: {progressIdStr}</color>"
                );
            }
        }

        // 変更があったアセットを一括でDirty設定して保存
        foreach (var asset in modifiedAssets)
        {
            EditorUtility.SetDirty(asset);
            updateCount++;
        }

        if (updateCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"<color=#00FF00>✓ 進行度ログの自動更新が完了しました！ ({updateCount} 件のアセットを上書きしました)</color>"
            );
        }
        else
        {
            Debug.Log("変更された進行度ログアセットはありませんでした。");
        }
    }

    /// <summary>
    /// 実際のデータ書き換え処理。
    /// 指定されたインデックスの枠が無ければ自動で生成し、テキストのみを上書きします。
    /// （conditionsなどのフラグ設定は絶対に保持します）
    /// </summary>
    private static void UpdateLogData(
        ProgressLogInfoData logData,
        string textType,
        string[] columns,
        string textContent
    )
    {
        if (textType == "Base")
        {
            // ベーステキストの上書き
            logData.logText = textContent;
        }
        else if (textType == "Additional")
        {
            // セクション番号とログ番号を取得
            if (
                int.TryParse(columns[ColumnIndex_SectionIndex].Trim(), out int sectionIndex)
                && int.TryParse(columns[ColumnIndex_LogIndex].Trim(), out int logIndex)
            )
            {
                // セクションリストが存在しない場合は初期化
                if (logData.logSections == null)
                {
                    logData.logSections = new List<ProgressLogSection>();
                }

                // 指定されたセクション番号までリストの要素を自動拡張する
                while (logData.logSections.Count <= sectionIndex)
                {
                    logData.logSections.Add(
                        new ProgressLogSection()
                        {
                            sectionName = $"Section {logData.logSections.Count}",
                            conditionalLogs = new List<ConditionalLog>(),
                        }
                    );
                }

                ProgressLogSection targetSection = logData.logSections[sectionIndex];

                // 条件付きログリストが存在しない場合は初期化
                if (targetSection.conditionalLogs == null)
                {
                    targetSection.conditionalLogs = new List<ConditionalLog>();
                }

                // 指定されたログ番号までリストの要素を自動拡張する
                while (targetSection.conditionalLogs.Count <= logIndex)
                {
                    targetSection.conditionalLogs.Add(
                        new ConditionalLog()
                        {
                            conditions = new List<FlagConditionPro>(), // フラグ条件の空リストを生成
                            additionalText = "",
                        }
                    );
                }

                // 該当する要素のテキストのみを上書きする
                targetSection.conditionalLogs[logIndex].additionalText = textContent;
            }
            else
            {
                Debug.LogWarning(
                    $"<color=yellow>Additionalデータのインデックス解析に失敗しました。ProgressID: {(int)logData.logName}</color>"
                );
            }
        }
    }

    #endregion

    #region ▼ ユーティリティ (CSVパース)

    /// <summary>
    /// CSV形式の文字列をパースして2次元の文字列リストに変換します。
    /// セル内の改行やダブルクォーテーションで囲まれたカンマに対応する堅牢なパーサーです。
    /// </summary>
    private static List<string[]> ParseCsv(string csvText)
    {
        List<string[]> rows = new List<string[]>();
        List<string> currentRow = new List<string>();
        bool inQuotes = false;
        string currentValue = "";

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
#endif

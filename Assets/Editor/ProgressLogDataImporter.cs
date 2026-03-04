#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// このファイルはエディタ拡張のため、Editorフォルダ内に配置してください。

public class ProgressLogDataImporter : EditorWindow
{
    // =================================================================
    // スプレッドシート（CSV）の列番号定義
    // ※ 0始まりで列番号を指定します。スプレッドシートの構成が変わった場合はここを変更してください。
    // =================================================================
    private const int ColumnIndex_ProgressID = 0; // 進行度ID（int値、例: 16001）
    private const int ColumnIndex_TextType = 2; // テキストの種類（"Base" または "Additional"）
    private const int ColumnIndex_SectionIndex = 3; // セクション番号（Additionalの場合のみ使用）
    private const int ColumnIndex_LogIndex = 4; // ログ番号（Additionalの場合のみ使用）

    // private const int ColumnIndex_Memo = 4; // 管理用メモ（読み飛ばす用）
    private const int ColumnIndex_Text = 5; // 実際にゲームで表示・追記されるテキスト

    private TextAsset csvFile;
    private const string SaveKey_CsvGuid = "ProgressLogImporter_CsvGuid"; //EditorPrefsに保存するためのキー

    [MenuItem("Tools/進行度ログインポーター")]
    public static void ShowWindow()
    {
        // カスタムウィンドウを表示する
        GetWindow<ProgressLogDataImporter>("進行度ログインポーター");
    }

    private void OnEnable()
    {
        // 保存されているGUID（アセットの固有ID）を読み込む
        string savedGuid = EditorPrefs.GetString(SaveKey_CsvGuid, "");
        if (!string.IsNullOrEmpty(savedGuid))
        {
            // GUIDからアセットのパスを取得し、TextAssetとしてロードする
            string path = AssetDatabase.GUIDToAssetPath(savedGuid);
            if (!string.IsNullOrEmpty(path))
            {
                csvFile = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("進行度ログデータ(CSV)の読み込み", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck(); // 変更監視の開始

        // CSVファイルをインスペクターからドラッグ＆ドロップでセットできるようにする
        csvFile = (TextAsset)
            EditorGUILayout.ObjectField("CSVファイル", csvFile, typeof(TextAsset), false);

        if (EditorGUI.EndChangeCheck()) // ファイルのセット状況に変更があった場合
        {
            if (csvFile != null)
            {
                // アセットのパスを取得し、GUIDに変換してEditorPrefsに保存する
                string path = AssetDatabase.GetAssetPath(csvFile);
                string guid = AssetDatabase.AssetPathToGUID(path);
                EditorPrefs.SetString(SaveKey_CsvGuid, guid);
            }
            else
            {
                // 空になった場合は保存データを消去する
                EditorPrefs.DeleteKey(SaveKey_CsvGuid);
            }
        }
        if (GUILayout.Button("データを更新する"))
        {
            if (csvFile == null)
            {
                Debug.LogWarning("CSVファイルが選択されていません。");
                return;
            }

            ImportCsvData(csvFile.text);
        }
    }

    /// <summary>
    /// CSVのテキストデータを受け取り、プロジェクト内のアセットを更新します。
    /// </summary>
    private void ImportCsvData(string csvText)
    {
        // CSVのパース（ダブルクォーテーション内のカンマや改行に対応した自作パーサー）
        List<string[]> rows = ParseCsv(csvText);

        // プロジェクト内のすべての ProgressLogInfoData を取得
        string[] guids = AssetDatabase.FindAssets("t:ProgressLogInfoData");
        List<ProgressLogInfoData> allLogData = new List<ProgressLogInfoData>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ProgressLogInfoData data = AssetDatabase.LoadAssetAtPath<ProgressLogInfoData>(path);
            if (data != null)
            {
                allLogData.Add(data);
            }
        }

        int updateCount = 0;
        int notFoundCount = 0;

        // 見出し行をスキップするため i = 1 から開始（スプレッドシートの1行目はヘッダー前提）
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
                // 該当するID（enumをintにキャストした値）のアセットを探す
                ProgressLogInfoData targetData = allLogData.Find(x => (int)x.logName == progressID);

                if (targetData != null)
                {
                    // Trim() を追加して見えない空白を除去
                    string textType = columns[ColumnIndex_TextType].Trim();
                    string textContent = columns[ColumnIndex_Text]; // 本文は意図的な空白の可能性があるのでTrimしない

                    // テキスト内の "\n" という文字列を実際の改行に変換
                    textContent = textContent.Replace("\\n", "\n");

                    UpdateLogData(targetData, textType, columns, textContent);

                    // アセットが変更されたことをUnityに通知
                    EditorUtility.SetDirty(targetData);
                    updateCount++;
                }
                else
                {
                    notFoundCount++;
                    Debug.LogWarning(
                        $"行 {i + 1}: 進行度ID [{progressID}] のアセットが見つかりません。事前にCreateメニューから作成し、Enumを設定してください。"
                    );
                }
            }
            else
            {
                // 空行やパース失敗の場合は無視
                if (!string.IsNullOrEmpty(progressIdStr))
                {
                    Debug.LogWarning(
                        $"行 {i + 1}: 進行度IDの読み込みに失敗しました。数値ではありません: {progressIdStr}"
                    );
                }
            }
        }

        // 変更をディスクに保存
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"進行度ログの更新が完了しました。更新されたアセットの数: {updateCount}");
    }

    /// <summary>
    /// 実際のデータ書き換え処理。
    /// 指定されたインデックスの枠が無ければ自動で生成し、テキストのみを上書きします。
    /// （conditionsなどのフラグ設定は絶対に保持します）
    /// </summary>
    private void UpdateLogData(
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
                    $"Additionalデータのインデックス解析に失敗しました。ProgressID: {(int)logData.logName}"
                );
            }
        }
    }

    /// <summary>
    /// CSV形式の文字列をパースして2次元の文字列リストに変換します。
    /// セル内の改行やダブルクォーテーションで囲まれたカンマに対応する堅牢なパーサーです。
    /// </summary>
    private List<string[]> ParseCsv(string csvText)
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
                    // エスケープされたダブルクォートか、クォートの終わりか
                    if (i + 1 < csvText.Length && csvText[i + 1] == '\"')
                    {
                        currentValue += '\"';
                        i++; // 次のクォートをスキップ
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
                    // カンマで列を区切る
                    currentRow.Add(currentValue);
                    currentValue = "";
                }
                else if (c == '\n' || c == '\r')
                {
                    // CRLF対応の行区切り
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

        // 最後の要素を追加
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
}
#endif

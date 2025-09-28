using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(ProgressLogDatabase))]
public class ProgressLogDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 元のインスペクターを表示
        DrawDefaultInspector();

        var database = (ProgressLogDatabase)target;

        // ボタンを追加
        if (GUILayout.Button("新規進行ログを自動検索・追加"))
        {
            if (EditorUtility.DisplayDialog("データベース更新の確認",
                "指定フォルダから新しい進行ログを検索し、リストの末尾に追加します。よろしいですか？", "はい", "いいえ"))
            {
                AddNewLogs(database);
            }
        }
    }

    /// <summary>
    /// 新しい進行ログデータを検索し、リストに追加する
    /// </summary>
    private void AddNewLogs(ProgressLogDatabase database)
    {
        // 検索対象のフォルダパスを定義
        const string progressLogPath = "Assets/ProgressLogData";

        // 1. リスト内のnull参照をクリーンアップ
        database.logs.RemoveAll(item => item == null);

        // 2. 指定フォルダから全てのProgressLogInfoDataのアセットを検索
        string[] guids = AssetDatabase.FindAssets("t:ProgressLogInfoData", new[] { progressLogPath });
        
        int addedCount = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var logData = AssetDatabase.LoadAssetAtPath<ProgressLogInfoData>(path);

            // 3. リストにまだ存在しない場合のみ、末尾に追加する
            if (logData != null && !database.logs.Contains(logData))
            {
                database.logs.Add(logData);
                addedCount++;
            }
        }

        if (addedCount > 0)
        {   
            // 変更を保存
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log($"新しい進行ログを{addedCount}件、データベースに追加しました！");
        }
        else
        {
            Debug.Log("新しい進行ログは見つかりませんでした。");
        }
    }
}
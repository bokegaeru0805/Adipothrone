using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TipsInfoDatabase))]
public class TipsInfoDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 元のインスペクターを表示
        DrawDefaultInspector();

        var database = (TipsInfoDatabase)target;

        // ボタンを追加
        if (GUILayout.Button("新規Tipsを自動検索・追加"))
        {
            if (
                EditorUtility.DisplayDialog(
                    "データベース更新の確認",
                    "指定フォルダから新しいTipsを検索し、リストの末尾に追加します。よろしいですか？",
                    "はい",
                    "いいえ"
                )
            )
            {
                AddNewTips(database);
            }
        }
    }

    /// <summary>
    /// 新しいTipsデータを検索し、リストに追加する
    /// </summary>
    private void AddNewTips(TipsInfoDatabase database)
    {
        // 検索対象のフォルダパスを定義
        const string tipsInfoPath = "Assets/TipsInfoData";

        // 1. リスト内のnull参照をクリーンアップ
        database.tips.RemoveAll(item => item == null);

        // 2. 指定フォルダから全てのTipsInfoDataのアセットを検索
        string[] guids = AssetDatabase.FindAssets("t:TipsInfoData", new[] { tipsInfoPath });

        int addedCount = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var tipData = AssetDatabase.LoadAssetAtPath<TipsInfoData>(path);

            // 3. リストにまだ存在しない場合のみ、末尾に追加する
            if (tipData != null && !database.tips.Contains(tipData))
            {
                database.tips.Add(tipData);
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            // 変更を保存
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log($"新しいTipsを{addedCount}件、データベースに追加しました！");
        }
        else
        {
            Debug.Log("新しいTipsは見つかりませんでした。");
        }
    }
}

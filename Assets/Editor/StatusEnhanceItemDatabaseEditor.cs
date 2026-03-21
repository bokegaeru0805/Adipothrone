using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StatusEnhanceItemDatabase))]
public class StatusEnhanceItemDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var database = (StatusEnhanceItemDatabase)target;

        if (GUILayout.Button("新規ステータス強化アイテムを自動検索・追加"))
        {
            if (
                EditorUtility.DisplayDialog(
                    "データベース更新の確認",
                    "指定フォルダから新しいステータス強化アイテムを検索し、リストの末尾に追加します。よろしいですか？",
                    "はい",
                    "いいえ"
                )
            )
            {
                AddNewItems(database);
            }
        }
    }

    private void AddNewItems(StatusEnhanceItemDatabase database)
    {
        // 検索対象のフォルダパスを定義
        const string statusEnhanceItemPath = "Assets/ItemData/EnhanceItem";

        // 1. まず、リスト内のnull（削除されたアイテムなど）を除去してクリーンアップする
        int removedCount = database.statusEnhanceItems.RemoveAll(item => item == null);
        if (removedCount > 0)
        {
            Debug.Log($"リストから存在しないアイテムを{removedCount}件削除しました。");
        }

        // 2. 指定フォルダから全てのStatusEnhanceItemDataのアセットを検索
        string[] guids = AssetDatabase.FindAssets(
            "t:StatusEnhanceItemData",
            new[] { statusEnhanceItemPath }
        );

        int addedCount = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StatusEnhanceItemData item = AssetDatabase.LoadAssetAtPath<StatusEnhanceItemData>(path);

            // 3. 読み込んだアイテムがリストにまだ存在しない場合のみ、末尾に追加する
            if (item != null && !database.statusEnhanceItems.Contains(item))
            {
                database.statusEnhanceItems.Add(item);
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            // データベースのアセットに変更があったことをUnityに通知
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"新しいステータス強化アイテムを{addedCount}件、データベースに追加しました！"
            );
        }
        else
        {
            Debug.Log("新しいステータス強化アイテムは見つかりませんでした。");
        }
    }
}

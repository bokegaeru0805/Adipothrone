using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(KeyItemDatabase))]
public class KeyItemDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var database = (KeyItemDatabase)target;

        if (GUILayout.Button("新規重要アイテムを自動検索・追加"))
        {
            if (EditorUtility.DisplayDialog("データベース更新の確認",
                "指定フォルダから新しい重要アイテムを検索し、リストの末尾に追加します。よろしいですか？", "はい", "いいえ"))
            {
                AddNewItems(database);
            }
        }
    }

    private void AddNewItems(KeyItemDatabase database)
    {
        // 検索対象のフォルダパスを定義
        const string keyItemPath = "Assets/ItemData/KeyItemData";

        // 1. まず、リスト内のnull（削除されたアイテムなど）を除去してクリーンアップする
        int removedCount = database.keyItems.RemoveAll(item => item == null);
        if (removedCount > 0)
        {
            Debug.Log($"リストから存在しないアイテムを{removedCount}件削除しました。");
        }

        // 2. 指定フォルダから全てのKeyItemDataのアセットを検索
        string[] guids = AssetDatabase.FindAssets("t:KeyItemData", new[] { keyItemPath });
        
        int addedCount = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            KeyItemData item = AssetDatabase.LoadAssetAtPath<KeyItemData>(path);

            // 3. 読み込んだアイテムがリストにまだ存在しない場合のみ、末尾に追加する
            if (item != null && !database.keyItems.Contains(item))
            {
                database.keyItems.Add(item);
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            // データベースのアセットに変更があったことをUnityに通知
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log($"新しい重要アイテムを{addedCount}件、データベースに追加しました！");
        }
        else
        {
            Debug.Log("新しい重要アイテムは見つかりませんでした。");
        }
    }
}
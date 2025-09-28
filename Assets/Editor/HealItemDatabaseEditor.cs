using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(HealItemDatabase))]
public class HealItemDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var database = (HealItemDatabase)target;

        if (GUILayout.Button("新規ヒールアイテムを自動検索・追加"))
        {
            if (EditorUtility.DisplayDialog("データベース更新の確認",
                "指定フォルダから新しいヒールアイテムを検索し、リストの末尾に追加します。よろしいですか？", "はい", "いいえ"))
            {
                AddNewItems(database);
            }
        }
    }

    private void AddNewItems(HealItemDatabase database)
    {
        // 検索対象のフォルダパスを定義
        const string healItemPath = "Assets/ItemData/HealItemData";

        // 1. まず、リスト内のnull（削除されたアイテムなど）を除去してクリーンアップする
        int removedCount = database.healItems.RemoveAll(item => item == null);
        if (removedCount > 0)
        {
            Debug.Log($"リストから存在しないアイテムを{removedCount}件削除しました。");
        }

        // 2. 指定フォルダから全てのHealItemDataのアセットを検索
        string[] guids = AssetDatabase.FindAssets("t:HealItemData", new[] { healItemPath });
        
        int addedCount = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            HealItemData item = AssetDatabase.LoadAssetAtPath<HealItemData>(path);

            // 3. 読み込んだアイテムがリストにまだ存在しない場合のみ、末尾に追加する
            if (item != null && !database.healItems.Contains(item))
            {
                database.healItems.Add(item);
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            // データベースのアセットに変更があったことをUnityに通知
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log($"新しいヒールアイテムを{addedCount}件、データベースに追加しました！");
        }
        else
        {
            Debug.Log("新しいヒールアイテムは見つかりませんでした。");
        }
    }
}
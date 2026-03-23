using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RecipeItemDatabase))]
public class RecipeItemDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var database = (RecipeItemDatabase)target;

        if (GUILayout.Button("新規レシピアイテムを自動検索・追加"))
        {
            if (
                EditorUtility.DisplayDialog(
                    "データベース更新の確認",
                    "指定フォルダから新しいレシピアイテムを検索し、リストの末尾に追加します。よろしいですか？",
                    "はい",
                    "いいえ"
                )
            )
            {
                AddNewItems(database);
            }
        }
    }

    private void AddNewItems(RecipeItemDatabase database)
    {
        // 検索対象のフォルダパスを定義
        const string recipeItemPath = "Assets/ItemData/RecipeItemData";

        // 1. まず、リスト内のnull（削除されたアイテムなど）を除去してクリーンアップする
        int removedCount = database.recipeItems.RemoveAll(item => item == null);
        if (removedCount > 0)
        {
            Debug.Log($"リストから存在しないアイテムを{removedCount}件削除しました。");
        }

        // 2. 指定フォルダから全てのRecipeItemDataのアセットを検索
        string[] guids = AssetDatabase.FindAssets("t:RecipeItemData", new[] { recipeItemPath });

        int addedCount = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            RecipeItemData item = AssetDatabase.LoadAssetAtPath<RecipeItemData>(path);

            // 3. 読み込んだアイテムがリストにまだ存在しない場合のみ、末尾に追加する
            if (item != null && !database.recipeItems.Contains(item))
            {
                database.recipeItems.Add(item);
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            // データベースのアセットに変更があったことをUnityに通知
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log($"新しいレシピアイテムを{addedCount}件、データベースに追加しました！");
        }
        else
        {
            Debug.Log("新しいレシピアイテムは見つかりませんでした。");
        }
    }
}

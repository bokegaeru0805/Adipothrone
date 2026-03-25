using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// データベース更新の共通処理とID重複チェック機能を提供する基底クラス
/// </summary>
public abstract class BaseDatabaseEditor<TDatabase> : Editor
    where TDatabase : UnityEngine.Object
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var database = (TDatabase)target;

        if (GUILayout.Button(GetButtonText()))
        {
            if (
                EditorUtility.DisplayDialog(
                    "データベース更新の確認",
                    GetDialogMessage(),
                    "はい",
                    "いいえ"
                )
            )
            {
                ExecuteUpdate(database);
            }
        }
    }

    protected abstract string GetButtonText();
    protected abstract string GetDialogMessage();

    // 派生クラスで具体的な更新処理（対象フォルダやリストの指定）を実装する
    protected abstract void ExecuteUpdate(TDatabase database);

    /// <summary>
    /// 指定されたフォルダからアイテムを検索し、リストに追加しながらID重複をチェックする共通メソッド
    /// </summary>
    protected int ProcessTargetList<TItem>(
        string folderPath,
        List<TItem> targetList,
        Dictionary<System.Enum, string> globalIdDict
    )
        where TItem : ScriptableObject, IItemIDProvider
    {
        int removedCount = targetList.RemoveAll(item => item == null);
        if (removedCount > 0)
        {
            Debug.Log($"リストから存在しないアイテムを{removedCount}件削除しました。");
        }

        string[] guids = AssetDatabase.FindAssets($"t:{typeof(TItem).Name}", new[] { folderPath });
        int addedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TItem item = AssetDatabase.LoadAssetAtPath<TItem>(path);

            if (item == null)
                continue;

            System.Enum itemID = item.GetItemID();

            // 辞書を使った重複チェック
            if (globalIdDict.ContainsKey(itemID))
            {
                Debug.LogError(
                    $"【ID重複】ID: {itemID} が複数のアセットで使用されています。 ファイル1: '{globalIdDict[itemID]}', ファイル2: '{item.name}'"
                );
                continue; // 重複がある場合はリストに追加しない
            }

            globalIdDict.Add(itemID, item.name);

            if (!targetList.Contains(item))
            {
                targetList.Add(item);
                addedCount++;
            }
        }

        return addedCount;
    }

    /// <summary>
    /// 更新完了後の保存とログ出力
    /// </summary>
    protected void SaveDatabase(TDatabase database, int totalAddedCount, string itemName)
    {
        if (totalAddedCount > 0)
        {
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log($"新しい{itemName}を{totalAddedCount}件、データベースに追加しました！");
        }
        else
        {
            Debug.Log($"新しい{itemName}は見つかりませんでした。");
        }
    }
}

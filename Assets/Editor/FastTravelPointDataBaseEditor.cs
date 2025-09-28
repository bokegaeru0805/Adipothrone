using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(FastTravelPointDataBase))]
public class FastTravelPointDataBaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 元のインスペクターを表示
        DrawDefaultInspector();

        var database = (FastTravelPointDataBase)target;

        // ボタンを追加
        if (GUILayout.Button("新規ファストトラベルポイントを自動検索・追加"))
        {
            if (EditorUtility.DisplayDialog("データベース更新の確認",
                "指定フォルダから新しいファストトラベルポイントを検索し、リストの末尾に追加します。よろしいですか？", "はい", "いいえ"))
            {
                AddNewFastTravelPoints(database);
            }
        }
    }

    /// <summary>
    /// 新しいファストトラベルポイントのデータを検索し、リストに追加する
    /// </summary>
    private void AddNewFastTravelPoints(FastTravelPointDataBase database)
    {
        // 検索対象のフォルダパスを定義
        const string fastTravelPointPath = "Assets/FastTravelPointData";

        // 1. リスト内のnull参照をクリーンアップ
        database.fastTravelPoints.RemoveAll(item => item == null);

        // 2. 指定フォルダから全てのFastTravelPointDataのアセットを検索
        string[] guids = AssetDatabase.FindAssets("t:FastTravelPointData", new[] { fastTravelPointPath });
        
        int addedCount = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var pointData = AssetDatabase.LoadAssetAtPath<FastTravelPointData>(path);

            // 3. リストにまだ存在しない場合のみ、末尾に追加する
            if (pointData != null && !database.fastTravelPoints.Contains(pointData))
            {
                database.fastTravelPoints.Add(pointData);
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            // ID順でソート（任意）
            database.fastTravelPoints = database.fastTravelPoints.OrderBy(p => (int)p.fastTravelId).ToList();
            
            // 変更を保存
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log($"新しいファストトラベルポイントを{addedCount}件、データベースに追加しました！");
        }
        else
        {
            Debug.Log("新しいファストトラベルポイントは見つかりませんでした。");
        }
    }
}
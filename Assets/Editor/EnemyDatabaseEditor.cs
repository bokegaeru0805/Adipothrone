using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(EnemyDatabase))]
public class EnemyDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 元のインスペクター（リスト表示など）を描画
        DrawDefaultInspector();

        // 操作対象のEnemyDatabaseインスタンスを取得
        var database = (EnemyDatabase)target;

        // ボタンを追加
        if (GUILayout.Button("新規エネミーデータを自動検索・追加"))
        {
            // 確認ダイアログを表示
            if (EditorUtility.DisplayDialog("データベース更新の確認",
                "指定フォルダ（Assets/EnemyData）から新しいエネミーデータを検索し、リストの末尾に追加します。よろしいですか？", "はい", "いいえ"))
            {
                // はいが押されたら追加処理を実行
                AddNewEnemies(database);
            }
        }
    }

    /// <summary>
    /// 新しいエネミーのデータを検索し、リストに追加する
    /// </summary>
    private void AddNewEnemies(EnemyDatabase database)
    {
        // 検索対象のフォルダパスを定義
        const string enemyDataPath = "Assets/EnemyData";

        // 1. リスト内のnull参照（データが削除された項目など）をクリーンアップ
        database.enemies.RemoveAll(item => item == null);

        // 2. 指定フォルダから全てのEnemyData型のアセットを検索
        // "t:EnemyData"は「EnemyData型のものを探す」という意味
        string[] guids = AssetDatabase.FindAssets("t:EnemyData", new[] { enemyDataPath });
        
        int addedCount = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var enemyData = AssetDatabase.LoadAssetAtPath<EnemyData>(path);

            // 3. アセットが有効で、かつリストにまだ存在しない場合のみ、末尾に追加する
            if (enemyData != null && !database.enemies.Contains(enemyData))
            {
                database.enemies.Add(enemyData);
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            // 変更があった場合のみ実行
            
            // // 4. ID順でソート（EnemyDataにenemyIDというenumがあると仮定）
            // database.enemies = database.enemies.OrderBy(e => (int)e.enemyID).ToList();
            
            // 5. 変更をエディタに通知して保存
            EditorUtility.SetDirty(database); // 変更があったことをマーク
            AssetDatabase.SaveAssets();      // アセットの変更をディスクに保存
            Debug.Log($"新しいエネミーデータを{addedCount}件、データベースに追加しました！");
        }
        else
        {
            Debug.Log("新しいエネミーデータは見つかりませんでした。");
        }
    }
}
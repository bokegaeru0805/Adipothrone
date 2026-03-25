using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyDatabase))]
public class EnemyDatabaseEditor : BaseDatabaseEditor<EnemyDatabase>
{
    // 元のインスペクター（リスト表示など）を描画 (※基底クラスで処理)
    // 操作対象のEnemyDatabaseインスタンスを取得 (※基底クラスで処理)
    // ボタンを追加
    protected override string GetButtonText() => "新規エネミーデータを自動検索・追加";

    // 確認ダイアログを表示
    protected override string GetDialogMessage() =>
        "指定フォルダ（Assets/EnemyData）から新しいエネミーデータを検索し、リストの末尾に追加します。よろしいですか？";

    /// <summary>
    /// 新しいエネミーのデータを検索し、リストに追加する
    /// </summary>
    protected override void ExecuteUpdate(EnemyDatabase database)
    {
        // はいが押されたら追加処理を実行
        // 検索対象のフォルダパスを定義
        const string enemyDataPath = "Assets/EnemyData";
        var idCheckDict = new Dictionary<System.Enum, string>();

        // 1. リスト内のnull参照（データが削除された項目など）をクリーンアップ
        // 2. 指定フォルダから全てのEnemyData型のアセットを検索
        // "t:EnemyData"は「EnemyData型のものを探す」という意味
        // 3. アセットが有効で、かつリストにまだ存在しない場合のみ、末尾に追加する
        int addedCount = ProcessTargetList(enemyDataPath, database.enemies, idCheckDict);

        // 変更があった場合のみ実行
        // // 4. ID順でソート（EnemyDataにenemyIDというenumがあると仮定）
        // // database.enemies = database.enemies.OrderBy(e => (int)e.enemyID).ToList();

        // 5. 変更をエディタに通知して保存
        // 変更があったことをマーク
        // アセットの変更をディスクに保存
        SaveDatabase(database, addedCount, "エネミーデータ");
    }
}

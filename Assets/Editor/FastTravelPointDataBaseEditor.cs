using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FastTravelPointDataBase))]
public class FastTravelPointDataBaseEditor : BaseDatabaseEditor<FastTravelPointDataBase>
{
    // 元のインスペクターを表示 (※基底クラスで処理)
    // ボタンを追加
    protected override string GetButtonText() => "新規ファストトラベルポイントを自動検索・追加";

    protected override string GetDialogMessage() =>
        "指定フォルダから新しいファストトラベルポイントを検索し、リストの末尾に追加します。よろしいですか？";

    /// <summary>
    /// 新しいファストトラベルポイントのデータを検索し、リストに追加する
    /// </summary>
    protected override void ExecuteUpdate(FastTravelPointDataBase database)
    {
        // 検索対象のフォルダパスを定義
        const string fastTravelPointPath = "Assets/FastTravelPointData";
        var idCheckDict = new Dictionary<System.Enum, string>();

        // 1. リスト内のnull参照をクリーンアップ
        // 2. 指定フォルダから全てのFastTravelPointDataのアセットを検索
        // 3. リストにまだ存在しない場合のみ、末尾に追加する
        int addedCount = ProcessTargetList(
            fastTravelPointPath,
            database.fastTravelPoints,
            idCheckDict
        );

        // ID順でソート（任意） (※ソート機能は除外しました)
        // 変更を保存
        SaveDatabase(database, addedCount, "ファストトラベルポイント");
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HealItemDatabase))]
public class HealItemDatabaseEditor : BaseDatabaseEditor<HealItemDatabase>
{
    protected override string GetButtonText() => "新規回復アイテムを自動検索・追加";

    protected override string GetDialogMessage() =>
        "指定フォルダから新しい回復アイテムを検索し、リストの末尾に追加します。よろしいですか？";

    protected override void ExecuteUpdate(HealItemDatabase database)
    {
        // 検索対象のフォルダパスを定義
        const string healItemPath = "Assets/ItemData/HealItemData";
        var idCheckDict = new Dictionary<System.Enum, string>();

        // 1. まず、リスト内のnull（削除されたアイテムなど）を除去してクリーンアップする
        // 2. 指定フォルダから全てのHealItemDataのアセットを検索
        // 3. 読み込んだアイテムがリストにまだ存在しない場合のみ、末尾に追加する
        int addedCount = ProcessTargetList(healItemPath, database.healItems, idCheckDict);

        // データベースのアセットに変更があったことをUnityに通知
        SaveDatabase(database, addedCount, "回復アイテム");
    }
}

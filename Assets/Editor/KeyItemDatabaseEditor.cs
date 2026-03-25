using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(KeyItemDatabase))]
public class KeyItemDatabaseEditor : BaseDatabaseEditor<KeyItemDatabase>
{
    protected override string GetButtonText() => "新規重要アイテムを自動検索・追加";

    protected override string GetDialogMessage() =>
        "指定フォルダから新しい重要アイテムを検索し、リストの末尾に追加します。よろしいですか？";

    protected override void ExecuteUpdate(KeyItemDatabase database)
    {
        // 検索対象のフォルダパスを定義
        const string keyItemPath = "Assets/ItemData/KeyItemData";
        var idCheckDict = new Dictionary<System.Enum, string>();

        // 1. まず、リスト内のnull（削除されたアイテムなど）を除去してクリーンアップする
        // 2. 指定フォルダから全てのKeyItemDataのアセットを検索
        // 3. 読み込んだアイテムがリストにまだ存在しない場合のみ、末尾に追加する
        int addedCount = ProcessTargetList(keyItemPath, database.keyItems, idCheckDict);

        // データベースのアセットに変更があったことをUnityに通知
        SaveDatabase(database, addedCount, "重要アイテム");
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MaterialItemDatabase))]
public class MaterialItemDatabaseEditor : BaseDatabaseEditor<MaterialItemDatabase>
{
    protected override string GetButtonText() => "新規素材アイテムを自動検索・追加";

    protected override string GetDialogMessage() =>
        "指定フォルダから新しい素材アイテムを検索し、リストの末尾に追加します。よろしいですか？";

    protected override void ExecuteUpdate(MaterialItemDatabase database)
    {
        // 検索対象のフォルダパスを定義
        const string materialItemPath = "Assets/ItemData/MaterialItemData";
        var idCheckDict = new Dictionary<System.Enum, string>();

        // 1. まず、リスト内のnull（削除されたアイテムなど）を除去してクリーンアップする
        // 2. 指定フォルダから全てのMaterialItemDataのアセットを検索
        // 3. 読み込んだアイテムがリストにまだ存在しない場合のみ、末尾に追加する
        int addedCount = ProcessTargetList(materialItemPath, database.materialItems, idCheckDict);

        // データベースのアセットに変更があったことをUnityに通知
        SaveDatabase(database, addedCount, "素材アイテム");
    }
}

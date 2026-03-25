using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RecipeItemDatabase))]
public class RecipeItemDatabaseEditor : BaseDatabaseEditor<RecipeItemDatabase>
{
    protected override string GetButtonText() => "新規レシピアイテムを自動検索・追加";

    protected override string GetDialogMessage() =>
        "指定フォルダから新しいレシピアイテムを検索し、リストの末尾に追加します。よろしいですか？";

    protected override void ExecuteUpdate(RecipeItemDatabase database)
    {
        // 検索対象のフォルダパスを定義
        const string recipeItemPath = "Assets/ItemData/RecipeItemData";
        var idCheckDict = new Dictionary<System.Enum, string>();

        // 1. まず、リスト内のnull（削除されたアイテムなど）を除去してクリーンアップする
        // 2. 指定フォルダから全てのRecipeItemDataのアセットを検索
        // 3. 読み込んだアイテムがリストにまだ存在しない場合のみ、末尾に追加する
        int addedCount = ProcessTargetList(recipeItemPath, database.recipeItems, idCheckDict);

        // データベースのアセットに変更があったことをUnityに通知
        SaveDatabase(database, addedCount, "レシピアイテム");
    }
}

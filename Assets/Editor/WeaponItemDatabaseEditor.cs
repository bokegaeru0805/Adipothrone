using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponItemDatabase))]
public class WeaponItemDatabaseEditor : BaseDatabaseEditor<WeaponItemDatabase>
{
    // ボタンのテキストを機能に合わせて変更
    protected override string GetButtonText() => "新規武器を自動検索・追加";

    protected override string GetDialogMessage() =>
        "指定フォルダから新しい武器を検索し、リストの末尾に追加します。よろしいですか？";

    /// <summary>
    /// 指定されたフォルダ内から新しい武器データを検索し、データベースに追加するメソッド
    /// </summary>
    protected override void ExecuteUpdate(WeaponItemDatabase database)
    {
        // 検索対象のフォルダパスを定義
        const string shootWeaponPath = "Assets/WeaponData/shoot";
        const string bladeWeaponPath = "Assets/WeaponData/blade";

        int totalAddedCount = 0;
        var idCheckDict = new Dictionary<System.Enum, string>();

        // --- Shoot 武器の処理 ---
        // null除去
        totalAddedCount += ProcessTargetList(shootWeaponPath, database.shoots, idCheckDict);

        // --- Blade 武器の処理 ---
        // null除去
        totalAddedCount += ProcessTargetList(bladeWeaponPath, database.blades, idCheckDict);

        SaveDatabase(database, totalAddedCount, "武器");
    }
}

using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponItemDatabase))]
public class WeaponItemDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var database = (WeaponItemDatabase)target;

        // ボタンのテキストを機能に合わせて変更
        if (GUILayout.Button("新規武器を自動検索・追加"))
        {
            if (
                EditorUtility.DisplayDialog(
                    "データベース更新の確認",
                    "指定フォルダから新しい武器を検索し、リストの末尾に追加します。よろしいですか？",
                    "はい",
                    "いいえ"
                )
            )
            {
                AddNewWeapons(database);
            }
        }
    }

    /// <summary>
    /// 指定されたフォルダ内から新しい武器データを検索し、データベースに追加するメソッド
    /// </summary>
    private void AddNewWeapons(WeaponItemDatabase database)
    {
        // 検索対象のフォルダパスを定義
        const string shootWeaponPath = "Assets/WeaponData/shoot";
        const string bladeWeaponPath = "Assets/WeaponData/blade";

        int totalAddedCount = 0;

        // --- Shoot 武器の処理 ---
        database.shoots.RemoveAll(item => item == null); // null除去
        string[] shootGuids = AssetDatabase.FindAssets(
            "t:ShootWeaponData",
            new[] { shootWeaponPath }
        );
        foreach (string guid in shootGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ShootWeaponData weapon = AssetDatabase.LoadAssetAtPath<ShootWeaponData>(path);

            if (weapon != null && !database.shoots.Contains(weapon))
            {
                database.shoots.Add(weapon);
                totalAddedCount++;
            }
        }

        // --- Blade 武器の処理 ---
        database.blades.RemoveAll(item => item == null); // null除去
        string[] bladeGuids = AssetDatabase.FindAssets(
            "t:BladeWeaponData",
            new[] { bladeWeaponPath }
        );
        foreach (string guid in bladeGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BladeWeaponData weapon = AssetDatabase.LoadAssetAtPath<BladeWeaponData>(path);

            if (weapon != null && !database.blades.Contains(weapon))
            {
                database.blades.Add(weapon);
                totalAddedCount++;
            }
        }

        if (totalAddedCount > 0)
        {
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log($"新しい武器を{totalAddedCount}件、データベースに追加しました！");
        }
        else
        {
            Debug.Log("新しい武器は見つかりませんでした。");
        }
    }
}

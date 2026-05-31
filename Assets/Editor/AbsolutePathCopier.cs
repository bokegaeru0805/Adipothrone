using UnityEngine;
using UnityEditor;
using System.IO;

public static class AbsolutePathCopier
{
    // 選択されたアセットの絶対パスをクリップボードにコピーする
    [MenuItem("Assets/Copy Absolute Path %#c", false, 19)]
    private static void CopyAbsolutePath()
    {
        // 選択中のアセットのプロジェクト内相対パスを取得
        string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);

        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogWarning("パスを取得できませんでした。アセットが正しく選択されているか確認してください。");
            return;
        }

        // プロジェクトルートを基準とした絶対パスに変換
        string absolutePath = Path.GetFullPath(assetPath);

        // クリップボードにコピー
        EditorGUIUtility.systemCopyBuffer = absolutePath;

        // コンソールに結果を表示
        Debug.Log($"絶対パスをクリップボードにコピーしました:\n{absolutePath}");
    }

    // アセットが選択されている場合のみメニュー（とショートカット）を有効化する
    [MenuItem("Assets/Copy Absolute Path %#c", true)]
    private static bool ValidateCopyAbsolutePath()
    {
        return Selection.activeObject != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(Selection.activeObject));
    }
}
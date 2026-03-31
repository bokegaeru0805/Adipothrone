#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;
using System;
using UnityEngine;

/// <summary>
/// ビルド実行の直前に自動で呼び出され、現在の日時をResourcesフォルダに書き出すエディタ拡張です。
/// </summary>
public class BuildDatePreProcessor : IPreprocessBuildWithReport
{
    // 実行順序（0が最初）
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        // Resourcesフォルダが存在しない場合は自動作成する
        string resourcesPath = "Assets/Resources";
        if (!Directory.Exists(resourcesPath))
        {
            Directory.CreateDirectory(resourcesPath);
        }

        // 現在の日時を取得して、カンマ区切りの文字列にする (例: "2026,3,31")
        DateTime now = DateTime.Now;
        string dateString = $"{now.Year},{now.Month},{now.Day}";

        // Resourcesフォルダ内にテキストファイルとして保存（上書き）
        string filePath = resourcesPath + "/BuildDate.txt";
        File.WriteAllText(filePath, dateString);

        // Unityエディタにファイルの変更を認識させる
        AssetDatabase.Refresh();
        Debug.Log($"[BuildDatePreProcessor] ビルド日時を記録しました: {dateString}");
    }
}
#endif

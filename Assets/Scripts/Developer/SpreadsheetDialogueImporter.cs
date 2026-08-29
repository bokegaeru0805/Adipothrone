using Fungus;
using UnityEngine;

/// <summary>
/// スプレッドシートのUnityExportタブから書き出したCSVをFungusへ同期するための設定です。
/// 実際の同期処理はEditor拡張側でのみ実行します。
/// </summary>
public class SpreadsheetDialogueImporter : MonoBehaviour
{
    [Header("同期先")]
    public Flowchart targetFlowchart;

    [Header("Google SheetsのUnityExportタブからダウンロードしたCSV")]
    public TextAsset unityExportCsv;
}

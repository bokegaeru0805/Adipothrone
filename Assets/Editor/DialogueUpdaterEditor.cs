using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// DialogueUpdaterコンポーネントのInspectorの表示をカスタマイズするエディタ拡張クラス。
/// </summary>
[CustomEditor(typeof(DialogueUpdater))] // このエディタがどのクラスを対象にするかを指定
public class DialogueUpdaterEditor : Editor
{
    /// <summary>
    /// InspectorのGUIを描画する際にUnityから呼び出されるメソッド。
    /// </summary>
    public override void OnInspectorGUI()
    {
        // まず、元のInspectorの項目（FlowchartやcsvFilesなど）を全て表示する
        DrawDefaultInspector();

        // 対象のDialogueUpdaterスクリプトのインスタンスを取得
        DialogueUpdater updater = (DialogueUpdater)target;

        // スペースを少し空けて、見た目を整える
        EditorGUILayout.Space();

        // --- CSV自動登録ボタン ---
        // GUILayout.Buttonを使って、高さ30の見やすいボタンを描画
        if (GUILayout.Button("関連CSVを自動登録", GUILayout.Height(30)))
        {
            // ボタンが押されたら、CSVを検索・登録するメソッドを呼び出す
            AutoRegisterCsvFiles(updater);
        }

        EditorGUILayout.Space(); // ボタン間のスペース

        // --- ダイアログ更新ボタン ---
        // ボタンを描画する。if文で囲むことで、ボタンが押された瞬間に中身が実行される
        if (GUILayout.Button("CSVからダイアログを更新", GUILayout.Height(40)))
        {
            // ボタンが押されたら、UpdateDialogueメソッドを呼び出す
            updater.UpdateDialogue();
        }
    }

    /// <summary>
    /// 指定されたDialogueUpdaterのcsvFilesリストを自動で更新します。
    /// </summary>
    /// <param name="updater">対象のDialogueUpdaterインスタンス</param>
    private void AutoRegisterCsvFiles(DialogueUpdater updater)
    {
        // --- ガード節：必要なものが設定されていなければ処理を中断 ---
        if (updater.targetFlowchart == null)
        {
            Debug.LogError("参照先のFlowchartが設定されていません。CSVの自動登録を中断しました。");
            return;
        }

        // 1. Flowchartの名前から検索キーワードを抽出
        string flowchartName = updater.targetFlowchart.name;
        int underscoreIndex = flowchartName.IndexOf('_');
        
        string searchKeyword;
        if (underscoreIndex != -1)
        {
            // "Flowchart_Chapter1" -> "Chapter1" のように、"_"より後の部分をキーワードとする
            searchKeyword = flowchartName.Substring(underscoreIndex + 1);
        }
        else
        {
            // "_"が含まれていない場合は、Flowchart名全体をキーワードとする
            searchKeyword = flowchartName;
        }

        // 2. "Assets/Text" フォルダ内の全CSVファイルを検索
        string searchPath = "Assets/Text";
        // AssetDatabase.FindAssetsを使って、指定パス内のCSVファイル(.csv)のGUIDを全て取得
        string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { searchPath });
        
        if (guids.Length == 0)
        {
            Debug.LogWarning($"'{searchPath}' フォルダ内にCSVファイルが見つかりませんでした。");
            return;
        }

        // 3. 見つかったCSVの中から、名前にキーワードが含まれるものだけをリストアップ
        List<TextAsset> foundCsvFiles = new List<TextAsset>();
        foreach (string guid in guids)
        {
            // GUIDからアセットのパスを取得
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // パスからファイル名を取得
            string fileName = Path.GetFileNameWithoutExtension(path);

            // ファイル名に検索キーワードが含まれていれば、リストに追加
            if (fileName.Contains(searchKeyword))
            {
                TextAsset csvAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if(csvAsset != null)
                {
                    foundCsvFiles.Add(csvAsset);
                }
            }
        }

        // 4. updaterのcsvFilesリストを、見つかったファイルのリストで上書き
        if (foundCsvFiles.Count > 0)
        {
            // Undo（元に戻す）操作に対応させるため、変更を記録
            Undo.RecordObject(updater, "Auto-register CSV files");
            
            updater.csvFiles = foundCsvFiles;
            
            // 変更をエディタに通知して、表示を更新
            EditorUtility.SetDirty(updater);
            
            Debug.Log($"キーワード '{searchKeyword}' を含む {foundCsvFiles.Count}個のCSVファイルを自動登録しました。");
        }
        else
        {
            Debug.LogWarning($"キーワード '{searchKeyword}' を含むCSVファイルが見つかりませんでした。");
        }
    }
}
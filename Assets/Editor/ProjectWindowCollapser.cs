using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;

/// <summary>
/// Projectウィンドウのフォルダを「Assets直下」まで一括で折りたたむエディタ拡張。
/// ショートカットキー（Ctrl + Alt + C）で実行されます。
/// </summary>
[InitializeOnLoad]
public static class ProjectWindowCollapser
{
    // ショートカットキーの設定
    private const KeyCode TRIGGER_KEY = KeyCode.C;
    
    #region Initialization

    /// <summary>
    /// コンストラクタ。エディタ起動時やコンパイル後に自動実行され、イベントを登録します。
    /// </summary>
    static ProjectWindowCollapser()
    {
        // どのウィンドウで作業していても反応するように、複数のGUIイベントにフックする
        // 1. Projectウィンドウ描画時
        EditorApplication.projectWindowItemOnGUI += (guid, rect) => CheckShortcut();
        // 2. Hierarchyウィンドウ描画時
        EditorApplication.hierarchyWindowItemOnGUI += (id, rect) => CheckShortcut();
        // 3. Sceneビュー描画時
        SceneView.duringSceneGui += (sceneView) => CheckShortcut();
    }

    #endregion

    #region Input Handling

    /// <summary>
    /// ショートカットキーの入力を監視します。
    /// </summary>
    private static void CheckShortcut()
    {
        Event e = Event.current;
        
        // キーが押されたタイミングのみ判定
        if (e == null || e.type != EventType.KeyDown) return;

        // ショートカット: Ctrl + Alt + C
        // (Windows/Mac問わず、ControlとAltの同時押し)
        if (e.keyCode == TRIGGER_KEY && e.control && e.alt)
        {
            e.Use(); // イベントを消費して、他への干渉を防ぐ

            // 描画中のデータ変更によるエラー（Layoutエラー等）を防ぐため、
            // 処理を現在のフレームの描画終了後（delayCall）に予約する
            EditorApplication.delayCall -= CollapseAggressive;
            EditorApplication.delayCall += CollapseAggressive;
        }
    }

    #endregion

    #region Core Logic

    /// <summary>
    /// 全てのProjectウィンドウに対して折りたたみ処理を実行します。
    /// </summary>
    private static void CollapseAggressive()
    {
        // Unity内部クラス "UnityEditor.ProjectBrowser" の型情報を取得
        var assembly = Assembly.GetAssembly(typeof(UnityEditor.Editor));
        var projectBrowserType = assembly.GetType("UnityEditor.ProjectBrowser");
        
        // 非アクティブなものも含め、全てのProjectウィンドウのインスタンスを取得
        var projectBrowsers = Resources.FindObjectsOfTypeAll(projectBrowserType);

        foreach (var window in projectBrowsers)
        {
            // Projectウィンドウには「1カラム（リスト）」と「2カラム（ツリー）」の表示モードがあるため、
            // 両方の内部ツリー構造に対してリセット処理を試みる

            // 1. 1カラム表示用 または 2カラム右側 ("m_AssetTree")
            CollapseTree(window, projectBrowserType, "m_AssetTreeState", "m_AssetTree");
            
            // 2. 2カラム表示左側のフォルダツリー ("m_FolderTree")
            CollapseTree(window, projectBrowserType, "m_FolderTreeState", "m_FolderTree");

            // ウィンドウ個別の再描画
            (window as EditorWindow).Repaint();
        }

        // 念押しでエディタ全体のProjectWindow再描画を行う
        EditorApplication.RepaintProjectWindow();
        
        // 完了ログ（必要に応じてコメントアウト解除）
        // Debug.Log("Project Folders Collapsed (Kept Assets Open)");
    }

    /// <summary>
    /// 指定された内部ツリー構造に対して、展開状態を「Assetsフォルダのみ」にリセットします。
    /// リフレクションを使用してUnityエディタの内部APIにアクセスします。
    /// </summary>
    /// <param name="window">ProjectBrowserのインスタンス</param>
    /// <param name="windowType">ProjectBrowserの型情報</param>
    /// <param name="stateFieldName">TreeState（データ）を保持するフィールド名</param>
    /// <param name="treeFieldName">TreeViewController（表示制御）を保持するフィールド名</param>
    private static void CollapseTree(Object window, System.Type windowType, string stateFieldName, string treeFieldName)
    {
        try
        {
            // --- 手順1: 現在の展開状態（expandedIDs）を操作する ---

            // TreeStateフィールドを取得
            var stateField = windowType.GetField(stateFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (stateField == null) return;

            var state = stateField.GetValue(window);
            if (state == null) return;

            // expandedIDsプロパティを取得
            var expandedIDsProp = state.GetType().GetProperty("expandedIDs", BindingFlags.Instance | BindingFlags.Public);
            if (expandedIDsProp == null) return;

            // "Assets" フォルダのInstanceIDを取得
            // これをリストに残すことで、ルート（Assets）自体は閉じずに中身だけを閉じた状態にする
            int assetsFolderID = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets").GetInstanceID();
            
            // AssetsフォルダのIDだけが入ったリストを作成
            var newExpandedIDs = new List<int> { assetsFolderID };

            // 展開リストを上書き設定
            expandedIDsProp.SetValue(state, newExpandedIDs, null);


            // --- 手順2: 表示を強制的に更新（リロード）する ---
            // データの変更だけでは見た目が更新されない場合があるため、ViewControllerを叩いて再構築させる

            // TreeViewControllerフィールドを取得
            var treeField = windowType.GetField(treeFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (treeField == null) return;

            var treeController = treeField.GetValue(window);
            if (treeController == null) return;

            // TreeViewController.ReloadData() メソッドを呼び出す
            var reloadMethod = treeController.GetType().GetMethod("ReloadData", BindingFlags.Instance | BindingFlags.Public);
            if (reloadMethod != null)
            {
                reloadMethod.Invoke(treeController, null);
            }
        }
        catch (System.Exception)
        {
            // 内部APIへのアクセスはバージョンによって失敗する可能性があるため、
            // エラーが発生してもエディタを止めずに無視して続行する
        }
    }

    #endregion
}
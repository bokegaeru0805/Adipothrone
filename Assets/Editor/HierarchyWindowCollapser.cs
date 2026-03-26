using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Hierarchyウィンドウのオブジェクトを「シーンのルート直下」まで一括で折りたたむエディタ拡張。
/// ショートカットキー（Ctrl + Alt + H）で実行されます。
/// </summary>
[InitializeOnLoad]
public static class HierarchyWindowCollapser
{
    // ショートカットキーの設定 (Hierarchy の 'H')
    private const KeyCode TRIGGER_KEY = KeyCode.H;

    #region Initialization

    /// <summary>
    /// コンストラクタ。エディタ起動時やコンパイル後に自動実行され、イベントを登録します。
    /// </summary>
    static HierarchyWindowCollapser()
    {
        // どのウィンドウで作業していても反応するように、複数のGUIイベントにフックする
        EditorApplication.projectWindowItemOnGUI += (guid, rect) => CheckShortcut();
        EditorApplication.hierarchyWindowItemOnGUI += (id, rect) => CheckShortcut();
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
        if (e == null || e.type != EventType.KeyDown)
            return;

        // ショートカット: Ctrl + Alt + H
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
    /// 全てのHierarchyウィンドウに対して折りたたみ処理を実行します。
    /// </summary>
    private static void CollapseAggressive()
    {
        // Unity内部クラス "UnityEditor.SceneHierarchyWindow" の型情報を取得
        var assembly = Assembly.GetAssembly(typeof(UnityEditor.Editor));
        var hierarchyWindowType = assembly.GetType("UnityEditor.SceneHierarchyWindow");

        if (hierarchyWindowType == null)
            return;

        // 非アクティブなものも含め、全てのHierarchyウィンドウのインスタンスを取得
        var hierarchyWindows = Resources.FindObjectsOfTypeAll(hierarchyWindowType);

        foreach (var window in hierarchyWindows)
        {
            CollapseHierarchy(window, hierarchyWindowType);

            // ウィンドウ個別の再描画
            (window as EditorWindow).Repaint();
        }

        // 完了ログ（必要に応じてコメントアウト解除）
        // Debug.Log("ヒエラルキーのオブジェクトを折りたたみました");
    }

    /// <summary>
    /// 指定されたHierarchyウィンドウ内の全てのルートオブジェクトを再帰的に折りたたみます。
    /// リフレクションを使用してUnityエディタの内部APIにアクセスします。
    /// </summary>
    /// <param name="window">SceneHierarchyWindowのインスタンス</param>
    /// <param name="windowType">SceneHierarchyWindowの型情報</param>
    private static void CollapseHierarchy(Object window, System.Type windowType)
    {
        try
        {
            // Unityのバージョンによって、SetExpandedRecursiveメソッドの所属が異なるため両方に対応
            // 古いバージョン：SceneHierarchyWindow クラスに直接実装
            // 新しいバージョン：SceneHierarchyWindow 内の sceneHierarchy プロパティ（SceneHierarchyクラス）に実装

            object target = window;
            var method = windowType.GetMethod(
                "SetExpandedRecursive",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (method == null)
            {
                var property = windowType.GetProperty(
                    "sceneHierarchy",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );
                if (property != null)
                {
                    target = property.GetValue(window);
                    if (target != null)
                    {
                        method = target
                            .GetType()
                            .GetMethod(
                                "SetExpandedRecursive",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                            );
                    }
                }
            }

            // メソッドの取得に成功した場合のみ実行
            if (method != null && target != null)
            {
                // 現在開かれている全てのシーンを取得
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded)
                        continue;

                    // シーン内のルートオブジェクトを取得し、それぞれを再帰的に閉じる
                    // ※これにより、シーンヘッダー自体は閉じられず、中のオブジェクトだけが綺麗に畳まれます
                    var rootObjects = scene.GetRootGameObjects();
                    foreach (var go in rootObjects)
                    {
                        // 内部メソッド SetExpandedRecursive(int id, bool expand) を呼び出す
                        method.Invoke(target, new object[] { go.GetInstanceID(), false });
                    }
                }
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

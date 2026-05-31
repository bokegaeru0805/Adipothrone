using UnityEditor;
using UnityEngine;

/// <summary>
/// Project Settings内に独自の「MyGame Settings」パネルを追加し、
/// エディタ用の設定（ON/OFF）を一元管理するクラスです。
/// </summary>
public class MyGameSettingsProvider : SettingsProvider
{
    // 既存のスクリプトと互換性を持たせるため、同じEditorPrefsのキーを使用します
    private const string MAXIMIZE_PREFS_KEY = "Tools/Maximize On Play";
    private const string FORCE_TITLE_PREFS_KEY = "GameInitializer_Enabled";

    // ギズモ表示設定用の一括管理キー
    private const string GIZMO_VISIBILITY_KEY = "MyGame_ShowCustomGizmos";

    // コンストラクタ
    public MyGameSettingsProvider(string path, SettingsScope scope)
        : base(path, scope) { }

    // UnityにこのクラスがSettingsProviderであることを認識させる属性
    [SettingsProvider]
    public static SettingsProvider CreateSettingsProvider()
    {
        // "Project/MyGame Settings" が、左側のリストに表示される階層になります
        var provider = new MyGameSettingsProvider("Project/MyGame", SettingsScope.Project);

        // 検索窓で引っかかりやすくするためのキーワード設定
        provider.keywords = new string[]
        {
            "Maximize",
            "Title",
            "Scene",
            "Play",
            "MyGame",
            "Gizmo",
        };
        return provider;
    }

    // パネル内のGUI（見た目）を描画するメソッド
    public override void OnGUI(string searchContext)
    {
        // 少し見やすくするために余白とインデントを追加
        EditorGUILayout.Space();
        EditorGUI.indentLevel++;

        GUILayout.Label("プレイモード設定 (Play Mode Settings)", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // --- 1. Maximize On Play の設定 ---
        bool currentMaximize = EditorPrefs.GetBool(MAXIMIZE_PREFS_KEY, false);
        EditorGUI.BeginChangeCheck();
        bool newMaximize = EditorGUILayout.Toggle("Maximize On Play", currentMaximize);
        if (EditorGUI.EndChangeCheck())
        {
            // 値が変更されたら保存
            EditorPrefs.SetBool(MAXIMIZE_PREFS_KEY, newMaximize);
        }

        // --- 2. Force Title Scene On Play の設定 ---
        bool currentForceTitle = EditorPrefs.GetBool(FORCE_TITLE_PREFS_KEY, false);
        EditorGUI.BeginChangeCheck();
        bool newForceTitle = EditorGUILayout.Toggle("Force Title Scene On Play", currentForceTitle);
        if (EditorGUI.EndChangeCheck())
        {
            // 値が変更されたら保存
            EditorPrefs.SetBool(FORCE_TITLE_PREFS_KEY, newForceTitle);
        }

        // --- 3. ギズモ表示設定 ---
        EditorGUILayout.Space();
        GUILayout.Label("ギズモ表示設定 (Gizmo Visibility)", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // CameraMoveArea と EnemyActivator 等のカスタムギズモを一括管理するトグル
        bool currentGizmoVisibility = EditorPrefs.GetBool(GIZMO_VISIBILITY_KEY, true);
        EditorGUI.BeginChangeCheck();
        bool newGizmoVisibility = EditorGUILayout.Toggle(
            "Show Custom Gizmos",
            currentGizmoVisibility
        );
        if (EditorGUI.EndChangeCheck())
        {
            // 値が変更されたら保存し、シーンビューをすぐに更新
            EditorPrefs.SetBool(GIZMO_VISIBILITY_KEY, newGizmoVisibility);
            SceneView.RepaintAll();
        }

        EditorGUI.indentLevel--;
    }

    // =====================================================================
    // ショートカット設定 (ここから)
    // =====================================================================

    /// <summary>
    /// 設定パネルを開閉するためのショートカット機能。
    /// ウィンドウが既に開いている場合は閉じ、閉じている場合は開きます。
    /// % = Ctrl (Mac: Cmd), & = Alt (Mac: Option), # = Shift
    /// </summary>
    [MenuItem("Tools/MyGame/Settings %&s")]
    public static void ToggleMyGameSettings()
    {
        // Unity内部のProject Settingsウィンドウの型情報を取得
        System.Type windowType = typeof(UnityEditor.Editor).Assembly.GetType(
            "UnityEditor.ProjectSettingsWindow"
        );

        if (windowType != null)
        {
            // 既に開いているProject Settingsウィンドウが存在するか検索
            Object[] openWindows = Resources.FindObjectsOfTypeAll(windowType);

            // ウィンドウが見つかった場合は閉じて処理を終了する
            if (openWindows.Length > 0 && openWindows[0] is EditorWindow window)
            {
                window.Close();
                // Debug.Log("設定画面を閉じました。");
                return;
            }
        }

        // ウィンドウが開いていない場合は、指定した自作設定のパスで開く
        SettingsService.OpenProjectSettings("Project/MyGame");
        // Debug.Log("設定画面を開きました。");
    }
}

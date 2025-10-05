using UnityEditor;
using UnityEngine;

/// <summary>
/// Unityエディタの再生ボタンを押した際に、Gameビューを自動で最大化する機能を提供するクラス。
/// ToolsメニューからON/OFFを切り替えられます。
/// </summary>
[InitializeOnLoad] // Unityエディタ起動時にこのクラスのコンストラクタを呼び出す
public static class MaximizeOnPlay
{
    private const string MENU_NAME = "Tools/Maximize On Play";
    private static bool isEnabled;

    // 静的コンストラクタ。エディタ起動時やスクリプトコンパイル後に一度だけ呼ばれる
    static MaximizeOnPlay()
    {
        // EditorPrefsから設定を読み込み、メニューの状態を更新
        isEnabled = EditorPrefs.GetBool(MENU_NAME, false);
        UpdateMenuCheckedState();

        // プレイモードの状態が変化したときのイベントに、メソッドを登録
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    // Toolsメニューに項目を追加
    [MenuItem(MENU_NAME)]
    private static void ToggleAction()
    {
        // 現在の状態を反転させ、EditorPrefsに保存
        isEnabled = !isEnabled;
        EditorPrefs.SetBool(MENU_NAME, isEnabled);
        UpdateMenuCheckedState();
    }

    // メニュー項目のチェック状態を更新
    private static void UpdateMenuCheckedState()
    {
        Menu.SetChecked(MENU_NAME, isEnabled);
    }

    // プレイモードの状態が変化したときに呼ばれる
    // プレイモードの状態が変化したときに呼ばれる
    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // 機能が無効の場合は何もしない
        if (!isEnabled)
        {
            return;
        }

        // Gameビューのウィンドウタイプをリフレクションで取得
        var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        var gameView = EditorWindow.GetWindow(gameViewType);

        // 再生ボタンが押された瞬間に最大化する
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            gameView.maximized = true;
        }
        // 再生が停止し、編集モードに戻った瞬間に最大化を解除する
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            gameView.maximized = false;
        }
    }
}
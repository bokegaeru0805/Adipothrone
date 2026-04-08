using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ゲーム起動時に初期シーンを強制的にロードするかどうかを制御するクラス。
/// ToolsメニューからON/OFFを切り替えられます。
/// </summary>
public static class GameInitializer
{
    public static bool IsInitialized { get; private set; } = false;
    private const string FirstSceneName = GameConstants.SCENE_NAME_TITLE;

#if UNITY_EDITOR
    // 設定保存用のキーとメニューパスの定義
    private const string MENU_NAME = "Tools/Force Title Scene On Play";
    private const string PREFS_KEY = "GameInitializer_Enabled";

    // --- ここから下が実行時の処理 ---

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadStartScene()
    {
        // EditorPrefsから設定を確認（無効なら何もしない）
        if (!EditorPrefs.GetBool(PREFS_KEY, false))
        {
            // Debug.Log("GameInitializerは無効です。現在のシーンから直接開始します。");
            return;
        }

        // 現在のシーンがタイトルシーンでなければロードする
        if (SceneManager.GetActiveScene().name != FirstSceneName)
        {
            Debug.Log($"<color=cyan>[GameInitializer]</color> 設定が有効なため、{FirstSceneName} シーンから開始します。");
            SceneManager.LoadScene(FirstSceneName);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeAfterSceneLoad()
    {
        // ここでも設定をチェック
        if (!EditorPrefs.GetBool(PREFS_KEY, false))
        {
            return;
        }

        if (IsInitialized)
            return;

        if (SceneManager.GetActiveScene().name == FirstSceneName)
        {
            // SaveLoadManagerが存在する場合のみ実行などの安全策をとっても良い
            if (SaveLoadManager.instance != null)
            {
                SaveLoadManager.instance.DisableSave();
            }
            IsInitialized = true;
        }
    }
#endif
}
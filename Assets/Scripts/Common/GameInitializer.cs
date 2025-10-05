using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ゲーム起動時に初期シーンをロードし、シーンロード後に一度だけ初期化処理を実行するクラス。
/// Resourcesフォルダ内の'GameInitializeSettings'アセットから設定を読み込みます。
/// </summary>
public static class GameInitializer
{
    public static bool IsInitialized { get; private set; } = false;
    private const string FirstSceneName = GameConstants.SceneName_Title;
    
    // 設定ファイルのパス
    private const string SETTINGS_PATH = "GameInitializeSettings";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadStartScene()
    {
        // 設定ファイルを読み込む処理を追加
        var settings = Resources.Load<GameInitializeSettings>(SETTINGS_PATH);
        // 設定ファイルが存在し、かつ有効になっている場合のみ処理を実行
        if (settings == null || !settings.isEnabled)
        {
            Debug.Log("GameInitializerは無効です。現在のシーンから直接開始します。");
            return;
        }

        if (SceneManager.GetActiveScene().name != FirstSceneName)
        {
            SceneManager.LoadScene(FirstSceneName);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeAfterSceneLoad()
    {
        // ここでも設定をチェック
        var settings = Resources.Load<GameInitializeSettings>(SETTINGS_PATH);
        if (settings == null || !settings.isEnabled)
        {
            // 初期化機能が無効な場合は、IsInitializedフラグも立てない
            return;
        }
        
        if (IsInitialized) return;
        
        if (SceneManager.GetActiveScene().name == FirstSceneName)
        {
            SaveLoadManager.instance.DisableSave();
            IsInitialized = true;
        }
    }
}
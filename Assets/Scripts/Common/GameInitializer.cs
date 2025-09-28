using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ゲーム起動時に初期シーンをロードし、シーンロード後に一度だけ初期化処理を実行するクラス
/// </summary>
public static class GameInitializer
{
    /// <summary>
    /// 初期化処理が完了したか
    /// </summary>
    public static bool IsInitialized { get; private set; } = false;

    // 最初にロードしたいシーン名。
    // 元のスクリプトの GameConstants.SceneName_Title に相当します。
    // ご自身のプロジェクトのシーン名に合わせて変更してください。
    private const string FirstSceneName = GameConstants.SceneName_Title;

    //=================================================================================
    // 起動時処理
    //=================================================================================

    /// <summary>
    /// ゲーム開始時(シーン読み込み前)に実行される。
    /// 主に、どのシーンから再生しても必ず初期シーンから始まるようにする役割を持つ。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadStartScene()
    {
        // 現在のシーンが目的のシーンでなければロードする
        if (SceneManager.GetActiveScene().name != FirstSceneName)
        {
            SceneManager.LoadScene(FirstSceneName);
        }
    }

    /// <summary>
    /// シーンがロードされた後に実行される。
    /// ゲーム全体の初期化処理をここで行う。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeAfterSceneLoad()
    {
        // すでに初期化が完了している場合は、何もしない
        if (IsInitialized)
        {
            return;
        }

        // 現在のシーンが初期シーンの場合にのみ、初期化処理を実行する
        if (SceneManager.GetActiveScene().name == FirstSceneName)
        {
            // --- ここから初期化処理 ---
            // 元のStartupInitializerのStart()にあった処理です。
            SaveLoadManager.instance.DisableSave();

            // 初期化完了フラグを立てる
            IsInitialized = true;
        }
    }
}
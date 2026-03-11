using System.Collections;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ゲーム全体の時間（Time.timeScale）を安全に管理するシングルトンクラス。
/// 複数のシステムからのポーズリクエストをカウントで管理します。
/// </summary>
public class TimeManager : MonoBehaviour
{
#pragma warning disable 0414 // 使われていない変数の警告（CS0414）を一時的に無効化
    [InfoBox("このスクリプトはDebugSceneでも用います。\nそのため、プレハブしておいてください。")]
    [ReadOnly]
    [SerializeField]
    private string _instruction = "設定不要";
#pragma warning restore 0414 // 警告の無効化を解除（これ以降のコードでは通常通り警告を出す）

    // --- シングルトン実装 ---
    public static TimeManager instance { get; private set; }
    private UIManager uiManager = null;

    /// <summary>
    /// 敵の動きがポーズされているかどうかを示します。
    /// TimeManagerはシングルトンであり、この状態は唯一のインスタンスを通じて
    /// ゲーム全体で一意に管理されるため、staticにはしません。
    /// </summary>
    public bool isEnemyMovePaused { get; private set; } = false;
    private bool isDebugScene = false; // 開発用フラグ：デバッグシーンかどうか
    #region TimeScale Control Variables
    private int pauseRequestCount = 0; // ポーズリクエストのカウント（0ならポーズ解除）
    private bool isSkipping = false; // スキップ中かどうかのフラグ
    private float currentSkipScale = 1f; // 現在のスキップ倍率
    private float debugBaseTimeScale = 1.0f; // デバッグで設定する基本のゲームスピード
    #endregion

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(gameObject); //シーンが変わると破棄されるので、不要
#if UNITY_EDITOR
            // デバッグシーンかどうかを判定
            isDebugScene = SceneManager.GetActiveScene().name.Contains("Debug");
#endif
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetEnemyMovePaused(bool pause)
    {
        isEnemyMovePaused = pause;
    }

    /// <summary>
    /// ヒットストップ演出を開始します。外部からはこのメソッドを呼び出します。
    /// </summary>
    public void TriggerHitStop()
    {
        // 実際の処理は内部のプライベートなコルーチンに任せる
        StartCoroutine(DoHitStop());
    }

    /// <summary>
    /// ヒットストップの実際の処理を行うコルーチン。
    /// </summary>
    private IEnumerator DoHitStop()
    {
        if (uiManager == null)
        {
            uiManager = UIManager.instance;
            if (uiManager == null)
            {
                Debug.LogError("UIManagerが見つかりません。TimeManagerは正常に動作しません。");
                yield break;
            }
        }

        // UIが開いている場合はHitStopを行わない
        if (uiManager.isMenuOpen)
        {
            yield break;
        }

        // デバッグの基本速度を基準にヒットストップの速度を計算し、不自然な挙動を防ぐ
        Time.timeScale = 0.3f * debugBaseTimeScale;
        yield return new WaitForSecondsRealtime(0.2f);

        // ヒットストップ終了時にゲームがポーズされているか（UIが開かれたかなど）を再度チェック
        if (uiManager != null && uiManager.isMenuOpen)
        {
            yield break;
        }

        ReleasePause();
    }

    /// <summary>
    /// 時間の停止をリクエストします。
    /// </summary>
    public void RequestPause()
    {
        pauseRequestCount++;
        UpdateTimeScale();
    }

    /// <summary>
    /// 時間停止のリクエストを解除します。
    /// </summary>
    public void ReleasePause()
    {
        pauseRequestCount = Mathf.Max(0, pauseRequestCount - 1);
        UpdateTimeScale();
    }

    /// <summary>
    /// スキップ（時間加速）を開始します。TimelineSkipManagerから呼ばれます。
    /// </summary>
    public void StartSkip(float skipScale)
    {
        isSkipping = true;
        currentSkipScale = skipScale;
        UpdateTimeScale();
    }

    /// <summary>
    /// スキップ（時間加速）を終了します。TimelineSkipManagerから呼ばれます。
    /// </summary>
    public void StopSkip()
    {
        isSkipping = false;
        currentSkipScale = 1f;
        UpdateTimeScale();
    }

    /// <summary>
    /// 現在の各状態（ポーズ、スキップなど）の優先度に基づいて、実際のTime.timeScaleを決定・適用します。
    /// </summary>
    private void UpdateTimeScale()
    {
        // 優先度1: ポーズ中（UIが開いているなど）は問答無用で0
        if (pauseRequestCount > 0)
        {
            Time.timeScale = 0f;
            return;
        }

        // 優先度2: スキップ中（ポーズされていない場合のみ適用される）
        if (isSkipping)
        {
            // エディタエラーを防ぐため最大100に制限
            Time.timeScale = Mathf.Min(currentSkipScale, 100f);
            return;
        }

        // 優先度3: 通常状態（固定値の1.0fから、デバッグ用変数に変更）
        Time.timeScale = debugBaseTimeScale;
    }

    /// <summary>
    /// デバッグメニューから基本のゲームスピード（タイムスケール）を変更します。
    /// </summary>
    /// <param name="scale">変更後の倍率</param>
    public void SetDebugTimeScale(float scale)
    {
        // 0以下の値や極端な加速によるエラーを防ぐため、下限と上限を設定 (例: 0.1 ～ 10.0)
        debugBaseTimeScale = Mathf.Clamp(scale, 0.1f, 10.0f);

        // 設定後に再計算処理を呼ぶことで即座に反映させる
        UpdateTimeScale();
    }

#if UNITY_EDITOR
    private void Update()
    {
        // デバッグシーンの場合、キーボード入力で時間停止をテスト可能にする
        if (isDebugScene)
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                SetEnemyMovePaused(!isEnemyMovePaused);
                Debug.Log(
                    $"<color=yellow>TimeManager:</color> Enemy Move Paused set to {isEnemyMovePaused}"
                );
            }
        }
    }
#endif
}

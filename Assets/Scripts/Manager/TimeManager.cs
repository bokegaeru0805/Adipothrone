using System.Collections;
using UnityEngine;

/// <summary>
/// ゲーム全体の時間（Time.timeScale）を安全に管理するシングルトンクラス。
/// 複数のシステムからのポーズリクエストをカウントで管理します。
/// </summary>
public class TimeManager : MonoBehaviour
{
    public static TimeManager instance { get; private set; }
    private UIManager uiManager = null;

    /// <summary>
    /// 敵の動きがポーズされているかどうかを示します。
    /// TimeManagerはシングルトンであり、この状態は唯一のインスタンスを通じて
    /// ゲーム全体で一意に管理されるため、staticにはしません。
    /// </summary>
    public bool isEnemyMovePaused { get; private set; } = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(gameObject); //シーンが変わると破棄されるので、不要
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

        Time.timeScale = 0.3f;
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
        Time.timeScale = 0f;
    }

    /// <summary>
    /// 時間停止のリクエストを解除します。
    /// </summary>
    public void ReleasePause()
    {
        Time.timeScale = 1f;
    }
}

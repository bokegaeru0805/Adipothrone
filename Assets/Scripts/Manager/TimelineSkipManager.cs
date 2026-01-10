using System.Collections;
using Fungus;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 時間制御を一括管理するクラス。
/// Timelineの倍速再生（早送り）と、システム全体のごく短時間での高速処理（スキップ）を提供します。
/// </summary>
public class TimelineSkipManager : MonoBehaviour
{
    public static TimelineSkipManager instance { get; private set; }

    [Header("Global Skip Settings (T Key)")]
    [Tooltip("全スキップ中のタイムスケール倍率")]
    [SerializeField]
    private float skipTimeScale = 50.0f;

    [Tooltip("全スキップ開始/終了時のフェード時間")]
    [SerializeField]
    private float fadeDuration = 0.5f;

    [Header("Local FastForward Settings (Z Key)")]
    [Tooltip("早送り時のTimeline倍率")]
    [SerializeField]
    private float fastForwardSpeed = 3.0f;

    // 現在スキップ中（Tキー）かどうか
    public bool IsSkipping { get; private set; } = false;

    // 現在早送り中（Zキー）かどうか
    public bool IsFastForwarding { get; private set; } = false;

    // 現在アクティブな（再生中の）Director
    private PlayableDirector activeDirector;
    private double defaultDirectorSpeed = 1.0;
    private float defaultFixedDeltaTime;

    // --- 内部変数 ---
    private bool isTalking = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        defaultFixedDeltaTime = Time.fixedDeltaTime;
    }

    private void OnEnable()
    {
        // 会話終了コマンドが走ったらスキップを止めるイベント購読
        TalkEndCommand.OnTalkEndExecuted += StopSkip;
        // GameManagerの会話状態変更イベントを購読
        GameManager.OnTalkingStateChanged += HandleTalkingStateChanged;
    }

    private void OnDisable()
    {
        TalkEndCommand.OnTalkEndExecuted -= StopSkip;
        GameManager.OnTalkingStateChanged -= HandleTalkingStateChanged;
    }

    #region PlayableDirector管理
    /// <summary>
    /// 制御対象のPlayableDirectorを登録する
    /// </summary>
    /// <param name="director">登録するPlayableDirector</param>
    public void RegisterDirector(PlayableDirector director)
    {
        activeDirector = director;
        if (director.playableGraph.IsValid())
        {
            defaultDirectorSpeed = director.playableGraph.GetRootPlayable(0).GetSpeed();
        }
        else
        {
            defaultDirectorSpeed = 1.0;
        }

        // 新しいTimelineになったら早送り状態はリセット
        IsFastForwarding = false;
    }

    /// <summary>
    /// PlayableDirectorの登録を解除する
    /// </summary>
    /// <param name="director">登録解除するPlayableDirector</param>
    public void UnregisterDirector(PlayableDirector director)
    {
        if (activeDirector == director)
        {
            activeDirector = null;
            IsFastForwarding = false;

            // 安全策：ミュート解除
            if (SEManager.instance != null)
                SEManager.instance.IsTimelineMuted = false;
        }
    }

    #endregion
    private void Update()
    {
        // 全スキップ中もしくは会話中でない場合は入力を受け付けない
        if (!isTalking || IsSkipping)
            return;

        // --- Zキー: ローカル早送り (Timeline速度変更) ---
        if (Input.GetKeyDown(KeyCode.Z))
            SetLocalFastForward(true);
        if (Input.GetKeyUp(KeyCode.Z))
            SetLocalFastForward(false);

        if (Input.GetKeyDown(KeyCode.T))
        {
            if (
                FungusSkipController.instance != null
                && FungusSkipController.instance.IsSkipAllowed()
            )
            {
                StartGlobalSkip();
                Debug.Log("Global Skip Started");
            }
        }
    }

    #region 早送り制御
    /// <summary>
    /// PlayableDirectorの早送りを設定する
    /// </summary>
    /// <param name="active">早送りを有効にするかどうか</param>
    public void SetLocalFastForward(bool active)
    {
        if (activeDirector == null || !activeDirector.playableGraph.IsValid())
            return;

        if (active)
        {
            if (!IsFastForwarding)
            {
                IsFastForwarding = true;
                if (SEManager.instance != null)
                    SEManager.instance.IsTimelineMuted = true;
                activeDirector.playableGraph.GetRootPlayable(0).SetSpeed(fastForwardSpeed);
            }
        }
        else
        {
            if (IsFastForwarding)
            {
                IsFastForwarding = false;
                if (SEManager.instance != null)
                    SEManager.instance.IsTimelineMuted = false;
                activeDirector.playableGraph.GetRootPlayable(0).SetSpeed(defaultDirectorSpeed);
            }
        }
    }
    #endregion

    #region スキップ制御
    /// <summary>
    /// 全スキップを開始する
    /// </summary>
    public void StartGlobalSkip()
    {
        if (IsSkipping)
            return;
        StartCoroutine(SkipRoutine());
    }

    /// <summary>
    /// 全スキップを停止する
    /// </summary>
    public void StopSkip()
    {
        if (!IsSkipping)
            return;
        IsSkipping = false; // ループを抜けるフラグ
    }

    /// <summary>
    /// 全スキップのコルーチン
    /// </summary>
    /// <returns></returns>
    private IEnumerator SkipRoutine()
    {
        IsSkipping = true;

        // ローカル早送り中なら一旦解除しておく
        if (IsFastForwarding)
            SetLocalFastForward(false);

        // プレイヤーの物理移動を停止する
        PlayerManager.instance.SetPlayerPhysicsActive(false);

        // 1. フェードアウト
        if (FadeCanvas.instance != null)
        {
            FadeCanvas.instance.FadeOut(fadeDuration);
            yield return new WaitForSecondsRealtime(fadeDuration);
        }

        // 2. 音声ミュート
        float originalVolume = AudioListener.volume;
        AudioListener.volume = 0f;

        // 3. 時間加速
        Time.timeScale = skipTimeScale;
        Time.fixedDeltaTime = 0.02f * skipTimeScale; // 物理演算の破綻防止

        // 4. 高速ループ
        while (IsSkipping)
        {
            // Fungusの高速処理
            if (FungusSkipController.instance != null)
            {
                FungusSkipController.instance.UpdateSkipProcessing();

                if (!FungusSkipController.instance.IsSkipAllowed())
                {
                    IsSkipping = false;
                }
            }

            // Timelineが終わっているかどうかの監視が必要ならここに追加
            // (基本はTalkEndCommandがStopSkipを呼んでくれるので不要)

            yield return null;
        }

        // 5. 復帰
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;
        AudioListener.volume = originalVolume;

        // --- プレイヤーの物理挙動を復元する ---
        PlayerManager.instance.SetPlayerPhysicsActive(true);

        // 6. フェードイン
        if (FadeCanvas.instance != null)
        {
            FadeCanvas.instance.FadeIn(fadeDuration);
        }

        //Debug.Log("Global Skip Ended");
    }
    #endregion

    /// <summary>
    /// GameManagerから会話状態の変更通知を受け取る
    /// </summary>
    private void HandleTalkingStateChanged(bool talkState)
    {
        isTalking = talkState;
    }
}

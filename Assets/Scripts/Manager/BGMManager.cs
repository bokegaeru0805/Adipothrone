using System.Collections;
using CriWare;
using CriWare.Assets;
using UnityEngine;

/// <summary>
/// CRIWARE (ADX2) を使用してBGMを管理するクラス。
/// 再生、フェード、ダッキング（AISAC）、Block遷移を管理します。
/// </summary>
public class BGMManager : MonoBehaviour
{
    #region 定数・設定

    private const string DUCKING_AISAC_NAME = "DuckingControl";
    private const string BGM_CATEGORY_NAME = "BGM";

    [Header("BGMのACBアセット")]
    [SerializeField]
    private CriAtomAcbAsset bgmAcbAsset;

    [SerializeField]
    [Range(0.0f, 1.0f)]
    private float duckingLevel = 0.5f;

    #endregion

    #region 再生状態

    public static BGMManager instance { get; private set; }

    private PlaybackSlot player1;
    private PlaybackSlot player2;
    private PlaybackSlot currentPlayer;
    private BGMCategory currentCategory = BGMCategory.None;
    private Coroutine activeFadeCoroutine;

    #endregion

    #region Unityライフサイクル

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this.gameObject);
        }

        if (bgmAcbAsset == null)
        {
            Debug.LogError("BGMManager: BGMのACBアセットが設定されていません。");
        }
    }

    private void Start()
    {
        // CRIWAREの初期化完了後に生成できるよう、AwakeではなくStartで初期化します。
        player1 = new PlaybackSlot();
        player2 = new PlaybackSlot();
        currentPlayer = null;
    }

    private void OnDestroy()
    {
        player1?.Dispose();
        player2?.Dispose();
        player1 = null;
        player2 = null;
        currentPlayer = null;
    }

    #endregion

    #region 再生・フェード制御

    /// <summary>
    /// 指定したBGMを再生します。再生中の場合はクロスフェードします。
    /// </summary>
    /// <param name="category">再生したいBGMのカテゴリ</param>
    public void Play(BGMCategory category)
    {
        if (currentCategory == category && IsPlayerActive(currentPlayer))
        {
            return;
        }

        if (!TryGetPlayableCueName(category, out string bgmName))
        {
            return;
        }

        if (!IsPlayerActive(currentPlayer))
        {
            currentPlayer = player1;
            currentPlayer.Player.SetVolume(1.0f);
            currentPlayer.Player.SetCue(bgmAcbAsset.Handle, bgmName);
            currentPlayer.Start();
            currentCategory = category;
        }
        else
        {
            StartCrossfadeInternal(category, 1.0f);
        }
    }

    /// <summary>
    /// 現在のBGMをフェードせずに停止します。
    /// </summary>
    public void Stop()
    {
        if (activeFadeCoroutine != null)
        {
            StopCoroutine(activeFadeCoroutine);
            activeFadeCoroutine = null;
        }

        player1.Stop();
        player2.Stop();
        currentPlayer = null;
        currentCategory = BGMCategory.None;
    }

    /// <summary>
    /// 指定したBGMをフェードインで再生します。
    /// すでにBGMが再生中の場合は、クロスフェードに処理を切り替えます。
    /// </summary>
    /// <param name="category">再生したいBGMのカテゴリ</param>
    /// <param name="duration">フェードインにかける時間（秒）</param>
    public void FadeIn(BGMCategory category, float duration)
    {
        if (currentCategory == category && activeFadeCoroutine == null)
            return;

        if (IsPlayerActive(currentPlayer))
        {
            StartCrossfadeInternal(category, duration);
        }
        else
        {
            if (activeFadeCoroutine != null)
            {
                StopCoroutine(activeFadeCoroutine);
            }
            player1.Stop();
            player2.Stop();
            activeFadeCoroutine = StartCoroutine(FadeInCoroutine(category, duration));
        }
    }

    /// <summary>
    /// フェードイン処理を行うコルーチン
    /// </summary>
    private IEnumerator FadeInCoroutine(BGMCategory category, float duration)
    {
        if (!TryGetPlayableCueName(category, out string bgmName))
        {
            activeFadeCoroutine = null;
            yield break;
        }

        currentPlayer = player1;

        currentPlayer.Player.SetCue(bgmAcbAsset.Handle, bgmName);
        currentPlayer.Player.SetVolume(0.0f);
        currentPlayer.Start();

        currentCategory = category;

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime; // Time.timeScaleの影響を受けない時間で計測
            float progress = Mathf.Clamp01(timer / duration);

            currentPlayer.Player.SetVolume(progress);
            currentPlayer.Player.UpdateAll();

            yield return null;
        }

        currentPlayer.Player.SetVolume(1.0f);
        currentPlayer.Player.UpdateAll();

        activeFadeCoroutine = null;
    }

    /// <summary>
    /// 現在のBGMをフェードアウトしながら停止します
    /// </summary>
    public void FadeOut(float duration)
    {
        if (IsPlayerActive(currentPlayer))
        {
            if (activeFadeCoroutine != null)
            {
                StopCoroutine(activeFadeCoroutine);
            }
            // フェードアウト中に使われていない方のプレイヤーを確実に止める
            PlaybackSlot otherPlayer = (currentPlayer == player1) ? player2 : player1;
            otherPlayer.Stop();

            activeFadeCoroutine = StartCoroutine(FadeOutCoroutine(duration));
        }
    }

    /// <summary>
    /// フェードアウト処理を行うコルーチン
    /// </summary>
    private IEnumerator FadeOutCoroutine(float duration)
    {
        PlaybackSlot playerToFade = currentPlayer;
        // 外部からはフェード開始時点で停止中として扱います。
        currentCategory = BGMCategory.None;

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime; // ゲームの時間停止に影響されないようにする;
            float progress = Mathf.Clamp01(timer / duration);

            playerToFade.Player.SetVolume(1.0f - progress);
            playerToFade.Player.UpdateAll();
            yield return null;
        }

        playerToFade.Stop();
        activeFadeCoroutine = null;
    }

    /// <summary>
    /// 指定したBGMにクロスフェードします
    /// </summary>
    /// <param name="newCategory">クロスフェード先のBGMカテゴリ</param>
    /// <param name="crossfadeDuration">クロスフェードにかける時間（秒）</param>
    public void Crossfade(BGMCategory newCategory, float crossfadeDuration = 1.0f)
    {
        if (currentCategory == newCategory && activeFadeCoroutine == null)
            return;

        // 停止中ならFadeInとして扱う
        if (!IsPlayerActive(currentPlayer))
        {
            FadeIn(newCategory, crossfadeDuration);
        }
        else
        {
            // 再生中なら安全なクロスフェード処理を呼び出す
            StartCrossfadeInternal(newCategory, crossfadeDuration);
        }
    }

    /// <summary>
    /// クロスフェードを開始するための安全な内部メソッド
    /// </summary>
    private void StartCrossfadeInternal(BGMCategory newCategory, float duration)
    {
        if (!TryGetPlayableCueName(newCategory, out _))
        {
            return;
        }

        if (activeFadeCoroutine != null)
        {
            StopCoroutine(activeFadeCoroutine);
        }

        // 2. 現在再生を担当していない方のプレイヤー（古いフェードイン担当）を特定して停止する
        // これにより、中途半端にフェードインしていた曲が確実に止まる
        if (IsPlayerActive(currentPlayer))
        {
            PlaybackSlot playerToStop = (currentPlayer == player1) ? player2 : player1;
            playerToStop.Stop();
        }

        activeFadeCoroutine = StartCoroutine(CrossfadeCoroutine(newCategory, duration));
    }

    /// <summary>
    /// クロスフェード処理を行うコルーチン
    /// </summary>
    private IEnumerator CrossfadeCoroutine(BGMCategory newCategory, float crossfadeDuration)
    {
        PlaybackSlot fadeInPlayer = (currentPlayer == player1) ? player2 : player1;
        PlaybackSlot fadeOutPlayer = currentPlayer;

        // クロスフェードの開始直後に、メインプレイヤーの判定を新しい曲（fadeInPlayer）に切り替える。
        // Time.timeScaleが2.0などでFungusが高速進行し、フェード完了前に次の曲が割り込んできた場合でも、
        // 古い曲ではなく「現在フェードイン中の最新の曲」を正しく基準にするため。
        currentPlayer = fadeInPlayer;
        currentCategory = newCategory;

        BGMData.TryGetCueName(newCategory, out string cueName);
        fadeInPlayer.Player.SetCue(bgmAcbAsset.Handle, cueName);
        fadeInPlayer.Player.SetVolume(0.0f);
        fadeInPlayer.Start();

        float timer = 0f;
        while (timer < crossfadeDuration)
        {
            timer += Time.unscaledDeltaTime; // ゲームの時間停止に影響されないようにする;
            float progress = Mathf.Clamp01(timer / crossfadeDuration);

            fadeOutPlayer.Player.SetVolume(1.0f - progress);
            fadeInPlayer.Player.SetVolume(progress);

            fadeOutPlayer.Player.UpdateAll();
            fadeInPlayer.Player.UpdateAll();

            yield return null;
        }

        fadeOutPlayer.Stop();
        fadeInPlayer.Player.SetVolume(1.0f);
        currentPlayer = fadeInPlayer;
        currentCategory = newCategory;

        activeFadeCoroutine = null;
    }

    #endregion

    #region 状態・音量・Block制御

    /// <summary>
    /// CRIWAREのBGMカテゴリ全体の音量を設定します。
    /// </summary>
    /// <param name="ratio">設定する音量</param>
    public void AdjustAllVolume(float ratio)
    {
        CriAtom.SetCategoryVolume(BGM_CATEGORY_NAME, ratio);
    }

    /// <summary>
    /// 指定したBGMが現在再生中かどうかを確認します。
    /// </summary>
    /// <param name="category">確認するBGMの識別子</param>
    /// <returns>指定したBGMが再生中または再生準備中の場合はtrue</returns>
    public bool IsPlayingCategory(BGMCategory category)
    {
        return currentCategory == category && IsPlayerActive(currentPlayer);
    }

    /// <summary>
    /// 現在再生中のBGMを、ブロックシーケンス内の次のBlockへ遷移させます。
    /// 実際の遷移タイミングはAtom Craft側の設定に従います。
    /// </summary>
    /// <returns>遷移要求を受け付けた場合はtrue、それ以外はfalse</returns>
    public bool TryTransitionToNextBlock()
    {
        if (!TryGetBlockTransitionContext(
                out CriAtomExPlayback playback,
                out CriAtomEx.CueInfo cueInfo))
        {
            return false;
        }

        int currentBlockIndex = playback.GetCurrentBlockIndex();
        if (currentBlockIndex < 0)
        {
            Debug.LogWarning("BGMManager: 現在のBGMはBlock遷移に対応していないか、Block情報を取得できません。");
            return false;
        }

        int nextBlockIndex = currentBlockIndex + 1;
        if (nextBlockIndex >= cueInfo.numBlocks)
        {
            return false;
        }

        playback.SetNextBlockIndex(nextBlockIndex);
        return true;
    }

    /// <summary>
    /// 現在再生中のBGMを、指定したBlockへ遷移させます。
    /// 実際の遷移タイミングはAtom Craft側の設定に従います。
    /// </summary>
    /// <param name="blockIndex">遷移先のBlock Index</param>
    /// <returns>遷移要求を受け付けた場合はtrue、それ以外はfalse</returns>
    public bool TryTransitionToBlock(int blockIndex)
    {
        if (!TryGetBlockTransitionContext(
                out CriAtomExPlayback playback,
                out CriAtomEx.CueInfo cueInfo))
        {
            return false;
        }

        if (blockIndex < 0 || blockIndex >= cueInfo.numBlocks)
        {
            return false;
        }

        playback.SetNextBlockIndex(blockIndex);
        return true;
    }

    /// <summary>
    /// CRIWAREのBGMカテゴリ全体の音量を取得します。
    /// </summary>
    /// <returns>現在設定されている音量</returns>
    public float GetAllVolume()
    {
        return CriAtom.GetCategoryVolume(BGM_CATEGORY_NAME);
    }

    /// <summary>
    /// 会話時などにBGM音量を下げる（ダッキング）かどうかを設定します。
    /// </summary>
    /// <param name="isDucking">trueで音量を下げ、falseで元の音量に戻す</param>
    public void SetDucking(bool isDucking)
    {
        if (currentPlayer == null)
        {
            return;
        }

        float targetValue = isDucking ? duckingLevel : 0.0f;

        currentPlayer.Player.SetAisacControl(DUCKING_AISAC_NAME, targetValue);
        currentPlayer.Player.UpdateAll();
    }

    #endregion

    #region Player・Playback管理

    /// <summary>
    /// プレイヤーが再生中、または再生準備中かどうかを判定するヘルパーメソッド
    /// </summary>
    private bool IsPlayerActive(PlaybackSlot player)
    {
        if (player == null)
        {
            return false;
        }

        CriAtomExPlayer.Status status = player.Player.GetStatus();
        return status == CriAtomExPlayer.Status.Playing || status == CriAtomExPlayer.Status.Prep;
    }

    /// <summary>
    /// Block遷移に必要な再生音とCue情報を取得します。
    /// </summary>
    private bool TryGetBlockTransitionContext(
        out CriAtomExPlayback playback,
        out CriAtomEx.CueInfo cueInfo)
    {
        playback = default;
        cueInfo = default;

        if (!IsPlayerActive(currentPlayer) || !currentPlayer.TryGetPlayback(out playback))
        {
            return false;
        }

        if (!TryGetPlayableCueName(currentCategory, out string bgmName)
            || !bgmAcbAsset.Handle.GetCueInfo(bgmName, out cueInfo))
        {
            Debug.LogError("BGMManager: 現在のBGMのCue情報を取得できません。");
            return false;
        }

        if (cueInfo.numBlocks == 0)
        {
            Debug.LogWarning("BGMManager: 現在のBGMはBlock遷移に対応していません。");
            return false;
        }

        return true;
    }

    /// <summary>
    /// BGMDataへの登録とACB内のCueの存在を確認し、再生可能なCue名を取得します。
    /// </summary>
    private bool TryGetPlayableCueName(BGMCategory category, out string cueName)
    {
        cueName = null;

        if (!BGMData.TryGetCueName(category, out cueName) || string.IsNullOrEmpty(cueName))
        {
            Debug.LogError(
                $"BGMManager: BGMCategory '{category}' に対応するCue名がBGMDataに登録されていません。"
            );
            return false;
        }

        if (bgmAcbAsset == null || bgmAcbAsset.Handle == null)
        {
            Debug.LogError("BGMManager: BGMのACBアセットを参照できません。");
            return false;
        }

        if (!bgmAcbAsset.Handle.Exists(cueName))
        {
            Debug.LogError(
                $"BGMManager: Cue '{cueName}'（BGMCategory: {category}）がBGMのACBに存在しません。"
            );
            return false;
        }

        return true;
    }

    /// <summary>
    /// CRIWARE Playerと、そのPlayerが開始した再生音を一組として管理します。
    /// </summary>
    private sealed class PlaybackSlot
    {
        public CriAtomExPlayer Player { get; } = new CriAtomExPlayer();

        private CriAtomExPlayback playback;
        private bool hasPlayback;

        public void Start()
        {
            playback = Player.Start();
            hasPlayback = true;
        }

        public void Stop()
        {
            Player.Stop();
            hasPlayback = false;
        }

        public bool TryGetPlayback(out CriAtomExPlayback currentPlayback)
        {
            currentPlayback = playback;
            return hasPlayback;
        }

        public void Dispose()
        {
            Player.Dispose();
            hasPlayback = false;
        }
    }

    #endregion
}

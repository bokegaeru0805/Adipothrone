using System.Collections;
using System.Collections.Generic;
using CriWare;
using CriWare.Assets;
using UnityEngine;

/// <summary>
/// CRIWARE (ADX2) を使用してBGMを管理するクラス。
/// BGMの再生、停止、クロスフェードに対応。
/// </summary>
public class DebugBGMManager : MonoBehaviour
{
    [Header("BGMのACBアセット")]
    [SerializeField]
    private CriAtomAcbAsset bgmAcbAsset;

    public static DebugBGMManager instance { get; private set; }

    private CriAtomExPlayer player1;
    private CriAtomExPlayer player2;
    private CriAtomExPlayer currentPlayer;
    private BGMCategory currentCategory = BGMCategory.None;
    private Coroutine activeFadeCoroutine = null;

    /// <summary>
    /// BGMカテゴリ（Enum）→ 実際のCue名 へのマッピング
    /// </summary>
    private static readonly Dictionary<BGMCategory, string> debugBGMNameTable = new Dictionary<
        BGMCategory,
        string
    >
    {
        { BGMCategory.bgm0, "bgm0" },
        { BGMCategory.bgm1, "bgm1" },
    };

    private void Awake()
    {
        // シングルトンパターンの実装
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    private void Start()
    {
        // プレイヤーは最初に一度だけ生成し、使い回す
        player1 = new CriAtomExPlayer();
        player2 = new CriAtomExPlayer();
    }

    private void OnDestroy()
    {
        // アプリケーション終了時に、確保したリソースをすべて破棄する
        if (activeFadeCoroutine != null)
        {
            StopCoroutine(activeFadeCoroutine);
            activeFadeCoroutine = null;
        }
        player1?.Dispose();
        player2?.Dispose();
    }

    public void PlayBGM0()
    {
        Play(BGMCategory.bgm0);
    }

    public void PlayBGM1()
    {
        Play(BGMCategory.bgm1);
    }

    /// <summary>
    /// 指定したBGMを再生します。すでに何か再生中の場合はクロスフェードします。
    /// </summary>
    public void Play(BGMCategory category, float crossfadeDuration = 1.0f)
    {
        if (currentCategory == category)
        {
            return;
        }

        if (currentPlayer == null || currentPlayer.GetStatus() != CriAtomExPlayer.Status.Playing)
        {
            // --- 停止からの再生 ---
            if (
                !debugBGMNameTable.TryGetValue(category, out string bgmName)
                || string.IsNullOrEmpty(bgmName)
            )
                return;

            currentPlayer = player1;
            currentPlayer.SetCue(bgmAcbAsset.Handle, bgmName);
            currentPlayer.SetVolume(1.0f);
            currentPlayer.Start();
            currentCategory = category;
        }
        else
        {
            // --- 再生中なのでクロスフェード ---
            Crossfade(category, crossfadeDuration);
        }
    }

    /// <summary>
    /// 現在のBGMを停止します
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
    /// 指定したBGMにクロスフェードします
    /// </summary>
    public void Crossfade(BGMCategory newCategory, float crossfadeDuration = 1.0f)
    {
        if (currentCategory == newCategory || !debugBGMNameTable.ContainsKey(newCategory))
        {
            return;
        }

        // BGMが再生されていない場合は、通常の再生に切り替える
        if (currentPlayer == null || currentPlayer.GetStatus() != CriAtomExPlayer.Status.Playing)
        {
            Play(newCategory, crossfadeDuration);
            return;
        }

        // 実行中の古いフェードコルーチンがあれば停止する
        if (activeFadeCoroutine != null)
        {
            StopCoroutine(activeFadeCoroutine);
        }

        // 新しいクロスフェードコルーチンを開始する
        activeFadeCoroutine = StartCoroutine(CrossfadeCoroutine(newCategory, crossfadeDuration));
    }

    /// <summary>
    /// クロスフェード処理を行うコルーチン
    /// </summary>
    private IEnumerator CrossfadeCoroutine(BGMCategory newCategory, float duration)
    {
        // フェードイン/アウトするプレイヤーを決定
        CriAtomExPlayer fadeInPlayer = (currentPlayer == player1) ? player2 : player1;
        CriAtomExPlayer fadeOutPlayer = currentPlayer;

        // 新しい曲を再生準備し、ボリューム0で再生開始
        fadeInPlayer.SetCue(bgmAcbAsset.Handle, debugBGMNameTable[newCategory]);
        fadeInPlayer.SetVolume(0.0f);
        fadeInPlayer.Start();

        // 指定時間をかけてボリュームを滑らかに変化させる
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / duration);

            fadeOutPlayer.SetVolume(1.0f - progress);
            fadeInPlayer.SetVolume(progress);

            // 変更を即座に反映
            fadeOutPlayer.UpdateAll();
            fadeInPlayer.UpdateAll();

            yield return null;
        }

        // 処理完了後、古いプレイヤーを停止し、メインプレイヤーを入れ替える
        fadeOutPlayer.Stop();
        fadeInPlayer.SetVolume(1.0f);
        currentPlayer = fadeInPlayer;
        currentCategory = newCategory;
        activeFadeCoroutine = null;
    }

    /// <summary>
    /// デバッグ用のBGM (bgm0, bgm1) のうち、現在再生されている方からもう一方へクロスフェードします。
    /// どちらも再生されていない場合は何もしません。
    /// </summary>
    /// <param name="duration">クロスフェードにかける時間（秒）</param>
    public void ToggleDebugBGM(float duration)
    {
        // 1. 現在bgm0が再生中かどうかを確認
        if (currentCategory == BGMCategory.bgm0)
        {
            // bgm0が再生中なら、bgm1へクロスフェードを開始
            Debug.Log($"BGMを {BGMCategory.bgm0} から {BGMCategory.bgm1} へ切り替えます。");
            Crossfade(BGMCategory.bgm1, duration);
        }
        // 2. 現在bgm1が再生中かどうかを確認
        else if (currentCategory == BGMCategory.bgm1)
        {
            // bgm1が再生中なら、bgm0へクロスフェードを開始
            Debug.Log($"BGMを {BGMCategory.bgm1} から {BGMCategory.bgm0} へ切り替えます。");
            Crossfade(BGMCategory.bgm0, duration);
        }
        
        // 3. 上記のどちらの条件にも当てはまらない場合（両方とも流れていない、または全く別の曲が再生中）は、
        //    何もせずにこの関数を終了します。
    }
}

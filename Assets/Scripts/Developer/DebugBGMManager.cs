using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    private string currentBgmName = null;
    private Coroutine activeFadeCoroutine = null;

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
            return;
        }

        if (CriAtomAssetsLoader.Instance == null)
        {
            Debug.LogError("CriAtomAssetsLoaderがシーンに存在しません。");
            return;
        }

        // // CriAtomAssetsLoaderからロード済みのACBアセットを取得する
        // var firstCueSheet = CriAtomAssetsLoader.Instance.CueSheets.FirstOrDefault();

        // // 取得できたか確認
        // if (firstCueSheet != null)
        // {
        //     // CueSheetからCriAtomAcbAssetを取得して、自身の変数に登録
        //     bgmAcbAsset = firstCueSheet.AcbAsset;
        //     Debug.Log($"リストの最初のACBアセット '{bgmAcbAsset.name}' の取得に成功しました。");

        //     // これで bgmAcbAsset を使って再生などの処理ができる
        // }
        // else
        // {
        //     Debug.LogError("CriAtomAssetsLoaderにロード済みのACBアセットがありません。");
        // }
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
        Play("bgm0");
    }

    public void PlayBGM1()
    {
        Play("bgm1");
    }

    /// <summary>
    /// 指定したBGMを再生します。すでに何か再生中の場合はクロスフェードします。
    /// </summary>
    public void Play(string bgmName, float crossfadeDuration = 1.0f)
    {
        // BGM名が空、または同じ曲が再生中の場合は何もしない
        if (string.IsNullOrEmpty(bgmName) || currentBgmName == bgmName)
        {
            return;
        }

        if (currentPlayer == null || currentPlayer.GetStatus() != CriAtomExPlayer.Status.Playing)
        {
            // --- 停止からの再生 ---
            currentPlayer = player1;
            currentPlayer.SetCue(bgmAcbAsset.Handle, bgmName);
            currentPlayer.SetVolume(1.0f);
            currentPlayer.Start();
            currentBgmName = bgmName;
        }
        else
        {
            // --- 再生中なのでクロスフェード ---
            Crossfade(bgmName, crossfadeDuration);
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
        currentBgmName = null;
    }

    /// <summary>
    /// 指定したBGMにクロスフェードします
    /// </summary>
    public void Crossfade(string newBgmName, float crossfadeDuration = 1.0f)
    {
        if (string.IsNullOrEmpty(newBgmName) || currentBgmName == newBgmName)
        {
            return;
        }

        // まだ再生されていない場合は、単純に再生する
        if (currentPlayer == null || currentPlayer.GetStatus() != CriAtomExPlayer.Status.Playing)
        {
            Play(newBgmName, crossfadeDuration);
            return;
        }

        // すでにクロスフェード中なら停止する
        if (activeFadeCoroutine != null)
        {
            StopCoroutine(activeFadeCoroutine);
        }

        activeFadeCoroutine = StartCoroutine(CrossfadeCoroutine(newBgmName, crossfadeDuration));
    }

    /// <summary>
    /// クロスフェード処理を行うコルーチン
    /// </summary>
    private IEnumerator CrossfadeCoroutine(string newBgmName, float duration)
    {
        // フェードイン/アウトするプレイヤーを決定
        CriAtomExPlayer fadeInPlayer = (currentPlayer == player1) ? player2 : player1;
        CriAtomExPlayer fadeOutPlayer = currentPlayer;

        // 新しい曲を再生準備し、ボリューム0で再生開始
        fadeInPlayer.SetCue(bgmAcbAsset.Handle, newBgmName);
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
        currentBgmName = newBgmName;
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
        if (currentBgmName == "bgm0")
        {
            Debug.Log("BGMを bgm0 から bgm1 へ切り替えます。");
            Crossfade("bgm1", duration);
        }
        // 2. 現在bgm1が再生中かどうかを確認
        else if (currentBgmName == "bgm1")
        {
            // bgm1が再生中なら、bgm0へクロスフェードを開始
            Debug.Log($"BGMを {currentBgmName} から bgm0 へ切り替えます。");
            Crossfade("bgm0", duration);
        }

        // 3. 上記のどちらの条件にも当てはまらない場合（両方とも流れていない、または全く別の曲が再生中）は、
        //    何もせずにこの関数を終了します。
    }
}

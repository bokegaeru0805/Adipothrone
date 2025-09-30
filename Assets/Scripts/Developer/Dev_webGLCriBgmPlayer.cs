using System.Collections;
using CriWare;
using CriWare.Assets;
using UnityEngine;

/// <summary>
/// クロスフェード機能を持つ、CRIWAREのBGMプレーヤー
/// </summary>
public class Dev_webGLCriBgmPlayer : MonoBehaviour
{
    [Header("BGMのACBアセット")]
    [SerializeField]
    private CriAtomAcbAsset bgmAcbAsset;
    private string initialCueName = "PlainsField1"; // 最初に再生するBGMのキュー名
    private string crossfadeCueName = "UniqueBoss"; // クロスフェード先のキュー名
    private float fadeDuration = 2.0f; // クロスフェードにかかる時間（秒）
    private string aisacControlName = "DuckingControl"; // Atom Craftで設定したAISACコントロール名
    private float duckingTime = 0.5f; // ダッキングで音量が変化する時間（秒）

    // BGM再生用に2つのプレーヤーを用意
    private CriAtomExPlayer player1;
    private CriAtomExPlayer player2;
    private CriAtomExPlayer currentPlayer; // 現在メインで再生しているプレーヤー
    private Coroutine duckingCoroutine; // 実行中のダッキングコルーチン

    void Start()
    {
        // プレーヤーを2つ生成
        player1 = new CriAtomExPlayer();
        player2 = new CriAtomExPlayer();
        // 最初はplayer1をメインプレイヤーとして設定
        currentPlayer = player1;
    }

    void OnDestroy()
    {
        // オブジェクト破棄時にすべてのプレーヤーを破棄してリソースを解放する
        if (player1 != null)
        {
            player1.Dispose();
            player1 = null;
        }
        if (player2 != null)
        {
            player2.Dispose();
            player2 = null;
        }
    }

    /// <summary>
    /// デバッグ用のキー入力
    /// </summary>
    void Update()
    {
        //マウスの左ボタンで最初の曲を再生
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("[左クリック]入力: 初期BGMを再生します。");
            PlayBGM(initialCueName);
        }

        // マウスの右ボタンでBGMを停止
        if (Input.GetMouseButtonDown(1))
        {
            Debug.Log("[右クリック]入力: BGMを停止します。");
            StopBGM();
        }

        // [A] キーでクロスフェードを開始
        if (Input.GetKeyDown(KeyCode.A))
        {
            Debug.Log($"[A]キー入力: {crossfadeCueName} へクロスフェードを開始します。");
            StartCrossfade(crossfadeCueName, fadeDuration);
        }

        // [D] キーでダッキング開始
        if (Input.GetKeyDown(KeyCode.D))
        {
            Debug.Log("[D]キー入力: BGMのダッキングを開始します。");
            SetDucking(true, duckingTime);
        }

        // [F] キーでダッキング解除
        if (Input.GetKeyDown(KeyCode.F))
        {
            Debug.Log("[F]キー入力: BGMのダッキングを解除します。");
            SetDucking(false, duckingTime);
        }
    }

    /// <summary>
    /// 指定した名前のBGMを再生します（クロスフェードなし）
    /// </summary>
    /// <param name="bgmName">ACBに含まれるキュー名</param>
    public void PlayBGM(string bgmName)
    {
        if (IsPlayerBusy())
            return; // クロスフェード中は実行しない

        if (bgmAcbAsset == null)
        {
            Debug.LogError("BGMのACBアセットが設定されていません。");
            return;
        }

        currentPlayer.Stop(); // 即時停止
        currentPlayer.SetCue(bgmAcbAsset.Handle, bgmName);
        currentPlayer.Start();
    }

    /// <summary>
    /// BGMを停止します（フェードなし）
    /// </summary>
    public void StopBGM()
    {
        if (IsPlayerBusy())
            return; // クロスフェード中は実行しない

        currentPlayer.Stop(); // 即時停止
    }

    /// <summary>
    /// 指定したBGMへクロスフェードで切り替えます
    /// </summary>
    /// <param name="newCueName">新しいBGMのキュー名</param>
    /// <param name="duration">クロスフェード時間</param>
    public void StartCrossfade(string newCueName, float duration)
    {
        // ACBアセットがなければエラー
        if (bgmAcbAsset == null)
        {
            Debug.LogError("BGMのACBアセットが設定されていません。");
            return;
        }

        // 再生中のプレーヤーがなければ、普通に再生して終了
        if (currentPlayer.GetStatus() != CriAtomExPlayer.Status.Playing)
        {
            PlayBGM(newCueName);
            return;
        }

        // コルーチンを開始
        StartCoroutine(CrossfadeCoroutine(newCueName, duration));
    }

    // クロスフェード処理を行うコルーチン
    private IEnumerator CrossfadeCoroutine(string newCueName, float duration)
    {
        // 1. メインではない方のプレイヤー（フェードイン担当）を特定する
        CriAtomExPlayer fadeInPlayer = (currentPlayer == player1) ? player2 : player1;
        CriAtomExPlayer fadeOutPlayer = currentPlayer;

        // 2. 新しい曲を再生準備し、音量0で再生開始
        fadeInPlayer.SetCue(bgmAcbAsset.Handle, newCueName);
        fadeInPlayer.SetVolume(0.0f);
        fadeInPlayer.Start();

        Debug.Log($"クロスフェード開始: {newCueName} へ");

        // 3. 指定した時間をかけて音量を滑らかに変化させる
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);

            fadeOutPlayer.SetVolume(1.0f - progress); // 現在の曲をフェードアウト
            fadeInPlayer.SetVolume(progress); // 新しい曲をフェードイン

            // ボリューム変更を即座に反映させる
            fadeOutPlayer.UpdateAll();
            fadeInPlayer.UpdateAll();

            yield return null; // 次のフレームまで待機
        }

        // 4. 処理の完了
        fadeOutPlayer.Stop(); // 古い曲を完全に停止
        fadeInPlayer.SetVolume(1.0f); // 新しい曲の音量を最大に
        fadeInPlayer.UpdateAll();

        currentPlayer = fadeInPlayer; // メインプレイヤーを入れ替え

        Debug.Log("クロスフェード完了");
    }

    /// <summary>
    /// いずれかのプレーヤーが再生中、またはポーズ中かを確認します
    /// </summary>
    private bool IsPlayerBusy()
    {
        var status1 = player1.GetStatus();
        var status2 = player2.GetStatus();
        bool busy = (
            status1 == CriAtomExPlayer.Status.Playing && status2 == CriAtomExPlayer.Status.Playing
        );

        if (busy)
        {
            Debug.LogWarning("クロスフェード処理中のため、新しい操作は実行できません。");
        }
        return busy;
    }

    /// <summary>
    /// BGMのダッキングを設定します
    /// </summary>
    /// <param name="duck">trueで音量を下げ、falseで元に戻す</param>
    /// <param name="duration">音量変化にかける時間</param>
    public void SetDucking(bool duck, float duration)
    {
        // 既存のダッキング処理があれば停止
        if (duckingCoroutine != null)
        {
            StopCoroutine(duckingCoroutine);
        }

        // AISACコントロール名が設定されていなければ処理しない
        if (string.IsNullOrEmpty(aisacControlName))
        {
            Debug.LogWarning("AISACコントロール名が設定されていません。");
            return;
        }

        // 新しいダッキング処理を開始
        duckingCoroutine = StartCoroutine(DuckingCoroutine(duck, duration));
    }

    private IEnumerator DuckingCoroutine(bool duck, float duration)
    {
        // AISAC値の開始値と目標値を設定 (0:通常, 1:ダッキング最大)
        float startValue = duck ? 0.0f : 1.0f;
        float endValue = duck ? 1.0f : 0.0f;

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            float currentValue = Mathf.Lerp(startValue, endValue, progress);

            // !!重要!! 両方のプレーヤーにAISACを適用する
            // クロスフェード中でも正しく動作させるため
            player1.SetAisacControl(aisacControlName, currentValue);
            player2.SetAisacControl(aisacControlName, currentValue);
            player1.UpdateAll();
            player2.UpdateAll();

            yield return null;
        }

        // 最終的な値を確実に設定
        player1.SetAisacControl(aisacControlName, endValue);
        player2.SetAisacControl(aisacControlName, endValue);
        player1.UpdateAll();
        player2.UpdateAll();

        duckingCoroutine = null; // コルーチン参照をクリア
    }
}
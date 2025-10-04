using System.Collections;
using System.Collections.Generic;
using CriWare;
using CriWare.Assets;
using UnityEngine;

/// <summary>
/// CRIWARE (ADX2) を使用してBGMを管理するクラス。
/// クロスフェード、ダッキング（AISAC）、動的なリソースロードに対応。
/// </summary>
public class BGMManager : MonoBehaviour
{
    // --- シングルトンインスタンス ---
    [Header("BGMのACBアセット")]
    [SerializeField]
    private CriAtomAcbAsset bgmAcbAsset;
    public static BGMManager instance { get; private set; }
    private CriAtomExPlayer player1; // AudioSourceの代わりにCriAtomExPlayerを2つ使用
    private CriAtomExPlayer player2;
    private CriAtomExPlayer currentPlayer; // 現在メインで再生しているプレイヤー
    private BGMCategory currentCategory = BGMCategory.None;
    private const string duckingAisacName = "DuckingControl"; // ダッキング用のAISAC名
    private const string BGMCategoryName = "BGM"; // BGMカテゴリのパラメータ名
    private float duckingLevel = 0.5f; // ダッキング時に下げる音量レベル (0.0 - 1.0)
    private Coroutine activeFadeCoroutine = null; // 現在実行中のフェードコルーチンを追跡するための変数

    /// <summary>
    /// BGMカテゴリ（Enum）→ 実際のCue名 へのマッピング
    /// </summary>
    private static readonly Dictionary<BGMCategory, string> bgmNameTable = new Dictionary<
        BGMCategory,
        string
    >
    {
        { BGMCategory.Title, "Title" },//仮Title
        { BGMCategory.GameOver, "GameOver" },
        { BGMCategory.Field_Quiet, "QuietField" },
        { BGMCategory.Field_Tutorial, "TutorialField" },
        { BGMCategory.Field_Waterfall1, "WaterFall1" },
        { BGMCategory.Boss_Electric, "ElectricBoss" },
        { BGMCategory.Boss_Chapter, "ChapterBoss" },
        { BGMCategory.Boss_Unique, "UniqueBoss" },
        { BGMCategory.Boss_Mid, "MidBoss" },
        { BGMCategory.Field_Plains, "PlainsField1" }, //仮PlainsField1
        { BGMCategory.Env_Water_Stream1, "WaterStream1" },
        { BGMCategory.Env_Birds, "PlainsField_Amb1" },
        { BGMCategory.Field_FirstVillage, "FirstVillage" },  //仮FirstVillage
        { BGMCategory.Field_Cave1, "CaveField_Amb1" },
        {
            BGMCategory.None,
            ""
        } // Noneは空文字列で扱う
        ,
    };

    private void Awake()
    {
        // シングルトンパターンの実装
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(this.gameObject); // シーンを跨いでも破棄しない
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
        // 初期化
        // プレイヤーは最初に一度だけ生成し、使い回す
        // Awakeで行うと、CRIWAREのシステム自体がまだ起動準備を完了していない場合があるため、Startで行う
        player1 = new CriAtomExPlayer();
        player2 = new CriAtomExPlayer();
    }

    private void OnDestroy()
    {
        // アプリケーション終了時に、確保したリソースをすべて破棄する（重要）
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
    /// 指定したBGMを再生します。再生中の場合はクロスフェードします。
    /// </summary>
    /// <param name="category">再生したいBGMのカテゴリ</param>
    public void Play(BGMCategory category)
    {
        // 再生中の曲が同じでも、再生されていなければ再開させる
        if (
            currentCategory == category
            && currentPlayer != null
            && currentPlayer.GetStatus() == CriAtomExPlayer.Status.Playing
        )
        {
            return;
        }

        if (!bgmNameTable.TryGetValue(category, out string bgmName))
        {
            Debug.LogWarning($"指定されたBGMカテゴリ {category} は登録されていません。");
            return;
        }

        // 停止中の場合は、最初のプレイヤーで再生開始
        if (currentPlayer == null || currentPlayer.GetStatus() != CriAtomExPlayer.Status.Playing)
        {
            currentPlayer = player1;
            currentPlayer.SetVolume(1.0f);
            currentPlayer.SetCue(bgmAcbAsset.Handle, bgmName);
            currentPlayer.Start();
            currentCategory = category;
        }
        else
        {
            // 再生中の場合は安全なクロスフェード処理を呼び出す
            StartCrossfadeInternal(category, 1.0f);
        }
    }

    /// <summary>
    /// 現在のBGMを停止します（フェードなし）
    /// </summary>
    public void Stop()
    {
        // 実行中のコルーチンを停止
        if (activeFadeCoroutine != null)
        {
            StopCoroutine(activeFadeCoroutine);
            activeFadeCoroutine = null;
        }

        // 全てのプレイヤーを停止
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

        if (currentPlayer != null && currentPlayer.GetStatus() == CriAtomExPlayer.Status.Playing)
        {
            // 安全なクロスフェード処理を呼び出す
            StartCrossfadeInternal(category, duration);
        }
        else
        {
            // 停止からのフェードイン処理を強化
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
        if (!bgmNameTable.TryGetValue(category, out string bgmName))
        {
            Debug.LogWarning($"指定されたBGMカテゴリ {category} は登録されていません。");
            yield break; // BGM名が見つからなければコルーチンを終了
        }

        // 1. 再生に使用するプレイヤーを決定し（player1をデフォルトとする）、メインプレイヤーに設定
        currentPlayer = player1;

        // 2. 新しい曲を準備し、ボリューム0で再生を開始
        currentPlayer.SetCue(bgmAcbAsset.Handle, bgmName);
        currentPlayer.SetVolume(0.0f);
        currentPlayer.Start();

        currentCategory = category;

        // 3. 指定時間をかけてボリュームを0から1へ滑らかに変化させる
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime; // Time.timeScaleの影響を受けない時間で計測
            float progress = Mathf.Clamp01(timer / duration);

            currentPlayer.SetVolume(progress);
            currentPlayer.UpdateAll(); // 変更を即座に反映

            yield return null;
        }

        // 4. 処理完了後、確実にボリュームを1にする
        currentPlayer.SetVolume(1.0f);
        currentPlayer.UpdateAll();

        //コルーチンが正常終了したことを通知
        activeFadeCoroutine = null;
    }

    /// <summary>
    /// 現在のBGMをフェードアウトしながら停止します
    /// </summary>
    public void FadeOut(float duration)
    {
        if (currentPlayer != null && currentPlayer.GetStatus() == CriAtomExPlayer.Status.Playing)
        {
            if (activeFadeCoroutine != null)
            {
                StopCoroutine(activeFadeCoroutine);
            }
            // フェードアウト中に使われていない方のプレイヤーを確実に止める
            CriAtomExPlayer otherPlayer = (currentPlayer == player1) ? player2 : player1;
            otherPlayer.Stop();

            activeFadeCoroutine = StartCoroutine(FadeOutCoroutine(duration));
        }
    }

    /// <summary>
    /// フェードアウト処理を行うコルーチン
    /// </summary>
    private IEnumerator FadeOutCoroutine(float duration)
    {
        CriAtomExPlayer playerToFade = currentPlayer;
        currentCategory = BGMCategory.None; // 先にカテゴリをNoneにしておく

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime; // ゲームの時間停止に影響されないようにする;
            float progress = Mathf.Clamp01(timer / duration);

            playerToFade.SetVolume(1.0f - progress);
            playerToFade.UpdateAll();
            yield return null;
        }

        playerToFade.Stop();
        //コルーチンが正常終了したことを通知
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
        if (currentPlayer == null || currentPlayer.GetStatus() != CriAtomExPlayer.Status.Playing)
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
        if (!bgmNameTable.ContainsKey(newCategory))
        {
            Debug.LogWarning($"BGMカテゴリ {newCategory} は登録されていません。");
            return;
        }

        // 1. 実行中の古いフェードコルーチンがあれば、まず停止する
        if (activeFadeCoroutine != null)
        {
            StopCoroutine(activeFadeCoroutine);
        }

        // 2. 現在再生を担当していない方のプレイヤー（古いフェードイン担当）を特定して停止する
        // これにより、中途半端にフェードインしていた曲が確実に止まる
        if (currentPlayer != null && currentPlayer.GetStatus() == CriAtomExPlayer.Status.Playing)
        {
            CriAtomExPlayer playerToStop = (currentPlayer == player1) ? player2 : player1;
            playerToStop.Stop();
        }

        // 3. 新しいクロスフェードコルーチンを開始する
        activeFadeCoroutine = StartCoroutine(CrossfadeCoroutine(newCategory, duration));
    }

    /// <summary>
    /// クロスフェード処理を行うコルーチン
    /// </summary>
    private IEnumerator CrossfadeCoroutine(BGMCategory newCategory, float crossfadeDuration)
    {
        // 1. フェードイン/アウトするプレイヤーを決定
        CriAtomExPlayer fadeInPlayer = (currentPlayer == player1) ? player2 : player1;
        CriAtomExPlayer fadeOutPlayer = currentPlayer;

        // 2. 新しい曲を再生準備し、ボリューム0で再生開始
        fadeInPlayer.SetCue(bgmAcbAsset.Handle, bgmNameTable[newCategory]);
        fadeInPlayer.SetVolume(0.0f);
        fadeInPlayer.Start();

        // 3. 指定時間をかけてボリュームを滑らかに変化させる
        float timer = 0f;
        while (timer < crossfadeDuration)
        {
            timer += Time.unscaledDeltaTime; // ゲームの時間停止に影響されないようにする;
            float progress = Mathf.Clamp01(timer / crossfadeDuration);

            fadeOutPlayer.SetVolume(1.0f - progress);
            fadeInPlayer.SetVolume(progress);

            // 変更を即座に反映
            fadeOutPlayer.UpdateAll();
            fadeInPlayer.UpdateAll();

            yield return null;
        }

        // 4. 処理完了後、古いプレイヤーを停止し、メインプレイヤーを入れ替える
        fadeOutPlayer.Stop();
        fadeInPlayer.SetVolume(1.0f);
        currentPlayer = fadeInPlayer;
        currentCategory = newCategory;

        //コルーチンが正常終了したことを通知
        activeFadeCoroutine = null;
    }

    /// <summary>
    /// 登録されているすべてのBGMの音量を調整する
    /// </summary>
    public void AdjustAllVolume(float ratio)
    {
        CriAtom.SetCategoryVolume(BGMCategoryName, ratio);
    }

    /// <summary>
    /// 指定したBGMが現在再生中かどうかを確認します
    /// </summary>
    public bool IsPlayingCategory(BGMCategory category)
    {
        return currentCategory == category
            && currentPlayer != null
            && currentPlayer.GetStatus() == CriAtomExPlayer.Status.Playing;
    }

    /// <summary>
    /// 現在のBGMの音量を取得します
    /// </summary>
    public float GetAllVolume()
    {
        return CriAtom.GetCategoryVolume(BGMCategoryName);
    }

    /// <summary>
    /// 会話時などにBGM音量を下げる（ダッキング）かどうかを設定します。
    /// </summary>
    /// <param name="isDucking">trueで音量を下げ、falseで元の音量に戻す</param>
    public void SetDucking(bool isDucking)
    {
        // 現在再生中のプレイヤーがいなければ何もしない
        if (currentPlayer == null)
        {
            return;
        }

        // isDuckingフラグに応じて、AISACに設定する値を決定
        // trueならInspectorで設定したduckingLevelの値を、falseなら0（元の音量）を設定
        float targetValue = isDucking ? duckingLevel : 0.0f;

        // AISACコントロールを設定してBGMの音量を変化させる
        currentPlayer.SetAisacControl(duckingAisacName, targetValue);
        currentPlayer.UpdateAll(); // 変更を即座に反映
    }
}

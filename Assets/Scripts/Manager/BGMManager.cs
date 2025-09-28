using System.Collections;
using System.Collections.Generic;
using CriWare;
using UnityEngine;

/// <summary>
/// CRIWARE (ADX2) を使用してBGMを管理するクラス。
/// クロスフェード、ダッキング（AISAC）、動的なリソースロードに対応。
/// </summary>
public class BGMManager : MonoBehaviour
{
    // --- シングルトンインスタンス ---
    public static BGMManager instance { get; private set; }
    private CriAtomExPlayer player1; // AudioSourceの代わりにCriAtomExPlayerを2つ使用
    private CriAtomExPlayer player2;
    private CriAtomExPlayer currentPlayer; // 現在メインで再生しているプレイヤー
    private BGMCategory currentCategory = BGMCategory.None;
    private const string defaultCueSheetName = "BGMSheet"; // デフォルトのCueSheet名
    private const string duckingAisacName = "DuckingControl"; // ダッキング用のAISAC名
    private const string BGMCategoryName = "BGM"; // BGMカテゴリのパラメータ名
    private float duckingLevel = 0.5f; // ダッキング時に下げる音量レベル (0.0 - 1.0)
    private CriAtomExAcb bgmAcb;
    private Coroutine activeFadeCoroutine = null; // 現在実行中のフェードコルーチンを追跡するための変数

    /// <summary>
    /// BGMカテゴリ（Enum）→ 実際のCue名 へのマッピング
    /// </summary>
    private static readonly Dictionary<BGMCategory, string> bgmNameTable = new Dictionary<
        BGMCategory,
        string
    >
    {
        { BGMCategory.Title, "Title" },
        { BGMCategory.GameOver, "GameOver" },
        { BGMCategory.Field_Quiet, "QuietField" },
        { BGMCategory.Field_Tutorial, "TutorialField" },
        { BGMCategory.Field_Waterfall1, "WaterFall1" },
        { BGMCategory.Boss_Electric, "ElectricBoss" },
        { BGMCategory.Boss_Chapter, "ChapterBoss" },
        { BGMCategory.Boss_Unique, "UniqueBoss" },
        { BGMCategory.Boss_Mid, "MidBoss" },
        { BGMCategory.Field_Plains, "PlainsField1" },
        { BGMCategory.Env_Water_Stream1, "WaterStream1" },
        { BGMCategory.Env_Birds, "PlainsField_Amb1" },
        { BGMCategory.Field_FirstVillage, "FirstVillage" },
        { BGMCategory.Field_Cave1, "CaveField1" },
        {
            BGMCategory.None,
            ""
        } // Noneは空文字列で扱う
        ,
    };

    // 実行中のフェード処理を安全に停止し、プレイヤーの状態をリセットする
    private void StopActiveFade()
    {
        if (activeFadeCoroutine != null)
        {
            StopCoroutine(activeFadeCoroutine);
            activeFadeCoroutine = null;

            // 中途半端な状態で残っている可能性のあるプレイヤーを両方停止
            player1.Stop();
            player2.Stop();
        }
    }

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
    }

    private void Start()
    {
        // 初期化
        // プレイヤーは最初に一度だけ生成し、使い回す
        //Awakeで行うと、CRIWAREのシステム自体がまだ起動準備を完了していない場合があるため、Startで行う
        player1 = new CriAtomExPlayer();
        player2 = new CriAtomExPlayer();
        bgmAcb = CriAtomExAcb.LoadAcbFile(
            null,
            defaultCueSheetName + ".acb",
            defaultCueSheetName + ".awb"
        );
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
        if (bgmAcb != null)
        {
            bgmAcb.Dispose();
            bgmAcb = null;
        }
    }

    /// <summary>
    /// 指定したBGMを再生します。再生中の場合はクロスフェードします。
    /// </summary>
    /// <param name="category">再生したいBGMのカテゴリ</param>
    public void Play(BGMCategory category)
    {
        // 同じ曲が再生中の場合は何もしない
        if (currentCategory == category)
        {
            return;
        }

        if (!bgmNameTable.TryGetValue(category, out string bgmName))
        {
            Debug.LogWarning($"指定されたBGMカテゴリ {category} は登録されていません。");
            return;
        }

        //常にクリーンな状態から始める
        StopActiveFade();

        // 停止中の場合は、最初のプレイヤーで再生開始
        if (currentPlayer == null || currentPlayer.GetStatus() != CriAtomExPlayer.Status.Playing)
        {
            currentPlayer = player1;
            currentPlayer.SetCue(bgmAcb, bgmName);
            currentPlayer.Start();
            currentCategory = category;
        }
        else
        {
            // 再生中の場合はクロスフェードを開始
            StopAllCoroutines(); // 既に実行中のフェード処理があれば中断
            Crossfade(category);
        }
    }

    /// <summary>
    /// 現在のBGMを停止します（フェードなし）
    /// </summary>
    public void Stop()
    {
        if (currentPlayer != null)
        {
            //常にクリーンな状態から始める
            StopActiveFade();

            currentPlayer.Stop();
        }
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
        // 同じ曲が再生中の場合は何もしない
        if (currentCategory == category)
        {
            return;
        }

        //常にクリーンな状態から始める
        StopActiveFade();

        // すでに何らかのBGMが再生されている場合は、クロスフェードを呼び出す
        if (currentPlayer != null && currentPlayer.GetStatus() == CriAtomExPlayer.Status.Playing)
        {
            Crossfade(category, duration);
        }
        else
        {
            // BGMが停止している場合は、フェードインコルーチンを開始
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
        currentPlayer.SetCue(bgmAcb, bgmName);
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
            // 常にクリーンな状態から始める
            StopActiveFade();

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
        if (!bgmNameTable.TryGetValue(newCategory, out string bgmName))
        {
            Debug.LogWarning($"指定されたBGMカテゴリ {newCategory} は登録されていません。");
            return;
        }

        //常にクリーンな状態から始める
        StopActiveFade();

        // 停止中の場合は、Playを呼び出すだけで良い
        if (currentPlayer == null || currentPlayer.GetStatus() != CriAtomExPlayer.Status.Playing)
        {
            Play(newCategory);
            return;
        }

        // クロスフェード処理を開始
        activeFadeCoroutine = StartCoroutine(CrossfadeCoroutine(newCategory, crossfadeDuration));
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
        fadeInPlayer.SetCue(bgmAcb, bgmNameTable[newCategory]);
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

    // /// <summary>
    // /// 必要であればACBファイルをロードし、キャッシュする
    // /// </summary>
    // private void LoadAcbIfNeeded(string cueSheetName)
    // {
    //     // 既にロード済みの場合は何もしない
    //     if (loadedAcbs.ContainsKey(cueSheetName))
    //     {
    //         return;
    //     }

    //     // 未ロードの場合はロードして辞書に登録
    //     var acb = CriAtomExAcb.LoadAcbFile(null, cueSheetName + ".acb", cueSheetName + ".awb");
    //     loadedAcbs.Add(cueSheetName, acb);
    // }
}

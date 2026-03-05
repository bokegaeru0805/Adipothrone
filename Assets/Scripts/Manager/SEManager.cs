using System;
using System.Collections;
using System.Collections.Generic;
using CriWare;
using CriWare.Assets;
using Fungus;
using UnityEngine;

/// <summary>
/// SEの再生を統括管理するマネージャクラス（CRI ADX2版）
/// </summary>
public class SEManager : MonoBehaviour
{
    [Header("SEのACBアセット")]
    [SerializeField]
    private CriAtomAcbAsset seAcbAsset;
    public static SEManager instance { get; private set; }
    private CriAtomExPlayer sePlayer;
    private const string SE_CATEGORY_NAME = "SE"; // SEカテゴリのパラメータ名
    public bool IsTimelineMuted { get; set; } = false; //Timeline操作中などにSEをミュートするためのフラグ

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }

        if (seAcbAsset == null)
        {
            Debug.LogError("SEManager: SEのACBアセットが設定されていません。");
        }
    }

    private void Start()
    {
        sePlayer = new CriAtomExPlayer();

        // Atom Craft側が3Dポジショニング設定でも、このプレイヤーでは常に通常のパン（Pan3d＝2D的な定位）として強制再生し、リスナー未設定エラーを防ぐ
        sePlayer.SetPanType(CriAtomEx.PanType.Pan3d);

        // SEの音量を設定
        if (SaveLoadManager.instance != null)
        {
            float seVolume = SaveLoadManager.instance.Settings.seVolume;
            AdjustAllSEVolume(seVolume);
        }
        else
        {
            Debug.LogError("SaveLoadManagerが見つかりません。");
        }
    }

    /// <summary>
    /// パラメータ指定付きでSEを再生（Timeline用）
    /// </summary>
    public void PlayEx(Enum cue, bool useVolume, float volume, bool usePitch, float pitch)
    {
        // ミュート中は何もせず戻る
        if (IsTimelineMuted)
        {
            // Debug.Log($"SEManager: Timelineがミュート中のため、SE '{cue}' の再生をスキップ。");
            return;
        }

        string cueName = SeCueDatabase.GetCueName(cue);
        if (cueName == null)
        {
            Debug.LogWarning(
                $"SEManager: 指定されたSE enum '{cue}' に対応するキュー名が見つかりません。"
            );
            return;
        }

        // 1. パラメータ設定
        // 指定がある場合だけセットし、なければデフォルト(Vol=1.0, Pitch=0)に戻すなどの運用が安全ですが、
        // ADX2の仕様上、前回の設定が残る可能性があるため、明示的にセットします。

        // ボリューム設定（指定がなければ、現在のカテゴリボリュームなどは触らずPlayerの倍率を1.0に戻す）
        sePlayer.SetVolume(useVolume ? volume : 1.0f);

        // ピッチ設定（指定がなければ0に戻す）
        sePlayer.SetPitch(usePitch ? pitch : 0f);

        // 2. 再生
        sePlayer.SetCue(seAcbAsset.Handle, cueName);
        sePlayer.Start();

        Debug.Log(
            $"Played SE: {cue}, Volume: {(useVolume ? volume.ToString() : "Default")} (Player Volume Multiplier), Pitch: {(usePitch ? pitch.ToString() : "Default")} (Cents)"
        );

        // 3. 次回の再生に影響が出ないよう、パラメータをリセットしておく（安全策）
        // ※ただしStart直後のResetは反映タイミングに注意が必要ですが、ADX2はStart時点のパラメータが使われるため基本OK
        // ここでは「次にPlay(Enum)だけ呼ばれた時」のためにリセットは行わず、
        // 常にSetVolume/SetPitchを行う運用にするか、以下のようにリセットを入れるか選択になります。
        // 今回は安全のため、次のフレーム以降のためにリセットしておきます。
        // sePlayer.SetVolume(1.0f);
        // sePlayer.SetPitch(0f);
    }

    /// <summary>
    /// 名前でSEを再生
    /// </summary>
    // --- UI系 ---
    public void PlayUISE(SE_UI se)
    {
        Play(se);
    }

    // --- PlayerAction系 ---
    public void PlayPlayerActionSE(SE_PlayerAction se)
    {
        Play(se);
    }

    // --- EnemyAction系 ---
    public void PlayEnemyActionSE(SE_EnemyAction se)
    {
        Play(se);
    }

    // --- Field系 ---
    public void PlayFieldSE(SE_Field se)
    {
        Play(se);
    }

    // --- SystemEvent系 ---
    public void PlaySystemEventSE(SE_SystemEvent se)
    {
        Play(se);
    }

    /// <summary>
    /// 指定されたenumに対応するSEを再生します。
    /// どのカテゴリのenumでも受け付けます。
    /// </summary>
    /// <param name="cue">再生したいSEのenum（例：SE_UI.Decision1）</param>
    public void Play(Enum cue)
    {
        // ミュート中は何もせず戻る
        if (IsTimelineMuted)
            return;

        // 辞書からキュー名（string）を取得
        string cueName = SeCueDatabase.GetCueName(cue);
        if (cueName == null)
        {
            Debug.LogWarning(
                $"SEManager: 指定されたSE enum '{cue}' に対応するキュー名が見つかりません。"
            );
            return;
        }
        sePlayer.SetCue(seAcbAsset.Handle, cueName);
        sePlayer.Start();
    }

    /// <summary>
    /// 名前でSEを停止
    /// </summary>
    // --- UI系 ---
    public void StopUISE(SE_UI se)
    {
        StopSE(se);
    }

    // --- PlayerAction系 ---
    public void StopPlayerActionSE(SE_PlayerAction se)
    {
        StopSE(se);
    }

    // --- EnemyAction系 ---
    public void StopEnemyActionSE(SE_EnemyAction se)
    {
        StopSE(se);
    }

    // --- Field系 ---
    public void StopFieldSE(SE_Field se)
    {
        StopSE(se);
    }

    // --- SystemEvent系 ---
    public void StopSystemEventSE(SE_SystemEvent se)
    {
        StopSE(se);
    }

    private void StopSE(Enum cue)
    {
        // 辞書からキュー名（string）を取得
        string cueName = SeCueDatabase.GetCueName(cue);
        if (cueName == null)
        {
            Debug.LogWarning(
                $"SEManager: 指定されたSE enum '{cue}' に対応するキュー名が見つかりません。"
            );
            return;
        }
        sePlayer.SetCue(seAcbAsset.Handle, cueName);
        sePlayer.Stop();
    }

    /// <summary>
    /// 指定したSEが再生中かどうかを返す
    /// </summary>
    // --- UI系 ---
    public bool IsPlayingUISE(SE_UI se)
    {
        return IsPlaying(se);
    }

    // --- PlayerAction系 ---
    public bool IsPlayingPlayerActionSE(SE_PlayerAction se)
    {
        return IsPlaying(se);
    }

    // --- EnemyAction系 ---
    public bool IsPlayingEnemyActionSE(SE_EnemyAction se)
    {
        return IsPlaying(se);
    }

    // --- Field系 ---
    public bool IsPlayingFieldSE(SE_Field se)
    {
        return IsPlaying(se);
    }

    // --- SystemEvent系 ---
    public bool IsPlayingSystemEventSE(SE_SystemEvent se)
    {
        return IsPlaying(se);
    }

    private bool IsPlaying(Enum cue)
    {
        // 辞書からキュー名（string）を取得
        string cueName = SeCueDatabase.GetCueName(cue);
        if (cueName == null)
        {
            Debug.LogWarning(
                $"SEManager: 指定されたSE enum '{cue}' に対応するキュー名が見つかりません。"
            );
            return false;
        }
        sePlayer.SetCue(seAcbAsset.Handle, cueName);

        if (sePlayer.GetStatus() == CriAtomExPlayer.Status.Playing)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// 指定したSEをピッチを変更して再生するメソッド
    /// </summary>
    // --- UI系 ---
    public void PlayUISEPitch(SE_UI se, float pitch) { }

    // --- PlayerAction系 ---
    public void PlayPlayerActionSEPitch(SE_PlayerAction se, float pitch) { }

    // --- EnemyAction系 ---
    public void PlayEnemyActionSEPitch(SE_EnemyAction se, float pitch) { }

    // --- Field系 ---
    public void PlayFieldSEPitch(SE_Field se, float pitch) { }

    // --- SystemEvent系 ---
    public void PlaySystemEventSEPitch(SE_SystemEvent se, float pitch) { }

    /// <summary>
    /// すべてのSEを停止する
    /// </summary>
    public void StopAllSE()
    {
        if (sePlayer != null)
        {
            sePlayer.Dispose();
            sePlayer = new CriAtomExPlayer();

            // プレイヤーを再生成した際も、常に通常のパン（Pan3d）として強制再生する
            sePlayer.SetPanType(CriAtomEx.PanType.Pan3d);
        }
    }

    /// <summary>
    /// すべてのSEの音量を調整する
    /// </summary>
    public void AdjustAllSEVolume(float ratio)
    {
        CriAtom.SetCategoryVolume(SE_CATEGORY_NAME, ratio);
    }

    /// <summary>
    /// 現在のSEの音量を取得します
    /// </summary>
    public float GetAllVolume()
    {
        return CriAtom.GetCategoryVolume(SE_CATEGORY_NAME);
    }
}

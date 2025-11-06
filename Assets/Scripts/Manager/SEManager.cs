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
    private const string SECategoryName = "SE"; // SEカテゴリのパラメータ名

    /// <summary>
    /// SaveLoadManagerから設定されたグローバル音量
    /// </summary>
    private float _globalSeVolume = 1.0f;

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
        if(sePlayer != null)
        {
            sePlayer.Dispose();
            sePlayer = new CriAtomExPlayer();
        }
    }

    /// <summary>
    /// すべてのSEの音量を調整する
    /// </summary>
    public void AdjustAllSEVolume(float ratio)
    {
        CriAtom.SetCategoryVolume(SECategoryName, ratio);
    }

    /// <summary>
    /// 現在のSEの音量を取得します
    /// </summary>
    public float GetAllVolume()
    {
        return CriAtom.GetCategoryVolume(SECategoryName);
    }
}

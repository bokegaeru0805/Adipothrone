using System;
using CriWare;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// オブジェクトの登場時および常駐時のSEを管理するクラス
/// インスペクターでカテゴリを選び、それに応じたSEを指定できます。
/// </summary>
[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class ObjectSeManager : MonoBehaviour
{
    // --- インスペクター設定項目 ---
    [Header("登場時")]
    [Tooltip("オブジェクト登場時にSEを再生するかどうか")]
    [SerializeField]
    private bool playAppearSe = false;

    [Tooltip("オブジェクト登場時に一度だけ再生するSE")]
    [SerializeField, ShowIf(nameof(playAppearSe))]
    private SeSelector appearSe;

    [Header("常駐時")]
    [Tooltip("オブジェクトが存在する間SEをループ再生するかどうか")]
    [SerializeField]
    private bool playLoopSe = false;

    [Tooltip(
        "オブジェクトが存在する間ループ再生するSE(このSEはデータ側でループ設定になっている前提です)"
    )]
    [SerializeField, ShowIf(nameof(playLoopSe))]
    private SeSelector loopSe;

    [Header("オブジェクトが非表示になるときにSEを再生するかどうか")]
    [SerializeField]
    private bool playDisappearSe = false;

    [Tooltip("オブジェクトが非表示になるときに一度だけ再生するSE")]
    [SerializeField, ShowIf(nameof(playDisappearSe))]
    private SeSelector disappearSe;

    private CriWare.Assets.CriAtomSePlayer sePlayer;
    private CriAtomExPlayback loopPlayback; // ループ再生制御用のPlaybackハンドル

    // --- 実行時処理 ---

    private void Awake()
    {
        sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
    }

    private void OnEnable()
    {
        if (playAppearSe)
        {
            // 登場時SEの再生処理
            PlaySe(appearSe);
        }

        // ループ再生を一度だけ実行し、ハンドルを保持
        if (playLoopSe)
        {
            PlayLoopSe();
        }
    }

    private void OnDisable()
    {
        //  全停止ではなく、ループ再生だけを停止
        StopLoopSe();
        if (playDisappearSe)
        {
            // 非表示時SEの再生処理
            PlaySe(disappearSe);
        }
    }

    private void FixedUpdate()
    {
        // // ポーズ制御（TimeManagerが存在する場合）
        // if (TimeManager.instance != null)
        // {
        //     if (TimeManager.instance.isEnemyMovePaused)
        //     {
        //         // ポーズ中かつ再生中なら一時停止
        //         if (loopPlayback.GetStatus() == CriAtomExPlayback.Status.Playing)
        //         {
        //             loopPlayback.Pause(true);
        //         }
        //     }
        //     else
        //     {
        //         // ポーズ解除かつ再生中なら再開
        //         if (loopPlayback.GetStatus() == CriAtomExPlayback.Status.Playing)
        //         {
        //             loopPlayback.Pause(false);
        //         }
        //     }
        // }
    }

    /// <summary>
    /// SE再生処理
    /// </summary>
    private void PlaySe(SeSelector selector)
    {
        // セレクターから有効なEnumを取得
        Enum selectedEnum = selector.GetSelectedEnum();

        // Enumが取得できなければ何もしない（None相当）
        if (selectedEnum == null)
        {
            Debug.LogWarning(
                "ObjectSeManager: SE再生処理で無効なSEが選択されました。再生をスキップします。"
            );
            return;
        }

        sePlayer.Play(selectedEnum); // SE再生
    }

    /// <summary>
    /// ループSE再生処理
    /// </summary>
    private void PlayLoopSe()
    {
        Enum selectedEnum = loopSe.GetSelectedEnum();
        if (selectedEnum == null)
            return;

        // 既に再生中なら二重再生しない
        if (loopPlayback.GetStatus() == CriAtomExPlayback.Status.Playing)
            return;

        // 再生してハンドルを保存
        loopPlayback = sePlayer.Play(selectedEnum);
    }

    /// <summary>
    /// ループSE停止処理
    /// </summary>
    private void StopLoopSe()
    {
        // 再生中または準備中なら停止
        var status = loopPlayback.GetStatus();
        if (status == CriAtomExPlayback.Status.Playing || status == CriAtomExPlayback.Status.Prep)
        {
            loopPlayback.Stop();
        }
    }
}

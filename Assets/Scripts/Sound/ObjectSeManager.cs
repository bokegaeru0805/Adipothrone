using System;
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

    [Tooltip("オブジェクトが存在する間ループ再生するSE")]
    [SerializeField, ShowIf(nameof(playLoopSe))]
    private SeSelector loopSe;

    [Header("オブジェクトが非表示になるときにSEを再生するかどうか")]
    [SerializeField]
    private bool playDisappearSe = false;

    [Tooltip("オブジェクトが非表示になるときに一度だけ再生するSE")]
    [SerializeField, ShowIf(nameof(playDisappearSe))]
    private SeSelector disappearSe;

    private CriWare.Assets.CriAtomSePlayer sePlayer;

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
    }

    private void OnDisable()
    {
        sePlayer.Stop();
        if (playDisappearSe)
        {
            // 非表示時SEの再生処理
            PlaySe(disappearSe);
        }
    }

    private void FixedUpdate()
    {
        //ループSEの再生状態を維持
        if (playLoopSe)
        {
            PlaySe(loopSe);
        }
    }

    /// <summary>
    /// SE再生処理（コメント表記）
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
}

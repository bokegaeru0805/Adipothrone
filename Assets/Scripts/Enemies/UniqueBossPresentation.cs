using System;
using UnityEngine;

#region ボス演出管理クラス
/// <summary>
/// ユニークボスの戦闘開始時や死亡時の演出（アニメーション、BGM制御など）を担当するクラス。
/// UniqueBossHealthから発行されるイベントを受け取って動作します。
/// </summary>
[RequireComponent(typeof(UniqueBossHealth), typeof(Animator))]
public class UniqueBossPresentation : MonoBehaviour
{
    #region インスペクター設定
    [Header("演出設定")]
    [Tooltip("戦闘開始時のBGMクロスフェード時間（秒）")]
    [SerializeField]
    private float crossFadeTime = 1.0f;

    [Tooltip("戦闘終了後にBGMを戻すまでの時間（秒）")]
    [SerializeField]
    private float returnMusicTime = 2.0f;

    [Tooltip("死亡アニメーションのパラメータ名")]
    [SerializeField]
    private string deathAnimParam = "death";
    #endregion

    #region 内部参照
    private UniqueBossHealth bossHealth;
    private Animator animator;
    #endregion

    #region 初期化・イベント登録
    /// <summary>
    /// コンポーネントのキャッシュを行います。
    /// </summary>
    private void Awake()
    {
        bossHealth = GetComponent<UniqueBossHealth>();
        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// オブジェクト有効時にイベントを購読します。
    /// </summary>
    private void OnEnable()
    {
        // HP管理スクリプトの各イベントを購読
        if (bossHealth != null)
        {
            bossHealth.OnBattleActivated += PlayBattleStartPresentation;
            bossHealth.OnDefeated += PlayDeathPresentation;
            bossHealth.OnReset += ResetPresentation;
        }
    }

    /// <summary>
    /// オブジェクト無効時にイベントの購読を解除します。
    /// </summary>
    private void OnDisable()
    {
        // メモリリークを防ぐための購読解除
        if (bossHealth != null)
        {
            bossHealth.OnBattleActivated -= PlayBattleStartPresentation;
            bossHealth.OnDefeated -= PlayDeathPresentation;
            bossHealth.OnReset -= ResetPresentation;
        }
    }
    #endregion

    #region 演出処理ロジック
    /// <summary>
    /// 戦闘開始時の演出を行います。
    /// 指定された時間で専用BGMへクロスフェードします。
    /// </summary>
    private void PlayBattleStartPresentation()
    {
        BGMManager.instance?.Crossfade(BGMCategory.Boss_Unique, crossFadeTime);
    }

    /// <summary>
    /// 死亡時の演出を行います。
    /// 死亡アニメーションへの遷移フラグを立てます。
    /// （※BGMのフェードアウトはアニメーション長に合わせてBossDeathStateBehaviourで行われます）
    /// </summary>
    private void PlayDeathPresentation()
    {
        if (animator != null && !string.IsNullOrEmpty(deathAnimParam))
        {
            animator.SetBool(deathAnimParam, true);

        }
    }

    /// <summary>
    /// ボスが初期状態に戻された際に、演出関連のアニメーターパラメータ等をリセットします。
    /// </summary>
    private void ResetPresentation()
    {
        if (animator != null && !string.IsNullOrEmpty(deathAnimParam))
        {
            animator.SetBool(deathAnimParam, false);
        }
    }

    /// <summary>
    /// BossDeathStateBehaviour（Animatorのステート）から呼び出される、アニメーション完全終了時の処理です。
    /// </summary>
    public void OnDeathAnimationFinished()
    {
        // オブジェクトが消える「前」に、次のエリアBGMを再生するよう指示
        CameraMoveArea.PlayCurrentAreaBgm(returnMusicTime);

        // 最後に自身を非アクティブ化して戦闘を完全に終了する
        this.gameObject.SetActive(false);
    }
    #endregion
}
#endregion

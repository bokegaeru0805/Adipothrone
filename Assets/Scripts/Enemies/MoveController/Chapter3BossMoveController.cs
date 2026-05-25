using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 章ボス（Chapter3）の移動および攻撃パターンを管理するコントローラークラスです。
/// </summary>
public class Chapter3BossMoveController : MonoBehaviour
{
    /// <summary>
    /// ボスの現在の状態を表す列挙型
    /// </summary>
    public enum BossState
    {
        Intro, // 登場演出中
        Idle, // 待機中（下端からの特定座標をキープ）
        NormalAttacking // 通常攻撃中
        ,
    }

    /// <summary>
    /// ボスの現在の状態
    /// </summary>
    public BossState CurrentState { get; private set; } = BossState.Intro;

    [Header("デバッグ機能")]
    [Tooltip(
        "trueの場合、各種待機時間や移動演出の時間を極短にしてデバッグを容易にします（エディタ上のみ有効）"
    )]
    [SerializeField]
    private bool isDebugNoWait = false;

    [Header("エリア境界の設定")]
    [SerializeField]
    private float areaLeftBound = -10f;

    [SerializeField]
    private float areaRightBound = 10f;

    [SerializeField]
    private float areaBottomBound = -5f;

    [SerializeField]
    private float areaTopBound = 10f;

    [Header("Idle状態の設定")]
    [Tooltip("Idle時に下端（areaBottomBound）から維持する高さ")]
    [SerializeField]
    private float idleHeightFromBottom = 2.0f;

    [Tooltip("Idle位置に移行する際にかかる移動時間（秒）")]
    [SerializeField]
    private float idleTransitionDuration = 1.0f;

    [Header("NormalAttack状態の設定")]
    [Tooltip("攻撃時に移動する下端からの高さ")]
    [SerializeField]
    private float attackHeightFromBottom = 4.0f;

    [Tooltip("攻撃準備時間（秒）")]
    [SerializeField]
    private float attackReadyDuration = 1.0f;

    [Tooltip("攻撃時間（秒）")]
    [SerializeField]
    private float attackDuration = 0.5f;

    [Tooltip("攻撃後待機時間（秒）")]
    [SerializeField]
    private float postAttackWaitDuration = 1.0f;

    [Tooltip("通常攻撃の攻撃力")]
    [SerializeField]
    private int normalAttackDamage = 10;

    [Tooltip("通常攻撃時に使用するContactDamageController")]
    [SerializeField]
    private ContactDamageController normalAttackDamageController;

    // 内部管理用変数
    private Animator _animator;
    private Coroutine _actionLoopCoroutine;
    private Tween _moveTween;

    // Animatorパラメータの事前キャッシュ
    private readonly int _idleTriggerHash = Animator.StringToHash("IdleTrigger");
    private readonly int _normalAttackReadyTriggerHash = Animator.StringToHash(
        "NormalAttackReadyTrigger"
    );
    private readonly int _normalAttackTriggerHash = Animator.StringToHash("NormalAttackTrigger");
    private readonly int _normalAttackReadySpeedHash = Animator.StringToHash(
        "NormalAttackReadySpeed"
    );
    private readonly int _normalAttackSpeedHash = Animator.StringToHash("NormalAttackSpeed");

    /// <summary>
    /// エディタ上かつisDebugNoWaitがtrueの場合のみ有効化されるデバッグ判定プロパティ
    /// </summary>
    private bool IsDebugNoWaitActive
    {
        get
        {
#if UNITY_EDITOR
            return isDebugNoWait;
#else
            return false;
#endif
        }
    }

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        ResetState();
    }

    /// <summary>
    /// ボスの状態をリセットし、初期行動（登場シーケンス）を開始します。
    /// </summary>
    public void ResetState()
    {
        if (_actionLoopCoroutine != null)
        {
            StopCoroutine(_actionLoopCoroutine);
            _actionLoopCoroutine = null;
        }

        if (_moveTween != null && _moveTween.IsActive())
        {
            _moveTween.Kill();
        }

        StartCoroutine(IntroSequence());
    }

    /// <summary>
    /// 登場時のシーケンスです。演出完了後にメインの行動ループへ移行します。
    /// </summary>
    private IEnumerator IntroSequence()
    {
        CurrentState = BossState.Intro;

        // 登場時の処理をここに記述（必要に応じて）
        yield return null;

        StartActionLoop();
    }

    /// <summary>
    /// メイン行動ループのコルーチンを開始します。
    /// </summary>
    private void StartActionLoop()
    {
        if (_actionLoopCoroutine != null)
        {
            StopCoroutine(_actionLoopCoroutine);
        }
        _actionLoopCoroutine = StartCoroutine(ActionLoopSequence());
    }

    /// <summary>
    /// 登場 -> 攻撃方法選択 -> 待機 -> 攻撃方法選択 を繰り返すメインループです。
    /// </summary>
    private IEnumerator ActionLoopSequence()
    {
        while (true)
        {
            // 1. 攻撃方法の選択と実行（現在は通常攻撃のみ）
            // ※この中で準備 -> 攻撃 -> リカバリー（待機）まで完了させます
            yield return StartCoroutine(PerformNormalAttack());

            // 2. 待機状態（Idle）への移行
            // ※待機が終わった後に、Idle状態へ戻し始めます
            yield return StartCoroutine(TransitionToIdle());

            // 3. 次の攻撃までのインターバル待機
            // ※Idle定位置に戻った後の待機時間です。ここでは仮で1.0fを入れています（必要に応じてインスペクター変数化してください）
            float waitTime = IsDebugNoWaitActive ? 0.1f : 1.0f;
            if (waitTime < 0.1f)
                waitTime = 0.1f; // 安全対策
            yield return new WaitForSeconds(waitTime);
        }
    }

    /// <summary>
    /// Idle状態に移行し、下端から一定の座標をキープするようにスムーズに移動します。
    /// </summary>
    private IEnumerator TransitionToIdle()
    {
        CurrentState = BossState.Idle;

        if (_animator != null)
            _animator.SetTrigger(_idleTriggerHash);

        // 下端からの目標Y座標を計算（X座標は現在の位置を維持）
        float targetY = areaBottomBound + idleHeightFromBottom;
        float duration = IsDebugNoWaitActive ? 0.1f : idleTransitionDuration;
        if (duration < 0.1f)
            duration = 0.1f;

        // DOTweenを使用してスムーズに移動
        _moveTween = transform.DOMoveY(targetY, duration).SetEase(Ease.InOutQuad);
        yield return _moveTween.WaitForCompletion();
    }

    /// <summary>
    /// 通常攻撃の一連のアクション（準備 -> 攻撃）を実行します。
    /// </summary>
    private IEnumerator PerformNormalAttack()
    {
        CurrentState = BossState.NormalAttacking;

        // 待機時間の決定（デバッグ時は0.1秒に補正して0除算を回避）
        float readyDuration = IsDebugNoWaitActive ? 0.1f : attackReadyDuration;
        float attackDur = IsDebugNoWaitActive ? 0.1f : attackDuration;
        float postWait = IsDebugNoWaitActive ? 0.1f : postAttackWaitDuration; // 攻撃後待機時間を追加

        if (readyDuration < 0.1f)
            readyDuration = 0.1f;
        if (attackDur < 0.1f)
            attackDur = 0.1f;
        if (postWait < 0.1f)
            postWait = 0.1f;

        // --- 1. 準備フェーズ ---
        if (_animator != null)
        {
            SetAnimatorSpeed(_normalAttackReadySpeedHash, readyDuration);
            _animator.SetTrigger(_normalAttackReadyTriggerHash);
        }

        // 攻撃準備時間をかけて指定の高さまで移動（X座標は現在のまま）
        float targetY = areaBottomBound + attackHeightFromBottom;
        _moveTween = transform.DOMoveY(targetY, readyDuration).SetEase(Ease.InOutQuad);
        yield return _moveTween.WaitForCompletion();

        // --- 2. 攻撃フェーズ ---
        if (normalAttackDamageController != null)
        {
            normalAttackDamageController.SetNormalDamage(normalAttackDamage);
        }

        if (_animator != null)
        {
            SetAnimatorSpeed(_normalAttackSpeedHash, attackDur);
            _animator.SetTrigger(_normalAttackTriggerHash);
        }

        yield return new WaitForSeconds(attackDur);

        // --- 3. 攻撃後待機（リカバリー）フェーズ ---
        // Animator側でExitTimeによってChapter3Boss_Recovery_Lowへ自動遷移するため、
        // 座標はそのままで攻撃後待機時間を消化します。
        yield return new WaitForSeconds(postWait);
    }

    /// <summary>
    /// 正規化（1秒）されたアニメーションを指定時間で再生するための速度を設定します。
    /// </summary>
    /// <param name="speedParamHash">変更対象とするAnimatorのFloatパラメータハッシュ</param>
    /// <param name="duration">アニメーションを完了させたい時間（秒）</param>
    private void SetAnimatorSpeed(int speedParamHash, float duration)
    {
        if (_animator == null)
            return;

        // 0除算および極端な高速再生による不具合を回避するため、最小値を0.1秒に制限
        float safeDuration = Mathf.Max(0.1f, duration);
        float speed = 1.0f / safeDuration;

        _animator.SetFloat(speedParamHash, speed);
    }

    /// <summary>
    /// ボスの左右の向きをRotation（Y軸回転）ベースで更新します。
    /// </summary>
    /// <param name="isFacingRight">右を向く場合はtrue、デフォルト（左向き）にする場合はfalse</param>
    public void UpdateFacingDirection(bool isFacingRight)
    {
        // flipXではなく、本体のRotationを回転させて全体の向きを反転させる
        if (isFacingRight)
        {
            transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }
        else
        {
            transform.rotation = Quaternion.identity;
        }
    }

    private void OnDestroy()
    {
        if (_moveTween != null && _moveTween.IsActive())
        {
            _moveTween.Kill();
        }
    }

    private void OnDrawGizmos()
    {
        // エリアの境界範囲をGizmosで立方体描画
        Vector3 center = new Vector3(
            (areaLeftBound + areaRightBound) / 2f,
            (areaTopBound + areaBottomBound) / 2f,
            transform.position.z
        );
        Vector3 size = new Vector3(
            areaRightBound - areaLeftBound,
            areaTopBound - areaBottomBound,
            0.1f
        );

        // 範囲内を薄い赤、外枠をはっきりした赤で表示
        Gizmos.color = new Color(1f, 0f, 0f, 0.05f);
        Gizmos.DrawCube(center, size);

        Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
        Gizmos.DrawWireCube(center, size);

        // 各状態の目標高さを視覚化するためのガイドラインを水平線で表示
        float drawLeft = areaLeftBound;
        float drawRight = areaRightBound;

        // Idle状態のキープ位置（青線）
        Gizmos.color = Color.blue;
        float idleY = areaBottomBound + idleHeightFromBottom;
        Gizmos.DrawLine(
            new Vector3(drawLeft, idleY, transform.position.z),
            new Vector3(drawRight, idleY, transform.position.z)
        );

        // 通常攻撃時の位置（オレンジ線）
        Gizmos.color = new Color(1f, 0.5f, 0f);
        float attackY = areaBottomBound + attackHeightFromBottom;
        Gizmos.DrawLine(
            new Vector3(drawLeft, attackY, transform.position.z),
            new Vector3(drawRight, attackY, transform.position.z)
        );
    }
}

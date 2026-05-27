using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 章ボス（Chapter3）の移動および攻撃パターンを管理するコントローラークラスです。
/// </summary>
public class Chapter3BossMoveController : MonoBehaviour
{
    private const string SHOOT_BULLET_POOLTAG = "Chapter3BossShoot";

    /// <summary>
    /// ボスの現在の状態を表す列挙型
    /// </summary>
    public enum BossState
    {
        Intro, // 登場演出中
        Idle, // 待機中（下端からの特定座標をキープ）
        LowAttacking, // 下段攻撃中
        HighAttacking, // 上段攻撃中
        ThrustAttacking, // 突き攻撃中
        ShootAttacking, // 射撃攻撃中
        RetreatTeleporting // 後退テレポート攻撃中 (新規追加)
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

    [Header("LowAttack(下段攻撃)状態の設定")]
    [Tooltip("攻撃時に移動する下端からの高さ")]
    [SerializeField]
    private float lowAttackHeightFromBottom = 4.0f;

    [Tooltip("攻撃準備時間（秒）")]
    [SerializeField]
    private float lowAttackReadyDuration = 1.0f;

    [Tooltip("攻撃時間（秒）")]
    [SerializeField]
    private float lowAttackDuration = 0.5f;

    [Tooltip("攻撃後待機時間（秒）")]
    [SerializeField]
    private float postLowAttackWaitDuration = 1.0f;

    [Tooltip("下段攻撃の攻撃力")]
    [SerializeField]
    private int lowAttackDamage = 10;

    [Tooltip("下段攻撃時に使用するContactDamageController")]
    [SerializeField]
    private ContactDamageController lowAttackDamageController;

    [Header("HighAttack(上段攻撃)状態の設定")]
    [Tooltip("LowAttack後にHighAttackを行う確率(0.0～1.0)")]
    [Range(0f, 1f)]
    [SerializeField]
    private float highAttackProbability = 0.5f;

    [Tooltip("LowAttack終了からHighAttackに移行するまでの待機時間（秒）")]
    [SerializeField]
    private float waitBeforeHighAttackDuration = 1.0f;

    [Tooltip("攻撃時に移動する下端からの高さ")]
    [SerializeField]
    private float highAttackHeightFromBottom = 6.0f;

    [Tooltip("攻撃準備時間（秒）")]
    [SerializeField]
    private float highAttackReadyDuration = 1.0f;

    [Tooltip("攻撃時間（秒）")]
    [SerializeField]
    private float highAttackDuration = 0.5f;

    [Tooltip("攻撃後待機時間（秒）")]
    [SerializeField]
    private float postHighAttackWaitDuration = 1.0f;

    [Tooltip("上段攻撃の攻撃力")]
    [SerializeField]
    private int highAttackDamage = 15;

    [Tooltip("上段攻撃時に使用するContactDamageController")]
    [SerializeField]
    private ContactDamageController highAttackDamageController;

    [Header("ThrustAttack(突き攻撃)状態の設定")]
    [Tooltip("剣の先のTransform（座標逆算用）")]
    [SerializeField]
    private Transform swordTipTransform;

    [Tooltip("これ以上近づかない・後方時の突進基準となる最小距離")]
    [SerializeField]
    private float minThrustDistance = 3.0f;

    [Tooltip("攻撃準備時（構え）に移動する下端からの高さ")]
    [SerializeField]
    private float thrustReadyHeightFromBottom = 3.0f;

    [Tooltip("攻撃準備時間（秒）")]
    [SerializeField]
    private float thrustReadyDuration = 1.0f;

    [Tooltip("攻撃（突進）時間（秒）")]
    [SerializeField]
    private float thrustDuration = 0.4f;

    [Tooltip("攻撃後待機時間（秒）")]
    [SerializeField]
    private float postThrustWaitDuration = 1.2f;

    [Tooltip("突き攻撃の攻撃力")]
    [SerializeField]
    private int thrustDamage = 20;

    [Tooltip("突き攻撃時に使用するContactDamageController")]
    [SerializeField]
    private ContactDamageController thrustDamageController;

    [Tooltip("突き攻撃時に再生するエフェクト（子オブジェクト）")]
    [SerializeField]
    private ParticleSystem thrustEffect;

    [Header("ShootAttack(射撃攻撃)状態の設定")]
    [Tooltip("攻撃準備時間（秒）")]
    [SerializeField]
    private float shootReadyDuration = 1.0f;

    [Tooltip("攻撃の基本Y座標オフセット")]
    [SerializeField]
    private float shootBulletHeightOffset = 1.0f;

    [Tooltip("弾の速度")]
    [SerializeField]
    private float shootBulletSpeed = 10.0f;

    [Tooltip("弾の攻撃力")]
    [SerializeField]
    private int shootDamage = 10;

    [Tooltip("Shoot攻撃フェーズ自体の時間（秒）")]
    [SerializeField]
    private float shootAttackDuration = 0.5f;

    [Tooltip("連射時の弾と弾の間の発射間隔（秒）")]
    [SerializeField]
    private float shootBulletInterval = 0.3f;

    [Tooltip("攻撃後待機時間（秒）")]
    [SerializeField]
    private float postShootWaitDuration = 1.0f;

    [Tooltip("Shoot攻撃時に再生するエフェクト（子オブジェクト）")]
    [SerializeField]
    private ParticleSystem shootEffect;

    [Header("後退テレポート(RetreatTeleport)状態の設定")]
    [Tooltip("背後への指定距離")]
    [SerializeField]
    private float retreatDistance = 10f;

    [Tooltip("壁からのマージン")]
    [SerializeField]
    private float retreatWallMargin = 2f;

    [Tooltip("初期の消滅にかかる時間（秒）")]
    [SerializeField]
    private float retreatInitialFadeOutTime = 1.0f;

    [Tooltip("ホログラム出現時間（秒）")]
    [SerializeField]
    private float retreatHologramAppearTime = 0.5f;

    [Tooltip("攻撃の時間（秒）")]
    [SerializeField]
    private float retreatAttackDuration = 1.0f;

    [Tooltip("ホログラム再消滅時間（秒）")]
    [SerializeField]
    private float retreatHologramDisappearTime = 0.5f;

    [Tooltip("予め指定する複数の地面からの高さ（areaBottomBoundからのオフセット値）")]
    [SerializeField]
    private float[] retreatHeights;

    [Tooltip(
        "ホログラム演出の対象となるSpriteRenderer（Slashエフェクト等を除外するため手動で設定）"
    )]
    [SerializeField]
    private SpriteRenderer[] hologramTargetRenderers;

    // 内部管理用変数
    private Animator _animator;
    private Coroutine _actionLoopCoroutine;
    private Tween _moveTween;
    private Transform _playerTransform;
    private bool _isFacingRight = false; // 現在右を向いているかどうかのフラグ（デフォルト左向き）

    // Animatorパラメータの事前キャッシュ
    private readonly int _idleTriggerHash = Animator.StringToHash("IdleTrigger");
    private readonly int _idleStateHash = Animator.StringToHash("Chapter3Boss_Idle");

    // LowAttack用ハッシュ
    private readonly int _lowAttackReadyTriggerHash = Animator.StringToHash(
        "LowAttackReadyTrigger"
    );
    private readonly int _lowAttackTriggerHash = Animator.StringToHash("LowAttackTrigger");
    private readonly int _lowAttackReadySpeedHash = Animator.StringToHash("LowAttackReadySpeed");
    private readonly int _lowAttackSpeedHash = Animator.StringToHash("LowAttackSpeed");

    // HighAttack用ハッシュ
    private readonly int _highAttackReadyTriggerHash = Animator.StringToHash(
        "HighAttackReadyTrigger"
    );
    private readonly int _comboHighAttackReadyTriggerHash = Animator.StringToHash(
        "ComboHighAttackReadyTrigger"
    );
    private readonly int _highAttackTriggerHash = Animator.StringToHash("HighAttackTrigger");
    private readonly int _highAttackReadySpeedHash = Animator.StringToHash("HighAttackReadySpeed");
    private readonly int _highAttackSpeedHash = Animator.StringToHash("HighAttackSpeed");

    // ThrustAttack用ハッシュ
    private readonly int _thrustReadyTriggerHash = Animator.StringToHash(
        "ThrustAttackReadyTrigger"
    );
    private readonly int _thrustTriggerHash = Animator.StringToHash("ThrustAttackTrigger");
    private readonly int _thrustReadySpeedHash = Animator.StringToHash("ThrustAttackReadySpeed");
    private readonly int _thrustSpeedHash = Animator.StringToHash("ThrustAttackSpeed");

    // ShootAttack用ハッシュ
    private readonly int _shootReadyTriggerHash = Animator.StringToHash("ShootAttackReadyTrigger");
    private readonly int _shootTriggerHash = Animator.StringToHash("ShootAttackTrigger");
    private readonly int _shootReadySpeedHash = Animator.StringToHash("ShootAttackReadySpeed");
    private readonly int _shootSpeedHash = Animator.StringToHash("ShootAttackSpeed");

    // HorizontalAttack用ハッシュ
    private readonly int _horizontalAttackReadyTriggerHash = Animator.StringToHash(
        "HorizontalAttackReadyTrigger"
    );
    private readonly int _horizontalAttackTriggerHash = Animator.StringToHash(
        "HorizontalAttackTrigger"
    );
    private readonly int _horizontalAttackReadySpeedHash = Animator.StringToHash(
        "HorizontalAttackReadySpeed"
    );
    private readonly int _horizontalAttackSpeedHash = Animator.StringToHash(
        "HorizontalAttackSpeed"
    );

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

        // Animatorの上書きに負けないタイミングで全攻撃判定の透明度を0に初期化
        SetDamageAreaAlpha(0f);

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
            // 本来は確率で攻撃を分岐させるが、今回はRetreatTeleportAttackで固定
            bool forceRetreatTeleportAttack = true;

            if (!forceRetreatTeleportAttack)
            {
                // 次にHighAttackを行うかどうかを事前に判定
                bool willDoHighAttack = Random.value <= highAttackProbability;

                // 1. LowAttackの実行（次にHighAttackが控えているかのフラグを渡す）
                yield return StartCoroutine(PerformLowAttack(willDoHighAttack));

                // 2. 条件を満たしている場合はHighAttackに派生
                if (willDoHighAttack)
                {
                    // 派生前の待機時間（LowAttackのpostWaitを行わない代わりに、ここを通る）
                    float beforeHighWait = IsDebugNoWaitActive
                        ? 0.1f
                        : waitBeforeHighAttackDuration;
                    if (beforeHighWait < 0.1f)
                        beforeHighWait = 0.1f;
                    yield return new WaitForSeconds(beforeHighWait);

                    // HighAttackの実行
                    yield return StartCoroutine(PerformHighAttack());
                }
            }
            else
            {
                // Shoot攻撃（ShootAttack）の実行（今回は3発発射を指定）
                yield return StartCoroutine(PerformRetreatTeleport(3));
            }

            // 3. 待機状態（Idle）への移行
            yield return StartCoroutine(TransitionToIdle());

            // 4. 次の行動ループまでのインターバル待機
            float waitTime = IsDebugNoWaitActive ? 0.1f : 1.0f;
            if (waitTime < 0.1f)
                waitTime = 0.1f;
            yield return new WaitForSeconds(waitTime);
        }
    }

    /// <summary>
    /// Idle状態に移行し、下端から一定の座標をキープするようにスムーズに移動します。
    /// </summary>
    private IEnumerator TransitionToIdle()
    {
        CurrentState = BossState.Idle;

        // 下端からの目標Y座標を計算（X座標は現在の位置を維持）
        float targetY = areaBottomBound + idleHeightFromBottom;
        float duration = IsDebugNoWaitActive ? 0.1f : idleTransitionDuration;
        if (duration < 0.1f)
            duration = 0.1f;

        if (_animator != null)
        {
            // 浮上にかかる移動時間（duration）と同じ秒数をかけて、徐々にIdleアニメーション状態へ遷移させます
            _animator.CrossFadeInFixedTime(_idleStateHash, duration);
        }

        // DOTweenを使用してスムーズに移動
        _moveTween = transform.DOMoveY(targetY, duration).SetEase(Ease.InOutQuad);
        yield return _moveTween.WaitForCompletion();
    }

    /// <summary>
    /// LowAttack（下段攻撃）の一連のアクションを実行します。
    /// </summary>
    private IEnumerator PerformLowAttack(bool skipPostWait)
    {
        CurrentState = BossState.LowAttacking;

        float readyDuration = IsDebugNoWaitActive ? 0.1f : lowAttackReadyDuration;
        float attackDur = IsDebugNoWaitActive ? 0.1f : lowAttackDuration;
        float postWait = IsDebugNoWaitActive ? 0.1f : postLowAttackWaitDuration;

        if (readyDuration < 0.1f)
            readyDuration = 0.1f;
        if (attackDur < 0.1f)
            attackDur = 0.1f;
        if (postWait < 0.1f)
            postWait = 0.1f;

        // --- 1. 準備フェーズ ---
        if (_animator != null)
        {
            SetAnimatorSpeed(_lowAttackReadySpeedHash, readyDuration);
            _animator.SetTrigger(_lowAttackReadyTriggerHash);
        }

        float targetY = areaBottomBound + lowAttackHeightFromBottom;
        _moveTween = transform.DOMoveY(targetY, readyDuration).SetEase(Ease.InOutQuad);
        yield return _moveTween.WaitForCompletion();

        // --- 2. 攻撃フェーズ ---
        if (lowAttackDamageController != null)
        {
            lowAttackDamageController.SetNormalDamage(lowAttackDamage);
        }

        if (_animator != null)
        {
            SetAnimatorSpeed(_lowAttackSpeedHash, attackDur);
            _animator.SetTrigger(_lowAttackTriggerHash);
        }

        yield return new WaitForSeconds(attackDur);

        // --- 3. 攻撃後待機（リカバリー）フェーズ ---
        if (!skipPostWait)
        {
            yield return new WaitForSeconds(postWait);
        }
    }

    /// <summary>
    /// HighAttack（上段攻撃）の一連のアクションを実行します。
    /// </summary>
    private IEnumerator PerformHighAttack()
    {
        CurrentState = BossState.HighAttacking;

        float readyDuration = IsDebugNoWaitActive ? 0.1f : highAttackReadyDuration;
        float attackDur = IsDebugNoWaitActive ? 0.1f : highAttackDuration;
        float postWait = IsDebugNoWaitActive ? 0.1f : postHighAttackWaitDuration;

        if (readyDuration < 0.1f)
            readyDuration = 0.1f;
        if (attackDur < 0.1f)
            attackDur = 0.1f;
        if (postWait < 0.1f)
            postWait = 0.1f;

        // --- 1. 準備フェーズ ---
        if (_animator != null)
        {
            SetAnimatorSpeed(_highAttackReadySpeedHash, readyDuration);
            _animator.SetTrigger(_comboHighAttackReadyTriggerHash);
        }

        float targetY = areaBottomBound + highAttackHeightFromBottom;
        _moveTween = transform.DOMoveY(targetY, readyDuration).SetEase(Ease.InOutQuad);
        yield return _moveTween.WaitForCompletion();

        // --- 2. 攻撃フェーズ ---
        if (highAttackDamageController != null)
        {
            highAttackDamageController.SetNormalDamage(highAttackDamage);
        }

        if (_animator != null)
        {
            SetAnimatorSpeed(_highAttackSpeedHash, attackDur);
            _animator.SetTrigger(_highAttackTriggerHash);
        }

        yield return new WaitForSeconds(attackDur);

        // --- 3. 攻撃後待機（リカバリー）フェーズ ---
        yield return new WaitForSeconds(postWait);
    }

    /// <summary>
    /// ThrustAttack（突き攻撃）の一連のアクションを実行します。
    /// </summary>
    private IEnumerator PerformThrustAttack()
    {
        CurrentState = BossState.ThrustAttacking;

        float readyDuration = IsDebugNoWaitActive ? 0.1f : thrustReadyDuration;
        float attackDur = IsDebugNoWaitActive ? 0.1f : thrustDuration;
        float postWait = IsDebugNoWaitActive ? 0.1f : postThrustWaitDuration;

        if (readyDuration < 0.1f)
            readyDuration = 0.1f;
        if (attackDur < 0.1f)
            attackDur = 0.1f;
        if (postWait < 0.1f)
            postWait = 0.1f;

        // --- 1. 準備フェーズ ---
        if (_animator != null)
        {
            SetAnimatorSpeed(_thrustReadySpeedHash, readyDuration);
            _animator.SetTrigger(_thrustReadyTriggerHash);
        }

        // 突き攻撃準備のための特定高さへ移動（X座標は現在のまま維持）
        float readyY = areaBottomBound + thrustReadyHeightFromBottom;
        _moveTween = transform.DOMoveY(readyY, readyDuration).SetEase(Ease.InOutQuad);
        yield return _moveTween.WaitForCompletion();

        // --- 2. 構え終了後の目標座標計算フェーズ ---
        UpdatePlayerTransformReference();

        // 現在のボスの向きを判定（右向きなら1、左向きなら-1）
        int facingDir = _isFacingRight ? 1 : -1;

        // 剣先の現在のX座標を取得（未設定の場合はボス本体の座標を代用）
        float currentSwordTipX =
            swordTipTransform != null ? swordTipTransform.position.x : transform.position.x;

        // プレイヤーの目標X座標を決定（プレイヤーが未取得の場合は剣先から前方の最小距離先を仮置き）
        float playerX =
            _playerTransform != null
                ? _playerTransform.position.x
                : currentSwordTipX + (facingDir * minThrustDistance);

        // 剣先の前方方向を基準とした、プレイヤーとの水平距離差を計算
        float forwardDistance = (playerX - currentSwordTipX) * facingDir;
        float targetSwordTipX;

        // プレイヤーが剣先から最小距離内（または背後）にいるか、最小距離より遠くにいるかで剣先の目標X座標を分岐
        if (forwardDistance < minThrustDistance)
        {
            targetSwordTipX = currentSwordTipX + (facingDir * minThrustDistance);
        }
        else
        {
            targetSwordTipX = playerX;
        }

        // 剣の先（swordTipTransform）とボス本体の現在位置のオフセット（ズレ）を計算
        Vector3 swordOffset = Vector3.zero;
        if (swordTipTransform != null)
        {
            swordOffset = swordTipTransform.position - transform.position;
        }

        // 剣の先が目標X座標および下端（bottom）のY座標に到達するように、本体の目標座標を逆算
        Vector3 targetBossPosition;
        targetBossPosition.x = targetSwordTipX - swordOffset.x;
        targetBossPosition.y = areaBottomBound - swordOffset.y;
        targetBossPosition.z = transform.position.z;

        // --- 3. 攻撃（突進）フェーズ ---
        if (thrustDamageController != null)
        {
            thrustDamageController.SetNormalDamage(thrustDamage);
        }

        if (_animator != null)
        {
            SetAnimatorSpeed(_thrustSpeedHash, attackDur);
            _animator.SetTrigger(_thrustTriggerHash);
        }

        // エフェクトの切り離しと再生に必要なローカルTransform情報を保存する変数
        Vector3 effectOriginalLocalPos = Vector3.zero;
        Quaternion effectOriginalLocalRot = Quaternion.identity;
        Vector3 effectOriginalLocalScale = Vector3.one;

        if (thrustEffect != null)
        {
            // 親に戻すときのために元のローカル座標・回転・スケールを記録
            effectOriginalLocalPos = thrustEffect.transform.localPosition;
            effectOriginalLocalRot = thrustEffect.transform.localRotation;
            effectOriginalLocalScale = thrustEffect.transform.localScale;

            Vector3 moveDirection = targetBossPosition - transform.position;
            if (moveDirection.sqrMagnitude > 0.001f) // 念のため移動距離がゼロでないか確認
            {
                // 現在地から目標地点への角度を算出し、Z軸回転に適用
                float effectAngle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
                thrustEffect.transform.rotation = Quaternion.Euler(0f, 0f, effectAngle);
            }
            else
            {
                // 移動距離がほぼ無い場合のフォールバック（左右の向きのみ合わせる）
                thrustEffect.transform.rotation = _isFacingRight
                    ? Quaternion.Euler(0f, 0f, 0f)
                    : Quaternion.Euler(0f, 180f, 0f);
            }

            // ボスの移動に追従しないよう、ワールド空間へ一時的に切り離す
            thrustEffect.transform.SetParent(null);

            // まさに突進を開始する瞬間に、その場でエフェクトを再生
            thrustEffect.Play();
        }

        // 迫力を出すため、Ease.OutExpo（超高速で突進し後半急減速する）を適用して目標座標へ一気に移動
        _moveTween = transform.DOMove(targetBossPosition, attackDur).SetEase(Ease.OutExpo);
        yield return new WaitForSeconds(attackDur);

        // --- 4. 攻撃後待機（リカバリー）フェーズ ---
        yield return new WaitForSeconds(postWait);

        // --- 5. 終了処理（エフェクトを親に戻す） ---
        if (thrustEffect != null)
        {
            thrustEffect.Stop(); // 次回の使用に向けて念のため停止

            // 再びボスの子オブジェクトに戻し、記録しておいたローカルTransformを復元する
            thrustEffect.transform.SetParent(transform);
            thrustEffect.transform.localPosition = effectOriginalLocalPos;
            thrustEffect.transform.localRotation = effectOriginalLocalRot;
            thrustEffect.transform.localScale = effectOriginalLocalScale;
        }
    }

    /// <summary>
    /// ShootAttack（射撃攻撃）の一連のアクションを実行します。
    /// </summary>
    /// <param name="shootCount">発射する弾の最大個数</param>
    private IEnumerator PerformShootAttack(int shootCount)
    {
        CurrentState = BossState.ShootAttacking;

        float readyDuration = IsDebugNoWaitActive ? 0.1f : shootReadyDuration;
        float attackDur = IsDebugNoWaitActive ? 0.1f : shootAttackDuration; // 攻撃フェーズ自体の時間
        float bulletInterval = IsDebugNoWaitActive ? 0.1f : shootBulletInterval; // 弾の発射間隔
        float postWait = IsDebugNoWaitActive ? 0.1f : postShootWaitDuration;

        if (readyDuration < 0.1f)
            readyDuration = 0.1f;
        if (attackDur < 0.1f)
            attackDur = 0.1f;
        if (bulletInterval < 0.1f)
            bulletInterval = 0.1f;
        if (postWait < 0.1f)
            postWait = 0.1f;

        // --- 1. 準備フェーズ ---
        if (_animator != null)
        {
            SetAnimatorSpeed(_shootReadySpeedHash, readyDuration);
            _animator.SetTrigger(_shootReadyTriggerHash);
        }

        yield return new WaitForSeconds(readyDuration);

        // --- 2. オフセットリストの作成と抽出 ---
        List<float> allOffsets = new List<float>
        {
            -2f * shootBulletHeightOffset,
            -1f * shootBulletHeightOffset,
            0f,
            1f * shootBulletHeightOffset,
            2f * shootBulletHeightOffset,
        };

        // リストをランダムにシャッフル
        for (int i = 0; i < allOffsets.Count; i++)
        {
            int randomIndex = Random.Range(i, allOffsets.Count);
            float temp = allOffsets[i];
            allOffsets[i] = allOffsets[randomIndex];
            allOffsets[randomIndex] = temp;
        }

        int actualCount = Mathf.Min(shootCount, allOffsets.Count);
        List<float> targetYOffsets = allOffsets.GetRange(0, actualCount);

        // --- 3. 発射ループ ---
        UpdatePlayerTransformReference();
        int facingDir = _isFacingRight ? 1 : -1;

        foreach (float yOffset in targetYOffsets)
        {
            UpdatePlayerTransformReference();

            float currentSwordTipX =
                swordTipTransform != null ? swordTipTransform.position.x : transform.position.x;

            // 懐判定
            bool shouldFire = true;
            if (_playerTransform != null)
            {
                float forwardDistance =
                    (_playerTransform.position.x - currentSwordTipX) * facingDir;
                if (forwardDistance <= 0f)
                {
                    shouldFire = false;
                }
            }

            // 懐に入られていた場合は、これ以降の射撃処理と postWait をすべてスキップして即座にコルーチンを抜ける
            if (!shouldFire)
            {
                yield break;
            }

            // アニメーションの再生速度には「攻撃時間（shootAttackDuration）」を指定
            if (_animator != null)
            {
                SetAnimatorSpeed(_shootSpeedHash, attackDur);
                _animator.SetTrigger(_shootTriggerHash);
            }

            // エフェクトを再生（子オブジェクトのまま）
            if (shootEffect != null)
            {
                shootEffect.Stop(); // 連射時に最初から再生されるよう一度停止する
                shootEffect.Play();
            }

            // 弾の発射
            FireShootBullet(yOffset, facingDir, currentSwordTipX);

            yield return new WaitForSeconds(attackDur); // 攻撃フェーズ自体の時間を待機

            // 次の弾を発射するまでの待機には「発射間隔（shootBulletInterval）」を使用
            yield return new WaitForSeconds(bulletInterval);
        }

        // --- 4. 攻撃後待機（リカバリー）フェーズ ---
        yield return new WaitForSeconds(postWait);
    }

    /// <summary>
    /// Shoot攻撃用の弾を生成・発射します。
    /// </summary>
    /// <param name="yOffset">プレイヤーに対するY座標オフセット</param>
    /// <param name="facingDir">現在のボスの向き（1 or -1）</param>
    /// <param name="startX">弾の生成X座標（剣先）</param>
    private void FireShootBullet(float yOffset, int facingDir, float startX)
    {
        // 発射位置の決定（Y座標も剣先を基準にする）
        float startY =
            swordTipTransform != null ? swordTipTransform.position.y : transform.position.y;
        Vector3 spawnPos = new Vector3(startX, startY, 0f);

        // ターゲット位置の計算
        Vector3 targetPos = Vector3.zero;
        if (_playerTransform != null)
        {
            targetPos = _playerTransform.position + new Vector3(0f, yOffset, 0f);
        }
        else
        {
            // プレイヤーがいない場合は前方へ飛ばす
            targetPos = spawnPos + new Vector3(facingDir * 10f, yOffset, 0f);
        }

        // プールから弾を取得して生成
        GameObject bullet = ObjectPooler.SceneInstance.SpawnFromPool(
            SHOOT_BULLET_POOLTAG,
            spawnPos,
            Quaternion.identity
        );

        if (bullet != null)
        {
            // 進行方向ベクトル
            Vector2 direction = (targetPos - spawnPos).normalized;

            // 弾の回転角度設定
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.Euler(0, 0, angle);

            // 速度の適用
            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = direction * shootBulletSpeed;
            }

            // 攻撃力の設定
            var damageController = bullet.GetComponent<ContactDamageController>();
            if (damageController != null)
            {
                damageController.SetNormalDamage(shootDamage);
            }
        }
    }

    /// <summary>
    /// プレイヤーのTransform参照を最新の状態に更新します。
    /// </summary>
    private void UpdatePlayerTransformReference()
    {
        if (_playerTransform == null)
        {
            if (PlayerManager.instance != null && PlayerManager.instance.PlayerGameObject != null)
            {
                _playerTransform = PlayerManager.instance.PlayerGameObject.transform;
            }
            else
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag(
                    GameConstants.PLAYER_TAG_NAME
                );
                if (playerObj != null)
                    _playerTransform = playerObj.transform;
            }
        }
    }

    /// <summary>
    /// 正規化（1秒）されたアニメーションを指定時間で再生するための速度を設定します。
    /// </summary>
    private void SetAnimatorSpeed(int speedParamHash, float duration)
    {
        if (_animator == null)
            return;

        float safeDuration = Mathf.Max(0.1f, duration);
        float speed = 1.0f / safeDuration;

        _animator.SetFloat(speedParamHash, speed);
    }

    /// <summary>
    /// 攻撃判定エリアのSpriteRendererの透明度を設定します。
    /// </summary>
    private void SetDamageAreaAlpha(float alpha)
    {
        if (lowAttackDamageController != null)
        {
            SpriteRenderer lowRenderer = lowAttackDamageController.GetComponent<SpriteRenderer>();
            if (lowRenderer != null)
            {
                Color color = lowRenderer.color;
                color.a = alpha;
                lowRenderer.color = color;
            }
        }

        if (highAttackDamageController != null)
        {
            SpriteRenderer highRenderer = highAttackDamageController.GetComponent<SpriteRenderer>();
            if (highRenderer != null)
            {
                Color color = highRenderer.color;
                color.a = alpha;
                highRenderer.color = color;
            }
        }

        if (thrustDamageController != null)
        {
            SpriteRenderer thrustRenderer = thrustDamageController.GetComponent<SpriteRenderer>();
            if (thrustRenderer != null)
            {
                Color color = thrustRenderer.color;
                color.a = alpha;
                thrustRenderer.color = color;
            }
        }
    }

    /// <summary>
    /// 後退しながら瞬間移動し、中間地点で攻撃を行う一連のアクションを実行します。
    /// </summary>
    /// <param name="teleportCount">中間地点で攻撃を行う回数</param>
    private IEnumerator PerformRetreatTeleport(int teleportCount)
    {
        CurrentState = BossState.RetreatTeleporting;

        float initialFadeTime = IsDebugNoWaitActive ? 0.1f : retreatInitialFadeOutTime;
        float appearTime = IsDebugNoWaitActive ? 0.1f : retreatHologramAppearTime;
        float attackDur = IsDebugNoWaitActive ? 0.1f : retreatAttackDuration;
        float disappearTime = IsDebugNoWaitActive ? 0.1f : retreatHologramDisappearTime;

        if (initialFadeTime < 0.1f)
            initialFadeTime = 0.1f;
        if (appearTime < 0.1f)
            appearTime = 0.1f;
        if (attackDur < 0.1f)
            attackDur = 0.1f;
        if (disappearTime < 0.1f)
            disappearTime = 0.1f;

        // --- 1. 広い方向の判定と向きの固定 ---
        float distToLeft = transform.position.x - areaLeftBound;
        float distToRight = areaRightBound - transform.position.x;

        // 端からの距離が遠い方（広い空間がある方）を選ぶ
        bool retreatToRight = distToRight >= distToLeft;

        // 後退方向とは「逆」を常に向き続けるように固定する（右に逃げるなら左向き）
        UpdateFacingDirection(!retreatToRight);

        // --- 2. 最終移動X座標の計算 ---
        float startX = transform.position.x;
        float finalX;

        if (retreatToRight)
        {
            float targetX = startX + retreatDistance;
            float wallLimitX = areaRightBound - retreatWallMargin;
            // 右へ進むので、値が小さい（自分に近い）方を採用
            finalX = Mathf.Min(targetX, wallLimitX);
        }
        else
        {
            float targetX = startX - retreatDistance;
            float wallLimitX = areaLeftBound + retreatWallMargin;
            // 左へ進むので、値が大きい（自分に近い）方を採用
            finalX = Mathf.Max(targetX, wallLimitX);
        }

        // --- 3. 最初の消滅演出 ---
        Sequence fadeOutSeq = DOTween.Sequence();
        foreach (var renderer in hologramTargetRenderers)
        {
            if (renderer != null && renderer.material != null)
            {
                Material mat = renderer.material;
                mat.EnableKeyword("_HOLOGRAM_ON");
                mat.SetFloat("_HologramBlend", 1.0f);

                fadeOutSeq.Join(renderer.DOFade(0f, initialFadeTime));
            }
        }
        yield return fadeOutSeq.SetEase(Ease.OutCubic).WaitForCompletion();

        // --- 4. 瞬間移動と攻撃のループ ---
        // 攻撃回数 + 1(最後の出現用) で等分することで、1回目の出現を1区画先から開始する
        float stepX = (finalX - startX) / (teleportCount + 1);

        for (int i = 1; i <= teleportCount; i++)
        {
            // 座標の決定
            float currentTargetX = startX + stepX * i;
            float currentHeight = 0f;
            if (retreatHeights != null && retreatHeights.Length > 0)
            {
                currentHeight = retreatHeights[Random.Range(0, retreatHeights.Length)];
            }
            float currentTargetY = areaBottomBound + currentHeight;

            transform.position = new Vector3(currentTargetX, currentTargetY, transform.position.z);

            // 出現に合わせたアニメーション (Ready)
            if (_animator != null)
            {
                SetAnimatorSpeed(_horizontalAttackReadySpeedHash, appearTime);
                _animator.SetTrigger(_horizontalAttackReadyTriggerHash);
            }

            // ホログラムによる出現演出
            Sequence appearSeq = DOTween.Sequence();
            foreach (var renderer in hologramTargetRenderers)
            {
                if (renderer != null && renderer.material != null)
                {
                    Material mat = renderer.material;
                    mat.EnableKeyword("_HOLOGRAM_ON");
                    mat.SetFloat("_HologramBlend", 1.0f);

                    // 透明度をパッと実体に戻す
                    Color c = renderer.color;
                    c.a = 1f;
                    renderer.color = c;

                    // ホログラムから実体へとブレンドさせる
                    appearSeq.Join(mat.DOFloat(0f, "_HologramBlend", appearTime));
                }
            }

            yield return appearSeq.WaitForCompletion();

            // 実体化後は念のためキーワードを無効化
            foreach (var renderer in hologramTargetRenderers)
            {
                if (renderer != null && renderer.material != null)
                {
                    renderer.material.DisableKeyword("_HOLOGRAM_ON");
                }
            }

            // 攻撃実行と待機
            if (_animator != null)
            {
                SetAnimatorSpeed(_horizontalAttackSpeedHash, attackDur);
                _animator.SetTrigger(_horizontalAttackTriggerHash);
            }

            yield return new WaitForSeconds(attackDur);

            // 再びホログラム演出で消滅
            Sequence disappearSeq = DOTween.Sequence();
            foreach (var renderer in hologramTargetRenderers)
            {
                if (renderer != null && renderer.material != null)
                {
                    Material mat = renderer.material;
                    mat.EnableKeyword("_HOLOGRAM_ON");

                    disappearSeq.Join(mat.DOFloat(1.0f, "_HologramBlend", disappearTime));
                    disappearSeq.Join(renderer.DOFade(0f, disappearTime));
                }
            }
            yield return disappearSeq.SetEase(Ease.OutCubic).WaitForCompletion();
        }

        // --- 5. 最後の出現（攻撃なしでIdleへ戻る） ---
        transform.position = new Vector3(
            finalX,
            areaBottomBound + idleHeightFromBottom,
            transform.position.z
        );

        Sequence finalAppearSeq = DOTween.Sequence();
        foreach (var renderer in hologramTargetRenderers)
        {
            if (renderer != null && renderer.material != null)
            {
                Material mat = renderer.material;
                mat.EnableKeyword("_HOLOGRAM_ON");
                mat.SetFloat("_HologramBlend", 1.0f);

                Color c = renderer.color;
                c.a = 1f;
                renderer.color = c;

                finalAppearSeq.Join(mat.DOFloat(0f, "_HologramBlend", appearTime));
            }
        }

        // 最終出現時はそのままIdleへ滑らかにクロスフェード
        if (_animator != null)
        {
            _animator.CrossFadeInFixedTime(_idleStateHash, appearTime);
        }

        yield return finalAppearSeq.WaitForCompletion();

        // 最終クリーンアップ
        foreach (var renderer in hologramTargetRenderers)
        {
            if (renderer != null && renderer.material != null)
            {
                renderer.material.DisableKeyword("_HOLOGRAM_ON");
            }
        }

        CurrentState = BossState.Idle;
    }

    /// <summary>
    /// ボスの左右の向きをRotation（Y軸回転）ベースで更新し、クラス内の向きフラグを保持します。
    /// </summary>
    public void UpdateFacingDirection(bool isFacingRight)
    {
        _isFacingRight = isFacingRight;

        if (_isFacingRight)
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

        Gizmos.color = new Color(1f, 0f, 0f, 0.05f);
        Gizmos.DrawCube(center, size);

        Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
        Gizmos.DrawWireCube(center, size);

        float drawLeft = areaLeftBound;
        float drawRight = areaRightBound;

        // Idle状態のキープ位置（青線）
        Gizmos.color = Color.blue;
        float idleY = areaBottomBound + idleHeightFromBottom;
        Gizmos.DrawLine(
            new Vector3(drawLeft, idleY, transform.position.z),
            new Vector3(drawRight, idleY, transform.position.z)
        );

        // LowAttack時の位置（オレンジ線）
        Gizmos.color = new Color(1f, 0.5f, 0f);
        float lowAttackY = areaBottomBound + lowAttackHeightFromBottom;
        Gizmos.DrawLine(
            new Vector3(drawLeft, lowAttackY, transform.position.z),
            new Vector3(drawRight, lowAttackY, transform.position.z)
        );

        // HighAttack時の位置（マゼンタ線）
        Gizmos.color = Color.magenta;
        float highAttackY = areaBottomBound + highAttackHeightFromBottom;
        Gizmos.DrawLine(
            new Vector3(drawLeft, highAttackY, transform.position.z),
            new Vector3(drawRight, highAttackY, transform.position.z)
        );

        // ThrustAttackの準備位置（白線）
        Gizmos.color = Color.white;
        float thrustReadyY = areaBottomBound + thrustReadyHeightFromBottom;
        Gizmos.DrawLine(
            new Vector3(drawLeft, thrustReadyY, transform.position.z),
            new Vector3(drawRight, thrustReadyY, transform.position.z)
        );
    }
}

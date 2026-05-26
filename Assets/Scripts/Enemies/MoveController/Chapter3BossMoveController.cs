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
        LowAttacking, // 下段攻撃中
        HighAttacking, // 上段攻撃中
        ThrustAttacking // 突き攻撃中
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
            // 本来は半々の確率でNormalAttackかThrustAttackを分岐させるが、今回は突き攻撃で固定
            bool forceThrustAttack = true;

            if (!forceThrustAttack)
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
                // 突き攻撃（ThrustAttack）の実行
                yield return StartCoroutine(PerformThrustAttack());
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

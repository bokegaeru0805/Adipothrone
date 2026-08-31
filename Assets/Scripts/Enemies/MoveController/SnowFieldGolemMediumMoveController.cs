using System.Collections;
using UnityEngine;

/// <summary>
/// 中型雪原ゴーレムの巡回移動、近距離攻撃、ジャンプ攻撃を制御します。
/// 物理ルートと表示用Visual Rootを分離し、左右反転はVisual RootのY回転だけで行います。
///
/// 攻撃AIの概要:
/// ・歩行中かつ攻撃可能なときだけ、現在向いている正面側にいるプレイヤーを攻撃対象として判定します。
/// ・近距離攻撃範囲に入った場合は、ジャンプ攻撃の条件も満たしていても近距離攻撃を優先します。
/// ・近距離攻撃は上段または下段を確率で選び、Animation Eventで槍のダメージ判定を有効化します。
/// ・ジャンプ攻撃はResetState時と近距離攻撃後に確率で予約され、専用距離内かつ頭上に必要な空間がある場合に開始します。
/// ・ジャンプ開始時のAnimation Eventで槍のダメージ判定を有効化し、着地後はエフェクトを再生して歩行へ戻ります。
/// ・各攻撃後は槍の判定を無効化し、歩行復帰後のクールダウンが終わるまで次の攻撃を行いません。
/// ・敵の移動停止中は、攻撃演出、復帰待ち、クールダウンの経過時間も停止します。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class SnowFieldGolemMediumMoveController : MonoBehaviour, IEnemyResettable
{
    #region 定数・列挙型

    private const float MOVE_RANGE = 10.0f;
    private const float CEILING_CHECK_BOTTOM_OFFSET = 0.05f;

    // Walkアニメーションが1倍速になる基準の水平方向移動速度。
    private const float DEFAULT_WALK_ANIMATION_MOVE_SPEED = 0.75f;

    // 移動速度の変化をWalkアニメーションへ反映する強さ。
    private const float WALK_ANIMATION_SPEED_RESPONSE = 0.6f;

    private enum EnemyVariant
    {
        None = 0,
        SnowField = 1,
    }

    private enum GolemState
    {
        None,
        AdjustingPosition,
        Walking,
        MeleeAttacking,
        PreparingToJump,
        Jumping,
        Landing,
        Recovering,
    }

    private enum MeleeAttackType
    {
        Upper,
        Lower,
    }

    #endregion

    #region インスペクター設定

    [Header("敵のタイプ")]
    [SerializeField]
    [Tooltip("敵のバリエーションタイプ。ダメージ等の初期化に使用します。")]
    private EnemyVariant _variantType = EnemyVariant.SnowField;

    [Header("基本設定")]
    [SerializeField]
    [Tooltip("この敵を管理するEnemyActivator。未設定の場合は親から自動取得します。")]
    private EnemyActivator _activator = null;

    [SerializeField]
    [Tooltip("trueの場合、ResetState時にActivator範囲内へランダム配置しません。")]
    private bool _isUseManualInitialPosition = false;

    [SerializeField]
    [Tooltip("足元中央を表すBottom基準のTransform。攻撃範囲、接地、天井判定の基準です。")]
    private Transform _pivotTransform = null;

    [Header("表示・Animator設定")]
    [SerializeField]
    [Tooltip(
        "Animatorとボーンを持つ表示専用の子オブジェクト。物理ルート自身は指定しないでください。"
    )]
    private Transform _visualRoot = null;

    [SerializeField]
    [Tooltip("Visual Rootに付いているAnimator。物理ルートのAnimatorは使用しません。")]
    private Animator _animator = null;

    [Header("通常移動")]
    [SerializeField, Min(0f)]
    [Tooltip("Walk中の水平方向の速度。")]
    private float _speedX = 3.0f;

    [SerializeField]
    [Tooltip("trueの場合、巡回範囲をAwake時点のPivotのX座標からの相対座標として扱います。")]
    private bool _isUseRelativeBounds = false;

    [SerializeField]
    [Tooltip("巡回範囲の左端のX座標。左右とも0の場合はActivatorから自動設定します。")]
    private float _leftBound = 0f;

    [SerializeField]
    [Tooltip("巡回範囲の右端のワールドX座標。")]
    private float _rightBound = 0f;

    [Header("近距離攻撃範囲")]
    [SerializeField, Min(0f)]
    [Tooltip("正面方向に近距離攻撃を検知するX距離。")]
    private float _meleeAttackRangeX = 3.0f;

    [SerializeField, Min(0f)]
    [Tooltip("Pivotから上方向に近距離攻撃を検知する距離。")]
    private float _meleeAttackRangeYUp = 2.0f;

    [SerializeField, Min(0f)]
    [Tooltip("Pivotから下方向に近距離攻撃を検知する距離。")]
    private float _meleeAttackRangeYDown = 1.0f;

    [Header("近距離攻撃設定")]
    [SerializeField, Range(0f, 1f)]
    [Tooltip("Attack_Upperを選択する確率。外れた場合はAttack_Lowerになります。")]
    private float _upperAttackProbability = 0.5f;

    [SerializeField, Min(0.01f)]
    [Tooltip(
        "Attack_Upper開始から末尾のAnimation Eventが発生するまでの時間。元クリップは1秒想定です。"
    )]
    private float _upperAttackEventTime = 0.7f;

    [SerializeField, Min(0.01f)]
    [Tooltip(
        "Attack_Lower開始から末尾のAnimation Eventが発生するまでの時間。元クリップは1秒想定です。"
    )]
    private float _lowerAttackEventTime = 0.7f;

    [SerializeField, Min(0f)]
    [Tooltip("近距離攻撃のAnimation Event発生後、槍をDamageableにしておく時間。")]
    private float _meleeSpearDamageDuration = 0.3f;

    [SerializeField, Min(0f)]
    [Tooltip("近距離攻撃のAnimation Event発生後、Walkへ戻るまでの時間。")]
    private float _meleeReturnToWalkDelay = 0.8f;

    [SerializeField, Min(0f)]
    [Tooltip("近距離攻撃からWalkへ戻った後、次の攻撃が可能になるまでの時間。")]
    private float _meleeAttackCooldown = 2.0f;

    [Header("ジャンプ攻撃の抽選")]
    [SerializeField, Range(0f, 1f)]
    [Tooltip("ResetState時と近距離攻撃後に、ジャンプ攻撃を予約する確率。")]
    private float _jumpAttackProbability = 0.4f;

    [Header("ジャンプ攻撃範囲")]
    [SerializeField, Min(0f)]
    [Tooltip("ジャンプ攻撃を開始できる正面方向の最小X距離。")]
    private float _jumpAttackMinRangeX = 3.0f;

    [SerializeField, Min(0f)]
    [Tooltip("ジャンプ攻撃を開始できる正面方向の最大X距離。")]
    private float _jumpAttackMaxRangeX = 7.0f;

    [SerializeField, Min(0f)]
    [Tooltip("Pivotから上方向にジャンプ攻撃対象を検知する距離。")]
    private float _jumpAttackRangeYUp = 3.0f;

    [SerializeField, Min(0f)]
    [Tooltip("Pivotから下方向にジャンプ攻撃対象を検知する距離。")]
    private float _jumpAttackRangeYDown = 1.0f;

    [Header("ジャンプ・天井判定")]
    [SerializeField, Min(0.01f)]
    [Tooltip("JumpStartの再生時間。元クリップは1秒想定です。")]
    private float _jumpStartDuration = 0.6f;

    [SerializeField]
    [Tooltip("ジャンプ時の水平方向の初速。")]
    private float _jumpVelocityX = 6.0f;

    [SerializeField]
    [Tooltip("ジャンプ時の垂直方向の初速。")]
    private float _jumpVelocityY = 8.0f;

    [SerializeField, Min(0f)]
    [Tooltip("ジャンプ開始直後に接地判定を無視する時間。")]
    private float _groundIgnoreAfterJumpTime = 0.1f;

    [SerializeField, Min(0f)]
    [Tooltip("ジャンプ攻撃に必要なPivotから上方向の空間。")]
    private float _requiredCeilingHeight = 5.0f;

    [SerializeField, Min(0.01f)]
    [Tooltip("天井空間を確認する矩形の横幅。キャラクター本体の幅に合わせます。")]
    private float _ceilingCheckWidth = 1.5f;

    [Header("ジャンプ着地後")]
    [SerializeField, Min(0f)]
    [Tooltip("着地後、槍をImmuneへ戻すまでの時間。")]
    private float _jumpSpearDisableDelayAfterLanding = 0.2f;

    [SerializeField, Min(0f)]
    [Tooltip("着地後、Walkへ戻るまでの時間。")]
    private float _jumpReturnToWalkDelayAfterLanding = 0.8f;

    [SerializeField, Min(0f)]
    [Tooltip("ジャンプ攻撃からWalkへ戻った後、次の攻撃が可能になるまでの時間。")]
    private float _jumpAttackCooldown = 2.5f;

    [Header("地面・配置判定")]
    [SerializeField, Min(0.01f)]
    [Tooltip("Pivotを中心に接地判定を行う円の半径。")]
    private float _groundCheckRadius = 0.15f;

    [SerializeField]
    [Tooltip("初期配置時の地面への埋まり判定位置。Pivotより少し上に配置します。")]
    private Transform _overlapCheckPoint = null;

    [SerializeField, Min(0.01f)]
    [Tooltip("初期配置時の埋まり判定を行う円の半径。")]
    private float _overlapCheckRadius = 0.4f;

    [SerializeField, Min(0f)]
    [Tooltip("地面への埋まりを解消するときの上方向への補正速度。")]
    private float _verticalAdjustSpeed = 10.0f;

    [Header("槍")]
    [SerializeField]
    [Tooltip("攻撃判定を持つ槍のTransform。Animation EventでTagを切り替えます。")]
    private Transform _spearTransform = null;

    [Header("着地エフェクト")]
    [SerializeField]
    [Tooltip(
        "着地時に表示する子GameObject。表示時にAnimatorのDefault Stateが自動再生される想定です。"
    )]
    private GameObject _landingEffect = null;

    [SerializeField, Min(0f)]
    [Tooltip("着地エフェクトを表示してから非表示に戻すまでの時間。")]
    private float _landingEffectDisplayDuration = 0.6f;

    [Header("スタック検出")]
    [SerializeField, Min(0.01f)]
    [Tooltip("この時間以上ほとんど移動しなかった場合、移動方向を反転します。")]
    private float _timeToReverseWhenStuck = 2.0f;

    [SerializeField, Min(0f)]
    [Tooltip("スタック判定で移動したとみなす最小距離。")]
    private float _stuckDistanceThreshold = 0.1f;

    [SerializeField, Min(0.01f)]
    [Tooltip("スタック判定の間隔。")]
    private float _stuckCheckInterval = 0.5f;

    #endregion

    #region コンポーネント・状態管理

    private Rigidbody2D _rbody;
    private EnemyHealth _enemyHP;
    private ContactDamageController _contactDamageController;
    private ContactDamageController _spearDamageController;
    private Transform _playerTransform;

    private LayerMask _groundLayer;
    private GolemState _currentState = GolemState.None;

    private bool _isFacingRight = true;
    private bool _canAttack = true;
    private bool _isJumpAttackReserved = false;
    private bool _isUseAutoBounds = false;
    private bool _hasProcessedAttackEvent = false;

    private Vector3 _initialPosition = Vector3.zero;
    private float _initialPivotPositionX = 0f;
    private float _resolvedLeftBound = 0f;
    private float _resolvedRightBound = 0f;
    private float _moveVelocityX = 0f;
    private float _jumpStartTime = 0f;
    private int _meleeSpearDamage = 20;
    private int _jumpSpearDamage = 20;

    private Vector3 _visualInitialLocalEulerAngles;
    private Vector2 _lastCheckedPosition;
    private float _timeStuck = 0f;

    private Transform _landingEffectOriginalParent;
    private Vector3 _landingEffectOriginalLocalPosition;
    private Quaternion _landingEffectOriginalLocalRotation;
    private Vector3 _landingEffectOriginalLocalScale;
    private Animator _landingEffectAnimator;

    private Coroutine _spearDisableCoroutine;
    private Coroutine _returnToWalkCoroutine;
    private Coroutine _attackCooldownCoroutine;
    private Coroutine _landingEffectCoroutine;

    #endregion

    #region Animatorパラメータ

    private static readonly int AnimIdleTrigger = Animator.StringToHash("IdleTrigger");
    private static readonly int AnimWalkTrigger = Animator.StringToHash("WalkTrigger");
    private static readonly int AnimAttackUpperTrigger = Animator.StringToHash(
        "AttackUpperTrigger"
    );
    private static readonly int AnimAttackLowerTrigger = Animator.StringToHash(
        "AttackLowerTrigger"
    );
    private static readonly int AnimJumpStartTrigger = Animator.StringToHash("JumpStartTrigger");
    private static readonly int AnimJumpLandTrigger = Animator.StringToHash("JumpLandTrigger");

    private static readonly int AnimWalkSpeed = Animator.StringToHash("WalkSpeed");
    private static readonly int AnimAttackUpperSpeed = Animator.StringToHash("AttackUpperSpeed");
    private static readonly int AnimAttackLowerSpeed = Animator.StringToHash("AttackLowerSpeed");
    private static readonly int AnimJumpStartSpeed = Animator.StringToHash("JumpStartSpeed");
    private static readonly int AnimVerticalSpeed = Animator.StringToHash("VerticalSpeed");
    private static readonly int AnimIsGrounded = Animator.StringToHash("IsGrounded");

    #endregion

    #region 判定用プロパティ

    private Vector3 PivotPosition =>
        _pivotTransform != null ? _pivotTransform.position : transform.position;

    private bool IsGrounded =>
        Physics2D.OverlapCircle(PivotPosition, _groundCheckRadius, _groundLayer) != null;

    private bool IsOverlappingGround =>
        _overlapCheckPoint != null
        && Physics2D.OverlapCircle(_overlapCheckPoint.position, _overlapCheckRadius, _groundLayer)
            != null;

    #endregion

    #region Unityライフサイクル

    private void Awake()
    {
        _initialPosition = transform.position;
        _initialPivotPositionX = PivotPosition.x;

        CacheComponents();
        ResolveVisualReferences();
        InitializeGroundLayer();
        InitializeVariantStatus();
        InitializeSpear();
        InitializeLandingEffect();

        _isUseAutoBounds =
            Mathf.Approximately(_leftBound, 0f) && Mathf.Approximately(_rightBound, 0f);
    }

    private void Start()
    {
        ResetState();
    }

    private void FixedUpdate()
    {
        if (_currentState == GolemState.AdjustingPosition)
            return;

        if (IsEnemyMovePaused())
        {
            if (_rbody.simulated)
                _rbody.simulated = false;

            return;
        }

        if (!_rbody.simulated)
            _rbody.simulated = true;

        UpdateAnimatorParameters();

        if (_currentState != GolemState.Walking)
            return;

        UpdateWalkMovement();
        TryStartAttack();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_currentState != GolemState.Walking)
            return;

        if (((1 << collision.gameObject.layer) & _groundLayer) == 0)
            return;

        foreach (ContactPoint2D contact in collision.contacts)
        {
            // 地面との接触ではなく、横向きの法線を持つ壁に当たった場合だけ反転します。
            if (Mathf.Abs(contact.normal.y) >= 0.1f)
                continue;

            ReverseMoveDirection();
            ApplyHorizontalVelocity();
            return;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        SetSpearDamageActive(false);
        RestoreLandingEffect();
    }

    #endregion

    #region IEnemyResettable

    /// <summary>
    /// 位置、物理状態、攻撃状態、Animator、槍および着地エフェクトを初期状態へ戻します。
    /// </summary>
    public void ResetState()
    {
        StopAllCoroutines(); // 実行中の攻撃・待機・補正処理をすべて停止します。
        ClearCoroutineReferences(); // 保持しているコルーチン参照を初期化します。

        ResolvePlayerTransform(); // PlayerManagerなどからプレイヤーのTransformを取得します。
        ResetDamageControllers(); // 敵本体と槍のダメージ量・HP状態を初期化します。
        ResetAttackObjects(); // 槍の攻撃判定と着地エフェクトを初期状態へ戻します。
        ResetRigidbody(); // Rigidbody2Dの速度・拘束・シミュレーション状態を初期化します。
        RestoreInitialPosition(); // 落下などで変化した座標をAwake時点の位置へ戻します。

        InitializeMoveBounds(); // Activatorを基準に左右の移動範囲を設定します。
        SetRandomInitialPosition(); // 移動範囲内のランダムなX座標へ配置します。
        InitializeMovementState(); // 移動方向・攻撃状態・ジャンプ予約などを初期化します。
        ResetAnimatorState(); // AnimatorのパラメータとTriggerを初期状態へ戻します。

        StartCoroutine(CheckAndAdjustPositionRoutine()); // 地面に埋まっている場合、上方向へ位置を補正します。
        StartCoroutine(CheckIfStuckRoutine()); // Walk中に停止した場合、移動方向を反転させます。
    }

    #endregion

    #region 初期化・参照解決

    private void CacheComponents()
    {
        _rbody = GetComponent<Rigidbody2D>();
        _enemyHP = GetComponent<EnemyHealth>();
        _contactDamageController = GetComponent<ContactDamageController>();
        _activator = _activator != null ? _activator : GetComponentInParent<EnemyActivator>();
    }

    private void InitializeGroundLayer()
    {
        _groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );
    }

    private void InitializeVariantStatus()
    {
        switch (_variantType)
        {
            case EnemyVariant.SnowField:
                _meleeSpearDamage = 118;
                _jumpSpearDamage = 160;
                break;

            default:
                Debug.LogError($"{name}のEnemyVariantが設定されていません。", this);
                break;
        }
    }

    private void InitializeSpear()
    {
        if (_spearTransform == null)
            return;

        _spearDamageController = _spearTransform.GetComponent<ContactDamageController>();
        SetSpearDamageActive(false);
    }

    private void InitializeLandingEffect()
    {
        if (_landingEffect == null)
            return;

        Transform effectTransform = _landingEffect.transform;
        _landingEffectOriginalParent = effectTransform.parent;
        _landingEffectOriginalLocalPosition = effectTransform.localPosition;
        _landingEffectOriginalLocalRotation = effectTransform.localRotation;
        _landingEffectOriginalLocalScale = effectTransform.localScale;
        _landingEffectAnimator = _landingEffect.GetComponent<Animator>();
        _landingEffect.SetActive(false);
    }

    /// <summary>
    /// 物理ルート自身のAnimatorを除外し、表示用の子AnimatorとVisual Rootを解決します。
    /// </summary>
    private void ResolveVisualReferences()
    {
        if (_animator == null)
        {
            foreach (Animator candidate in GetComponentsInChildren<Animator>(true))
            {
                if (candidate.transform == transform)
                    continue;

                _animator = candidate;
                break;
            }
        }

        if (_visualRoot == null && _animator != null)
            _visualRoot = _animator.transform;

        if (_animator == null)
        {
            Debug.LogError($"{name}: 表示用の子Animatorが設定されていません。", this);
        }

        if (_visualRoot == null)
        {
            Debug.LogError($"{name}: Visual Rootが設定されていません。", this);
            return;
        }

        if (_visualRoot == transform)
        {
            Debug.LogError($"{name}: Visual Rootに物理ルート自身は指定できません。", this);
            return;
        }

        _visualInitialLocalEulerAngles = _visualRoot.localEulerAngles;
    }

    private void ResolvePlayerTransform()
    {
        _playerTransform =
            PlayerManager.instance != null
                ? PlayerManager.instance.PlayerGameObject?.transform
                : null;

        // PlayerManagerは存在していても、初期化順によってPlayerGameObjectが未設定の場合があります。
        if (_playerTransform == null)
        {
            _playerTransform = GameObject
                .FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME)
                ?.transform;
        }
    }

    private void ResetDamageControllers()
    {
        _enemyHP?.ResetState();
        _contactDamageController?.SetNormalDamage(0);
        _spearDamageController?.SetNormalDamage(0);
    }

    private void ResetAttackObjects()
    {
        SetSpearDamageActive(false);
        RestoreLandingEffect();
    }

    private void ResetRigidbody()
    {
        if (_rbody == null)
            return;

        _rbody.simulated = true;
        _rbody.constraints = RigidbodyConstraints2D.FreezeRotation;
        _rbody.velocity = Vector2.zero;
    }

    private void RestoreInitialPosition()
    {
        transform.position = _initialPosition;
    }

    private void InitializeMovementState()
    {
        SetRandomInitialMoveDirection();

        _currentState = GolemState.AdjustingPosition;
        _canAttack = true;
        _hasProcessedAttackEvent = false;

        _timeStuck = 0f;
        _lastCheckedPosition = transform.position;

        RollJumpAttackReservation();
    }

    /// <summary>
    /// 最初に歩き出す方向を左右からランダムに決定し、表示の向きを更新します。
    /// </summary>
    private void SetRandomInitialMoveDirection()
    {
        _isFacingRight = Random.value < 0.5f;

        float moveSpeed = Mathf.Abs(_speedX);
        _moveVelocityX = _isFacingRight ? moveSpeed : -moveSpeed;

        ApplyFacingRotation();
    }

    private void ClearCoroutineReferences()
    {
        _spearDisableCoroutine = null;
        _returnToWalkCoroutine = null;
        _attackCooldownCoroutine = null;
        _landingEffectCoroutine = null;
    }

    private void InitializeMoveBounds()
    {
        if (!_isUseAutoBounds)
        {
            float originX = _isUseRelativeBounds ? _initialPivotPositionX : 0f;
            float firstBound = originX + _leftBound;
            float secondBound = originX + _rightBound;
            _resolvedLeftBound = Mathf.Min(firstBound, secondBound);
            _resolvedRightBound = Mathf.Max(firstBound, secondBound);
            return;
        }

        _resolvedLeftBound = 0f;
        _resolvedRightBound = 0f;

        if (_activator == null)
            return;

        Collider2D activatorCollider = _activator.GetComponent<Collider2D>();
        if (activatorCollider == null)
            return;

        float activatorLeftBound = activatorCollider.bounds.min.x;
        float activatorRightBound = activatorCollider.bounds.max.x;
        float randomCenter = Random.Range(activatorLeftBound, activatorRightBound);

        _resolvedLeftBound = Mathf.Max(randomCenter - MOVE_RANGE / 2f, activatorLeftBound);
        _resolvedRightBound = Mathf.Min(randomCenter + MOVE_RANGE / 2f, activatorRightBound);

        if (_resolvedRightBound - _resolvedLeftBound >= MOVE_RANGE)
            return;

        if (Mathf.Approximately(_resolvedLeftBound, activatorLeftBound))
        {
            _resolvedRightBound = Mathf.Min(
                activatorRightBound,
                _resolvedLeftBound + MOVE_RANGE
            );
        }
        else
        {
            _resolvedLeftBound = Mathf.Max(
                activatorLeftBound,
                _resolvedRightBound - MOVE_RANGE
            );
        }
    }

    private void SetRandomInitialPosition()
    {
        if (_isUseManualInitialPosition || _resolvedRightBound <= _resolvedLeftBound)
            return;

        float targetPivotX = Random.Range(_resolvedLeftBound, _resolvedRightBound);
        float differenceX = targetPivotX - PivotPosition.x;
        transform.position += new Vector3(differenceX, 0f, 0f);
    }

    private void ResetAnimatorState()
    {
        if (_animator == null)
            return;

        ResetAnimatorTriggers();

        _animator.SetFloat(AnimWalkSpeed, CalculateWalkAnimationSpeed());
        _animator.SetFloat(AnimAttackUpperSpeed, 1f);
        _animator.SetFloat(AnimAttackLowerSpeed, 1f);
        _animator.SetFloat(AnimJumpStartSpeed, 1f);
        _animator.SetFloat(AnimVerticalSpeed, 0f);
        _animator.SetBool(AnimIsGrounded, IsGrounded);
        _animator.SetTrigger(AnimIdleTrigger);
    }

    private void ResetAnimatorTriggers()
    {
        _animator.ResetTrigger(AnimIdleTrigger);
        _animator.ResetTrigger(AnimWalkTrigger);
        _animator.ResetTrigger(AnimAttackUpperTrigger);
        _animator.ResetTrigger(AnimAttackLowerTrigger);
        _animator.ResetTrigger(AnimJumpStartTrigger);
        _animator.ResetTrigger(AnimJumpLandTrigger);
    }

    #endregion

    #region 通常移動・向き制御

    private void BeginWalkingWithoutCooldown()
    {
        _currentState = GolemState.Walking;
        _canAttack = true;

        ApplyHorizontalVelocity();
        UpdateWalkAnimationSpeed();
        _animator?.SetTrigger(AnimWalkTrigger);
    }

    private void BeginWalkingAfterAttack(float cooldown, bool rerollJumpReservation)
    {
        if (rerollJumpReservation)
            RollJumpAttackReservation();

        _currentState = GolemState.Walking;

        ApplyHorizontalVelocity();
        UpdateWalkAnimationSpeed();
        _animator?.SetTrigger(AnimWalkTrigger);

        StartAttackCooldown(cooldown);
    }

    private void UpdateWalkMovement()
    {
        float pivotX = PivotPosition.x;

        bool reachedLeftBound = pivotX <= _resolvedLeftBound && _moveVelocityX < 0f;
        bool reachedRightBound = pivotX >= _resolvedRightBound && _moveVelocityX > 0f;

        if (reachedLeftBound || reachedRightBound)
            ReverseMoveDirection();

        ApplyHorizontalVelocity();
    }

    private void ApplyHorizontalVelocity()
    {
        _rbody.velocity = new Vector2(_moveVelocityX, _rbody.velocity.y);
    }

    private void ReverseMoveDirection()
    {
        _moveVelocityX = -_moveVelocityX;
        _isFacingRight = _moveVelocityX >= 0f;
        ApplyFacingRotation();
    }

    private void ApplyFacingRotation()
    {
        if (_visualRoot == null)
            return;

        Vector3 eulerAngles = _visualInitialLocalEulerAngles;
        eulerAngles.y += _isFacingRight ? 0f : 180f;
        _visualRoot.localRotation = Quaternion.Euler(eulerAngles);
    }

    private void UpdateWalkAnimationSpeed()
    {
        if (_animator == null)
            return;

        _animator.SetFloat(AnimWalkSpeed, CalculateWalkAnimationSpeed());
    }

    /// <summary>
    /// 現在の移動速度からWalkアニメーションの再生倍率を計算します。
    /// 単純比例では高速時にアニメーションが速くなりすぎるため、平方根で増加を緩やかにしています。
    /// </summary>
    private float CalculateWalkAnimationSpeed()
    {
        float speedRatio = Mathf.Abs(_speedX) / DEFAULT_WALK_ANIMATION_MOVE_SPEED;

        return 1f + (Mathf.Sqrt(speedRatio) - 1f) * WALK_ANIMATION_SPEED_RESPONSE;
    }

    #endregion

    #region 攻撃判定

    private void TryStartAttack()
    {
        if (!_canAttack || _playerTransform == null)
            return;

        // 両方の条件を満たす場合は、近距離攻撃を必ず優先します。
        if (IsPlayerInMeleeAttackRange())
        {
            StartMeleeAttack();
            return;
        }

        if (_isJumpAttackReserved && IsPlayerInJumpAttackRange() && HasEnoughCeilingSpace())
        {
            StartJumpAttack();
        }
    }

    private bool IsPlayerInMeleeAttackRange()
    {
        if (_playerTransform == null)
            return false;

        Vector2 difference = _playerTransform.position - PivotPosition;
        float forwardDistance = difference.x * GetFacingMultiplier();

        bool isWithinRangeX = forwardDistance > 0f && forwardDistance <= _meleeAttackRangeX;
        bool isWithinRangeY =
            difference.y >= -_meleeAttackRangeYDown && difference.y <= _meleeAttackRangeYUp;

        return isWithinRangeX && isWithinRangeY;
    }

    private bool IsPlayerInJumpAttackRange()
    {
        if (_playerTransform == null)
            return false;

        Vector2 difference = _playerTransform.position - PivotPosition;
        float forwardDistance = difference.x * GetFacingMultiplier();

        bool isWithinRangeX =
            forwardDistance >= _jumpAttackMinRangeX && forwardDistance <= _jumpAttackMaxRangeX;
        bool isWithinRangeY =
            difference.y >= -_jumpAttackRangeYDown && difference.y <= _jumpAttackRangeYUp;

        return isWithinRangeX && isWithinRangeY;
    }

    /// <summary>
    /// Pivot直上の矩形内に地面系Layerが存在しないことを確認します。
    /// 足元付近を誤検出しないよう、判定開始位置をわずかに上へずらしています。
    /// </summary>
    private bool HasEnoughCeilingSpace()
    {
        float checkHeight = Mathf.Max(0.01f, _requiredCeilingHeight - CEILING_CHECK_BOTTOM_OFFSET);

        Vector2 boxSize = new Vector2(_ceilingCheckWidth, checkHeight);
        Vector2 boxCenter =
            (Vector2)PivotPosition + Vector2.up * (CEILING_CHECK_BOTTOM_OFFSET + checkHeight / 2f);

        return Physics2D.OverlapBox(boxCenter, boxSize, 0f, _groundLayer) == null;
    }

    private float GetFacingMultiplier()
    {
        return _isFacingRight ? 1f : -1f;
    }

    #endregion

    #region 近距離攻撃

    private void StartMeleeAttack()
    {
        _canAttack = false;
        _currentState = GolemState.MeleeAttacking;
        _hasProcessedAttackEvent = false;
        _rbody.velocity = Vector2.zero;
        SetSpearDamageActive(false);

        MeleeAttackType attackType =
            Random.value <= _upperAttackProbability ? MeleeAttackType.Upper : MeleeAttackType.Lower;

        PlayMeleeAttackAnimation(attackType);
    }

    private void PlayMeleeAttackAnimation(MeleeAttackType attackType)
    {
        if (_animator == null)
            return;

        if (attackType == MeleeAttackType.Upper)
        {
            _animator.SetFloat(
                AnimAttackUpperSpeed,
                CalculateNormalizedAnimationSpeed(_upperAttackEventTime)
            );
            _animator.SetTrigger(AnimAttackUpperTrigger);
            return;
        }

        _animator.SetFloat(
            AnimAttackLowerSpeed,
            CalculateNormalizedAnimationSpeed(_lowerAttackEventTime)
        );
        _animator.SetTrigger(AnimAttackLowerTrigger);
    }

    /// <summary>
    /// 近距離攻撃のAnimation Eventを起点に、槍の無効化とWalk復帰を並列で開始します。
    /// </summary>
    private void StartMeleeRecoveryTimers()
    {
        _currentState = GolemState.Recovering;
        StopRecoveryCoroutines();

        _spearDisableCoroutine = StartCoroutine(
            DisableSpearAfterDelayRoutine(_meleeSpearDamageDuration)
        );

        _returnToWalkCoroutine = StartCoroutine(
            ReturnToWalkAfterDelayRoutine(
                _meleeReturnToWalkDelay,
                _meleeAttackCooldown,
                true,
                GolemState.Recovering
            )
        );
    }

    #endregion

    #region ジャンプ攻撃

    private void StartJumpAttack()
    {
        _canAttack = false;
        _isJumpAttackReserved = false;
        _currentState = GolemState.PreparingToJump;
        _hasProcessedAttackEvent = false;
        _rbody.velocity = Vector2.zero;
        SetSpearDamageActive(false);

        if (_animator != null)
        {
            _animator.SetFloat(
                AnimJumpStartSpeed,
                CalculateNormalizedAnimationSpeed(_jumpStartDuration)
            );
            _animator.SetTrigger(AnimJumpStartTrigger);
        }

        StartCoroutine(JumpAttackRoutine());
    }

    private IEnumerator JumpAttackRoutine()
    {
        yield return StartCoroutine(WaitWhileEnemyMoveActive(_jumpStartDuration));

        if (_currentState != GolemState.PreparingToJump)
            yield break;

        _currentState = GolemState.Jumping;
        _jumpStartTime = Time.time;

        float horizontalVelocity = GetFacingMultiplier() * _jumpVelocityX;
        _rbody.velocity = new Vector2(horizontalVelocity, _jumpVelocityY);

        yield return StartCoroutine(WaitUntilLandedRoutine());

        if (_currentState != GolemState.Jumping)
            yield break;

        BeginJumpLanding();
    }

    private IEnumerator WaitUntilLandedRoutine()
    {
        while (true)
        {
            if (!IsEnemyMovePaused())
            {
                bool hasPassedIgnoreTime = Time.time - _jumpStartTime >= _groundIgnoreAfterJumpTime;
                bool isFallingOrStopped = _rbody.velocity.y <= 0.01f;

                if (hasPassedIgnoreTime && isFallingOrStopped && IsGrounded)
                    yield break;
            }

            yield return null;
        }
    }

    private void BeginJumpLanding()
    {
        _currentState = GolemState.Landing;
        _rbody.velocity = Vector2.zero;

        UpdateAnimatorParameters();
        _animator?.SetTrigger(AnimJumpLandTrigger);

        PlayLandingEffect();
        StartJumpLandingRecoveryTimers();
    }

    /// <summary>
    /// 着地を起点に、槍の無効化とWalk復帰をそれぞれ独立した時間で開始します。
    /// </summary>
    private void StartJumpLandingRecoveryTimers()
    {
        _currentState = GolemState.Recovering;
        StopRecoveryCoroutines();

        _spearDisableCoroutine = StartCoroutine(
            DisableSpearAfterDelayRoutine(_jumpSpearDisableDelayAfterLanding)
        );

        _returnToWalkCoroutine = StartCoroutine(
            ReturnToWalkAfterDelayRoutine(
                _jumpReturnToWalkDelayAfterLanding,
                _jumpAttackCooldown,
                false,
                GolemState.Recovering
            )
        );
    }

    #endregion

    #region Animation Event

    /// <summary>
    /// Attack_Upper、Attack_Lower、JumpStartの末尾から呼び出します。
    /// 槍をDamageableへ変更し、近距離攻撃の場合は回復タイマーも開始します。
    /// </summary>
    public void OnSpearAttackAnimationEvent()
    {
        if (_hasProcessedAttackEvent)
            return;

        bool isMeleeAttack = _currentState == GolemState.MeleeAttacking;
        bool isJumpAttack =
            _currentState == GolemState.PreparingToJump || _currentState == GolemState.Jumping;

        if (!isMeleeAttack && !isJumpAttack)
            return;

        _hasProcessedAttackEvent = true;
        SetSpearDamage(isMeleeAttack ? _meleeSpearDamage : _jumpSpearDamage);
        SetSpearDamageActive(true);

        if (isMeleeAttack)
            StartMeleeRecoveryTimers();
    }

    #endregion

    #region 槍・回復・クールダウン

    private void SetSpearDamage(int damage)
    {
        _spearDamageController?.SetNormalDamage(damage);
    }

    private void SetSpearDamageActive(bool isActive)
    {
        if (_spearTransform == null)
            return;

        _spearTransform.gameObject.tag = isActive
            ? GameConstants.DAMAGEABLE_ENEMY_TAG_NAME
            : GameConstants.IMMUNE_ENEMY_TAG_NAME;
    }

    private IEnumerator DisableSpearAfterDelayRoutine(float delay)
    {
        yield return StartCoroutine(WaitWhileEnemyMoveActive(delay));

        SetSpearDamageActive(false);
        _spearDisableCoroutine = null;
    }

    private IEnumerator ReturnToWalkAfterDelayRoutine(
        float delay,
        float cooldown,
        bool rerollJumpReservation,
        GolemState requiredState
    )
    {
        yield return StartCoroutine(WaitWhileEnemyMoveActive(delay));

        if (_currentState != requiredState)
            yield break;

        _returnToWalkCoroutine = null;
        BeginWalkingAfterAttack(cooldown, rerollJumpReservation);
    }

    private void StartAttackCooldown(float cooldown)
    {
        if (_attackCooldownCoroutine != null)
            StopCoroutine(_attackCooldownCoroutine);

        _canAttack = false;
        _attackCooldownCoroutine = StartCoroutine(AttackCooldownRoutine(cooldown));
    }

    private IEnumerator AttackCooldownRoutine(float cooldown)
    {
        yield return StartCoroutine(WaitWhileEnemyMoveActive(cooldown));

        _canAttack = true;
        _attackCooldownCoroutine = null;
    }

    private void StopRecoveryCoroutines()
    {
        StopCoroutineAndClear(ref _spearDisableCoroutine);
        StopCoroutineAndClear(ref _returnToWalkCoroutine);
    }

    private void StopCoroutineAndClear(ref Coroutine coroutine)
    {
        if (coroutine == null)
            return;

        StopCoroutine(coroutine);
        coroutine = null;
    }

    private void RollJumpAttackReservation()
    {
        _isJumpAttackReserved = Random.value <= _jumpAttackProbability;
    }

    private float CalculateNormalizedAnimationSpeed(float duration)
    {
        return 1f / Mathf.Max(0.01f, duration);
    }

    #endregion

    #region 着地エフェクト

    private void PlayLandingEffect()
    {
        if (_landingEffect == null)
            return;

        StopCoroutineAndClear(ref _landingEffectCoroutine);
        RestoreLandingEffect();
        ApplyLandingEffectFacingPosition();

        // 本体から切り離し、再生中のエフェクトを着地点へ固定します。
        _landingEffect.transform.SetParent(null, true);
        _landingEffect.SetActive(true);

        // 非表示から再表示するたびに、Default Stateを先頭から再生します。
        if (_landingEffectAnimator != null)
        {
            _landingEffectAnimator.Rebind();
            _landingEffectAnimator.Update(0f);
        }

        _landingEffectCoroutine = StartCoroutine(HideLandingEffectRoutine());
    }

    /// <summary>
    /// 右向き用に保存された初期位置を基準に、左向き時はX座標を反転します。
    /// </summary>
    private void ApplyLandingEffectFacingPosition()
    {
        Transform effectTransform = _landingEffect.transform;
        float facingMultiplier = _isFacingRight ? 1f : -1f;

        effectTransform.localPosition = new Vector3(
            _landingEffectOriginalLocalPosition.x * facingMultiplier,
            _landingEffectOriginalLocalPosition.y,
            _landingEffectOriginalLocalPosition.z
        );
    }

    private IEnumerator HideLandingEffectRoutine()
    {
        yield return StartCoroutine(WaitWhileEnemyMoveActive(_landingEffectDisplayDuration));

        RestoreLandingEffect();
        _landingEffectCoroutine = null;
    }

    private void RestoreLandingEffect()
    {
        if (_landingEffect == null)
            return;

        Transform effectTransform = _landingEffect.transform;
        _landingEffect.SetActive(false);
        effectTransform.SetParent(_landingEffectOriginalParent, false);
        effectTransform.localPosition = _landingEffectOriginalLocalPosition;
        effectTransform.localRotation = _landingEffectOriginalLocalRotation;
        effectTransform.localScale = _landingEffectOriginalLocalScale;
    }

    #endregion

    #region 共通コルーチン・補助処理

    /// <summary>
    /// 敵の移動停止中は経過時間を進めず、指定時間だけ待機します。
    /// </summary>
    private IEnumerator WaitWhileEnemyMoveActive(float duration)
    {
        float timer = 0f;

        while (timer < duration)
        {
            if (!IsEnemyMovePaused())
                timer += Time.deltaTime;

            yield return null;
        }
    }

    private IEnumerator CheckAndAdjustPositionRoutine()
    {
        if (_overlapCheckPoint != null && IsOverlappingGround)
        {
            _rbody.simulated = false;

            while (IsOverlappingGround)
            {
                if (!IsEnemyMovePaused())
                {
                    transform.position += Vector3.up * (_verticalAdjustSpeed * Time.deltaTime);
                }

                yield return null;
            }

            _rbody.simulated = true;
        }

        BeginWalkingWithoutCooldown();
    }

    private IEnumerator CheckIfStuckRoutine()
    {
        while (true)
        {
            yield return StartCoroutine(WaitWhileEnemyMoveActive(_stuckCheckInterval));

            if (_currentState != GolemState.Walking)
            {
                ResetStuckDetection();
                continue;
            }

            float movedDistance = Vector2.Distance(transform.position, _lastCheckedPosition);

            _timeStuck =
                movedDistance < _stuckDistanceThreshold ? _timeStuck + _stuckCheckInterval : 0f;

            _lastCheckedPosition = transform.position;

            if (_timeStuck < _timeToReverseWhenStuck)
                continue;

            _timeStuck = 0f;
            ReverseMoveDirection();
            ApplyHorizontalVelocity();
        }
    }

    private void ResetStuckDetection()
    {
        _timeStuck = 0f;
        _lastCheckedPosition = transform.position;
    }

    private bool IsEnemyMovePaused()
    {
        return TimeManager.instance != null && TimeManager.instance.isEnemyMovePaused;
    }

    private void UpdateAnimatorParameters()
    {
        if (_animator == null || _rbody == null)
            return;

        _animator.SetBool(AnimIsGrounded, IsGrounded);
        _animator.SetFloat(AnimVerticalSpeed, _rbody.velocity.y);

        if (_currentState == GolemState.Walking)
            UpdateWalkAnimationSpeed();
    }

    #endregion

    #region デバッグ描画

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 pivotPosition =
            _pivotTransform != null ? _pivotTransform.position : transform.position;

        bool facingRight = Application.isPlaying ? _isFacingRight : true;
        float facingMultiplier = facingRight ? 1f : -1f;

        DrawPivotAndGroundCheckGizmos(pivotPosition);
        DrawMeleeRangeGizmo(pivotPosition, facingMultiplier);
        DrawJumpRangeGizmo(pivotPosition, facingMultiplier);
        DrawCeilingCheckGizmo(pivotPosition);
        DrawOverlapCheckGizmo();
        DrawFacingDirectionGizmo(pivotPosition, facingRight);
    }

    private void OnDrawGizmos()
    {
        float gizmoLeftBound;
        float gizmoRightBound;

        if (Application.isPlaying)
        {
            gizmoLeftBound = _resolvedLeftBound;
            gizmoRightBound = _resolvedRightBound;
        }
        else
        {
            if (Mathf.Approximately(_leftBound, 0f) && Mathf.Approximately(_rightBound, 0f))
                return;

            float originX = _isUseRelativeBounds ? PivotPosition.x : 0f;
            gizmoLeftBound = Mathf.Min(originX + _leftBound, originX + _rightBound);
            gizmoRightBound = Mathf.Max(originX + _leftBound, originX + _rightBound);
        }

        if (gizmoRightBound <= gizmoLeftBound)
            return;

        Vector3 pivotPosition =
            _pivotTransform != null ? _pivotTransform.position : transform.position;

        Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
        Vector3 center = new Vector3(
            (gizmoLeftBound + gizmoRightBound) / 2f,
            pivotPosition.y,
            pivotPosition.z
        );
        Vector3 size = new Vector3(gizmoRightBound - gizmoLeftBound, 2f, 0.1f);
        Gizmos.DrawCube(center, size);
    }

    private void DrawPivotAndGroundCheckGizmos(Vector3 pivotPosition)
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(pivotPosition, 0.12f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pivotPosition, _groundCheckRadius);
    }

    private void DrawMeleeRangeGizmo(Vector3 pivotPosition, float facingMultiplier)
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);

        Vector3 size = new Vector3(
            _meleeAttackRangeX,
            _meleeAttackRangeYUp + _meleeAttackRangeYDown,
            0.1f
        );
        Vector3 center =
            pivotPosition
            + new Vector3(
                facingMultiplier * _meleeAttackRangeX / 2f,
                (_meleeAttackRangeYUp - _meleeAttackRangeYDown) / 2f,
                0f
            );

        Gizmos.DrawCube(center, size);
    }

    private void DrawJumpRangeGizmo(Vector3 pivotPosition, float facingMultiplier)
    {
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.2f);

        float rangeWidth = Mathf.Max(0f, _jumpAttackMaxRangeX - _jumpAttackMinRangeX);
        Vector3 size = new Vector3(rangeWidth, _jumpAttackRangeYUp + _jumpAttackRangeYDown, 0.1f);
        Vector3 center =
            pivotPosition
            + new Vector3(
                facingMultiplier * (_jumpAttackMinRangeX + rangeWidth / 2f),
                (_jumpAttackRangeYUp - _jumpAttackRangeYDown) / 2f,
                0f
            );

        Gizmos.DrawCube(center, size);
    }

    private void DrawCeilingCheckGizmo(Vector3 pivotPosition)
    {
        Gizmos.color = new Color(1f, 0f, 1f, 0.2f);

        float checkHeight = Mathf.Max(0.01f, _requiredCeilingHeight - CEILING_CHECK_BOTTOM_OFFSET);
        Vector3 size = new Vector3(_ceilingCheckWidth, checkHeight, 0.1f);
        Vector3 center =
            pivotPosition + Vector3.up * (CEILING_CHECK_BOTTOM_OFFSET + checkHeight / 2f);

        Gizmos.DrawCube(center, size);
    }

    private void DrawOverlapCheckGizmo()
    {
        if (_overlapCheckPoint == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_overlapCheckPoint.position, _overlapCheckRadius);
    }

    private void DrawFacingDirectionGizmo(Vector3 pivotPosition, bool facingRight)
    {
        Gizmos.color = Color.blue;
        Vector3 direction = facingRight ? Vector3.right : Vector3.left;
        Gizmos.DrawLine(pivotPosition, pivotPosition + direction * 0.8f);
    }
#endif

    #endregion
}

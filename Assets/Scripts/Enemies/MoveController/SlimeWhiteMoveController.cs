using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 白スライムの挙動（移動・ジャンプ攻撃・着地時エフェクトおよびFreezeMist生成）を制御するクラス。
/// 移動時は進行方向に向きを合わせ、攻撃時はその向きを固定してアクションを行います。
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class SlimeWhiteMoveController : MonoBehaviour, IEnemyResettable
{
    #region 定数・列挙型

    private const float MOVE_RANGE = 10.0f;
    private const string MIST_POOL_TAG = "FreezeMist";

    private enum EnemyVariant
    {
        None = 0,
        SnowField = 1,
    }

    private enum SlimeState
    {
        None,
        Moving,
        PreparingToJump,
        Jumping,
        Recovering,
        AdjustingPosition,
    }

    private enum JumpAttackType
    {
        Normal,
        High,
    }

    #endregion

    #region インスペクター設定

    [Header("敵のタイプ")]
    [SerializeField]
    [Tooltip("敵のバリエーションタイプを指定します。ステータス等の初期化に影響します。")]
    private EnemyVariant _variantType = EnemyVariant.SnowField;

    [Header("基本設定")]
    [SerializeField]
    [Tooltip("この敵をアクティブにする範囲を管理するActivatorコンポーネント。")]
    private EnemyActivator _activator = null;

    [SerializeField]
    [Tooltip("trueの場合、ResetState時にActivator範囲内のランダム配置を行いません。")]
    private bool _isUseManualInitialPosition = false;

    [Header("通常移動設定")]
    [SerializeField]
    [Tooltip("通常移動時の水平方向の速度。")]
    private float _speedX = 4.0f;

    [SerializeField]
    [Tooltip("trueの場合、移動可能範囲をAwake時点の自身のX座標からの相対座標として扱います。")]
    private bool _isUseRelativeBounds = false;

    [SerializeField]
    [Tooltip("移動可能範囲の左端のX座標。両端が0の場合は自動で設定されます。")]
    private float _leftBound = 0f;

    [SerializeField]
    [Tooltip("移動可能範囲の右端のX座標。")]
    private float _rightBound = 0f;

    [Header("攻撃範囲")]
    [SerializeField]
    [Tooltip("自身の正面方向に対する攻撃判定の長さ。")]
    private float _attackRangeX = 4.0f;

    [SerializeField]
    [Tooltip("自身の足元から上方向への攻撃判定範囲。")]
    private float _attackRangeY = 2.0f;

    [SerializeField]
    [Tooltip("自身の足元から下方向への攻撃判定範囲。")]
    private float _attackRangeYDown = 1.0f;

    [Header("ジャンプ攻撃の抽選")]
    [SerializeField, Range(0f, 1f)]
    [Tooltip("HighJumpを選ぶ確率。外れた場合は通常Jumpになります。")]
    private float _highJumpProbability = 0.35f;

    [Header("通常Jump設定")]
    [SerializeField]
    [Tooltip("通常Jump開始前の待機時間。")]
    private float _normalJumpStartWaitTime = 0.5f;

    [SerializeField]
    [Tooltip("通常ジャンプ時の水平方向の初速。")]
    private float _normalJumpVelocityX = 4.0f;

    [SerializeField]
    [Tooltip("通常ジャンプ時の垂直方向の初速。")]
    private float _normalJumpVelocityY = 6.0f;

    [SerializeField]
    [Tooltip("通常ジャンプ着地後の硬直時間。")]
    private float _normalAfterLandingWaitTime = 0.5f;

    [Header("HighJump設定")]
    [SerializeField]
    [Tooltip("HighJump開始前の待機時間。HighJumpStartアニメーションは1秒正規化想定です。")]
    private float _highJumpStartWaitTime = 0.8f;

    [SerializeField]
    [Tooltip("ハイジャンプ時の水平方向の初速。")]
    private float _highJumpVelocityX = 7.0f;

    [SerializeField]
    [Tooltip("ハイジャンプ時の垂直方向の初速。")]
    private float _highJumpVelocityY = 10.0f;

    [SerializeField]
    [Tooltip("ハイジャンプ着地後の硬直時間。")]
    private float _highAfterLandingWaitTime = 0.8f;

    [Header("ジャンプ中・着地判定")]
    [SerializeField]
    [Tooltip("ジャンプ直後に接地判定を無視する時間（即座に着地判定されるのを防ぐため）。")]
    private float _groundIgnoreAfterJumpTime = 0.1f;

    [Header("地面判定用の設定")]
    [SerializeField]
    [Tooltip("接地判定の基準となるTransform。")]
    private Transform _groundCheck;

    [SerializeField]
    [Tooltip("接地判定を行う円の半径。")]
    private float _groundCheckRadius = 0.2f;

    [Header("配置調整用の設定")]
    [SerializeField]
    [Tooltip("地面への埋まり判定を行うTransform。")]
    private Transform _overlapCheckPoint;

    [SerializeField]
    [Tooltip("埋まり判定を行う円の半径。")]
    private float _overlapCheckRadius = 0.5f;

    [SerializeField]
    [Tooltip("埋まり解消時に上方向へ補正移動する速度。")]
    private float _verticalAdjustSpeed = 10.0f;

    [Header("着地エフェクト設定")]
    [SerializeField]
    [Tooltip("着地時に表示するエフェクトのゲームオブジェクト（普段は子オブジェクトとして配置）。")]
    private GameObject _landingEffect;

    [SerializeField]
    [Tooltip("着地エフェクトがフェードアウトして消えるまでの時間（秒）。")]
    private float _landingEffectFadeDuration = 0.5f;

    [Header("FreezeMist設定")]
    [SerializeField, Range(0f, 1f)]
    [Tooltip("着地時にFreezeMistを生成する確率。")]
    private float _mistSpawnProbability = 0.35f;

    [SerializeField]
    [Tooltip("FreezeMistを生成する位置（自身の座標からのオフセット）。")]
    private Vector2 _mistSpawnOffset = Vector2.zero;

    [SerializeField]
    [Tooltip("着地してからFreezeMistを生成するまでの遅延時間（秒）。")]
    private float _mistSpawnDelay = 0.2f;

    [SerializeField]
    [Tooltip("生成したFreezeMistが自然消滅するまでの時間。")]
    private float _mistDuration = 3.0f;

    [Header("スタック検出")]
    [SerializeField]
    [Tooltip("壁などに引っかかり、スタックしたと判定されるまでの時間。")]
    private float _timeToReverseWhenStuck = 2.0f;

    [SerializeField]
    [Tooltip("スタック判定の基準となる、1チェックあたりの最小移動距離。")]
    private float _stuckDistanceThreshold = 0.1f;

    [SerializeField]
    [Tooltip("スタック判定を行う間隔（秒）。")]
    private float _stuckCheckInterval = 0.5f;

    #endregion

    #region プライベート変数

    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rbody;
    private EnemyHealth _enemyHP;
    private ContactDamageController _contactDamageController;
    private CriWare.Assets.CriAtomSePlayer _sePlayer;
    private Transform _playerTransform;

    private LayerMask _groundLayer;
    private SlimeState _currentState = SlimeState.None;
    private bool _isFacingRight = true;
    private bool _isUseAutoBounds = false;
    private Vector3 _initialPosition = Vector3.zero;
    private float _resolvedLeftBound = 0f;
    private float _resolvedRightBound = 0f;
    private float _vx = 0f;
    private float _jumpStartTime = 0f;
    private int _damage = 0;

    // スタック検出用
    private Vector2 _lastCheckedPosition;
    private float _timeStuck = 0f;

    // 着地エフェクトの初期状態保存用
    private Vector3 _landingEffectOriginalLocalPos = Vector3.zero;
    private Quaternion _landingEffectOriginalLocalRot = Quaternion.identity;
    private Vector3 _landingEffectOriginalLocalScale = Vector3.one;

    private readonly List<FreezeMistController> _spawnedMists = new List<FreezeMistController>();

    // Animatorハッシュ
    private static readonly int AnimIsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int AnimVerticalSpeed = Animator.StringToHash("VerticalSpeed");
    private static readonly int AnimJumpStartTrigger = Animator.StringToHash("JumpStartTrigger");
    private static readonly int AnimHighJumpStartTrigger = Animator.StringToHash("HighJumpStartTrigger");
    private static readonly int AnimIdleTrigger = Animator.StringToHash("IdleTrigger");
    private static readonly int AnimJumpStartSpeed = Animator.StringToHash("JumpStartSpeed");
    private static readonly int AnimHighJumpStartSpeed = Animator.StringToHash("HighJumpStartSpeed");

    private bool _isGrounded
    {
        get
        {
            return _groundCheck != null
                && Physics2D.OverlapCircle(_groundCheck.position, _groundCheckRadius, _groundLayer);
        }
    }

    private bool _isOverlappingGround
    {
        get
        {
            return _overlapCheckPoint != null
                && Physics2D.OverlapCircle(_overlapCheckPoint.position, _overlapCheckRadius, _groundLayer);
        }
    }

    #endregion

    #region Unityライフサイクル

    private void Awake()
    {
        _initialPosition = transform.position;

        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _rbody = GetComponent<Rigidbody2D>();
        _enemyHP = GetComponent<EnemyHealth>();
        _contactDamageController = GetComponent<ContactDamageController>();
        _sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
        _activator = GetComponentInParent<EnemyActivator>();

        _groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );

        switch (_variantType)
        {
            case EnemyVariant.SnowField:
                _damage = 92;
                break;
            default:
                Debug.LogError($"{this.name}のEnemyVariantが設定されていません。", this);
                break;
        }

        // 着地エフェクトの初期ローカル情報を保存し、非表示にしておく
        if (_landingEffect != null)
        {
            _landingEffectOriginalLocalPos = _landingEffect.transform.localPosition;
            _landingEffectOriginalLocalRot = _landingEffect.transform.localRotation;
            _landingEffectOriginalLocalScale = _landingEffect.transform.localScale;
            _landingEffect.SetActive(false);
        }

        _isUseAutoBounds = _leftBound == 0f && _rightBound == 0f;
    }

    private void Start()
    {
        ResetState();
    }

    private void FixedUpdate()
    {
        if (_playerTransform == null || _currentState == SlimeState.AdjustingPosition)
            return;

        if (IsEnemyMovePaused())
        {
            if (_rbody.simulated) _rbody.simulated = false;
            return;
        }
        else if (!_rbody.simulated)
        {
            _rbody.simulated = true;
        }

        UpdateAnimatorParameters();

        switch (_currentState)
        {
            case SlimeState.Moving:
                UpdateFacingDirection();
                UpdateNormalMovement();

                if (IsPlayerInAttackRange())
                {
                    JumpAttackType attackType = Random.value <= _highJumpProbability
                            ? JumpAttackType.High
                            : JumpAttackType.Normal;

                    StartCoroutine(JumpAttackRoutine(attackType));
                }
                break;

            case SlimeState.PreparingToJump:
            case SlimeState.Jumping:
            case SlimeState.Recovering:
                // ジャンプ準備・中・硬直中は向きや移動を更新しない
                break;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_currentState != SlimeState.Moving)
            return;

        if (((1 << collision.gameObject.layer) & _groundLayer) == 0)
            return;

        // 壁（法線のY成分がほぼ0）にぶつかったら進行方向を反転
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (Mathf.Abs(contact.normal.y) < 0.1f)
            {
                ReverseMoveDirection();
                _rbody.velocity = new Vector2(_vx, _rbody.velocity.y);
                return;
            }
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ClearSpawnedMists();
        RestoreLandingEffectParent(); // 非アクティブ時にエフェクトが放置されるのを防ぐ
    }

    #endregion

    #region インターフェース実装 (IEnemyResettable)

    public void ResetState()
    {
        StopAllCoroutines();
        ClearSpawnedMists();
        RestoreLandingEffectParent();

        ResolvePlayerTransform();

        _enemyHP?.ResetState();
        _contactDamageController?.SetNormalDamage(_damage);

        if (_rbody != null)
        {
            _rbody.simulated = true;
            _rbody.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rbody.velocity = Vector2.zero;
        }

        transform.position = _initialPosition;

        InitializeMoveBounds();
        SetRandomInitialPosition();
        SetRandomMoveDirection();
        UpdateFacingDirection();

        tag = GameConstants.IMMUNE_ENEMY_TAG_NAME;
        _currentState = SlimeState.Moving;
        _timeStuck = 0f;
        _lastCheckedPosition = transform.position;

        ResetAnimatorState();

        StartCoroutine(CheckAndAdjustPosition());
        StartCoroutine(CheckIfStuckCoroutine());
    }

    #endregion

    #region 初期化・状態リセット処理

    private void ResolvePlayerTransform()
    {
        if (PlayerManager.instance != null)
        {
            _playerTransform = PlayerManager.instance.PlayerGameObject?.transform;
        }
        else
        {
            _playerTransform = GameObject.FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME)?.transform;
        }
    }

    private void InitializeMoveBounds()
    {
        if (!_isUseAutoBounds)
        {
            float originX = _isUseRelativeBounds ? _initialPosition.x : 0f;
            float firstBound = originX + _leftBound;
            float secondBound = originX + _rightBound;
            _resolvedLeftBound = Mathf.Min(firstBound, secondBound);
            _resolvedRightBound = Mathf.Max(firstBound, secondBound);
            return;
        }

        _resolvedLeftBound = 0f;
        _resolvedRightBound = 0f;

        if (_activator == null) return;

        Collider2D activatorCollider = _activator.GetComponent<Collider2D>();
        if (activatorCollider == null) return;

        float activatorLeftBound = activatorCollider.bounds.min.x;
        float activatorRightBound = activatorCollider.bounds.max.x;

        float randomCenter = Random.Range(activatorLeftBound, activatorRightBound);
        _resolvedLeftBound = Mathf.Max(randomCenter - MOVE_RANGE / 2f, activatorLeftBound);
        _resolvedRightBound = Mathf.Min(randomCenter + MOVE_RANGE / 2f, activatorRightBound);

        if (_resolvedRightBound - _resolvedLeftBound < MOVE_RANGE)
        {
            if (Mathf.Approximately(_resolvedLeftBound, activatorLeftBound))
                _resolvedRightBound = Mathf.Min(activatorRightBound, _resolvedLeftBound + MOVE_RANGE);
            else
                _resolvedLeftBound = Mathf.Max(activatorLeftBound, _resolvedRightBound - MOVE_RANGE);
        }
    }

    private void SetRandomInitialPosition()
    {
        if (_isUseManualInitialPosition || _resolvedRightBound <= _resolvedLeftBound) return;

        Vector3 startPos = transform.position;
        transform.position = new Vector3(
            Random.Range(_resolvedLeftBound, _resolvedRightBound),
            startPos.y,
            startPos.z
        );
    }

    private void SetRandomMoveDirection()
    {
        _vx = (Random.value < 0.5f ? -1f : 1f) * _speedX;
    }

    private void ResetAnimatorState()
    {
        if (_animator == null) return;

        _animator.ResetTrigger(AnimJumpStartTrigger);
        _animator.ResetTrigger(AnimHighJumpStartTrigger);
        _animator.ResetTrigger(AnimIdleTrigger);

        _animator.SetFloat(AnimJumpStartSpeed, 1f);
        _animator.SetFloat(AnimHighJumpStartSpeed, 1f);

        UpdateAnimatorParameters();
        _animator.SetTrigger(AnimIdleTrigger);
    }

    #endregion

    #region 通常移動・向き制御

    private void UpdateFacingDirection()
    {
        if (_spriteRenderer == null) return;

        if (_vx > 0f)
        {
            _isFacingRight = true;
            _spriteRenderer.flipX = false;
        }
        else if (_vx < 0f)
        {
            _isFacingRight = false;
            _spriteRenderer.flipX = true;
        }
    }

    private void UpdateNormalMovement()
    {
        if ((transform.position.x <= _resolvedLeftBound && _vx < 0f) ||
            (transform.position.x >= _resolvedRightBound && _vx > 0f))
        {
            ReverseMoveDirection();
        }

        _rbody.velocity = new Vector2(_vx, _rbody.velocity.y);
    }

    private void ReverseMoveDirection()
    {
        _vx = -_vx;
    }

    /// <summary>
    /// プレイヤーが攻撃範囲内にいるかを判定します。
    /// 現在の移動方向を「正面」とし、自身の向いている方向にのみ攻撃判定を持ちます。
    /// </summary>
    private bool IsPlayerInAttackRange()
    {
        if (_playerTransform == null) return false;

        Vector2 diff = _playerTransform.position - transform.position;

        // _vx > 0 なら右向き（1）、_vx < 0 なら左向き（-1）として距離を計算
        float facingMultiplier = _vx >= 0f ? 1f : -1f;
        float forwardDistance = diff.x * facingMultiplier;

        bool isWithinRangeX = forwardDistance >= 0f && forwardDistance <= _attackRangeX;
        bool isWithinRangeY = diff.y >= -_attackRangeYDown && diff.y <= _attackRangeY;

        return isWithinRangeX && isWithinRangeY;
    }

    #endregion

    #region ジャンプ攻撃制御

    private IEnumerator JumpAttackRoutine(JumpAttackType attackType)
    {
        _currentState = SlimeState.PreparingToJump;
        tag = GameConstants.IMMUNE_ENEMY_TAG_NAME;
        _rbody.velocity = Vector2.zero;

        // 攻撃準備に入った時点の移動方向で向きを固定
        UpdateFacingDirection();

        float startWaitTime;
        float jumpVelocityX;
        float jumpVelocityY;
        float afterLandingWaitTime;

        if (attackType == JumpAttackType.High)
        {
            startWaitTime = _highJumpStartWaitTime;
            jumpVelocityX = _highJumpVelocityX;
            jumpVelocityY = _highJumpVelocityY;
            afterLandingWaitTime = _highAfterLandingWaitTime;

            _animator.SetFloat(AnimHighJumpStartSpeed, GetAnimationSpeedFromDuration(startWaitTime));
            _animator.SetTrigger(AnimHighJumpStartTrigger);
        }
        else
        {
            startWaitTime = _normalJumpStartWaitTime;
            jumpVelocityX = _normalJumpVelocityX;
            jumpVelocityY = _normalJumpVelocityY;
            afterLandingWaitTime = _normalAfterLandingWaitTime;

            _animator.SetFloat(AnimJumpStartSpeed, GetAnimationSpeedFromDuration(startWaitTime));
            _animator.SetTrigger(AnimJumpStartTrigger);
        }

        // ジャンプ前の溜め時間待機
        yield return StartCoroutine(WaitWhileEnemyMoveActive(startWaitTime));

        if (_currentState != SlimeState.PreparingToJump) yield break;

        // ジャンプ開始
        _currentState = SlimeState.Jumping;
        _jumpStartTime = Time.time;
        tag = GameConstants.DAMAGEABLE_ENEMY_TAG_NAME;

        float dirX = _isFacingRight ? 1f : -1f;
        _rbody.velocity = new Vector2(dirX * jumpVelocityX, jumpVelocityY);
        _sePlayer?.Play(SE_EnemyAction.Attack_slime1);

        // 着地するまで待機（アニメーションの遷移は Animator 側で行う）
        yield return StartCoroutine(WaitUntilLanded());

        if (_currentState != SlimeState.Jumping) yield break;

        // 着地・硬直開始
        _currentState = SlimeState.Recovering;
        tag = GameConstants.IMMUNE_ENEMY_TAG_NAME;
        _rbody.velocity = Vector2.zero;
        UpdateAnimatorParameters(); // 着地直後のAnimator条件を確実にする

        // 着地時の付随アクション
        PlayLandingEffect();
        StartCoroutine(SpawnMistWithDelayRoutine());

        // 着地後の硬直時間待機
        yield return StartCoroutine(WaitWhileEnemyMoveActive(afterLandingWaitTime));

        // 状態リセットして通常移動へ戻る
        _animator.SetTrigger(AnimIdleTrigger);
        _currentState = SlimeState.Moving;
        SetRandomMoveDirection();
    }

    private float GetAnimationSpeedFromDuration(float duration)
    {
        return 1f / Mathf.Max(0.01f, duration);
    }

    private IEnumerator WaitUntilLanded()
    {
        while (true)
        {
            if (!IsEnemyMovePaused())
            {
                // ジャンプ直後の誤判定を防ぐ時間経過チェックと、落下中（y速度 <= 0）であることを確認
                bool hasPassedIgnoreTime = Time.time - _jumpStartTime >= _groundIgnoreAfterJumpTime;
                bool isFallingOrStopped = _rbody.velocity.y <= 0.01f;

                if (hasPassedIgnoreTime && isFallingOrStopped && _isGrounded)
                    yield break;
            }
            yield return null;
        }
    }

    #endregion

    #region エフェクト・FreezeMist制御

    /// <summary>
    /// 着地エフェクトを再生します。
    /// エフェクトが本体の動きに追従しないよう、ワールド空間に切り離して再生します。
    /// </summary>
    private void PlayLandingEffect()
    {
        if (_landingEffect == null) return;

        // ワールド空間に固定するため親から切り離す
        _landingEffect.transform.SetParent(null);

        SpriteRenderer effectSr = _landingEffect.GetComponent<SpriteRenderer>();
        if (effectSr != null)
        {
            Color color = effectSr.color;
            color.a = 1f;
            effectSr.color = color;

            _landingEffect.SetActive(true);
            StartCoroutine(LandingEffectFadeOutRoutine(effectSr));
        }
    }

    private IEnumerator LandingEffectFadeOutRoutine(SpriteRenderer effectSr)
    {
        yield return effectSr
            .DOFade(0f, _landingEffectFadeDuration)
            .SetEase(Ease.OutCubic)
            .WaitForCompletion();

        RestoreLandingEffectParent();
    }

    /// <summary>
    /// 再生が完了したエフェクトをスライムの子オブジェクトに戻し、初期状態を復元します。
    /// </summary>
    private void RestoreLandingEffectParent()
    {
        if (_landingEffect == null) return;

        _landingEffect.SetActive(false);
        _landingEffect.transform.SetParent(transform);
        _landingEffect.transform.localPosition = _landingEffectOriginalLocalPos;
        _landingEffect.transform.localRotation = _landingEffectOriginalLocalRot;
        _landingEffect.transform.localScale = _landingEffectOriginalLocalScale;
    }

    private IEnumerator SpawnMistWithDelayRoutine()
    {
        yield return new WaitForSeconds(_mistSpawnDelay);

        // 待機中に状態がリセットされていた場合は生成を中止
        if (!this.gameObject.activeInHierarchy || _currentState == SlimeState.None)
            yield break;

        TrySpawnFreezeMist();
    }

    private void TrySpawnFreezeMist()
    {
        if (Random.value > _mistSpawnProbability) return;

        if (ObjectPooler.SceneInstance == null)
        {
            Debug.LogWarning($"{this.name}: ObjectPoolerが見つからないためFreezeMistを生成できません。", this);
            return;
        }

        Vector3 spawnPos = transform.position + new Vector3(_mistSpawnOffset.x, _mistSpawnOffset.y, 0f);
        GameObject mistObject = ObjectPooler.SceneInstance.SpawnFromPool(MIST_POOL_TAG, spawnPos, Quaternion.identity);

        if (mistObject != null)
        {
            FreezeMistController mistController = mistObject.GetComponent<FreezeMistController>();
            if (mistController != null)
            {
                _spawnedMists.Add(mistController);
                mistController.Initialize(Vector2.zero, _mistDuration, 0f, 0f);
            }
        }
    }

    private void ClearSpawnedMists()
    {
        for (int i = _spawnedMists.Count - 1; i >= 0; i--)
        {
            FreezeMistController mist = _spawnedMists[i];
            if (mist != null && mist.gameObject.activeInHierarchy)
            {
                mist.ReturnToPool();
            }
        }
        _spawnedMists.Clear();
    }

    #endregion

    #region 補助・スタック・接地判定

    private IEnumerator WaitWhileEnemyMoveActive(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            if (!IsEnemyMovePaused()) timer += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator CheckAndAdjustPosition()
    {
        if (_overlapCheckPoint == null) yield break;

        if (_isOverlappingGround)
        {
            _currentState = SlimeState.AdjustingPosition;
            _rbody.simulated = false;

            while (_isOverlappingGround)
            {
                transform.position += new Vector3(0f, _verticalAdjustSpeed * Time.deltaTime, 0f);
                yield return null;
            }

            _rbody.simulated = true;
            _currentState = SlimeState.Moving;
        }
    }

    private IEnumerator CheckIfStuckCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(_stuckCheckInterval);

            if (_currentState != SlimeState.Moving || IsEnemyMovePaused())
            {
                _timeStuck = 0f;
                _lastCheckedPosition = transform.position;
                continue;
            }

            float distanceMoved = Vector2.Distance(transform.position, _lastCheckedPosition);

            if (distanceMoved < _stuckDistanceThreshold)
            {
                _timeStuck += _stuckCheckInterval;
            }
            else
            {
                _timeStuck = 0f;
            }

            _lastCheckedPosition = transform.position;

            if (_timeStuck >= _timeToReverseWhenStuck)
            {
                _timeStuck = 0f;
                ReverseMoveDirection();
                _rbody.velocity = new Vector2(_vx, _rbody.velocity.y);
            }
        }
    }

    private bool IsEnemyMovePaused()
    {
        return TimeManager.instance != null && TimeManager.instance.isEnemyMovePaused;
    }

    private void UpdateAnimatorParameters()
    {
        if (_animator == null || _rbody == null) return;

        _animator.SetBool(AnimIsGrounded, _isGrounded);
        _animator.SetFloat(AnimVerticalSpeed, _rbody.velocity.y);
    }

    #endregion

    #region デバッグ描画

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        bool currentFacingRight = _isFacingRight;

        if (!Application.isPlaying)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                currentFacingRight = !sr.flipX;
            }
        }

        // 攻撃範囲 (向いている方向のみに表示)
        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        Vector3 attackSize = new Vector3(_attackRangeX, _attackRangeY + _attackRangeYDown, 0.1f);

        float centerXOffset = (_attackRangeX / 2f) * (currentFacingRight ? 1f : -1f);
        float centerYOffset = (_attackRangeY - _attackRangeYDown) / 2f;
        Vector3 attackCenter = transform.position + new Vector3(centerXOffset, centerYOffset, 0f);
        Gizmos.DrawCube(attackCenter, attackSize);

        // 接地判定
        if (_groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_groundCheck.position, _groundCheckRadius);
        }

        // 埋まり判定
        if (_overlapCheckPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_overlapCheckPoint.position, _overlapCheckRadius);
        }

        // Mist生成位置
        Gizmos.color = Color.yellow;
        Vector3 mistPos = transform.position + new Vector3(_mistSpawnOffset.x, _mistSpawnOffset.y, 0f);
        Gizmos.DrawSphere(mistPos, 0.15f);

        // 向きの目安
        Gizmos.color = Color.blue;
        Vector3 dir = currentFacingRight ? Vector3.right : Vector3.left;
        Gizmos.DrawLine(transform.position, transform.position + dir * 0.75f);
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
            if (_leftBound == 0f && _rightBound == 0f) return;

            float originX = _isUseRelativeBounds ? transform.position.x : 0f;
            gizmoLeftBound = Mathf.Min(originX + _leftBound, originX + _rightBound);
            gizmoRightBound = Mathf.Max(originX + _leftBound, originX + _rightBound);
        }

        if (gizmoRightBound <= gizmoLeftBound) return;

        Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
        Vector3 center = new Vector3(
            (gizmoLeftBound + gizmoRightBound) / 2f,
            transform.position.y,
            transform.position.z
        );
        Vector3 size = new Vector3(gizmoRightBound - gizmoLeftBound, 2f, 0.1f);
        Gizmos.DrawCube(center, size);
    }
#endif

    #endregion
}

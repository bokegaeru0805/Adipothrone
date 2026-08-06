using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class SnowFairyMoveController : MonoBehaviour, IEnemyResettable
{
    private const int BEZIER_LENGTH_SAMPLE_COUNT = 64;

    private enum EnemyVariant
    {
        None = 0,
        SnowField = 1,
    }

    private enum SnowFairyState
    {
        Move = 0,
        CrystalShot = 1,
        CrystalShotRecovery = 2,
        SnowballDropPrepare = 3,
        SnowballDropExecute = 4,
        SnowballDropRecovery = 5,
    }

    [Header("敵のタイプ")]
    [SerializeField, Tooltip("敵のバリエーションタイプ。攻撃力等の初期化に使用します。")]
    private EnemyVariant _variantType = EnemyVariant.SnowField;

    [Header("参照設定")]
    [SerializeField, Tooltip("この敵を管理するEnemyActivator。未設定時は親から取得します。")]
    private EnemyActivator _activator = null;

    [SerializeField, Tooltip("雪の結晶射撃で生成するPrefab。SnowFairyCrystalProjectileが必要です。")]
    private GameObject _crystalProjectilePrefab = null;

    [SerializeField, Tooltip("雪玉落下攻撃の出現エフェクト用子オブジェクト。Animatorが必要です。")]
    private GameObject _snowballSpawnEffectObject = null;

    [SerializeField, Tooltip("雪玉落下攻撃で落とす子オブジェクト。SnowFairySnowballProjectileが必要です。")]
    private GameObject _snowballObject = null;

    [BoxGroup("手動移動範囲")]
    [SerializeField, Tooltip("EnemyActivatorではなく、手動で指定したワールドX範囲を使用します。")]
    private bool _isUseManualBounds = false;

    [BoxGroup("手動移動範囲")]
    [SerializeField, ShowIf(nameof(_isUseManualBounds)), Tooltip("手動移動範囲の左端（ワールドX座標）")]
    private float _manualLeftBound = -5f;

    [BoxGroup("手動移動範囲")]
    [SerializeField, ShowIf(nameof(_isUseManualBounds)), Tooltip("手動移動範囲の右端（ワールドX座標）")]
    private float _manualRightBound = 5f;

    [Header("基本移動設定")]
    [SerializeField, Tooltip("ベジェ曲線上を移動する速度（1秒あたりのワールド座標距離）")]
    private float _moveSpeed = 3f;

    [SerializeField, Tooltip("始点と終点を除く制御点数。三次ベジェのため2を使用します。")]
    private int _controlPointCount = 2;

    [SerializeField, Tooltip("地面から維持する最小高さ")]
    private float _minHeightFromGround = 2f;

    [SerializeField, Tooltip("地面から維持する最大高さ")]
    private float _maxHeightFromGround = 5f;

    [SerializeField, Tooltip("地面を検出するRaycastの最大距離")]
    private float _groundDetectDistance = 40f;

    [SerializeField, Tooltip("EnemyActivator範囲の左右端から取る余白")]
    private float _horizontalBoundsMargin = 0.5f;

    [SerializeField, Tooltip("目的地の抽選を試行する最大回数")]
    private int _waypointPickAttempts = 12;

    [Header("雪の結晶射撃設定")]
    [SerializeField, Tooltip("プレイヤーを検知する円形範囲の半径")]
    private float _crystalShotDetectionRadius = 6f;

    [SerializeField, Tooltip("雪の結晶の発射位置Offset。右向き基準です。")]
    private Vector2 _crystalShotSpawnOffset = new Vector2(0.5f, 0f);

    private int _crystalShotDamage = 20;

    [BoxGroup("雪の結晶の飛翔・舞い挙動")]
    [SerializeField, Tooltip("雪の結晶の前進速度")]
    private float _crystalShotSpeed = 6f;

    [BoxGroup("雪の結晶の飛翔・舞い挙動")]
    [SerializeField, Tooltip("減速後も維持する最低速度")]
    private float _crystalShotMinimumSpeed = 3f;

    [BoxGroup("雪の結晶の飛翔・舞い挙動")]
    [SerializeField, Tooltip("1秒あたりの減速量")]
    private float _crystalShotDeceleration = 0.5f;

    [BoxGroup("雪の結晶の飛翔・舞い挙動")]
    [SerializeField, Tooltip("不規則な横方向ドリフトの最大幅")]
    private float _crystalShotDriftStrength = 0.25f;

    [BoxGroup("雪の結晶の飛翔・舞い挙動")]
    [SerializeField, Tooltip("横方向ドリフトの変化速度")]
    private float _crystalShotDriftFrequency = 0.8f;

    [BoxGroup("雪の結晶の飛翔・舞い挙動")]
    [SerializeField, Tooltip("ランダム決定する基本回転速度の最小値（度/秒）")]
    private float _crystalShotMinimumRotationSpeed = 120f;

    [BoxGroup("雪の結晶の飛翔・舞い挙動")]
    [SerializeField, Tooltip("ランダム決定する基本回転速度の最大値（度/秒）")]
    private float _crystalShotMaximumRotationSpeed = 240f;

    [BoxGroup("雪の結晶の飛翔・舞い挙動")]
    [SerializeField, Tooltip("基本回転速度に加える不規則な揺らぎ（度/秒）")]
    private float _crystalShotRotationFluctuation = 80f;

    [BoxGroup("雪の結晶の飛翔・舞い挙動")]
    [SerializeField, Tooltip("回転速度の揺らぎが変化する速さ")]
    private float _crystalShotRotationNoiseSpeed = 0.9f;

    [BoxGroup("雪の結晶の飛翔・舞い挙動")]
    [SerializeField, Range(0.05f, 1f), Tooltip("疑似反転時のXスケール最小倍率")]
    private float _crystalShotMinimumXScaleRate = 0.12f;

    [BoxGroup("雪の結晶の飛翔・舞い挙動")]
    [SerializeField, Tooltip("1秒あたりの疑似反転回数")]
    private float _crystalShotScaleFlipFrequency = 2f;

    [BoxGroup("雪の結晶の飛翔・舞い挙動")]
    [SerializeField, Range(0f, 0.9f), Tooltip("Yスケールの伸縮量")]
    private float _crystalShotYScaleFluctuation = 0.12f;

    [BoxGroup("雪の結晶の飛翔・舞い挙動")]
    [SerializeField, Tooltip("Yスケール伸縮の1秒あたりの周期数")]
    private float _crystalShotYScaleFrequency = 1.3f;

    [SerializeField, Tooltip("雪の結晶が消えるまでの時間")]
    private float _crystalShotProjectileLifeTime = 5f;

    [SerializeField, Tooltip("雪の結晶射撃開始から弾を発射するまでの時間")]
    private float _crystalShotReleaseDelay = 0.2f;

    [SerializeField, Tooltip("雪の結晶発射後の硬直時間")]
    private float _crystalShotRecoveryDuration = 1f;

    [Header("雪玉落下攻撃設定")]
    [SerializeField, Tooltip("前回の雪玉落下攻撃から次に使用可能になるまでの時間")]
    private float _snowballDropInterval = 8f;

    [SerializeField, Tooltip("妖精のローカル座標を基準とする雪玉生成位置")]
    private Vector2 _snowballDropOffset = new Vector2(0f, 1.5f);

    [SerializeField, Tooltip("雪玉落下攻撃の準備にかける時間")]
    private float _snowballDropPrepareDuration = 1.5f;

    [SerializeField, Tooltip("準備開始から雪玉を完全表示するまでの時間")]
    private float _snowballFadeInDuration = 1f;

    [SerializeField, Tooltip("準備終了の何秒前から出現エフェクトを薄くするか")]
    private float _effectFadeOutLeadTime = 0.4f;

    [SerializeField, Tooltip("雪玉の落下速度")]
    private float _snowballFallSpeed = 8f;

    private int _snowballDropDamage = 20;

    [SerializeField, Tooltip("接地しなかった雪玉が消えるまでの時間")]
    private float _snowballLifeTime = 5f;

    [SerializeField, Tooltip("雪玉を落とした後の硬直時間")]
    private float _snowballDropRecoveryDuration = 1.2f;

    private static readonly int IdleTriggerHash = Animator.StringToHash("IdleTrigger");
    private static readonly int CrystalShotTriggerHash = Animator.StringToHash("Attack1Trigger");
    private static readonly int SnowballDropPrepareTriggerHash =
        Animator.StringToHash("Attack2PrepareTrigger");
    private static readonly int SnowballDropExecuteTriggerHash =
        Animator.StringToHash("Attack2ExecuteTrigger");

    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rbody;
    private Transform _playerTransform;
    private LayerMask _groundLayer;
    private SnowFairyState _currentState = SnowFairyState.Move;
    private readonly List<Vector2> _bezierPoints = new List<Vector2>(4);
    private readonly float[] _bezierSampleParameters =
        new float[BEZIER_LENGTH_SAMPLE_COUNT + 1];
    private readonly float[] _bezierSampleDistances =
        new float[BEZIER_LENGTH_SAMPLE_COUNT + 1];
    private float _stateTimer;
    private float _moveDistance;
    private float _bezierTotalLength;
    private float _snowballDropTimer;
    private bool _hasReleasedCrystalShot;
    private bool _hasValidMovementBounds;
    private float _leftBound;
    private float _rightBound;
    private Vector3 _initialPosition;
    private Transform _snowballOriginalParent;
    private Vector3 _snowballOriginalLocalPosition;
    private Quaternion _snowballOriginalLocalRotation;
    private SnowFairySnowballProjectile _snowballProjectile;
    private SpriteRenderer[] _snowballSpriteRenderers;
    private SpriteRenderer[] _effectSpriteRenderers;
    private Animator _spawnEffectAnimator;

    private void Awake()
    {
        _groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _rbody = GetComponent<Rigidbody2D>();
        _rbody.gravityScale = 0f;
        _rbody.velocity = Vector2.zero;
        _rbody.constraints |= RigidbodyConstraints2D.FreezeRotation;
        _initialPosition = transform.position;

        if (_activator == null)
            _activator = GetComponentInParent<EnemyActivator>();

        // EnemyVariantに応じた攻撃ダメージの初期化
        switch (_variantType)
        {
            case EnemyVariant.SnowField:
                _crystalShotDamage = 20;
                _snowballDropDamage = 20;
                break;
            default:
                Debug.LogError($"{name}のEnemyVariantが設定されていません。", this);
                break;
        }

        CacheSnowballDropObjects();
    }

    private void Start()
    {
        ResetState();
    }

    private void Update()
    {
        // 飛行敵のため、攻撃中を含めて物理重力による落下を許可しません。
        if (_rbody != null && _rbody.velocity != Vector2.zero)
            _rbody.velocity = Vector2.zero;

        if (TimeManager.instance != null && TimeManager.instance.isEnemyMovePaused)
        {
            if (_animator != null)
                _animator.enabled = false;
            return;
        }

        if (_animator != null && !_animator.enabled)
            _animator.enabled = true;

        float deltaTime = Time.deltaTime;
        _snowballDropTimer += deltaTime;

        switch (_currentState)
        {
            case SnowFairyState.Move:
                UpdateMove(deltaTime);
                break;
            case SnowFairyState.CrystalShot:
                UpdateCrystalShot(deltaTime);
                break;
            case SnowFairyState.CrystalShotRecovery:
                UpdateRecovery(deltaTime, _crystalShotRecoveryDuration);
                break;
            case SnowFairyState.SnowballDropPrepare:
                UpdateSnowballDropPrepare(deltaTime);
                break;
            case SnowFairyState.SnowballDropRecovery:
                UpdateRecovery(deltaTime, _snowballDropRecoveryDuration);
                break;
        }
    }

    private void OnDisable()
    {
        RestoreSnowball();
    }

    public void ResetState()
    {
        AcquirePlayerTransform();
        CalculateMovementBounds();
        RestoreSnowball();

        transform.position = _initialPosition;
        if (_rbody != null)
        {
            _rbody.gravityScale = 0f;
            _rbody.velocity = Vector2.zero;
            _rbody.angularVelocity = 0f;
        }
        _spriteRenderer.flipX = false;
        _snowballDropTimer = 0f;
        ChangeState(SnowFairyState.Move);
    }

    private void AcquirePlayerTransform()
    {
        if (PlayerManager.instance != null && PlayerManager.instance.PlayerGameObject != null)
        {
            _playerTransform = PlayerManager.instance.PlayerGameObject.transform;
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME);
        _playerTransform = playerObject != null ? playerObject.transform : null;
    }

    private void CalculateMovementBounds()
    {
        _hasValidMovementBounds = false;

        if (_isUseManualBounds)
        {
            _leftBound = Mathf.Min(_manualLeftBound, _manualRightBound);
            _rightBound = Mathf.Max(_manualLeftBound, _manualRightBound);
            ApplyHorizontalBoundsMargin();
            _hasValidMovementBounds = true;
            return;
        }

        if (_activator == null)
            _activator = GetComponentInParent<EnemyActivator>();

        Collider2D activatorCollider = _activator != null ? _activator.GetComponent<Collider2D>() : null;
        if (activatorCollider == null)
        {
            _leftBound = _initialPosition.x;
            _rightBound = _initialPosition.x;
            Debug.LogError(
                $"{name}: 手動範囲が無効で、EnemyActivatorのCollider2Dも取得できません。移動範囲を決定できません。",
                this
            );
            return;
        }

        _leftBound = activatorCollider.bounds.min.x;
        _rightBound = activatorCollider.bounds.max.x;
        ApplyHorizontalBoundsMargin();
        _hasValidMovementBounds = true;
    }

    private void ApplyHorizontalBoundsMargin()
    {
        _leftBound += _horizontalBoundsMargin;
        _rightBound -= _horizontalBoundsMargin;
        if (_leftBound > _rightBound)
        {
            float center = (_leftBound + _rightBound) * 0.5f;
            _leftBound = center;
            _rightBound = center;
        }
    }

    private void BeginMove()
    {
        _bezierPoints.Clear();
        if (!_hasValidMovementBounds)
        {
            SetAnimationTrigger(IdleTriggerHash);
            return;
        }

        _bezierPoints.Add(transform.position);

        int pointCount = Mathf.Max(2, _controlPointCount);
        for (int i = 0; i < pointCount; i++)
            _bezierPoints.Add(PickGroundRelativePoint());

        _bezierPoints.Add(PickGroundRelativePoint());
        BuildBezierLengthTable();
        _moveDistance = 0f;
        SetAnimationTrigger(IdleTriggerHash);
    }

    private void BuildBezierLengthTable()
    {
        _bezierTotalLength = 0f;
        _bezierSampleParameters[0] = 0f;
        _bezierSampleDistances[0] = 0f;

        Vector2 previous = EvaluateBezier(_bezierPoints, 0f);
        for (int i = 1; i <= BEZIER_LENGTH_SAMPLE_COUNT; i++)
        {
            float t = i / (float)BEZIER_LENGTH_SAMPLE_COUNT;
            Vector2 current = EvaluateBezier(_bezierPoints, t);
            _bezierTotalLength += Vector2.Distance(previous, current);
            _bezierSampleParameters[i] = t;
            _bezierSampleDistances[i] = _bezierTotalLength;
            previous = current;
        }
    }

    private Vector2 PickGroundRelativePoint()
    {
        for (int attempt = 0; attempt < Mathf.Max(1, _waypointPickAttempts); attempt++)
        {
            float x = Random.Range(_leftBound, _rightBound);
            Vector2 rayOrigin = new Vector2(x, transform.position.y + _groundDetectDistance * 0.5f);
            RaycastHit2D hit = Physics2D.Raycast(
                rayOrigin,
                Vector2.down,
                _groundDetectDistance,
                _groundLayer
            );
            if (hit.collider != null)
            {
                float minHeight = Mathf.Min(_minHeightFromGround, _maxHeightFromGround);
                float maxHeight = Mathf.Max(_minHeightFromGround, _maxHeightFromGround);
                return new Vector2(x, hit.point.y + Random.Range(minHeight, maxHeight));
            }
        }

        return transform.position;
    }

    private void UpdateMove(float deltaTime)
    {
        if (!_hasValidMovementBounds || _bezierPoints.Count == 0)
            return;

        if (_bezierTotalLength <= Mathf.Epsilon)
        {
            SelectActionAfterMove();
            return;
        }

        _moveDistance = Mathf.Min(
            _moveDistance + (Mathf.Max(0f, _moveSpeed) * deltaTime),
            _bezierTotalLength
        );
        float t = GetBezierParameterAtDistance(_moveDistance);
        Vector2 previousPosition = transform.position;
        Vector2 nextPosition = EvaluateBezier(_bezierPoints, t);
        transform.position = new Vector3(nextPosition.x, nextPosition.y, transform.position.z);
        UpdateFacing(nextPosition.x - previousPosition.x);

        if (_moveDistance >= _bezierTotalLength)
            SelectActionAfterMove();
    }

    private float GetBezierParameterAtDistance(float distance)
    {
        for (int i = 1; i <= BEZIER_LENGTH_SAMPLE_COUNT; i++)
        {
            if (_bezierSampleDistances[i] < distance)
                continue;

            float segmentLength =
                _bezierSampleDistances[i] - _bezierSampleDistances[i - 1];
            if (segmentLength <= Mathf.Epsilon)
                return _bezierSampleParameters[i];

            float segmentRate =
                (distance - _bezierSampleDistances[i - 1]) / segmentLength;
            return Mathf.Lerp(
                _bezierSampleParameters[i - 1],
                _bezierSampleParameters[i],
                segmentRate
            );
        }

        return 1f;
    }

    private static Vector2 EvaluateBezier(List<Vector2> points, float t)
    {
        if (points == null || points.Count == 0)
            return Vector2.zero;

        Vector2[] workingPoints = points.ToArray();
        for (int level = workingPoints.Length - 1; level > 0; level--)
        {
            for (int i = 0; i < level; i++)
                workingPoints[i] = Vector2.Lerp(workingPoints[i], workingPoints[i + 1], t);
        }
        return workingPoints[0];
    }

    private void SelectActionAfterMove()
    {
        if (_snowballDropTimer >= _snowballDropInterval && CanUseSnowballDrop())
        {
            ChangeState(SnowFairyState.SnowballDropPrepare);
            return;
        }

        if (_playerTransform != null
            && Vector2.Distance(transform.position, _playerTransform.position)
                <= _crystalShotDetectionRadius
            && _crystalProjectilePrefab != null)
        {
            ChangeState(SnowFairyState.CrystalShot);
            return;
        }

        ChangeState(SnowFairyState.Move);
    }

    private void UpdateCrystalShot(float deltaTime)
    {
        _stateTimer += deltaTime;
        if (!_hasReleasedCrystalShot && _stateTimer >= _crystalShotReleaseDelay)
        {
            _hasReleasedCrystalShot = true;
            FireCrystalShotProjectile();
            ChangeState(SnowFairyState.CrystalShotRecovery);
        }
    }

    private void FireCrystalShotProjectile()
    {
        if (_playerTransform == null || _crystalProjectilePrefab == null)
            return;

        float playerDeltaX = _playerTransform.position.x - transform.position.x;
        if (Mathf.Abs(playerDeltaX) > 0.001f)
            _spriteRenderer.flipX = playerDeltaX < 0f;

        float facingSign = _spriteRenderer.flipX ? -1f : 1f;
        Vector2 offset = new Vector2(
            _crystalShotSpawnOffset.x * facingSign,
            _crystalShotSpawnOffset.y
        );
        Vector2 spawnPosition = (Vector2)transform.position + offset;
        GameObject projectileObject = Instantiate(
            _crystalProjectilePrefab,
            spawnPosition,
            Quaternion.identity
        );
        SnowFairyCrystalProjectile projectile =
            projectileObject.GetComponent<SnowFairyCrystalProjectile>();
        if (projectile == null)
        {
            Debug.LogError("雪の結晶PrefabにSnowFairyCrystalProjectileがありません。", projectileObject);
            Destroy(projectileObject);
            return;
        }

        Vector2 direction = ((Vector2)_playerTransform.position - spawnPosition).normalized;
        projectile.Launch(
            direction,
            _crystalShotSpeed,
            _crystalShotMinimumSpeed,
            _crystalShotDeceleration,
            _crystalShotDriftStrength,
            _crystalShotDriftFrequency,
            _crystalShotMinimumRotationSpeed,
            _crystalShotMaximumRotationSpeed,
            _crystalShotRotationFluctuation,
            _crystalShotRotationNoiseSpeed,
            _crystalShotMinimumXScaleRate,
            _crystalShotScaleFlipFrequency,
            _crystalShotYScaleFluctuation,
            _crystalShotYScaleFrequency,
            _crystalShotProjectileLifeTime,
            _crystalShotDamage
        );
    }

    private bool CanUseSnowballDrop()
    {
        return _snowballObject != null
            && _snowballProjectile != null
            && _snowballSpawnEffectObject != null;
    }

    private void BeginSnowballDropPrepare()
    {
        _snowballDropTimer = 0f;
        Vector3 spawnPosition = transform.TransformPoint(_snowballDropOffset);

        _snowballSpawnEffectObject.transform.position = spawnPosition;
        _snowballSpawnEffectObject.SetActive(true);
        SetAlpha(_effectSpriteRenderers, 1f);
        if (_spawnEffectAnimator != null)
            _spawnEffectAnimator.Play(0, -1, 0f);

        _snowballObject.transform.SetParent(transform, true);
        _snowballObject.transform.position = spawnPosition;
        _snowballObject.SetActive(true);
        _snowballProjectile.PrepareForDisplay();
        SetAlpha(_snowballSpriteRenderers, 0f);
    }

    private void UpdateSnowballDropPrepare(float deltaTime)
    {
        _stateTimer += deltaTime;
        float snowballAlpha = Mathf.Clamp01(
            _stateTimer / Mathf.Max(0.01f, _snowballFadeInDuration)
        );
        SetAlpha(_snowballSpriteRenderers, snowballAlpha);

        float fadeStart = Mathf.Max(
            0f,
            _snowballDropPrepareDuration - _effectFadeOutLeadTime
        );
        if (_stateTimer >= fadeStart)
        {
            float effectAlpha = 1f - Mathf.Clamp01(
                (_stateTimer - fadeStart) / Mathf.Max(0.01f, _effectFadeOutLeadTime)
            );
            SetAlpha(_effectSpriteRenderers, effectAlpha);
        }

        if (_stateTimer >= _snowballDropPrepareDuration)
            ExecuteSnowballDrop();
    }

    private void ExecuteSnowballDrop()
    {
        _snowballSpawnEffectObject.SetActive(false);
        SetAlpha(_snowballSpriteRenderers, 1f);
        _snowballObject.transform.SetParent(null, true);
        SetAnimationTrigger(SnowballDropExecuteTriggerHash);
        _currentState = SnowFairyState.SnowballDropExecute;
        _snowballProjectile.Launch(
            _snowballFallSpeed,
            _snowballLifeTime,
            _snowballDropDamage,
            RestoreSnowball
        );
        ChangeState(SnowFairyState.SnowballDropRecovery);
    }

    private void UpdateRecovery(float deltaTime, float duration)
    {
        _stateTimer += deltaTime;
        if (_stateTimer >= duration)
            ChangeState(SnowFairyState.Move);
    }

    private void ChangeState(SnowFairyState newState)
    {
        _currentState = newState;
        _stateTimer = 0f;

        switch (newState)
        {
            case SnowFairyState.Move:
                BeginMove();
                break;
            case SnowFairyState.CrystalShot:
                _hasReleasedCrystalShot = false;
                SetAnimationTrigger(CrystalShotTriggerHash);
                break;
            case SnowFairyState.SnowballDropPrepare:
                SetAnimationTrigger(SnowballDropPrepareTriggerHash);
                BeginSnowballDropPrepare();
                break;
        }
    }

    private void SetAnimationTrigger(int triggerHash)
    {
        if (_animator == null)
            return;

        _animator.ResetTrigger(IdleTriggerHash);
        _animator.ResetTrigger(CrystalShotTriggerHash);
        _animator.ResetTrigger(SnowballDropPrepareTriggerHash);
        _animator.ResetTrigger(SnowballDropExecuteTriggerHash);
        _animator.SetTrigger(triggerHash);
    }

    private void UpdateFacing(float deltaX)
    {
        if (deltaX > 0.001f)
            _spriteRenderer.flipX = false;
        else if (deltaX < -0.001f)
            _spriteRenderer.flipX = true;
    }

    private void CacheSnowballDropObjects()
    {
        if (_snowballSpawnEffectObject != null)
        {
            _spawnEffectAnimator = _snowballSpawnEffectObject.GetComponent<Animator>();
            _effectSpriteRenderers =
                _snowballSpawnEffectObject.GetComponentsInChildren<SpriteRenderer>(true);
            _snowballSpawnEffectObject.SetActive(false);
        }

        if (_snowballObject == null)
            return;

        _snowballOriginalParent = _snowballObject.transform.parent;
        _snowballOriginalLocalPosition = _snowballObject.transform.localPosition;
        _snowballOriginalLocalRotation = _snowballObject.transform.localRotation;
        _snowballProjectile = _snowballObject.GetComponent<SnowFairySnowballProjectile>();
        _snowballSpriteRenderers = _snowballObject.GetComponentsInChildren<SpriteRenderer>(true);
        _snowballObject.SetActive(false);
    }

    private void RestoreSnowball()
    {
        if (_snowballObject == null)
            return;

        if (_snowballProjectile != null)
            _snowballProjectile.Cancel();
        _snowballObject.transform.SetParent(_snowballOriginalParent, false);
        _snowballObject.transform.localPosition = _snowballOriginalLocalPosition;
        _snowballObject.transform.localRotation = _snowballOriginalLocalRotation;
        SetAlpha(_snowballSpriteRenderers, 1f);
        _snowballObject.SetActive(false);

        if (_snowballSpawnEffectObject != null)
            _snowballSpawnEffectObject.SetActive(false);
    }

    private static void SetAlpha(SpriteRenderer[] renderers, float alpha)
    {
        if (renderers == null)
            return;

        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            if (spriteRenderer == null)
                continue;
            Color color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }
    }

    private void OnDrawGizmosSelected()
    {
        DrawMovementBoundsGizmos();

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _crystalShotDetectionRadius);

        Vector3 rightCrystalShotPosition =
            transform.position + (Vector3)_crystalShotSpawnOffset;
        Vector3 leftCrystalShotPosition =
            transform.position
            + new Vector3(-_crystalShotSpawnOffset.x, _crystalShotSpawnOffset.y, 0f);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
        Gizmos.DrawLine(transform.position, rightCrystalShotPosition);
        Gizmos.DrawWireSphere(rightCrystalShotPosition, 0.12f);
        Gizmos.DrawLine(transform.position, leftCrystalShotPosition);
        Gizmos.DrawWireSphere(leftCrystalShotPosition, 0.12f);

        Vector3 snowballDropPosition = transform.TransformPoint(_snowballDropOffset);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.position, snowballDropPosition);
        Gizmos.DrawWireSphere(snowballDropPosition, 0.18f);
        Gizmos.DrawLine(
            snowballDropPosition,
            snowballDropPosition + Vector3.down * Mathf.Max(0f, _groundDetectDistance)
        );

        DrawBezierGizmos();
    }

    private void DrawMovementBoundsGizmos()
    {
        float left;
        float right;

        if (_isUseManualBounds)
        {
            left = Mathf.Min(_manualLeftBound, _manualRightBound);
            right = Mathf.Max(_manualLeftBound, _manualRightBound);
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 1f);
        }
        else
        {
            EnemyActivator activator = _activator != null
                ? _activator
                : GetComponentInParent<EnemyActivator>();
            Collider2D activatorCollider =
                activator != null ? activator.GetComponent<Collider2D>() : null;
            if (activatorCollider == null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, 0.35f);
                return;
            }

            left = activatorCollider.bounds.min.x;
            right = activatorCollider.bounds.max.x;
            Gizmos.color = new Color(1f, 0.75f, 0.15f, 1f);
        }

        left += _horizontalBoundsMargin;
        right -= _horizontalBoundsMargin;
        if (left > right)
        {
            float center = (left + right) * 0.5f;
            left = center;
            right = center;
        }

        float halfRayLength = Mathf.Max(0f, _groundDetectDistance) * 0.5f;
        float top = transform.position.y + halfRayLength;
        float bottom = transform.position.y - halfRayLength;
        Gizmos.DrawLine(new Vector3(left, bottom), new Vector3(left, top));
        Gizmos.DrawLine(new Vector3(right, bottom), new Vector3(right, top));
        Gizmos.DrawLine(new Vector3(left, top), new Vector3(right, top));
        Gizmos.DrawLine(new Vector3(left, bottom), new Vector3(right, bottom));

        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.8f);
        Gizmos.DrawLine(
            new Vector3(left, transform.position.y),
            new Vector3(right, transform.position.y)
        );
    }

    private void DrawBezierGizmos()
    {
        if (_bezierPoints == null || _bezierPoints.Count < 2)
            return;

        for (int i = 0; i < _bezierPoints.Count; i++)
        {
            Vector3 point = _bezierPoints[i];
            Gizmos.color = i == 0 || i == _bezierPoints.Count - 1
                ? Color.magenta
                : Color.yellow;
            Gizmos.DrawWireSphere(point, 0.12f);

            Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.6f);
            Gizmos.DrawLine(
                point + (Vector3.up * (_groundDetectDistance * 0.5f)),
                point + (Vector3.down * (_groundDetectDistance * 0.5f))
            );

            if (i > 0)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
                Gizmos.DrawLine(_bezierPoints[i - 1], point);
            }
        }

        Gizmos.color = Color.magenta;
        Vector2 previous = EvaluateBezier(_bezierPoints, 0f);
        const int segmentCount = 32;
        for (int i = 1; i <= segmentCount; i++)
        {
            Vector2 current = EvaluateBezier(_bezierPoints, i / (float)segmentCount);
            Gizmos.DrawLine(previous, current);
            previous = current;
        }
    }
}

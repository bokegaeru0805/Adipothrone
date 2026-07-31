using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 大型雪原ゴーレムの待機、つらら攻撃、近距離攻撃を制御します。
/// 物理ルートと、Animator・全身ボーンを持つ表示ルートを分離して扱います。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class SnowFieldGolemLargeMoveController : MonoBehaviour, IEnemyResettable
{
    #region 列挙型

    private enum EnemyVariant
    {
        None = 0,
        SnowField = 1,
    }

    private enum GolemState
    {
        None = 0,
        Idle = 1,
        IcicleAttacking = 2,
        MeleeAttacking = 3,
    }

    #endregion

    #region インスペクター設定

    [Header("敵のタイプ")]
    [SerializeField, Tooltip("敵のバリエーションタイプ。ダメージ等の初期化に使用します。")]
    private EnemyVariant _variantType = EnemyVariant.SnowField;

    [Header("基本設定")]
    [SerializeField, Tooltip("足元中央を表すBottom基準のTransform。")]
    private Transform _pivotTransform = null;

    [SerializeField, Tooltip("Animatorと全身ボーンを持つ表示専用ルート。")]
    private Transform _visualRoot = null;

    [SerializeField, Tooltip("表示専用ルート以下のAnimator。")]
    private Animator _animator = null;

    [SerializeField, Tooltip("Defaultの右向きを維持する場合はtrue。向きは実行中固定です。")]
    private bool _isFacingRight = true;

    [Header("共通攻撃範囲")]
    [SerializeField, Min(0f), Tooltip("Pivotから上方向にプレイヤーを検知する共通距離。")]
    private float _attackRangeYUp = 2f;

    [SerializeField, Min(0f), Tooltip("Pivotから下方向にプレイヤーを検知する共通距離。")]
    private float _attackRangeYDown = 1f;

    [Header("つらら攻撃")]
    [SerializeField, Min(0f), Tooltip("前方につらら攻撃を検知するX距離。近距離攻撃より優先されます。")]
    private float _icicleAttackRangeX = 8f;

    [SerializeField, Min(0.01f), Tooltip("つらら攻撃の実行アニメーション再生時間。元クリップは1秒想定です。")]
    private float _icicleAttackExecuteDuration = 1f;

    [SerializeField, Min(0.01f), Tooltip("つらら攻撃のRecoveryアニメーション再生時間。元クリップは1秒想定です。")]
    private float _icicleAttackRecoveryDuration = 1f;

    [SerializeField, Tooltip("生成するIcicleMoveController付きPrefab。")]
    private GameObject _iciclePrefab = null;

    [SerializeField, Min(1), Tooltip("1回のつらら攻撃で生成するIcicleの個数。")]
    private int _icicleCount = 5;

    [SerializeField, Min(0), Tooltip("この個数以下まで減ると、つらら攻撃を再使用できます。")]
    private int _icicleRemainingThreshold = 1;

    [SerializeField, Min(0f), Tooltip("Pivotから前方へ離す最初のつららの距離。")]
    private float _icicleForwardOffset = 2f;

    [SerializeField, Min(0f), Tooltip("Pivotから最後のつららまでの前方限界距離。")]
    private float _icicleLimitDistance = 8f;

    [SerializeField, Tooltip("Pivotを基準にしたつららの生成高さ。")]
    private float _icicleHeight = 5f;

    [SerializeField, Min(0f), Tooltip("前回のつららを消す時間。")]
    private float _icicleDisappearDuration = 0.1f;

    [SerializeField, Min(0f), Tooltip("新しいつららをホログラムから表示する時間。")]
    private float _icicleAppearDuration = 0.5f;

    [SerializeField, Min(0f), Tooltip("つららの表示完了後、落下を許可するまでの時間。")]
    private float _icicleFallWaitDuration = 0.5f;

    [SerializeField, Min(0f), Tooltip("落下許可後、時間条件でつらら攻撃が再使用可能になるまでの時間。")]
    private float _icicleAttackReuseTime = 5f;

    [SerializeField, Min(0f), Tooltip("本体がIdleへ戻った後の攻撃後待機時間。")]
    private float _icicleAttackPostDelay = 1f;

    [Header("近距離攻撃")]
    [SerializeField, Min(0f), Tooltip("前方に近距離攻撃を検知するX距離。")]
    private float _meleeAttackRangeX = 3f;

    [SerializeField, Min(0.01f), Tooltip("近距離攻撃のPrepareアニメーション再生時間。元クリップは1秒想定です。")]
    private float _meleeAttackPrepareDuration = 1f;

    [SerializeField, Min(0f), Tooltip("Prepare終了後、Executeを開始するまで追加で待機する時間。")]
    private float _meleeAttackPreExecuteDelay = 0.2f;

    [SerializeField, Min(0.01f), Tooltip("近距離攻撃のExecuteアニメーション再生時間。元クリップは1秒想定です。")]
    private float _meleeAttackExecuteDuration = 1f;

    [SerializeField, Min(0.01f), Tooltip("近距離攻撃のRecoveryアニメーション再生時間。元クリップは1秒想定です。")]
    private float _meleeAttackRecoveryDuration = 1f;

    [SerializeField, Min(0f), Tooltip("近距離攻撃後、次の攻撃が可能になるまでの待機時間。")]
    private float _meleeAttackPostDelay = 1f;

    [SerializeField, Tooltip("近距離攻撃の実行中だけDamageableにする腕。")]
    private GameObject _armDamageObject = null;

    [Header("近距離攻撃 Impact")]
    [SerializeField, Tooltip("Animation Eventで表示する、Animator制御済みの子エフェクト。")]
    private GameObject _impactEffect = null;

    [SerializeField, Tooltip("Pivotから見た右向き基準の表示Offset。")]
    private Vector2 _impactEffectOffset = Vector2.zero;

    [SerializeField, Min(0f), Tooltip("ImpactEffectを表示してから非表示へ戻すまでの時間。")]
    private float _impactEffectDisplayDuration = 0.6f;

    [SerializeField, Tooltip("ImpactEffectと同時に生成するPrefab。")]
    private GameObject _groundSweepShockwavePrefab = null;

    [SerializeField, Min(0f), Tooltip("GroundSweepShockwaveが正面方向へ直進する速度。")]
    private float _shockwaveSpeed = 8f;

    [SerializeField, Min(0f), Tooltip("GroundSweepShockwaveが衝突しなかった場合に非表示となるまでの時間。")]
    private float _shockwaveLifeTime = 2f;

    #endregion

    #region コンポーネント・状態管理

    private Rigidbody2D _rbody;
    private EnemyHealth _enemyHP;
    private ContactDamageController _contactDamageController;
    private ContactDamageController _armDamageController;
    private Transform _playerTransform;
    private Animator _impactEffectAnimator;
    private LayerMask _groundLayer;
    private GolemState _currentState = GolemState.None;
    private Vector3 _visualInitialLocalEulerAngles;
    private readonly List<IcicleMoveController> _activeIcicles = new List<IcicleMoveController>();
    private readonly List<GroundSweepShockwaveMoveController> _spawnedShockwaves =
        new List<GroundSweepShockwaveMoveController>();
    private Coroutine _attackCoroutine;
    private Coroutine _impactEffectCoroutine;
    private Sequence _icicleHologramSequence;
    private bool _canStartAttack = true;
    private bool _isIcicleAttackTimeReady = true;
    private bool _hasProcessedImpactEvent;
    private float _icicleAttackReuseElapsed;
    private int _bodyDamage = 20;
    private int _armDamage = 20;
    private int _shockwaveDamage = 20;

    #endregion

    #region Animatorパラメータ

    private static readonly int AnimIdleTrigger = Animator.StringToHash("IdleTrigger");
    private static readonly int AnimIcicleAttackExecuteTrigger =
        Animator.StringToHash("Attack1ExecuteTrigger");
    private static readonly int AnimIcicleAttackRecoveryTrigger =
        Animator.StringToHash("Attack1RecoveryTrigger");
    private static readonly int AnimMeleeAttackPrepareTrigger =
        Animator.StringToHash("Attack2PrepareTrigger");
    private static readonly int AnimMeleeAttackExecuteTrigger =
        Animator.StringToHash("Attack2ExecuteTrigger");
    private static readonly int AnimMeleeAttackRecoveryTrigger =
        Animator.StringToHash("Attack2RecoveryTrigger");
    private static readonly int AnimIcicleAttackExecuteSpeed =
        Animator.StringToHash("Attack1ExecuteSpeed");
    private static readonly int AnimIcicleAttackRecoverySpeed =
        Animator.StringToHash("Attack1RecoverySpeed");
    private static readonly int AnimMeleeAttackPrepareSpeed =
        Animator.StringToHash("Attack2PrepareSpeed");
    private static readonly int AnimMeleeAttackExecuteSpeed =
        Animator.StringToHash("Attack2ExecuteSpeed");
    private static readonly int AnimMeleeAttackRecoverySpeed =
        Animator.StringToHash("Attack2RecoverySpeed");

    #endregion

    #region 判定用プロパティ

    private Vector3 PivotPosition =>
        _pivotTransform != null ? _pivotTransform.position : transform.position;

    private float FacingMultiplier => _isFacingRight ? 1f : -1f;

    #endregion

    #region Unityライフサイクル

    private void Awake()
    {
        _rbody = GetComponent<Rigidbody2D>();
        _enemyHP = GetComponent<EnemyHealth>();
        _contactDamageController = GetComponent<ContactDamageController>();
        _armDamageController = _armDamageObject != null
            ? _armDamageObject.GetComponent<ContactDamageController>()
            : null;
        _impactEffectAnimator = _impactEffect != null ? _impactEffect.GetComponent<Animator>() : null;
        _groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );

        InitializeVariantStatus();
        ResolveVisualReferences();
        SetArmDamageActive(false);
        if (_impactEffect != null)
            _impactEffect.SetActive(false);
    }

    private void Start()
    {
        ResetState();
    }

    private void Update()
    {
        bool isPaused = TimeManager.instance != null && TimeManager.instance.isEnemyMovePaused;
        if (isPaused)
        {
            _icicleHologramSequence?.Pause();
            return;
        }

        _icicleHologramSequence?.Play();
        if (!_isIcicleAttackTimeReady)
            _icicleAttackReuseElapsed += Time.deltaTime;
        UpdateIcicleAttackAvailability();
        if (_currentState == GolemState.Idle && _canStartAttack)
            TryStartAttack();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _icicleHologramSequence?.Kill();
        SetArmDamageActive(false);
        if (_impactEffect != null)
            _impactEffect.SetActive(false);
        DestroyTrackedIcicles();
        HideSpawnedShockwaves();
    }

    #endregion

    #region リセット・Animation Event

    /// <summary>
    /// 攻撃、生成物、物理状態、Animatorを初期状態へ戻します。
    /// </summary>
    public void ResetState()
    {
        StopAllCoroutines();
        _icicleHologramSequence?.Kill();
        _attackCoroutine = null;
        _impactEffectCoroutine = null;

        ResolvePlayerTransform();
        _enemyHP?.ResetState();
        _contactDamageController?.SetNormalDamage(_bodyDamage);
        _armDamageController?.SetNormalDamage(_armDamage);

        if (_rbody != null)
        {
            _rbody.simulated = true;
            _rbody.velocity = Vector2.zero;
            _rbody.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        SetArmDamageActive(false);
        if (_impactEffect != null)
            _impactEffect.SetActive(false);
        DestroyTrackedIcicles();
        HideSpawnedShockwaves();

        _canStartAttack = true;
        _isIcicleAttackTimeReady = true;
        _icicleAttackReuseElapsed = 0f;
        _hasProcessedImpactEvent = false;
        ApplyFacingRotation();
        ResetAnimator();
        ChangeState(GolemState.Idle);
    }

    /// <summary>
    /// 近距離攻撃実行アニメーションのEventからImpactと衝撃波を発生させます。
    /// </summary>
    public void OnMeleeAttackImpactAnimationEvent()
    {
        if (_currentState != GolemState.MeleeAttacking || _hasProcessedImpactEvent)
            return;

        _hasProcessedImpactEvent = true;
        ShowImpactEffect();
        SpawnGroundSweepShockwave();
    }

    #endregion

    #region 初期化・参照解決

    /// <summary>
    /// Variantごとの接触ダメージを設定します。
    /// ダメージ値はInspectorへ公開せず、敵タイプの定義として一箇所で管理します。
    /// </summary>
    private void InitializeVariantStatus()
    {
        switch (_variantType)
        {
            case EnemyVariant.SnowField:
                _bodyDamage = 20;
                _armDamage = 20;
                _shockwaveDamage = 20;
                break;
            default:
                Debug.LogError($"{name}のEnemyVariantが設定されていません。", this);
                break;
        }
    }

    private void ResolveVisualReferences()
    {
        if (_animator == null)
        {
            foreach (Animator candidate in GetComponentsInChildren<Animator>(true))
            {
                // 本体またはImpactEffectのAnimatorを、表示ルート用Animatorとして誤取得しないよう除外します。
                if (
                    candidate.transform == transform
                    || (_impactEffect != null && candidate.gameObject == _impactEffect)
                )
                    continue;

                _animator = candidate;
                break;
            }
        }

        if (_visualRoot == null && _animator != null)
            _visualRoot = _animator.transform;
        if (_visualRoot != null)
            _visualInitialLocalEulerAngles = _visualRoot.localEulerAngles;
        else
            Debug.LogError($"{name}: 表示専用Visual Rootが設定されていません。", this);
    }

    private void ResolvePlayerTransform()
    {
        _playerTransform = PlayerManager.instance != null
            ? PlayerManager.instance.PlayerGameObject?.transform
            : null;
        if (_playerTransform == null)
            _playerTransform = GameObject.FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME)?.transform;
    }

    private void ApplyFacingRotation()
    {
        if (_visualRoot == null)
            return;
        Vector3 angles = _visualInitialLocalEulerAngles;
        angles.y += _isFacingRight ? 0f : 180f;
        _visualRoot.localRotation = Quaternion.Euler(angles);
    }

    #endregion

    #region 攻撃選択・範囲判定

    /// <summary>
    /// 条件を満たす攻撃を開始します。
    /// 両方の範囲にプレイヤーがいる場合は、つらら攻撃を優先します。
    /// </summary>
    private void TryStartAttack()
    {
        if (_playerTransform == null)
        {
            ResolvePlayerTransform();
            return;
        }

        if (CanUseIcicleAttack() && IsPlayerInRange(_icicleAttackRangeX))
        {
            _attackCoroutine = StartCoroutine(IcicleAttackRoutine());
            return;
        }

        if (IsPlayerInRange(_meleeAttackRangeX))
            _attackCoroutine = StartCoroutine(MeleeAttackRoutine());
    }

    private bool IsPlayerInRange(float rangeX)
    {
        Vector2 difference = _playerTransform.position - PivotPosition;
        float forwardDistance = difference.x * FacingMultiplier;

        return forwardDistance > 0f
            && forwardDistance <= rangeX
            && difference.y >= -_attackRangeYDown
            && difference.y <= _attackRangeYUp;
    }

    private bool CanUseIcicleAttack()
    {
        return _isIcicleAttackTimeReady
            || GetRemainingIcicleCount() <= _icicleRemainingThreshold;
    }

    private void UpdateIcicleAttackAvailability()
    {
        if (
            !_isIcicleAttackTimeReady
            && _icicleAttackReuseElapsed >= _icicleAttackReuseTime
        )
            _isIcicleAttackTimeReady = true;
    }

    #endregion

    #region 攻撃シーケンス

    /// <summary>
    /// アニメーション後に既存のつららと新しいつららを入れ替え、
    /// 表示完了後に各つららの個別感知を許可します。
    /// </summary>
    private IEnumerator IcicleAttackRoutine()
    {
        _canStartAttack = false;
        _isIcicleAttackTimeReady = false;
        ChangeState(GolemState.IcicleAttacking);
        PlayAnimation(
            AnimIcicleAttackExecuteTrigger,
            AnimIcicleAttackExecuteSpeed,
            _icicleAttackExecuteDuration
        );
        yield return WaitWhileEnemyMoveActive(_icicleAttackExecuteDuration);

        PlayAnimation(
            AnimIcicleAttackRecoveryTrigger,
            AnimIcicleAttackRecoverySpeed,
            _icicleAttackRecoveryDuration
        );
        yield return WaitWhileEnemyMoveActive(_icicleAttackRecoveryDuration);

        List<IcicleMoveController> oldIcicles = new List<IcicleMoveController>(_activeIcicles);
        List<IcicleMoveController> newIcicles = CreateIcicleGroup();

        // 演出中にResetStateが呼ばれても新旧両方を回収できるよう、生成直後から追跡します。
        _activeIcicles.AddRange(newIcicles);
        yield return PlayIcicleReplacementEffect(oldIcicles, newIcicles);

        foreach (IcicleMoveController oldIcicle in oldIcicles)
            _activeIcicles.Remove(oldIcicle);

        yield return WaitWhileEnemyMoveActive(_icicleFallWaitDuration);

        // 一括落下ではなく、ここから各Icicleが自身の範囲でプレイヤーを検知します。
        foreach (IcicleMoveController icicle in newIcicles)
            if (icicle != null)
                icicle.AllowExternalFall();

        _icicleAttackReuseElapsed = 0f;

        ChangeState(GolemState.Idle);
        PlayIdleAnimation();
        yield return WaitWhileEnemyMoveActive(_icicleAttackPostDelay);
        _canStartAttack = true;
        _attackCoroutine = null;
    }

    /// <summary>
    /// Prepare、攻撃前待機、Execute、Recovery、攻撃後待機を順番に実行します。
    /// 腕のダメージ判定はExecute中だけ有効です。
    /// </summary>
    private IEnumerator MeleeAttackRoutine()
    {
        _canStartAttack = false;
        _hasProcessedImpactEvent = false;
        ChangeState(GolemState.MeleeAttacking);
        SetArmDamageActive(false);

        PlayAnimation(
            AnimMeleeAttackPrepareTrigger,
            AnimMeleeAttackPrepareSpeed,
            _meleeAttackPrepareDuration
        );
        yield return WaitWhileEnemyMoveActive(_meleeAttackPrepareDuration);
        yield return WaitWhileEnemyMoveActive(_meleeAttackPreExecuteDelay);

        SetArmDamageActive(true);
        PlayAnimation(
            AnimMeleeAttackExecuteTrigger,
            AnimMeleeAttackExecuteSpeed,
            _meleeAttackExecuteDuration
        );
        yield return WaitWhileEnemyMoveActive(_meleeAttackExecuteDuration);
        SetArmDamageActive(false);

        PlayAnimation(
            AnimMeleeAttackRecoveryTrigger,
            AnimMeleeAttackRecoverySpeed,
            _meleeAttackRecoveryDuration
        );
        yield return WaitWhileEnemyMoveActive(_meleeAttackRecoveryDuration);
        ChangeState(GolemState.Idle);
        PlayIdleAnimation();
        yield return WaitWhileEnemyMoveActive(_meleeAttackPostDelay);

        _canStartAttack = true;
        _attackCoroutine = null;
    }

    #endregion

    #region つららの生成・HOLOGRAM演出

    /// <summary>
    /// 前方Offsetから限界距離まで、指定数のつららを等間隔に生成します。
    /// </summary>
    private List<IcicleMoveController> CreateIcicleGroup()
    {
        List<IcicleMoveController> result = new List<IcicleMoveController>();
        if (_iciclePrefab == null)
        {
            Debug.LogError($"{name}: Icicle Prefabが設定されていません。", this);
            return result;
        }

        int count = Mathf.Max(1, _icicleCount);
        for (int i = 0; i < count; i++)
        {
            float ratio = count == 1 ? 0f : (float)i / (count - 1);
            float forwardLimit = Mathf.Max(_icicleForwardOffset, _icicleLimitDistance);
            float forwardDistance = Mathf.Lerp(_icicleForwardOffset, forwardLimit, ratio);
            Vector3 position =
                PivotPosition
                + new Vector3(forwardDistance * FacingMultiplier, _icicleHeight, 0f);
            GameObject icicleObject = Instantiate(_iciclePrefab, position, Quaternion.identity);
            IcicleMoveController icicle = icicleObject.GetComponent<IcicleMoveController>();
            if (icicle == null)
            {
                Debug.LogError($"{icicleObject.name}: IcicleMoveControllerがありません。", icicleObject);
                Destroy(icicleObject);
                continue;
            }

            icicle.PrepareExternalSummon(position);
            result.Add(icicle);
        }
        return result;
    }

    /// <summary>
    /// 古いつららの消失と新しいつららの実体化を、同一Sequenceで同時に再生します。
    /// </summary>
    private IEnumerator PlayIcicleReplacementEffect(
        List<IcicleMoveController> oldIcicles,
        List<IcicleMoveController> newIcicles
    )
    {
        _icicleHologramSequence?.Kill();
        _icicleHologramSequence = DOTween.Sequence();

        foreach (IcicleMoveController oldIcicle in oldIcicles)
        {
            if (oldIcicle == null || !oldIcicle.gameObject.activeInHierarchy)
                continue;

            AddHologramDisappear(
                _icicleHologramSequence,
                oldIcicle.gameObject,
                _icicleDisappearDuration
            );
        }

        foreach (IcicleMoveController newIcicle in newIcicles)
        {
            if (newIcicle == null)
                continue;

            AddHologramAppear(
                _icicleHologramSequence,
                newIcicle.gameObject,
                _icicleAppearDuration
            );
        }

        yield return _icicleHologramSequence.WaitForCompletion();

        // 実体化後にKeywordを無効化し、通常描画へ戻します。
        foreach (IcicleMoveController newIcicle in newIcicles)
        {
            if (newIcicle == null)
                continue;

            foreach (
                SpriteRenderer renderer in newIcicle.GetComponentsInChildren<SpriteRenderer>(true)
            )
                renderer.material.DisableKeyword("_HOLOGRAM_ON");
        }

        foreach (IcicleMoveController oldIcicle in oldIcicles)
            if (oldIcicle != null)
                Destroy(oldIcicle.gameObject);

        _icicleHologramSequence = null;
    }

    private static void AddHologramAppear(Sequence sequence, GameObject target, float duration)
    {
        foreach (SpriteRenderer renderer in target.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Material material = renderer.material;
            material.EnableKeyword("_HOLOGRAM_ON");
            if (material.HasProperty("_HologramBlend"))
            {
                material.SetFloat("_HologramBlend", 1f);
                sequence.Join(material.DOFloat(0f, "_HologramBlend", duration));
            }
            Color color = renderer.color;
            color.a = 1f;
            renderer.color = color;
        }
    }

    private static void AddHologramDisappear(Sequence sequence, GameObject target, float duration)
    {
        foreach (SpriteRenderer renderer in target.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Material material = renderer.material;
            material.EnableKeyword("_HOLOGRAM_ON");
            if (material.HasProperty("_HologramBlend"))
                sequence.Join(material.DOFloat(1f, "_HologramBlend", duration));
            sequence.Join(renderer.DOFade(0f, duration));
        }
    }

    private int GetRemainingIcicleCount()
    {
        int count = 0;
        foreach (IcicleMoveController icicle in _activeIcicles)
            if (icicle != null && icicle.IsExternalSummonAlive)
                count++;
        return count;
    }

    private void DestroyTrackedIcicles()
    {
        foreach (IcicleMoveController icicle in _activeIcicles)
            if (icicle != null)
                Destroy(icicle.gameObject);

        _activeIcicles.Clear();
    }

    #endregion

    #region 近距離攻撃・Impact

    private void SetArmDamageActive(bool isActive)
    {
        if (_armDamageObject == null)
            return;
        _armDamageObject.tag = isActive
            ? GameConstants.DAMAGEABLE_ENEMY_TAG_NAME
            : GameConstants.IMMUNE_ENEMY_TAG_NAME;
    }

    private void ShowImpactEffect()
    {
        if (_impactEffect == null)
            return;
        if (_impactEffectCoroutine != null)
            StopCoroutine(_impactEffectCoroutine);

        Vector2 offset = new Vector2(
            _impactEffectOffset.x * FacingMultiplier,
            _impactEffectOffset.y
        );
        _impactEffect.transform.position = PivotPosition + (Vector3)offset;

        // 再アクティブ化して、Effect Animatorを必ず先頭フレームから再生します。
        _impactEffect.SetActive(false);
        _impactEffect.SetActive(true);
        if (_impactEffectAnimator != null)
            _impactEffectAnimator.Play(0, -1, 0f);
        _impactEffectCoroutine = StartCoroutine(HideImpactEffectRoutine());
    }

    private IEnumerator HideImpactEffectRoutine()
    {
        yield return WaitWhileEnemyMoveActive(_impactEffectDisplayDuration);
        if (_impactEffect != null)
            _impactEffect.SetActive(false);
        _impactEffectCoroutine = null;
    }

    private void SpawnGroundSweepShockwave()
    {
        if (_groundSweepShockwavePrefab == null)
            return;
        Vector2 offset = new Vector2(
            _impactEffectOffset.x * FacingMultiplier,
            _impactEffectOffset.y
        );
        Vector3 position = PivotPosition + (Vector3)offset;
        GameObject shockwaveObject = Instantiate(
            _groundSweepShockwavePrefab,
            position,
            Quaternion.identity
        );

        // 非アクティブな原本を複製した場合も、ControllerのAwakeを先に実行させます。
        shockwaveObject.SetActive(true);

        GroundSweepShockwaveMoveController shockwave =
            shockwaveObject.GetComponent<GroundSweepShockwaveMoveController>();
        if (shockwave == null)
        {
            Debug.LogError(
                $"{shockwaveObject.name}: GroundSweepShockwaveMoveControllerがありません。",
                shockwaveObject
            );
            Destroy(shockwaveObject);
            return;
        }

        _spawnedShockwaves.Add(shockwave);
        shockwave.Launch(_isFacingRight, _shockwaveSpeed, _shockwaveLifeTime, _shockwaveDamage);
    }

    private void HideSpawnedShockwaves()
    {
        foreach (GroundSweepShockwaveMoveController shockwave in _spawnedShockwaves)
            if (shockwave != null)
                shockwave.gameObject.SetActive(false);
        _spawnedShockwaves.Clear();
    }

    #endregion

    #region 共通待機・Animator・状態管理

    /// <summary>
    /// 敵の行動停止中は経過時間へ加算せず、指定された実行時間だけ待機します。
    /// </summary>
    private IEnumerator WaitWhileEnemyMoveActive(float duration)
    {
        float elapsed = 0f;
        while (elapsed < Mathf.Max(0f, duration))
        {
            if (TimeManager.instance == null || !TimeManager.instance.isEnemyMovePaused)
                elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void PlayAnimation(int triggerHash, int speedHash, float duration)
    {
        if (_animator == null)
            return;

        // 各クリップは1秒基準のため、指定時間の逆数を再生速度として設定します。
        _animator.SetFloat(speedHash, 1f / Mathf.Max(0.01f, duration));
        _animator.SetTrigger(triggerHash);
    }

    private void PlayIdleAnimation()
    {
        _animator?.SetTrigger(AnimIdleTrigger);
    }

    private void ResetAnimator()
    {
        if (_animator == null)
            return;
        _animator.ResetTrigger(AnimIdleTrigger);
        _animator.ResetTrigger(AnimIcicleAttackExecuteTrigger);
        _animator.ResetTrigger(AnimIcicleAttackRecoveryTrigger);
        _animator.ResetTrigger(AnimMeleeAttackPrepareTrigger);
        _animator.ResetTrigger(AnimMeleeAttackExecuteTrigger);
        _animator.ResetTrigger(AnimMeleeAttackRecoveryTrigger);
        _animator.SetFloat(AnimIcicleAttackExecuteSpeed, 1f);
        _animator.SetFloat(AnimIcicleAttackRecoverySpeed, 1f);
        _animator.SetFloat(AnimMeleeAttackPrepareSpeed, 1f);
        _animator.SetFloat(AnimMeleeAttackExecuteSpeed, 1f);
        _animator.SetFloat(AnimMeleeAttackRecoverySpeed, 1f);
    }

    private void ChangeState(GolemState state)
    {
        _currentState = state;
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Vector3 pivot = _pivotTransform != null ? _pivotTransform.position : transform.position;
        float facingMultiplier = _isFacingRight ? 1f : -1f;

        DrawPivotGizmo(pivot);
        DrawFacingDirectionGizmo(pivot, facingMultiplier);
        DrawAttackRangeGizmo(
            pivot,
            facingMultiplier,
            _icicleAttackRangeX,
            new Color(0f, 0.55f, 1f, 0.18f)
        );
        DrawAttackRangeGizmo(
            pivot,
            facingMultiplier,
            _meleeAttackRangeX,
            new Color(1f, 0.15f, 0.1f, 0.22f)
        );
        DrawIciclePlacementGizmos(pivot, facingMultiplier);
        DrawImpactAndShockwaveGizmos(pivot, facingMultiplier);
    }

    private void DrawPivotGizmo(Vector3 pivot)
    {
        const float pivotRadius = 0.16f;

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(pivot, pivotRadius);
        Gizmos.DrawLine(pivot + Vector3.left * pivotRadius, pivot + Vector3.right * pivotRadius);
        Gizmos.DrawLine(pivot + Vector3.down * pivotRadius, pivot + Vector3.up * pivotRadius);

        Gizmos.color = new Color(1f, 1f, 1f, 0.65f);
        Gizmos.DrawLine(
            pivot + Vector3.down * _attackRangeYDown,
            pivot + Vector3.up * _attackRangeYUp
        );
        Gizmos.DrawWireSphere(pivot + Vector3.up * _attackRangeYUp, 0.08f);
        Gizmos.DrawWireSphere(pivot + Vector3.down * _attackRangeYDown, 0.08f);
    }

    private static void DrawFacingDirectionGizmo(Vector3 pivot, float facingMultiplier)
    {
        Vector3 direction = Vector3.right * facingMultiplier;
        Vector3 arrowEnd = pivot + direction * 1.25f;
        Vector3 arrowBack = arrowEnd - direction * 0.3f;

        Gizmos.color = new Color(1f, 0.85f, 0f, 1f);
        Gizmos.DrawLine(pivot, arrowEnd);
        Gizmos.DrawLine(arrowEnd, arrowBack + Vector3.up * 0.2f);
        Gizmos.DrawLine(arrowEnd, arrowBack + Vector3.down * 0.2f);
    }

    private void DrawAttackRangeGizmo(
        Vector3 pivot,
        float facingMultiplier,
        float rangeX,
        Color fillColor
    )
    {
        float height = _attackRangeYUp + _attackRangeYDown;
        Vector3 center = pivot
            + new Vector3(
                rangeX * 0.5f * facingMultiplier,
                (_attackRangeYUp - _attackRangeYDown) * 0.5f,
                0f
            );
        Vector3 size = new Vector3(rangeX, height, 0.05f);

        Gizmos.color = fillColor;
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(fillColor.r, fillColor.g, fillColor.b, 0.95f);
        Gizmos.DrawWireCube(center, size);
    }

    private void DrawIciclePlacementGizmos(Vector3 pivot, float facingMultiplier)
    {
        int count = Mathf.Max(1, _icicleCount);
        float forwardLimit = Mathf.Max(_icicleForwardOffset, _icicleLimitDistance);
        Vector3 firstPosition = pivot
            + new Vector3(_icicleForwardOffset * facingMultiplier, _icicleHeight, 0f);
        Vector3 lastPosition = pivot
            + new Vector3(forwardLimit * facingMultiplier, _icicleHeight, 0f);

        Gizmos.color = new Color(0.1f, 1f, 1f, 0.9f);
        Gizmos.DrawLine(firstPosition, lastPosition);

        Gizmos.color = new Color(0.1f, 1f, 1f, 0.45f);
        Gizmos.DrawLine(pivot, firstPosition);
        Gizmos.DrawLine(
            new Vector3(firstPosition.x, pivot.y, pivot.z),
            firstPosition
        );

        for (int i = 0; i < count; i++)
        {
            float ratio = count == 1 ? 0f : (float)i / (count - 1);
            float forwardDistance = Mathf.Lerp(_icicleForwardOffset, forwardLimit, ratio);
            Vector3 position = pivot
                + new Vector3(forwardDistance * facingMultiplier, _icicleHeight, 0f);

            Gizmos.color = i == 0
                ? new Color(0f, 1f, 0.65f, 1f)
                : new Color(0.1f, 0.8f, 1f, 1f);
            Gizmos.DrawWireSphere(position, 0.18f);
            Gizmos.DrawLine(position + Vector3.down * 0.25f, position + Vector3.up * 0.25f);
        }

        Gizmos.color = new Color(0f, 0.65f, 1f, 1f);
        Gizmos.DrawLine(lastPosition + Vector3.down * 0.35f, lastPosition + Vector3.up * 0.35f);
    }

    private void DrawImpactAndShockwaveGizmos(Vector3 pivot, float facingMultiplier)
    {
        Vector3 impactPosition = pivot
            + new Vector3(
                _impactEffectOffset.x * facingMultiplier,
                _impactEffectOffset.y,
                0f
            );

        Gizmos.color = new Color(1f, 0f, 1f, 0.85f);
        Gizmos.DrawLine(pivot, impactPosition);
        Gizmos.DrawWireSphere(impactPosition, 0.22f);
        Gizmos.DrawLine(impactPosition + Vector3.left * 0.3f, impactPosition + Vector3.right * 0.3f);
        Gizmos.DrawLine(impactPosition + Vector3.down * 0.3f, impactPosition + Vector3.up * 0.3f);

        Vector3 shockwaveEnd = impactPosition + Vector3.right * facingMultiplier * 1.5f;
        Gizmos.color = new Color(1f, 0.45f, 0f, 1f);
        Gizmos.DrawLine(impactPosition, shockwaveEnd);
        Gizmos.DrawWireCube(shockwaveEnd, new Vector3(0.18f, 0.18f, 0.05f));
    }

    #endregion
}

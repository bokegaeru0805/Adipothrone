using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class ApothecaryMoveController : MonoBehaviour
{
    /// <summary>
    /// ボスの現在の状態
    /// </summary>
    public enum ApothecaryState
    {
        Intro,
        Idle,
        Attacking,
        FireAttacking, // 炎攻撃状態
        WindAttacking, // 風攻撃状態
        IceAttacking, // 氷攻撃状態
        ThunderAttacking, // 雷攻撃状態
        LightAttacking, // 光攻撃状態
    }

    public ApothecaryState CurrentState { get; private set; } = ApothecaryState.Intro;

    [Header("デバッグ機能")]
    [Tooltip("trueの場合、待機時間を0にしてデバッグを容易にします（エディタ上のみ有効）")]
    [SerializeField]
    private bool isDebugNoWait = false;

    /// <summary>
    /// エディタ上でのみisDebugNoWaitの値を返し、ビルド後は常にfalseを返します
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

    [Header("共通攻撃設定")]
    [Tooltip("弾の発射位置オフセット(右向き時)")]
    [SerializeField]
    private Vector2 normalAttackOffset = new Vector2(1.0f, 1.0f);

    [Header("炎攻撃の設定")]
    [Tooltip("ThrowReadyアニメーションでの待機時間")]
    [SerializeField]
    private float fireThrowReadyDuration = 1.0f;

    [Tooltip("Throwアニメーション開始から発射までの遅延時間")]
    [SerializeField]
    private float fireThrowDelayDuration = 0.5f;

    [Tooltip("炎弾の最小発射角度(度)")]
    [SerializeField]
    private float fireAngleMin = 45f;

    [Tooltip("炎弾の最大発射角度(度)")]
    [SerializeField]
    private float fireAngleMax = 75f;

    [Tooltip("炎弾の連続発射間隔")]
    [SerializeField]
    private float fireInterval = 0.2f;

    [Tooltip("炎弾の最小発射回数")]
    [SerializeField]
    private int fireCountMin = 3;

    [Tooltip("炎弾の最大発射回数")]
    [SerializeField]
    private int fireCountMax = 6;

    [Tooltip("炎弾の初速")]
    [SerializeField]
    private float fireBulletSpeed = 10f;

    [Tooltip("炎弾(空中)のダメージ")]
    [SerializeField]
    private int fireAirDamage = 10;

    [Tooltip("炎上(着地後)のダメージ")]
    [SerializeField]
    private int fireGroundDamage = 15;

    [Tooltip("着地後の炎上継続時間")]
    [SerializeField]
    private float fireGroundDuration = 3.0f;

    [Tooltip("炎弾の最初の炎上確率(0.0〜1.0)")]
    [SerializeField]
    private float fireInitialBurnProbability = 0.3f;

    [Tooltip("炎弾の限界貫通(すり抜け)数。この回数に達したら次は100%炎上する")]
    [SerializeField]
    private int fireMaxPierceCount = 3;

    [Header("風攻撃の設定")]
    [Tooltip("ThrowReadyアニメーションでの待機時間")]
    [SerializeField]
    private float windThrowReadyDuration = 1.2f;

    [Tooltip("Throwアニメーション開始から発射までの遅延時間")]
    [SerializeField]
    private float windThrowDelayDuration = 0.4f;

    [Tooltip("風弾の最小発射回数")]
    [SerializeField]
    private int windCountMin = 2;

    [Tooltip("風弾の最大発射回数")]
    [SerializeField]
    private int windCountMax = 5;

    [Tooltip("風弾の連続発射間隔")]
    [SerializeField]
    private float windInterval = 0.3f;

    [Tooltip("プレイヤーを狙う際の角度のブレ幅(±度)")]
    [SerializeField]
    private float windAngleVariance = 15f;

    [Tooltip("風弾の初速")]
    [SerializeField]
    private float windBulletSpeed = 12f;

    [Tooltip("風弾のダメージ")]
    [SerializeField]
    private int windDamage = 10;

    [Tooltip("風弾ヒット時のノックバック力")]
    [SerializeField]
    private float windKnockbackForce = 15f;

    [Tooltip("範囲外に出てから消滅するまでの時間(秒)")]
    [SerializeField]
    private float windDisappearDelay = 2.0f;

    [Header("氷攻撃の設定")]
    [Tooltip("LookBackアニメーションでの待機時間")]
    [SerializeField]
    private float iceLookBackDuration = 1.0f;

    [Tooltip("Throwアニメーション開始から攻撃完了扱いにするまでの時間")]
    [SerializeField]
    private float iceThrowDelayDuration = 0.5f;

    [Tooltip("1つの足場あたりにぶら下げるつららの最小数")]
    [SerializeField]
    private int iceCountMin = 1;

    [Tooltip("1つの足場あたりにぶら下げるつららの最大数")]
    [SerializeField]
    private int iceCountMax = 3;

    [Tooltip("円軌道（波紋）が広がる速度")]
    [SerializeField]
    private float iceRippleSpeed = 15f;

    [Header("エリア境界の設定 (消滅・取得判定用)")]
    [SerializeField]
    private float areaLeftBound = -10f;

    [SerializeField]
    private float areaRightBound = 10f;

    [SerializeField]
    private float areaBottomBound = -5f;

    [SerializeField]
    private float areaTopBound = 10f;

    [Header("オブジェクトプール設定")]
    [Tooltip("発射する炎弾のプレハブ")]
    [SerializeField]
    private ApothecaryFireBullet fireBulletPrefab;

    [Tooltip("発射する風弾のプレハブ")]
    [SerializeField]
    private ApothecaryWindBullet windBulletPrefab;

    [Tooltip("発射する氷弾(つらら)のプレハブ")]
    [SerializeField]
    private IcicleMoveController iciclePrefab;

    [Tooltip("発射する雷のプレハブ")]
    [SerializeField]
    private ApothecaryThunder thunderPrefab;

    [Header("雷攻撃の設定")]
    [Tooltip("LookBackアニメーションでの待機時間")]
    [SerializeField]
    private float thunderLookBackDuration = 1.0f;

    [Tooltip("Throwアニメーション開始から攻撃完了扱いにするまでの時間")]
    [SerializeField]
    private float thunderThrowDelayDuration = 0.5f;

    [Tooltip("雷の最小発生数")]
    [SerializeField]
    private int thunderCountMin = 5;

    [Tooltip("雷の最大発生数")]
    [SerializeField]
    private int thunderCountMax = 10;

    [Tooltip("雷同士の最小距離（近すぎないようにするための距離）")]
    [SerializeField]
    private float thunderMinDistance = 2.0f;

    [Tooltip("雷のダメージ")]
    [SerializeField]
    private int thunderDamage = 20;

    [Tooltip("予兆エフェクトの表示時間")]
    [SerializeField]
    private float thunderWarningDuration = 1.0f;

    [Tooltip("予兆から本体への移行（スケール/フェード）にかかる時間")]
    [SerializeField]
    private float thunderTransitionDuration = 0.3f;

    [Tooltip("落雷（ダメージ発生）の継続時間")]
    [SerializeField]
    private float thunderAttackDuration = 0.5f;

    [Header("光攻撃の設定")]
    [Tooltip("LiftHoldのオフセット位置(右向き時)")]
    [SerializeField]
    private Vector2 lightAttackOffset = new Vector2(1.5f, 2.0f);

    [Tooltip("LiftHoldアニメーション開始後のチャージ前待機時間")]
    [SerializeField]
    private float lightLiftHoldReadyDuration = 1.0f;

    [Tooltip("光攻撃のチャージ（予測線回転）時間")]
    [SerializeField]
    private float lightChargeDuration = 2.0f;

    [Tooltip("レーザーの展開（WidthとScaleYの拡大）にかかる時間")]
    [SerializeField]
    private float lightLaserExpandDuration = 0.2f;

    [Tooltip("レーザーの攻撃持続時間")]
    [SerializeField]
    private float lightAttackDuration = 2.0f;

    [Tooltip("攻撃終了時のフェード・縮小消滅にかかる時間")]
    [SerializeField]
    private float lightEndDuration = 0.5f;

    [Tooltip("レーザーの発射本数")]
    [SerializeField]
    private int lightLaserCount = 4;

    [Tooltip("地形を貫通するレーザーと予測線の固定長さ")]
    [SerializeField]
    private float lightLaserLength = 30f;

    [Tooltip("予測線の回転速度（度/秒）")]
    [SerializeField]
    private float lightRotationSpeed = 45f;

    [Tooltip("光レーザーのダメージ")]
    [SerializeField]
    private int lightDamage = 30;

    [Header("光攻撃の予測線演出設定")]
    [Tooltip("予測線のチャージ開始時の太さ")]
    [SerializeField]
    private float lightPredictionStartWidth = 0.05f;

    [Tooltip("予測線のチャージ完了直前の太さ")]
    [SerializeField]
    private float lightPredictionEndWidth = 0.3f;

    [Tooltip("チャージ時間(0〜1)に応じた予測線の太さの変化率（Linearやイージングを設定）")]
    [SerializeField]
    private AnimationCurve lightPredictionWidthCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Tooltip("予測線のカラーグラデーション（左側がチャージ開始時、右側がチャージ完了時）")]
    [SerializeField]
    private Gradient lightPredictionColorGradient;

    [Header("光攻撃の参照・プール設定")]
    [Tooltip("チャージ中に表示される核となるエフェクト(SpriteRendererなど)")]
    [SerializeField]
    private SpriteRenderer lightCoreSpriteRenderer;

    [Tooltip("ボスのプレハブの子オブジェクトとして配置されたChargeEffect_Master")]
    [SerializeField]
    private ChargeEffect_Master lightChargeEffect;

    [Tooltip("予測線とレーザー本体を含むプレハブ")]
    [SerializeField]
    private ApothecaryLightLaser lightLaserPrefab;

    [Header("ポーション演出の設定")]
    [Tooltip("ポーションを表示する子オブジェクトのSpriteRenderer")]
    [SerializeField]
    private SpriteRenderer potionSpriteRenderer;

    [Tooltip("炎攻撃時に持たせるポーションのスプライト")]
    [SerializeField]
    private Sprite firePotionSprite;

    [Tooltip("風攻撃時に持たせるポーションのスプライト")]
    [SerializeField]
    private Sprite windPotionSprite;

    [Tooltip("氷攻撃時に持たせるポーションのスプライト")]
    [SerializeField]
    private Sprite icePotionSprite;

    [Tooltip("雷攻撃時に持たせるポーションのスプライト")]
    [SerializeField]
    private Sprite thunderPotionSprite;

    [Tooltip("光攻撃時に持たせるポーションのスプライト")]
    [SerializeField]
    private Sprite lightPotionSprite;

    // 初期生成する弾の数をconst変数として定義
    private const int INITIAL_FIRE_POOL_SIZE = 10;
    private const int INITIAL_WIND_POOL_SIZE = 10;
    private const int INITIAL_ICICLE_POOL_SIZE = 15;
    private const int INITIAL_THUNDER_POOL_SIZE = 15;
    private const int INITIAL_LIGHT_POOL_SIZE = 8;

    // 専用のオブジェクトプール
    private List<ApothecaryFireBullet> fireBulletPool = new List<ApothecaryFireBullet>();
    private List<ApothecaryWindBullet> windBulletPool = new List<ApothecaryWindBullet>();
    private List<IcicleMoveController> iciclePool = new List<IcicleMoveController>();
    private List<ApothecaryThunder> thunderPool = new List<ApothecaryThunder>();
    private List<ApothecaryLightLaser> lightLaserPool = new List<ApothecaryLightLaser>();

    // 取得した足場のコライダーを保持するリスト
    private List<Collider2D> _icePlatforms = new List<Collider2D>();

    private Coroutine attackLoopCoroutine;
    private Animator _animator;
    private Transform _playerTransform;
    private SpriteRenderer _bodySpriteRenderer;
    private List<IcicleMoveController> _reservedIcicles = new List<IcicleMoveController>();

    // Animatorのパラメータをハッシュ化してキャッシュ
    private readonly int _throwReadyHash = Animator.StringToHash("ThrowReadyTrigger");
    private readonly int _throwHash = Animator.StringToHash("ThrowTrigger");
    private readonly int _idleHash = Animator.StringToHash("IdleTrigger");
    private readonly int _lookBackHash = Animator.StringToHash("LookBackTrigger");
    private readonly int _liftHoldHash = Animator.StringToHash("LiftHoldTrigger");

    private bool isFacingRight = true;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _bodySpriteRenderer = GetComponent<SpriteRenderer>();

        if (lightChargeEffect != null)
        {
            lightChargeEffect.StopEffect();
        }

        if (lightCoreSpriteRenderer != null)
        {
            lightCoreSpriteRenderer.gameObject.SetActive(false);
        }

        if (potionSpriteRenderer != null)
        {
            potionSpriteRenderer.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        ResetState();
    }

    /// <summary>
    /// ボスの状態をリセットし、初期行動を開始します
    /// </summary>
    public void ResetState()
    {
        // プレイヤーの取得
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

        // プール
        InitializePools();

        if (attackLoopCoroutine != null)
        {
            StopCoroutine(attackLoopCoroutine);
        }

        if (lightChargeEffect != null)
        {
            lightChargeEffect.StopEffect();
        }

        if (lightCoreSpriteRenderer != null)
        {
            lightCoreSpriteRenderer.gameObject.SetActive(false);
        }

        if (potionSpriteRenderer != null)
        {
            potionSpriteRenderer.gameObject.SetActive(false);
        }

        // エリア内にある薄い足場（OBJECT_GROUND）を取得
        Vector2 pointA = new Vector2(areaLeftBound, areaTopBound);
        Vector2 pointB = new Vector2(areaRightBound, areaBottomBound);
        Collider2D[] colliders = Physics2D.OverlapAreaAll(
            pointA,
            pointB,
            LayerMask.GetMask(GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND)
        );
        _icePlatforms.Clear();
        _icePlatforms.AddRange(colliders);

        StartCoroutine(IntroSequence());
    }

    private void FixedUpdate()
    {
        // Idle状態のときのみ、プレイヤーの方向へ自動で向きを更新します
        // 攻撃状態（FireAttackingなど）のときはこの処理がスキップされ、向きが固定されます
        if (CurrentState == ApothecaryState.Idle)
        {
            UpdateFacingDirection();
        }
    }

    /// <summary>
    /// プレイヤーの位置に応じて、ボス本体の向きを自動的に更新します
    /// </summary>
    private void UpdateFacingDirection()
    {
        if (_playerTransform == null)
            return;

        // プレイヤーが自身の右側にいるかどうかでフラグを更新
        isFacingRight = _playerTransform.position.x > transform.position.x;

        // SpriteRendererの左右反転を適用
        if (_bodySpriteRenderer != null)
        {
            _bodySpriteRenderer.flipX = !isFacingRight;
        }
    }

    /// <summary>
    /// 登場時のシーケンス
    /// </summary>
    private IEnumerator IntroSequence()
    {
        CurrentState = ApothecaryState.Intro;

        // 登場時の移動やアニメーションの待機処理を記述します
        yield return null;

        // 登場完了後、攻撃ループを開始します
        StartAttackLoop();
    }

    /// <summary>
    /// 攻撃ループを開始します
    /// </summary>
    private void StartAttackLoop()
    {
        if (attackLoopCoroutine != null)
        {
            StopCoroutine(attackLoopCoroutine);
        }
        attackLoopCoroutine = StartCoroutine(AttackLoopSequence());
    }

    /// <summary>
    /// 攻撃方法の選択と待機を繰り返すシーケンス
    /// </summary>
    private IEnumerator AttackLoopSequence()
    {
        while (true)
        {
            // 1. 攻撃方法の選択
            // 今後ランダムにする場合は以下のコメントアウトを外して調整します
            /*
            float rand = Random.value;
            if (rand < 0.33f)
                yield return StartCoroutine(PerformFireAttack());
            else if (rand < 0.66f)
                yield return StartCoroutine(PerformWindAttack());
            else
                yield return StartCoroutine(PerformIceAttack());
            */

            // 現状はテストのため、炎攻撃に固定しています
            yield return StartCoroutine(PerformFireAttack());

            // 3. 待機状態
            CurrentState = ApothecaryState.Idle;

            // 4. 次の攻撃までのインターバル待機
            float waitTime = IsDebugNoWaitActive ? 0f : 2.0f; // 2.0fは例としてのインターバル
            yield return new WaitForSeconds(waitTime);
        }
    }

    /// <summary>
    /// 各弾の専用プールを初期化します
    /// </summary>
    private void InitializePools()
    {
        // 炎弾の初期化
        if (fireBulletPrefab != null)
        {
            for (int i = 0; i < INITIAL_FIRE_POOL_SIZE; i++)
            {
                ApothecaryFireBullet bullet = Instantiate(
                    fireBulletPrefab,
                    transform.position,
                    Quaternion.identity
                );
                bullet.gameObject.SetActive(false);
                fireBulletPool.Add(bullet);
            }
        }
        else
        {
            Debug.LogWarning("FireBulletPrefabが設定されていません。");
        }

        // 風弾の初期化
        if (windBulletPrefab != null)
        {
            for (int i = 0; i < INITIAL_WIND_POOL_SIZE; i++)
            {
                ApothecaryWindBullet bullet = Instantiate(
                    windBulletPrefab,
                    transform.position,
                    Quaternion.identity
                );
                bullet.gameObject.SetActive(false);
                windBulletPool.Add(bullet);
            }
        }
        else
        {
            Debug.LogWarning("WindBulletPrefabが設定されていません。");
        }

        // 氷弾の初期化
        if (iciclePrefab != null)
        {
            for (int i = 0; i < INITIAL_ICICLE_POOL_SIZE; i++)
            {
                IcicleMoveController icicle = Instantiate(
                    iciclePrefab,
                    transform.position,
                    Quaternion.identity
                );
                icicle.gameObject.SetActive(false);
                iciclePool.Add(icicle);
            }
        }
        else
        {
            Debug.LogWarning("IciclePrefabが設定されていません。");
        }

        // 雷弾の初期化
        if (thunderPrefab != null)
        {
            for (int i = 0; i < INITIAL_THUNDER_POOL_SIZE; i++)
            {
                ApothecaryThunder thunder = Instantiate(
                    thunderPrefab,
                    transform.position,
                    Quaternion.identity
                );
                thunder.gameObject.SetActive(false);
                thunderPool.Add(thunder);
            }
        }
        else
        {
            Debug.LogWarning("ThunderPrefabが設定されていません。");
        }

        // 光レーザーの初期化
        if (lightLaserPrefab != null)
        {
            for (int i = 0; i < INITIAL_LIGHT_POOL_SIZE; i++)
            {
                ApothecaryLightLaser laser = Instantiate(
                    lightLaserPrefab,
                    transform.position,
                    Quaternion.identity
                );
                laser.gameObject.SetActive(false);
                lightLaserPool.Add(laser);
            }
        }
        else
        {
            Debug.LogWarning("LightLaserPrefabが設定されていません。");
        }
    }

    /// <summary>
    /// プールから非アクティブな炎弾を取得します
    /// </summary>
    private ApothecaryFireBullet GetFireBulletFromPool()
    {
        foreach (var bullet in fireBulletPool)
        {
            if (bullet != null && !bullet.gameObject.activeInHierarchy)
            {
                return bullet;
            }
        }

        ApothecaryFireBullet newBullet = Instantiate(
            fireBulletPrefab,
            transform.position,
            Quaternion.identity
        );
        newBullet.gameObject.SetActive(false);
        fireBulletPool.Add(newBullet);
        return newBullet;
    }

    /// <summary>
    /// プールから非アクティブな風弾を取得します
    /// </summary>
    private ApothecaryWindBullet GetWindBulletFromPool()
    {
        foreach (var bullet in windBulletPool)
        {
            if (bullet != null && !bullet.gameObject.activeInHierarchy)
            {
                return bullet;
            }
        }

        ApothecaryWindBullet newBullet = Instantiate(
            windBulletPrefab,
            transform.position,
            Quaternion.identity
        );
        newBullet.gameObject.SetActive(false);
        windBulletPool.Add(newBullet);
        return newBullet;
    }

    /// <summary>
    /// プールから非アクティブな氷弾(つらら)を取得します
    /// </summary>
    private IcicleMoveController GetIcicleFromPool()
    {
        foreach (var icicle in iciclePool)
        {
            if (icicle != null && !icicle.gameObject.activeInHierarchy)
            {
                return icicle;
            }
        }

        IcicleMoveController newIcicle = Instantiate(
            iciclePrefab,
            transform.position,
            Quaternion.identity
        );
        newIcicle.gameObject.SetActive(false);
        iciclePool.Add(newIcicle);
        return newIcicle;
    }

    /// <summary>
    /// 炎攻撃の一連の動作
    /// </summary>
    private IEnumerator PerformFireAttack()
    {
        CurrentState = ApothecaryState.FireAttacking;

        float currentThrowReadyDuration = IsDebugNoWaitActive ? 0f : fireThrowReadyDuration;
        float currentThrowDelayDuration = IsDebugNoWaitActive ? 0f : fireThrowDelayDuration;
        float currentFireInterval = IsDebugNoWaitActive ? 0f : fireInterval;

        // 1. 待機モーション
        if (_animator != null)
            _animator.SetTrigger(_throwReadyHash);
        if (currentThrowReadyDuration > 0f)
            yield return new WaitForSeconds(currentThrowReadyDuration);

        // 2. 攻撃モーション開始
        if (_animator != null)
            _animator.SetTrigger(_throwHash);

        if (potionSpriteRenderer != null && firePotionSprite != null)
        {
            potionSpriteRenderer.sprite = firePotionSprite;
            potionSpriteRenderer.gameObject.SetActive(true);
        }

        if (currentThrowDelayDuration > 0f)
            yield return new WaitForSeconds(currentThrowDelayDuration);

        int currentFireCount = Random.Range(fireCountMin, fireCountMax + 1);

        // 3. 連続発射ループ
        for (int i = 0; i < currentFireCount; i++)
        {
            float offsetX = isFacingRight ? normalAttackOffset.x : -normalAttackOffset.x;
            Vector3 spawnPos = transform.position + new Vector3(offsetX, normalAttackOffset.y, 0f);

            float randomAngle = Random.Range(fireAngleMin, fireAngleMax);
            float finalAngle = isFacingRight ? randomAngle : 180f - randomAngle;

            Vector2 fireDirection = new Vector2(
                Mathf.Cos(finalAngle * Mathf.Deg2Rad),
                Mathf.Sin(finalAngle * Mathf.Deg2Rad)
            );

            ApothecaryFireBullet bullet = GetFireBulletFromPool();
            if (bullet != null)
            {
                bullet.transform.position = spawnPos;
                bullet.gameObject.SetActive(true);

                bullet.Setup(
                    fireDirection,
                    fireBulletSpeed,
                    fireAirDamage,
                    fireGroundDamage,
                    fireGroundDuration,
                    fireInitialBurnProbability,
                    fireMaxPierceCount
                );
            }

            if (currentFireInterval > 0f)
                yield return new WaitForSeconds(currentFireInterval);
        }

        // 4. 復帰

        if (potionSpriteRenderer != null)
        {
            potionSpriteRenderer.gameObject.SetActive(false);
        }

        if (_animator != null)
            _animator.SetTrigger(_idleHash);
        CurrentState = ApothecaryState.Idle;
    }

    /// <summary>
    /// 風攻撃の一連の動作
    /// </summary>
    private IEnumerator PerformWindAttack()
    {
        CurrentState = ApothecaryState.WindAttacking;

        float currentThrowReadyDuration = IsDebugNoWaitActive ? 0f : windThrowReadyDuration;
        float currentThrowDelayDuration = IsDebugNoWaitActive ? 0f : windThrowDelayDuration;
        float currentWindInterval = IsDebugNoWaitActive ? 0f : windInterval;

        // 1. 待機モーション
        if (_animator != null)
            _animator.SetTrigger(_throwReadyHash);
        if (currentThrowReadyDuration > 0f)
            yield return new WaitForSeconds(currentThrowReadyDuration);

        // 2. 攻撃モーション開始
        if (_animator != null)
            _animator.SetTrigger(_throwHash);

        if (potionSpriteRenderer != null && windPotionSprite != null)
        {
            potionSpriteRenderer.sprite = windPotionSprite;
            potionSpriteRenderer.gameObject.SetActive(true);
        }

        if (currentThrowDelayDuration > 0f)
            yield return new WaitForSeconds(currentThrowDelayDuration);

        int currentWindCount = Random.Range(windCountMin, windCountMax + 1);

        // 3. 連続発射ループ
        for (int i = 0; i < currentWindCount; i++)
        {
            float offsetX = isFacingRight ? normalAttackOffset.x : -normalAttackOffset.x;
            Vector3 spawnPos = transform.position + new Vector3(offsetX, normalAttackOffset.y, 0f);

            Vector2 targetDirection = isFacingRight ? Vector2.right : Vector2.left;
            if (_playerTransform != null)
            {
                targetDirection = (_playerTransform.position - spawnPos).normalized;
            }

            float baseAngle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg;
            float finalAngle = baseAngle + Random.Range(-windAngleVariance, windAngleVariance);

            Vector2 windDirection = new Vector2(
                Mathf.Cos(finalAngle * Mathf.Deg2Rad),
                Mathf.Sin(finalAngle * Mathf.Deg2Rad)
            );

            ApothecaryWindBullet bullet = GetWindBulletFromPool();
            if (bullet != null)
            {
                bullet.transform.position = spawnPos;
                bullet.gameObject.SetActive(true);

                bullet.Setup(
                    windDirection,
                    windBulletSpeed,
                    windDamage,
                    windKnockbackForce,
                    areaLeftBound,
                    areaRightBound,
                    areaBottomBound,
                    areaTopBound,
                    windDisappearDelay
                );
            }

            if (currentWindInterval > 0f)
                yield return new WaitForSeconds(currentWindInterval);
        }

        // 4. 復帰
        if (potionSpriteRenderer != null)
        {
            potionSpriteRenderer.gameObject.SetActive(false);
        }

        if (_animator != null)
            _animator.SetTrigger(_idleHash);
        CurrentState = ApothecaryState.Idle;
    }

    /// <summary>
    /// 氷攻撃の一連の動作
    /// </summary>
    private IEnumerator PerformIceAttack()
    {
        CurrentState = ApothecaryState.IceAttacking;

        float currentLookBackDuration = IsDebugNoWaitActive ? 1f : iceLookBackDuration;
        float currentThrowDelayDuration = IsDebugNoWaitActive ? 1f : iceThrowDelayDuration;

        // 1. 以前の攻撃で残っているつららを全て消去する
        _reservedIcicles.Clear();
        foreach (var icicle in iciclePool)
        {
            if (icicle != null && icicle.gameObject.activeInHierarchy)
            {
                // 直接消さずに、つらら自身に破壊演出を行わせる
                icicle.ForceCrash();
            }
        }

        // 2. 待機モーション (背を向ける)
        if (_animator != null)
            _animator.SetTrigger(_lookBackHash);
        if (currentLookBackDuration > 0f)
            yield return new WaitForSeconds(currentLookBackDuration);

        // 3. 発射モーション (波紋を広げる)
        if (_animator != null)
            _animator.SetTrigger(_throwHash);

        if (potionSpriteRenderer != null && icePotionSprite != null)
        {
            potionSpriteRenderer.sprite = icePotionSprite;
            potionSpriteRenderer.gameObject.SetActive(true);
        }

        float offsetX = isFacingRight ? normalAttackOffset.x : -normalAttackOffset.x;
        Vector3 spawnPos = transform.position + new Vector3(offsetX, normalAttackOffset.y, 0f);

        // 各足場につららを配置する計算と、遅延出現の登録
        foreach (var platform in _icePlatforms)
        {
            if (platform == null)
                continue;

            float minX = platform.bounds.min.x;
            float maxX = platform.bounds.max.x;
            float bottomY = platform.bounds.min.y;

            int icicleCount = Random.Range(iceCountMin, iceCountMax + 1);
            float stepX = (maxX - minX) / (icicleCount + 1);

            for (int i = 1; i <= icicleCount; i++)
            {
                float targetX = minX + (stepX * i);
                Vector3 targetPos = new Vector3(targetX, bottomY, 0f);

                float distance = Vector3.Distance(spawnPos, targetPos);
                float delay = distance / iceRippleSpeed;

                IcicleMoveController icicle = GetUnreservedIcicleFromPool();
                if (icicle != null)
                {
                    // 重複して取得されないように予約リストに入れておく（まだSetActiveはfalseのまま）
                    _reservedIcicles.Add(icicle);
                    StartCoroutine(SpawnIcicleWithDelay(icicle, targetPos, delay));
                }
            }
        }

        // 4. モーション完了までの待機
        if (currentThrowDelayDuration > 0f)
            yield return new WaitForSeconds(currentThrowDelayDuration);

        // 5. 復帰
        if (potionSpriteRenderer != null)
        {
            potionSpriteRenderer.gameObject.SetActive(false);
        }

        if (_animator != null)
            _animator.SetTrigger(_idleHash);
        CurrentState = ApothecaryState.Idle;
    }

    /// <summary>
    /// 指定された時間遅延させてから、つららを配置・出現させます
    /// </summary>
    private IEnumerator SpawnIcicleWithDelay(
        IcicleMoveController icicle,
        Vector3 position,
        float delay
    )
    {
        if (!IsDebugNoWaitActive && delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (icicle != null)
        {
            // 出現する直前に予約リストから解除
            if (_reservedIcicles.Contains(icicle))
            {
                _reservedIcicles.Remove(icicle);
            }

            // 正しい座標に配置した上で、内部で SetActive(true) やエフェクト再生を行う
            icicle.SpawnAsBossSummon(position);
        }
    }

    /// <summary>
    /// プールからまだこのターンで予約されていない非アクティブな氷弾(つらら)を取得します
    /// </summary>
    private IcicleMoveController GetUnreservedIcicleFromPool()
    {
        foreach (var icicle in iciclePool)
        {
            if (
                icicle != null
                && !icicle.gameObject.activeInHierarchy
                && !_reservedIcicles.Contains(icicle)
            )
            {
                return icicle;
            }
        }

        IcicleMoveController newIcicle = Instantiate(
            iciclePrefab,
            transform.position,
            Quaternion.identity
        );
        newIcicle.gameObject.SetActive(false);
        iciclePool.Add(newIcicle);
        return newIcicle;
    }

    /// <summary>
    /// プールから非アクティブな雷を取得します（足りない場合は新規生成する最適化処理）
    /// </summary>
    private ApothecaryThunder GetThunderFromPool()
    {
        foreach (var thunder in thunderPool)
        {
            if (thunder != null && !thunder.gameObject.activeInHierarchy)
            {
                return thunder;
            }
        }

        // 動的拡張: プールが枯渇した場合はその場で新しく生成してリストに追加する
        ApothecaryThunder newThunder = Instantiate(
            thunderPrefab,
            transform.position,
            Quaternion.identity
        );
        newThunder.gameObject.SetActive(false);
        thunderPool.Add(newThunder);
        return newThunder;
    }

    /// <summary>
    /// 雷攻撃の一連の動作
    /// </summary>
    private IEnumerator PerformThunderAttack()
    {
        CurrentState = ApothecaryState.ThunderAttacking;

        float currentLookBackDuration = IsDebugNoWaitActive ? 0f : thunderLookBackDuration;
        float currentThrowDelayDuration = IsDebugNoWaitActive ? 0f : thunderThrowDelayDuration;

        // 1. 待機モーション (背を向ける)
        if (_animator != null)
            _animator.SetTrigger(_lookBackHash);
        if (currentLookBackDuration > 0f)
            yield return new WaitForSeconds(currentLookBackDuration);

        // 2. 発射モーション
        if (_animator != null)
            _animator.SetTrigger(_throwHash);

        if (potionSpriteRenderer != null && thunderPotionSprite != null)
        {
            potionSpriteRenderer.sprite = thunderPotionSprite;
            potionSpriteRenderer.gameObject.SetActive(true);
        }

        // 3. 配置座標の計算（リジェクション・サンプリングを用いた均等なランダム配置）
        int targetCount = Random.Range(thunderCountMin, thunderCountMax + 1);
        List<Vector2> spawnPositions = new List<Vector2>();
        int maxAttempts = 30; // 1つの座標を決定するための最大試行回数

        for (int i = 0; i < targetCount; i++)
        {
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // エリア内の空中も含めた完全ランダムな座標を生成
                float randX = Random.Range(areaLeftBound, areaRightBound);
                float randY = Random.Range(areaBottomBound, areaTopBound);
                Vector2 candidatePos = new Vector2(randX, randY);

                bool isTooClose = false;

                // 既存の配置座標と距離を比較し、近すぎるものがないかチェック
                foreach (Vector2 pos in spawnPositions)
                {
                    if (Vector2.Distance(candidatePos, pos) < thunderMinDistance)
                    {
                        isTooClose = true;
                        break;
                    }
                }

                // どの座標とも十分な距離が保たれていれば採用
                if (!isTooClose)
                {
                    spawnPositions.Add(candidatePos);
                    break;
                }
            }
        }

        // 4. 雷の生成と起動
        foreach (Vector2 pos in spawnPositions)
        {
            ApothecaryThunder thunder = GetThunderFromPool();
            if (thunder != null)
            {
                // この時点で位置を設定しアクティブ化。実際の時間差エフェクト等は雷自身(DOTween)が管理する
                thunder.transform.position = pos;
                thunder.gameObject.SetActive(true);
                thunder.Setup(
                    thunderDamage,
                    thunderWarningDuration,
                    thunderTransitionDuration,
                    thunderAttackDuration
                );
            }
        }

        // 5. モーション完了までの待機
        if (currentThrowDelayDuration > 0f)
            yield return new WaitForSeconds(currentThrowDelayDuration);

        // 6. 復帰
        if (potionSpriteRenderer != null)
        {
            potionSpriteRenderer.gameObject.SetActive(false);
        }

        if (_animator != null)
            _animator.SetTrigger(_idleHash);
        CurrentState = ApothecaryState.Idle;
    }

    /// <summary>
    /// プールから非アクティブな光レーザーを取得します
    /// </summary>
    private ApothecaryLightLaser GetLightLaserFromPool()
    {
        foreach (var laser in lightLaserPool)
        {
            if (laser != null && !laser.gameObject.activeInHierarchy)
            {
                return laser;
            }
        }

        ApothecaryLightLaser newLaser = Instantiate(
            lightLaserPrefab,
            transform.position,
            Quaternion.identity
        );
        newLaser.gameObject.SetActive(false);
        lightLaserPool.Add(newLaser);
        return newLaser;
    }

    /// <summary>
    /// 光攻撃の一連の動作
    /// </summary>
    private IEnumerator PerformLightAttack()
    {
        CurrentState = ApothecaryState.LightAttacking;

        float currentLiftHoldReadyDuration = IsDebugNoWaitActive ? 0f : lightLiftHoldReadyDuration;
        float currentChargeDuration = IsDebugNoWaitActive ? 0f : lightChargeDuration;

        // 1. 初めからLiftHoldアニメーションを行う
        if (_animator != null)
            _animator.SetTrigger(_liftHoldHash);

        if (potionSpriteRenderer != null && lightPotionSprite != null)
        {
            potionSpriteRenderer.sprite = lightPotionSprite;
            potionSpriteRenderer.gameObject.SetActive(true);
        }

        if (currentLiftHoldReadyDuration > 0f)
            yield return new WaitForSeconds(currentLiftHoldReadyDuration);

        // 向きに応じたオフセット位置の計算
        float offsetX = isFacingRight ? lightAttackOffset.x : -lightAttackOffset.x;
        Vector3 spawnPos = transform.position + new Vector3(offsetX, lightAttackOffset.y, 0f);

        // --- チャージフェーズ ---

        // 核となるエフェクトのフェードイン表示
        if (lightCoreSpriteRenderer != null)
        {
            lightCoreSpriteRenderer.transform.position = spawnPos;
            Color c = lightCoreSpriteRenderer.color;
            c.a = 0f;
            lightCoreSpriteRenderer.color = c;
            lightCoreSpriteRenderer.gameObject.SetActive(true);

            if (currentChargeDuration > 0f)
            {
                lightCoreSpriteRenderer.DOFade(1f, currentChargeDuration).SetEase(Ease.InOutQuad);
            }
            else
            {
                c.a = 1f;
                lightCoreSpriteRenderer.color = c;
            }
        }

        // ChargeEffect_Master の再生
        if (lightChargeEffect != null)
        {
            lightChargeEffect.transform.position = spawnPos;
            lightChargeEffect.SetDuration(currentChargeDuration > 0f ? currentChargeDuration : 1f);
            lightChargeEffect.PlayEffect();
        }

        // レーザーのプール準備
        float baseAngle = Random.Range(0f, 360f); // 0度を基準にした360度のランダムオフセット
        List<ApothecaryLightLaser> activeLasers = new List<ApothecaryLightLaser>();

        for (int i = 0; i < lightLaserCount; i++)
        {
            ApothecaryLightLaser laser = GetLightLaserFromPool();
            if (laser != null)
            {
                laser.gameObject.SetActive(true);
                laser.Setup(lightDamage);
                activeLasers.Add(laser);
            }
        }

        // 等確率（50%）で回転の向きを決定する (1f: 反時計回り / -1f: 時計回り)
        float rotationDirection = (Random.value < 0.5f) ? 1f : -1f;

        float timer = 0f;
        float currentRotation = 0f;

        // 予測線の回転描画
        while (timer < currentChargeDuration)
        {
            currentRotation += lightRotationSpeed * Time.deltaTime;

            // チャージの進捗度を 0.0 〜 1.0 の範囲で計算
            float progress = currentChargeDuration > 0f ? (timer / currentChargeDuration) : 1f;

            // カーブから太さの比率を取得し、現在の太さを計算
            float widthRatio = lightPredictionWidthCurve.Evaluate(progress);
            float currentWidth = Mathf.Lerp(
                lightPredictionStartWidth,
                lightPredictionEndWidth,
                widthRatio
            );

            // グラデーションから現在の色を取得
            Color currentColor = lightPredictionColorGradient.Evaluate(progress);

            for (int i = 0; i < activeLasers.Count; i++)
            {
                // 等間隔に配置 (360度 / 本数)
                float initialAngle = baseAngle + (360f / lightLaserCount) * i;
                float angle = initialAngle + (currentRotation * rotationDirection);

                // 現在の太さと色をUpdatePredictionLineへ渡す
                activeLasers[i]
                    .UpdatePredictionLine(
                        spawnPos,
                        angle,
                        lightLaserLength,
                        currentWidth,
                        currentColor
                    );
            }

            timer += Time.deltaTime;
            yield return null;
        }
        // --- 攻撃フェーズ ---

        if (lightChargeEffect != null)
        {
            lightChargeEffect.StopEffect();
        }

        // 予測線を消し、レーザー本体の展開（WidthとScaleYの操作）を開始
        foreach (var laser in activeLasers)
        {
            laser.Fire(lightLaserExpandDuration, lightLaserLength);
        }

        // 展開し終わるまで待機
        yield return new WaitForSeconds(lightLaserExpandDuration);

        // 完全に展開し終わった後にダメージ判定(BoxCollider2D)を有効にする
        foreach (var laser in activeLasers)
        {
            laser.EnableDamage();
        }

        // 攻撃持続時間の待機
        yield return new WaitForSeconds(lightAttackDuration);

        // --- 終了フェーズ ---

        if (lightCoreSpriteRenderer != null)
        {
            lightCoreSpriteRenderer
                .DOFade(0f, lightEndDuration)
                .OnComplete(() =>
                {
                    lightCoreSpriteRenderer.gameObject.SetActive(false);
                });
        }

        foreach (var laser in activeLasers)
        {
            laser.End(lightEndDuration);
        }

        // 消滅までの時間を待機して終了
        yield return new WaitForSeconds(lightEndDuration);

        if (potionSpriteRenderer != null)
        {
            potionSpriteRenderer.gameObject.SetActive(false);
        }

        if (_animator != null)
            _animator.SetTrigger(_idleHash);

        CurrentState = ApothecaryState.Idle;
    }

    private void OnDestroy()
    {
        // イベントの購読解除や、実行中のTween、コルーチンの停止などを記述します
    }

    private void OnDrawGizmosSelected()
    {
        // 1. エリア境界の表示（透過度のある色で塗りつぶし、境界線を強調）
        Vector3 center = new Vector3(
            (areaLeftBound + areaRightBound) / 2f,
            (areaTopBound + areaBottomBound) / 2f,
            0f
        );
        Vector3 size = new Vector3(
            areaRightBound - areaLeftBound,
            areaTopBound - areaBottomBound,
            0.1f // 完全に0にせず、少しだけ厚みを持たせて描画を安定させます
        );

        // 境界内の塗りつぶし（薄い緑）
        Gizmos.color = new Color(0f, 1f, 0f, 0.05f);
        Gizmos.DrawCube(center, size);

        // 境界の線（少し濃い緑）
        Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
        Gizmos.DrawWireCube(center, size);

        // 2. 通常攻撃（炎・風・氷）の発射点オフセット表示（右向きのみ）
        Gizmos.color = Color.red;
        Vector3 normalSpawnPos =
            transform.position + new Vector3(normalAttackOffset.x, normalAttackOffset.y, 0f);
        Gizmos.DrawWireSphere(normalSpawnPos, 0.2f);
        Gizmos.DrawLine(transform.position, normalSpawnPos); // ボス中心からの繋がりを視覚化
#if UNITY_EDITOR
        // 発射点にラベルを表示
        UnityEditor.Handles.Label(
            normalSpawnPos + new Vector3(0.3f, 0f, 0f),
            "Normal Attack Offset",
            new GUIStyle() { normal = new GUIStyleState() { textColor = Color.red } }
        );
#endif

        // 3. 光攻撃用（LiftHold）の発射点オフセット表示（右向きのみ）
        Gizmos.color = Color.yellow;
        Vector3 lightSpawnPos =
            transform.position + new Vector3(lightAttackOffset.x, lightAttackOffset.y, 0f);
        Gizmos.DrawWireSphere(lightSpawnPos, 0.25f); // 判別しやすいように少しだけ球体を大きく
        Gizmos.DrawLine(transform.position, lightSpawnPos); // ボス中心からの繋がりを視覚化
#if UNITY_EDITOR
        // 光攻撃の発射点にラベルを表示
        UnityEditor.Handles.Label(
            lightSpawnPos + new Vector3(0.3f, 0.3f, 0f),
            "Light Attack Offset (LiftHold)",
            new GUIStyle() { normal = new GUIStyleState() { textColor = Color.yellow } }
        );
#endif
    }
}

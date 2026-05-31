using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// ボスキャラクター「Apothecary」の移動、各種攻撃パターンの実行、
/// テレポート回避、およびオブジェクトプールを統合管理するコントローラークラスです。
/// </summary>
public class ApothecaryMoveController : MonoBehaviour
{
    #region --- Enum・プロパティ ---

    /// <summary>
    /// ボスの現在の状態を表す列挙型
    /// </summary>
    public enum ApothecaryState
    {
        Intro, // 登場演出中
        Idle, // 待機中（次の行動待ち）
        Attacking, // 汎用的な攻撃状態
        FireAttacking, // 炎攻撃状態
        WindAttacking, // 風攻撃状態
        IceAttacking, // 氷攻撃状態
        ThunderAttacking, // 雷攻撃状態
        LightAttacking, // 光攻撃状態
        Teleporting, // ダメージ蓄積による瞬間移動状態
    }

    public ApothecaryState CurrentState { get; private set; } = ApothecaryState.Intro;

    #endregion --- Enum・プロパティ ---


    #region --- インスペクター設定（基本・デバッグ） ---

    [Header("デバッグ機能")]
    [Tooltip(
        "trueの場合、各種待機時間や演出のディレイを0にしてデバッグを容易にします（エディタ上のみ有効）"
    )]
    [SerializeField]
    private bool isDebugNoWait = false;

    /// <summary>
    /// エディタ上でのみisDebugNoWaitの値を返し、ビルド後の実機環境では常にfalseを返すプロパティ
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

    [Header("エリア境界の設定 (消滅・取得判定用)")]
    [SerializeField]
    private float areaLeftBound = -10f;

    [SerializeField]
    private float areaRightBound = 10f;

    [SerializeField]
    private float areaBottomBound = -5f;

    [SerializeField]
    private float areaTopBound = 10f;

    [Header("共通攻撃設定")]
    [Tooltip("弾の発射位置オフセット(右向き時)")]
    [SerializeField]
    private Vector2 normalAttackOffset = new Vector2(1.0f, 1.0f);

    #endregion --- インスペクター設定（基本・デバッグ） ---


    #region --- インスペクター設定（各種攻撃・演出） ---

    [Header("ポーション演出の設定")]
    [Tooltip("ポーションを表示する子オブジェクトのSpriteRenderer")]
    [SerializeField]
    private SpriteRenderer potionSpriteRenderer;

    [SerializeField, Tooltip("炎攻撃時に持たせるポーションのスプライト")]
    private Sprite firePotionSprite;

    [SerializeField, Tooltip("風攻撃時に持たせるポーションのスプライト")]
    private Sprite windPotionSprite;

    [SerializeField, Tooltip("氷攻撃時に持たせるポーションのスプライト")]
    private Sprite icePotionSprite;

    [SerializeField, Tooltip("雷攻撃時に持たせるポーションのスプライト")]
    private Sprite thunderPotionSprite;

    [SerializeField, Tooltip("光攻撃時に持たせるポーションのスプライト")]
    private Sprite lightPotionSprite;

    [Header("炎攻撃の設定")]
    [SerializeField, Tooltip("ThrowReadyアニメーションでの待機時間")]
    private float fireThrowReadyDuration = 1.0f;

    [SerializeField, Tooltip("Throwアニメーション開始から発射までの遅延時間")]
    private float fireThrowDelayDuration = 0.5f;

    [SerializeField, Tooltip("炎弾の最小発射角度(度)")]
    private float fireAngleMin = 45f;

    [SerializeField, Tooltip("炎弾の最大発射角度(度)")]
    private float fireAngleMax = 75f;

    [SerializeField, Tooltip("炎弾の連続発射間隔")]
    private float fireInterval = 0.2f;

    [SerializeField, Tooltip("炎弾の最小発射回数")]
    private int fireCountMin = 3;

    [SerializeField, Tooltip("炎弾の最大発射回数")]
    private int fireCountMax = 6;

    [SerializeField, Tooltip("炎弾の初速")]
    private float fireBulletSpeed = 10f;

    [SerializeField, Tooltip("炎弾(空中)のダメージ")]
    private int fireAirDamage = 10;

    [SerializeField, Tooltip("炎上(着地後)のダメージ")]
    private int fireGroundDamage = 15;

    [SerializeField, Tooltip("着地後の炎上継続時間")]
    private float fireGroundDuration = 3.0f;

    [SerializeField, Tooltip("炎弾の最初の炎上確率(0.0〜1.0)")]
    private float fireInitialBurnProbability = 0.3f;

    [SerializeField, Tooltip("炎弾の限界貫通(すり抜け)数。この回数に達したら次は100%炎上する")]
    private int fireMaxPierceCount = 3;

    [Header("風攻撃の設定")]
    [SerializeField, Tooltip("ThrowReadyアニメーションでの待機時間")]
    private float windThrowReadyDuration = 1.2f;

    [SerializeField, Tooltip("Throwアニメーション開始から発射までの遅延時間")]
    private float windThrowDelayDuration = 0.4f;

    [SerializeField, Tooltip("風弾の最小発射回数")]
    private int windCountMin = 2;

    [SerializeField, Tooltip("風弾の最大発射回数")]
    private int windCountMax = 5;

    [SerializeField, Tooltip("風弾の連続発射間隔")]
    private float windInterval = 0.3f;

    [SerializeField, Tooltip("プレイヤーを狙う際の角度のブレ幅(±度)")]
    private float windAngleVariance = 15f;

    [SerializeField, Tooltip("風弾の初速")]
    private float windBulletSpeed = 12f;

    [SerializeField, Tooltip("風弾のダメージ")]
    private int windDamage = 10;

    [SerializeField, Tooltip("風弾ヒット時のノックバック力")]
    private float windKnockbackForce = 15f;

    [SerializeField, Tooltip("範囲外に出てから消滅するまでの時間(秒)")]
    private float windDisappearDelay = 2.0f;

    [Header("氷攻撃の設定")]
    [SerializeField, Tooltip("LookBackアニメーションでの待機時間")]
    private float iceLookBackDuration = 1.0f;

    [SerializeField, Tooltip("1つの足場あたりにぶら下げるつららの最小数")]
    private int iceCountMin = 1;

    [SerializeField, Tooltip("1つの足場あたりにぶら下げるつららの最大数")]
    private int iceCountMax = 3;

    [SerializeField, Tooltip("円軌道（波紋）が広がる速度")]
    private float iceRippleSpeed = 15f;

    [Header("雷攻撃の設定")]
    [SerializeField, Tooltip("LookBackアニメーションでの待機時間")]
    private float thunderLookBackDuration = 1.0f;

    [SerializeField, Tooltip("Throwアニメーション開始から攻撃完了扱いにするまでの時間")]
    private float thunderThrowDelayDuration = 0.5f;

    [SerializeField, Tooltip("雷の最小発生数")]
    private int thunderCountMin = 5;

    [SerializeField, Tooltip("雷の最大発生数")]
    private int thunderCountMax = 10;

    [SerializeField, Tooltip("雷同士の最小距離（近すぎないようにするための距離）")]
    private float thunderMinDistance = 2.0f;

    [SerializeField, Tooltip("雷のダメージ")]
    private int thunderDamage = 20;

    [SerializeField, Tooltip("予兆エフェクトの表示時間")]
    private float thunderWarningDuration = 1.0f;

    [SerializeField, Tooltip("予兆から本体への移行（スケール/フェード）にかかる時間")]
    private float thunderTransitionDuration = 0.3f;

    [SerializeField, Tooltip("落雷（ダメージ発生）の継続時間")]
    private float thunderAttackDuration = 0.5f;

    [Header("光攻撃の設定")]
    [SerializeField, Tooltip("LiftHoldのオフセット位置(右向き時)")]
    private Vector2 lightAttackOffset = new Vector2(1.5f, 2.0f);

    [SerializeField, Tooltip("LiftHoldアニメーション開始後のチャージ前待機時間")]
    private float lightLiftHoldReadyDuration = 1.0f;

    [SerializeField, Tooltip("光攻撃のチャージ（予測線回転）時間")]
    private float lightChargeDuration = 2.0f;

    [SerializeField, Tooltip("レーザーの展開（WidthとScaleYの拡大）にかかる時間")]
    private float lightLaserExpandDuration = 0.2f;

    [SerializeField, Tooltip("レーザーの攻撃持続時間")]
    private float lightAttackDuration = 2.0f;

    [SerializeField, Tooltip("攻撃終了時のフェード・縮小消滅にかかる時間")]
    private float lightEndDuration = 0.5f;

    [SerializeField, Tooltip("レーザーの発射本数")]
    private int lightLaserCount = 4;

    [SerializeField, Tooltip("地形を貫通するレーザーと予測線の固定長さ")]
    private float lightLaserLength = 30f;

    [SerializeField, Tooltip("予測線の回転速度（度/秒）")]
    private float lightRotationSpeed = 45f;

    [SerializeField, Tooltip("光レーザーのダメージ")]
    private int lightDamage = 30;

    [Header("光攻撃の予測線演出設定")]
    [SerializeField, Tooltip("予測線のチャージ開始時の太さ")]
    private float lightPredictionStartWidth = 0.05f;

    [SerializeField, Tooltip("予測線のチャージ完了直前の太さ")]
    private float lightPredictionEndWidth = 0.3f;

    [
        SerializeField,
        Tooltip("チャージ時間(0〜1)に応じた予測線の太さの変化率（Linearやイージングを設定）")
    ]
    private AnimationCurve lightPredictionWidthCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [
        SerializeField,
        Tooltip("予測線のカラーグラデーション（左側がチャージ開始時、右側がチャージ完了時）")
    ]
    private Gradient lightPredictionColorGradient;

    [Header("光攻撃の参照・プール設定")]
    [SerializeField, Tooltip("チャージ中に表示される核となるエフェクト(SpriteRendererなど)")]
    private SpriteRenderer lightCoreSpriteRenderer;

    [SerializeField, Tooltip("ボスのプレハブの子オブジェクトとして配置されたChargeEffect_Master")]
    private ChargeEffect_Master lightChargeEffect;

    [Header("テレポート設定")]
    [SerializeField, Tooltip("ダメージを受けてからテレポートするまでの待機時間（秒）")]
    private float teleportTriggerTime = 2.0f;

    [SerializeField, Tooltip("テレポートを発動する最大HPの蓄積ダメージ割合 (例: 0.1 = 10%)")]
    private float teleportDamageRatio = 0.1f;

    [SerializeField, Tooltip("テレポート開始時のダッシュ・フェードアウトにかかる時間")]
    private float teleportOutDuration = 0.5f;

    [SerializeField, Tooltip("魔法陣の展開と本体のフェードインにかかる時間")]
    private float teleportInDuration = 0.5f;

    [SerializeField, Tooltip("テレポートの移動先として選ばれる座標のリスト")]
    private List<Vector2> teleportPoints = new List<Vector2>();

    [SerializeField, Tooltip("テレポート出現時に使用する魔法陣コントローラー（子オブジェクト）")]
    private MagicCircleController magicCircleController;

    [Header("オブジェクトプール用のプレハブ参照")]
    [SerializeField]
    private ApothecaryFireBullet fireBulletPrefab;

    [SerializeField]
    private ApothecaryWindBullet windBulletPrefab;

    [SerializeField]
    private IcicleMoveController iciclePrefab;

    [SerializeField]
    private ApothecaryThunder thunderPrefab;

    [SerializeField]
    private ApothecaryLightLaser lightLaserPrefab;

    #endregion --- インスペクター設定（各種攻撃・演出） ---


    #region --- 定数・内部変数 ---

    // 初期生成する弾の数をconst変数として定義
    private const int INITIAL_FIRE_POOL_SIZE = 10;
    private const int INITIAL_WIND_POOL_SIZE = 10;
    private const int INITIAL_ICICLE_POOL_SIZE = 15;
    private const int INITIAL_THUNDER_POOL_SIZE = 15;
    private const int INITIAL_LIGHT_POOL_SIZE = 8;

    // 専用のオブジェクトプールを保持するリスト
    private List<ApothecaryFireBullet> fireBulletPool = new List<ApothecaryFireBullet>();
    private List<ApothecaryWindBullet> windBulletPool = new List<ApothecaryWindBullet>();
    private List<IcicleMoveController> iciclePool = new List<IcicleMoveController>();
    private List<ApothecaryThunder> thunderPool = new List<ApothecaryThunder>();
    private List<ApothecaryLightLaser> lightLaserPool = new List<ApothecaryLightLaser>();

    // シーン上の情報をキャッシュするリスト
    private List<Collider2D> _icePlatforms = new List<Collider2D>(); // 取得した薄い足場のコライダー保持用
    private List<IcicleMoveController> _reservedIcicles = new List<IcicleMoveController>(); // 発射予約済みのつらら保持用

    // コンポーネントおよび状態キャッシュ
    private Coroutine attackLoopCoroutine;
    private Animator _animator;
    private Transform _playerTransform;
    private SpriteRenderer _bodySpriteRenderer;
    private bool isFacingRight = true;

    // テレポート（ダメージ監視）用の内部変数
    private CharacterHealth _characterHealth;
    private int _accumulatedDamage = 0;
    private float _timeSinceLastDamage = 0f;
    private bool _hasAccumulatedDamage = false;

    // Animatorのパラメータを事前ハッシュ化して処理を高速化
    private readonly int _throwReadyHash = Animator.StringToHash("ThrowReadyTrigger");
    private readonly int _throwHash = Animator.StringToHash("ThrowTrigger");
    private readonly int _handOnHipHash = Animator.StringToHash("HandOnHipTrigger");
    private readonly int _lookBackHash = Animator.StringToHash("LookBackTrigger");
    private readonly int _liftHoldHash = Animator.StringToHash("LiftHoldTrigger");
    private readonly int _backDashHash = Animator.StringToHash("BackDashTrigger");

    #endregion --- 定数・内部変数 ---


    #region --- Unityライフサイクル ---

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _bodySpriteRenderer = GetComponent<SpriteRenderer>();
        _characterHealth = GetComponent<CharacterHealth>();

        // 起動時は演出用オブジェクトを非表示にしておく
        if (lightChargeEffect != null)
            lightChargeEffect.StopEffect();
        if (lightCoreSpriteRenderer != null)
            lightCoreSpriteRenderer.gameObject.SetActive(false);
        if (potionSpriteRenderer != null)
            potionSpriteRenderer.gameObject.SetActive(false);
    }

    private void Start()
    {
        // シーン開始時に自動でボスの状態をリセットし起動
        ResetState();
    }

    private void OnEnable()
    {
        // 自身がダメージを受けた際のイベントを購読
        if (_characterHealth != null)
        {
            _characterHealth.OnDamageTaken += HandleDamageTaken;
        }
    }

    private void OnDisable()
    {
        // オブジェクト非アクティブ時にイベント購読を解除
        if (_characterHealth != null)
        {
            _characterHealth.OnDamageTaken -= HandleDamageTaken;
        }
    }

    private void OnDestroy()
    {
        // 破棄時、実行中のTweenなどがあればキルする処理を記述します
    }

    private void FixedUpdate()
    {
        // --- テレポートの監視ロジック ---
        // ダメージが蓄積しており、かつボスが攻撃をしていない(Idle)場合に監視タイマーを進める
        if (_hasAccumulatedDamage && CurrentState == ApothecaryState.Idle)
        {
            _timeSinceLastDamage += Time.fixedDeltaTime;

            float damageThreshold = _characterHealth.MaxHP * teleportDamageRatio;

            // 「最後の被弾から指定秒数経過したか」または「蓄積ダメージが指定割合を超えたか」
            if (
                _timeSinceLastDamage >= teleportTriggerTime
                || _accumulatedDamage >= damageThreshold
            )
            {
                // テレポートが発動する際、裏で動いている攻撃ループを完全に停止させる（アニメーションの競合を防ぐ）
                if (attackLoopCoroutine != null)
                {
                    StopCoroutine(attackLoopCoroutine);
                    attackLoopCoroutine = null;
                }

                StartCoroutine(PerformTeleport());
            }
        }

        // Idle状態のときのみ、プレイヤーの方向へ自動で向きを更新します
        // 攻撃状態（FireAttackingなど）のときはこの処理がスキップされ、攻撃時の向きが固定されます
        if (CurrentState == ApothecaryState.Idle)
        {
            UpdateFacingDirection();
        }
    }

    #endregion --- Unityライフサイクル ---


    #region --- 初期化・状態管理 ---

    /// <summary>
    /// ボスの状態をリセットし、プールや周辺環境を取得した上で初期行動を開始します
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

        // 各種オブジェクトプールの生成と初期化
        InitializePools();

        // 実行中の行動やエフェクトを強制リセット
        if (attackLoopCoroutine != null)
            StopCoroutine(attackLoopCoroutine);
        if (lightChargeEffect != null)
            lightChargeEffect.StopEffect();
        if (lightCoreSpriteRenderer != null)
            lightCoreSpriteRenderer.gameObject.SetActive(false);
        if (potionSpriteRenderer != null)
            potionSpriteRenderer.gameObject.SetActive(false);
        if (magicCircleController != null)
            magicCircleController.ChangeScaleXY(Vector2.zero, 0f);

        // 氷攻撃（つらら生成）用に、エリア内にある薄い足場（OBJECT_GROUND）を取得・保持する
        Vector2 pointA = new Vector2(areaLeftBound, areaTopBound);
        Vector2 pointB = new Vector2(areaRightBound, areaBottomBound);
        Collider2D[] colliders = Physics2D.OverlapAreaAll(
            pointA,
            pointB,
            LayerMask.GetMask(GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND)
        );
        _icePlatforms.Clear();
        _icePlatforms.AddRange(colliders);

        // 登場シーケンスの開始
        StartCoroutine(IntroSequence());
    }

    /// <summary>
    /// ダメージを受け取って蓄積するイベントハンドラー
    /// </summary>
    private void HandleDamageTaken(int damage)
    {
        _accumulatedDamage += damage;
        _timeSinceLastDamage = 0f; // ダメージを受けるたびにタイマーをリセット
        _hasAccumulatedDamage = true;
    }

    /// <summary>
    /// プレイヤーの位置に応じて、ボス本体のスプライトの向き（左右反転）を自動的に更新します
    /// </summary>
    private void UpdateFacingDirection()
    {
        if (_playerTransform == null)
            return;

        // プレイヤーが自身の右側にいるかどうかでフラグを更新
        isFacingRight = _playerTransform.position.x > transform.position.x;

        this.transform.localScale = new Vector3(
            isFacingRight ? 1 : -1,
            transform.localScale.y,
            transform.localScale.z
        );
    }

    #endregion --- 初期化・状態管理 ---


    #region --- メイン行動ループ ---

    /// <summary>
    /// 登場時のシーケンス。モーション完了後にメインの攻撃ループへ移行します。
    /// </summary>
    private IEnumerator IntroSequence()
    {
        CurrentState = ApothecaryState.Intro;

        // 登場時の移動やアニメーションの待機処理などをここに記述します
        yield return null;

        // 登場完了後、攻撃ループを開始
        StartAttackLoop();
    }

    /// <summary>
    /// 攻撃ループのコルーチンを安全に開始します。
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
    /// 攻撃方法の抽選と待機を繰り返すメインシーケンス。
    /// </summary>
    private IEnumerator AttackLoopSequence()
    {
        while (true)
        {
            // 1. 攻撃方法の選択（現状はテストのため、光攻撃を例に記述）
            // 実際はランダム抽選等で PerformFireAttack() や PerformIceAttack() 等に分岐させます
            yield return StartCoroutine(PerformLightAttack());

            // 2. 待機状態への移行
            CurrentState = ApothecaryState.Idle;

            // 3. 次の攻撃までのインターバル待機
            float waitTime = IsDebugNoWaitActive ? 0f : 3.0f;
            yield return new WaitForSeconds(waitTime);
        }
    }

    #endregion --- メイン行動ループ ---


    #region --- オブジェクトプール管理 ---

    /// <summary>
    /// 弾やエフェクトをあらかじめ生成し、非アクティブ状態で保持しておくプールを初期化します。
    /// これにより、戦闘中のInstantiate/Destroyによる負荷（スパイク）を防ぎます。
    /// </summary>
    private void InitializePools()
    {
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
            Debug.LogWarning("FireBulletPrefabが設定されていません。");

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
            Debug.LogWarning("WindBulletPrefabが設定されていません。");

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
            Debug.LogWarning("IciclePrefabが設定されていません。");

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
            Debug.LogWarning("ThunderPrefabが設定されていません。");

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
            Debug.LogWarning("LightLaserPrefabが設定されていません。");
    }

    /// <summary>
    /// プールから非アクティブな炎弾を取得します。
    /// もしプール内の弾が全て使用中（枯渇状態）だった場合は、新たに生成してリストを拡張します。
    /// </summary>
    private ApothecaryFireBullet GetFireBulletFromPool()
    {
        foreach (var bullet in fireBulletPool)
        {
            if (bullet != null && !bullet.gameObject.activeInHierarchy)
                return bullet;
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

    /// <summary> プールから非アクティブな風弾を取得します </summary>
    private ApothecaryWindBullet GetWindBulletFromPool()
    {
        foreach (var bullet in windBulletPool)
        {
            if (bullet != null && !bullet.gameObject.activeInHierarchy)
                return bullet;
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
    /// プールから、まだこのターンで「予約」されていない非アクティブな氷弾(つらら)を取得します
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

    /// <summary> プールから非アクティブな雷を取得します </summary>
    private ApothecaryThunder GetThunderFromPool()
    {
        foreach (var thunder in thunderPool)
        {
            if (thunder != null && !thunder.gameObject.activeInHierarchy)
                return thunder;
        }

        ApothecaryThunder newThunder = Instantiate(
            thunderPrefab,
            transform.position,
            Quaternion.identity
        );
        newThunder.gameObject.SetActive(false);
        thunderPool.Add(newThunder);
        return newThunder;
    }

    /// <summary> プールから非アクティブな光レーザーを取得します </summary>
    private ApothecaryLightLaser GetLightLaserFromPool()
    {
        foreach (var laser in lightLaserPool)
        {
            if (laser != null && !laser.gameObject.activeInHierarchy)
                return laser;
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

    #endregion --- オブジェクトプール管理 ---


    #region --- 各種攻撃アクション（コルーチン） ---

    /// <summary>
    /// 炎攻撃の一連の動作。ポーションを構え、確率ですり抜けるバウンド炎弾を連射します。
    /// </summary>
    private IEnumerator PerformFireAttack()
    {
        CurrentState = ApothecaryState.FireAttacking;

        float currentThrowReadyDuration = IsDebugNoWaitActive ? 0f : fireThrowReadyDuration;
        float currentThrowDelayDuration = IsDebugNoWaitActive ? 0f : fireThrowDelayDuration;
        float currentFireInterval = IsDebugNoWaitActive ? 0f : fireInterval;

        // 1. 待機モーションとポーション表示
        if (_animator != null)
            _animator.SetTrigger(_throwReadyHash);
        if (currentThrowReadyDuration > 0f)
            yield return new WaitForSeconds(currentThrowReadyDuration);

        // 2. 攻撃モーション開始
        if (potionSpriteRenderer != null && firePotionSprite != null)
        {
            potionSpriteRenderer.sprite = firePotionSprite;
            potionSpriteRenderer.gameObject.SetActive(true);
        }

        if (_animator != null)
            _animator.SetTrigger(_throwHash);
        if (currentThrowDelayDuration > 0f)
            yield return new WaitForSeconds(currentThrowDelayDuration);

        // 3. 連続発射ループ
        int currentFireCount = Random.Range(fireCountMin, fireCountMax + 1);
        for (int i = 0; i < currentFireCount; i++)
        {
            float offsetX = isFacingRight ? normalAttackOffset.x : -normalAttackOffset.x;
            Vector3 spawnPos = transform.position + new Vector3(offsetX, normalAttackOffset.y, 0f);

            // 発射角度を計算（右向きならそのまま、左向きなら180度反転）
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

                // 発射パラメーターと貫通（すり抜け）確率を弾に渡してセットアップ
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

        // 4. 攻撃終了・復帰処理
        if (potionSpriteRenderer != null)
            potionSpriteRenderer.gameObject.SetActive(false);
        if (_animator != null)
            _animator.SetTrigger(_handOnHipHash);

        CurrentState = ApothecaryState.Idle;
    }

    /// <summary>
    /// 風攻撃の一連の動作。プレイヤーを狙ってブレのある直進弾を発射します。
    /// </summary>
    private IEnumerator PerformWindAttack()
    {
        CurrentState = ApothecaryState.WindAttacking;

        float currentThrowReadyDuration = IsDebugNoWaitActive ? 0f : windThrowReadyDuration;
        float currentThrowDelayDuration = IsDebugNoWaitActive ? 0f : windThrowDelayDuration;
        float currentWindInterval = IsDebugNoWaitActive ? 0f : windInterval;

        if (_animator != null)
            _animator.SetTrigger(_throwReadyHash);
        if (currentThrowReadyDuration > 0f)
            yield return new WaitForSeconds(currentThrowReadyDuration);

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
        for (int i = 0; i < currentWindCount; i++)
        {
            float offsetX = isFacingRight ? normalAttackOffset.x : -normalAttackOffset.x;
            Vector3 spawnPos = transform.position + new Vector3(offsetX, normalAttackOffset.y, 0f);

            // ターゲット（プレイヤー）への方向を計算し、そこにランダムなブレ角度を加算する
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

        if (potionSpriteRenderer != null)
            potionSpriteRenderer.gameObject.SetActive(false);
        if (_animator != null)
            _animator.SetTrigger(_handOnHipHash);

        CurrentState = ApothecaryState.Idle;
    }

    /// <summary>
    /// 氷攻撃の一連の動作。フィールド上の薄い足場を検知し、そこからつららを時間差で落下させます。
    /// </summary>
    private IEnumerator PerformIceAttack()
    {
        CurrentState = ApothecaryState.IceAttacking;

        float currentLookBackDuration = IsDebugNoWaitActive ? 1f : iceLookBackDuration;

        // 1. 以前の攻撃で残っているつららを全て消去する（直接Destroyせず破壊演出を呼ぶ）
        _reservedIcicles.Clear();
        foreach (var icicle in iciclePool)
        {
            if (icicle != null && icicle.gameObject.activeInHierarchy)
            {
                icicle.ForceCrash();
            }
        }

        if (_animator != null)
            _animator.SetTrigger(_lookBackHash);
        if (currentLookBackDuration > 0f)
            yield return new WaitForSeconds(currentLookBackDuration);

        if (_animator != null)
            _animator.SetTrigger(_throwHash);
        if (potionSpriteRenderer != null && icePotionSprite != null)
        {
            potionSpriteRenderer.sprite = icePotionSprite;
            potionSpriteRenderer.gameObject.SetActive(true);
        }

        float offsetX = isFacingRight ? normalAttackOffset.x : -normalAttackOffset.x;
        Vector3 spawnPos = transform.position + new Vector3(offsetX, normalAttackOffset.y, 0f);

        // 2. 各足場につららを配置する計算と、遅延出現の登録
        float maxDelay = 0f; // 最も長いディレイ時間を記録するための変数を初期化

        foreach (var platform in _icePlatforms)
        {
            if (platform == null)
                continue;

            // 足場の幅を取得し、均等につららを配置するためのステップ幅を計算
            float minX = platform.bounds.min.x;
            float maxX = platform.bounds.max.x;
            float bottomY = platform.bounds.min.y;

            int icicleCount = Random.Range(iceCountMin, iceCountMax + 1);
            float stepX = (maxX - minX) / (icicleCount + 1);

            for (int i = 1; i <= icicleCount; i++)
            {
                float targetX = minX + (stepX * i);
                Vector3 targetPos = new Vector3(targetX, bottomY, 0f);

                // 発射地点（ボス）からの距離に応じて、出現までのディレイ（波紋が届く時間）を計算する
                float distance = Vector3.Distance(spawnPos, targetPos);
                float delay = distance / iceRippleSpeed;

                // 今回計算されたディレイが、これまでの最大値より大きければ上書きする
                if (delay > maxDelay)
                {
                    maxDelay = delay;
                }

                IcicleMoveController icicle = GetUnreservedIcicleFromPool();
                if (icicle != null)
                {
                    _reservedIcicles.Add(icicle);
                    // 指定したディレイ後に生成処理を走らせるコルーチンを登録
                    StartCoroutine(SpawnIcicleWithDelay(icicle, targetPos, delay));
                }
            }
        }

        // 3. モーション完了までの待機
        // インスペクターの固定値ではなく、ループ内で計算された「最も遠いつららが生成されるまでの時間」を待機時間として使用する
        float currentThrowDelayDuration = IsDebugNoWaitActive ? 1f : maxDelay;
        if (currentThrowDelayDuration > 0f)
            yield return new WaitForSeconds(currentThrowDelayDuration);

        // 4. 攻撃終了・復帰処理
        if (potionSpriteRenderer != null)
            potionSpriteRenderer.gameObject.SetActive(false);
        if (_animator != null)
            _animator.SetTrigger(_handOnHipHash);

        CurrentState = ApothecaryState.Idle;
    }

    /// <summary>
    /// 氷攻撃用：指定された時間遅延させてから、つららを配置・出現させます
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
            // 出現する直前に予約リストから解除し、他の処理が安全にプールから取得できるようにする
            if (_reservedIcicles.Contains(icicle))
                _reservedIcicles.Remove(icicle);

            // 正しい座標に配置した上で、内部で SetActive(true) やエフェクト再生を行う
            icicle.SpawnAsBossSummon(position);
        }
    }

    /// <summary>
    /// 雷攻撃の一連の動作。リジェクション・サンプリングを用いて、雷同士が近すぎないように配置します。
    /// </summary>
    private IEnumerator PerformThunderAttack()
    {
        CurrentState = ApothecaryState.ThunderAttacking;

        float currentLookBackDuration = IsDebugNoWaitActive ? 0f : thunderLookBackDuration;
        float currentThrowDelayDuration = IsDebugNoWaitActive ? 0f : thunderThrowDelayDuration;

        if (_animator != null)
            _animator.SetTrigger(_lookBackHash);
        if (currentLookBackDuration > 0f)
            yield return new WaitForSeconds(currentLookBackDuration);

        if (_animator != null)
            _animator.SetTrigger(_throwHash);
        if (potionSpriteRenderer != null && thunderPotionSprite != null)
        {
            potionSpriteRenderer.sprite = thunderPotionSprite;
            potionSpriteRenderer.gameObject.SetActive(true);
        }

        // --- 配置座標の計算（リジェクション・サンプリングを用いた均等なランダム配置） ---
        // 単純なRandomでは雷が同じ場所に固まってしまう可能性があるため、
        // 「新しく生成した座標が、既に決まった座標に近すぎないか」をテストし、合格したものだけを採用します。
        int targetCount = Random.Range(thunderCountMin, thunderCountMax + 1);
        List<Vector2> spawnPositions = new List<Vector2>();
        int maxAttempts = 30; // 無限ループを防ぐため、1つの座標を決定するための最大試行回数を設定

        for (int i = 0; i < targetCount; i++)
        {
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // エリア内の空中も含めた完全ランダムな座標を仮生成
                float randX = Random.Range(areaLeftBound, areaRightBound);
                float randY = Random.Range(areaBottomBound, areaTopBound);
                Vector2 candidatePos = new Vector2(randX, randY);

                bool isTooClose = false;

                // 既存の採用済み座標との距離を比較
                foreach (Vector2 pos in spawnPositions)
                {
                    if (Vector2.Distance(candidatePos, pos) < thunderMinDistance)
                    {
                        isTooClose = true;
                        break;
                    }
                }

                // どの座標とも十分な距離（thunderMinDistance以上）が保たれていれば採用
                if (!isTooClose)
                {
                    spawnPositions.Add(candidatePos);
                    break;
                }
            }
        }

        // 雷の生成と起動
        foreach (Vector2 pos in spawnPositions)
        {
            ApothecaryThunder thunder = GetThunderFromPool();
            if (thunder != null)
            {
                // この時点で位置を設定しアクティブ化。実際の時間差エフェクト等は雷自身(ApothecaryThunder)が管理する
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

        if (currentThrowDelayDuration > 0f)
            yield return new WaitForSeconds(currentThrowDelayDuration);

        if (potionSpriteRenderer != null)
            potionSpriteRenderer.gameObject.SetActive(false);
        if (_animator != null)
            _animator.SetTrigger(_handOnHipHash);

        CurrentState = ApothecaryState.Idle;
    }

    /// <summary>
    /// 光攻撃の一連の動作。予測線を回転させながらチャージし、極太のレーザーを放ちます。
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
                lightCoreSpriteRenderer.DOFade(1f, currentChargeDuration).SetEase(Ease.InOutQuad);
            else
            {
                c.a = 1f;
                lightCoreSpriteRenderer.color = c;
            }
        }

        // ChargeEffect_Master によるパーティクルとSEの再生
        if (lightChargeEffect != null)
        {
            lightChargeEffect.transform.position = spawnPos;
            lightChargeEffect.SetDuration(currentChargeDuration > 0f ? currentChargeDuration : 1f);
            lightChargeEffect.PlayEffect();
        }

        // レーザーのプール準備
        float baseAngle = Random.Range(0f, 360f); // 0度を基準にした360度のランダムな開始角度
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

        // 等確率（50%）で予測線が回る方向を決定する (1f: 反時計回り / -1f: 時計回り)
        float rotationDirection = (Random.value < 0.5f) ? 1f : -1f;
        float timer = 0f;
        float currentRotation = 0f;

        // 予測線の回転と太さ・色の変化描画ループ
        while (timer < currentChargeDuration)
        {
            currentRotation += lightRotationSpeed * Time.deltaTime;

            // チャージの進捗度を 0.0 〜 1.0 の範囲で計算
            float progress = currentChargeDuration > 0f ? (timer / currentChargeDuration) : 1f;

            // アニメーションカーブ(AnimationCurve)から太さの変化比率を取得し、現在の太さをLerpで計算
            float widthRatio = lightPredictionWidthCurve.Evaluate(progress);
            float currentWidth = Mathf.Lerp(
                lightPredictionStartWidth,
                lightPredictionEndWidth,
                widthRatio
            );

            // インスペクターで設定されたグラデーションから、現在の進捗度に応じた色を取得
            Color currentColor = lightPredictionColorGradient.Evaluate(progress);

            for (int i = 0; i < activeLasers.Count; i++)
            {
                // 全てのレーザーを均等な角度に配置し、決定した方向へ回転させる
                float initialAngle = baseAngle + (360f / lightLaserCount) * i;
                float angle = initialAngle + (currentRotation * rotationDirection);

                // 現在の太さと色をレーザーオブジェクト側へ渡して描画を更新
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

        // --- 攻撃（発射）フェーズ ---
        if (lightChargeEffect != null)
            lightChargeEffect.StopEffect();

        // 予測線を消し、レーザー本体の展開（WidthとScaleYの操作）を開始
        foreach (var laser in activeLasers)
        {
            laser.Fire(lightLaserExpandDuration, lightLaserLength);
        }

        // 完全に展開し終わるまで待機
        yield return new WaitForSeconds(lightLaserExpandDuration);

        // 完全に展開し終わった後に、レーザー本体のダメージ判定(BoxCollider2D)を有効にする
        foreach (var laser in activeLasers)
        {
            laser.EnableDamage();
        }

        // 攻撃持続時間の待機
        yield return new WaitForSeconds(lightAttackDuration);

        // --- 終了（消滅）フェーズ ---
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
            potionSpriteRenderer.gameObject.SetActive(false);
        if (_animator != null)
            _animator.SetTrigger(_handOnHipHash);

        CurrentState = ApothecaryState.Idle;
    }

    #endregion --- 各種攻撃アクション（コルーチン） ---


    #region --- 回避アクション (テレポート) ---

    private IEnumerator PerformTeleport()
    {
        CurrentState = ApothecaryState.Teleporting;

        // 蓄積情報をリセット
        _accumulatedDamage = 0;
        _hasAccumulatedDamage = false;
        _timeSinceLastDamage = 0f;

        // 攻撃を受け付けないように無敵化（タグの変更）
        this.gameObject.tag = GameConstants.UNTAGGED_TAG_NAME;

        // アニメーションをBackDashに切り替え
        if (_animator != null)
            _animator.SetTrigger(_backDashHash);

        // --- 1. テレポート開始（消滅） ---
        // 向いている方向と反対へ少し下がる位置を計算
        float moveDir = isFacingRight ? -1f : 1f;
        Vector3 dashTargetPos = transform.position + new Vector3(moveDir * 2f, 0f, 0f);

        // 変更：消滅時にも魔法陣を表示（出現時の逆の手順で、縦に閉じるように縮小）
        if (magicCircleController != null)
        {
            magicCircleController.ChangeScaleX(1f, 0f); // 横幅を即時に1へ展開
            magicCircleController.ChangeScaleY(0f, teleportOutDuration, 1f); // 縦幅を1から0へ徐々に縮小して消滅を演出
        }

        // DOTweenのSequenceを用いて、後ろへの移動と透明化（フェードアウト）を同時に行う
        Sequence outSeq = DOTween.Sequence();
        outSeq.Join(transform.DOMove(dashTargetPos, teleportOutDuration).SetEase(Ease.OutCubic));
        if (_bodySpriteRenderer != null)
        {
            outSeq.Join(_bodySpriteRenderer.DOFade(0f, teleportOutDuration));
        }

        yield return new WaitForSeconds(teleportOutDuration);

        // --- 2. 座標移動 ---
        Vector3 nextPos = transform.position;
        if (teleportPoints != null && teleportPoints.Count > 0)
        {
            List<Vector3> validPoints = new List<Vector3>();
            foreach (var point in teleportPoints)
            {
                // 現在位置から近すぎる（例：2.0f以内）ポイントはワープ先として不自然なため除外する
                if (Vector3.Distance(point, transform.position) > 2.0f)
                {
                    validPoints.Add(point);
                }
            }

            // 有効な候補があればそこからランダム、無ければ（全て近い等の場合）全体からランダムに決定
            if (validPoints.Count > 0)
                nextPos = validPoints[Random.Range(0, validPoints.Count)];
            else
                nextPos = teleportPoints[Random.Range(0, teleportPoints.Count)];
        }

        // 完全に透明な状態で、座標を到達地点へワープさせる（isFacingRight はまだ変更しない）
        transform.position = nextPos;

        // --- 3. 魔法陣展開 ---
        if (magicCircleController != null)
        {
            magicCircleController.ChangeScaleX(1f, 0f); // 横幅だけ即時に1へ展開
            magicCircleController.ChangeScaleY(1f, teleportInDuration, 0f); // 縦幅を0から1へ徐々に展開して出現を演出
            yield return new WaitForSeconds(teleportInDuration);
        }
        else
        {
            yield return new WaitForSeconds(teleportInDuration);
        }

        // --- 4. 本体の出現 ---
        if (_bodySpriteRenderer != null)
        {
            _bodySpriteRenderer.DOFade(1f, teleportInDuration);
        }
        yield return new WaitForSeconds(teleportInDuration);

        // --- 5. 終了処理 ---
        if (magicCircleController != null)
        {
            // 出現完了後、魔法陣のスケールを0にして非表示状態にリセット
            magicCircleController.ChangeScaleXY(Vector2.zero, 0.3f);
        }

        // プレイヤーの位置に合わせて向きを更新
        UpdateFacingDirection();

        // ダメージ判定を復帰させる
        this.gameObject.tag = GameConstants.IMMUNE_ENEMY_TAG_NAME;

        if (_animator != null)
        {
            // 【重要】アニメーションの競合対策
            // テレポート中は裏で様々なトリガーがキューとして溜まっている可能性があるため、
            // 全てのトリガーを一度リセットし、「HandOnHip（待機）」と「次の攻撃」が同時に発動してフリーズするのを防ぐ
            _animator.ResetTrigger(_throwReadyHash);
            _animator.ResetTrigger(_throwHash);
            _animator.ResetTrigger(_lookBackHash);
            _animator.ResetTrigger(_liftHoldHash);
            _animator.ResetTrigger(_backDashHash);

            _animator.SetTrigger(_handOnHipHash);
        }

        CurrentState = ApothecaryState.Idle;

        // 同フレームで次の攻撃Triggerが呼ばれてアニメーションが壊れるのを防ぐため、
        // テレポート完了後にボスの隙（インターバル）を作ってからメインループを再開する
        float teleportWaitTime = IsDebugNoWaitActive ? 0f : 1.5f;
        if (teleportWaitTime > 0f)
        {
            yield return new WaitForSeconds(teleportWaitTime);
        }

        StartAttackLoop();
    }

    #endregion --- 回避アクション (テレポート) ---


    #region --- デバッグ表示 (Gizmos) ---

    /// <summary>
    /// エディタ上でオブジェクトを選択した際、各種範囲や発射位置などを可視化します
    /// </summary>
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

        // 4. テレポートポイントの表示
        if (teleportPoints != null)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < teleportPoints.Count; i++)
            {
                Gizmos.DrawWireSphere(teleportPoints[i], 0.3f);
#if UNITY_EDITOR
                // エディタ上でテレポート先を見やすくラベリング
                UnityEditor.Handles.Label(
                    new Vector3(teleportPoints[i].x, teleportPoints[i].y, 0f)
                        + new Vector3(0.4f, 0f, 0f),
                    $"Teleport Point {i}",
                    new GUIStyle() { normal = new GUIStyleState() { textColor = Color.cyan } }
                );
#endif
            }
        }
    }

    #endregion --- デバッグ表示 (Gizmos) ---
}

using System.Collections;
using System.Collections.Generic;
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
        IceAttacking // 氷攻撃状態
        ,
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

    // 初期生成する弾の数をconst変数として定義
    private const int INITIAL_FIRE_POOL_SIZE = 10;
    private const int INITIAL_WIND_POOL_SIZE = 10;
    private const int INITIAL_ICICLE_POOL_SIZE = 15;

    // 専用のオブジェクトプール
    private List<ApothecaryFireBullet> fireBulletPool = new List<ApothecaryFireBullet>();
    private List<ApothecaryWindBullet> windBulletPool = new List<ApothecaryWindBullet>();
    private List<IcicleMoveController> iciclePool = new List<IcicleMoveController>();

    // 取得した足場のコライダーを保持するリスト
    private List<Collider2D> _icePlatforms = new List<Collider2D>();

    private Coroutine attackLoopCoroutine;
    private Animator _animator;
    private Transform _playerTransform;
    private List<IcicleMoveController> _reservedIcicles = new List<IcicleMoveController>();

    // Animatorのパラメータをハッシュ化してキャッシュ
    private readonly int _throwReadyHash = Animator.StringToHash("ThrowReadyTrigger");
    private readonly int _throwHash = Animator.StringToHash("ThrowTrigger");
    private readonly int _idleHash = Animator.StringToHash("IdleTrigger");
    private readonly int _lookBackHash = Animator.StringToHash("LookBackTrigger");

    private void Awake()
    {
        // 必要なコンポーネントの取得や初期化処理を記述します
        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        // プレイヤーの取得
        _playerTransform = GameObject
            .FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME)
            ?.transform;
        if (_playerTransform == null)
        {
            Debug.LogWarning("プレイヤーが見つかりませんでした。");
        }

        // イベントの購読などを記述します
        InitializePools();
        ResetState();
    }

    /// <summary>
    /// ボスの状態をリセットし、初期行動を開始します
    /// </summary>
    public void ResetState()
    {
        if (attackLoopCoroutine != null)
        {
            StopCoroutine(attackLoopCoroutine);
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
        // ゲームのポーズ状態の確認や、常時行われる移動・向きの更新などを記述します
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

            // 現状はテストのため、氷攻撃に固定しています
            yield return StartCoroutine(PerformIceAttack());

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
        if (currentThrowDelayDuration > 0f)
            yield return new WaitForSeconds(currentThrowDelayDuration);

        bool isFacingRight = true;
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
                    fireGroundDuration
                );
            }

            if (currentFireInterval > 0f)
                yield return new WaitForSeconds(currentFireInterval);
        }

        // 4. 復帰
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
        if (currentThrowDelayDuration > 0f)
            yield return new WaitForSeconds(currentThrowDelayDuration);

        bool isFacingRight = true;
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

        bool isFacingRight = true;
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

    private void OnDestroy()
    {
        // イベントの購読解除や、実行中のTween、コルーチンの停止などを記述します
    }

    private void OnDrawGizmosSelected()
    {
        // 攻撃の発射位置をGizmosで表示
        Gizmos.color = Color.red;
        Vector3 rightSpawnPos =
            transform.position + new Vector3(normalAttackOffset.x, normalAttackOffset.y, 0f);
        Gizmos.DrawWireSphere(rightSpawnPos, 0.2f);

        Gizmos.color = Color.blue;
        Vector3 leftSpawnPos =
            transform.position + new Vector3(-normalAttackOffset.x, normalAttackOffset.y, 0f);
        Gizmos.DrawWireSphere(leftSpawnPos, 0.2f);

        // エリア境界の表示
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Vector3 center = new Vector3(
            (areaLeftBound + areaRightBound) / 2f,
            (areaTopBound + areaBottomBound) / 2f,
            0f
        );
        Vector3 size = new Vector3(
            areaRightBound - areaLeftBound,
            areaTopBound - areaBottomBound,
            0f
        );
        Gizmos.DrawWireCube(center, size);
    }
}

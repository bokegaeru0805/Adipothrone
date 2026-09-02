using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ロボット（Fabo）が発射する弾の挙動、当たり判定、エフェクト生成を管理するコントローラークラス。
/// 武器データ（ShootWeaponData）を元に初期化され、直線・放物線・3-Wayなどの軌道を描きます。
/// </summary>
[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class FaboProjectileController : PoolableObject
{
    public const string RobotShootPoolTag = "RobotShoot";

    private struct HitCooldown
    {
        public GameObject target;
        public float remainingTime;
    }

    #region キャッシュ・外部参照
    private CriWare.Assets.CriAtomSePlayer sePlayer;
    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rigidbody;
    private CircleCollider2D _circleCollider;
    private Animator _animator;
    private LayerMask _groundLayer;
    #endregion

    #region インスペクター設定
    [Header("エフェクト設定")]
    [SerializeField, Tooltip("非ボスヒット時に追加再生するエフェクトの数")]
    private int subHitEffectCount = 3;

    [SerializeField, Tooltip("非ボスヒット時の追加エフェクトが散らばる半径")]
    private float subHitEffectSpawnRadius = 1.5f;

    [Header("3-Way弾設定")]
    [SerializeField, Tooltip("上下の弾が広がる高さ（Parallel3Way時のみ適用）")]
    private float height = 1.5f;

    // オブジェクトプールのタグ指定
    private string hitEffectPoolTag = "HitEffect1";
    private string subHitEffectPoolTag = "HitEffect2";
    #endregion

    #region 動的パラメータ（ShootWeaponDataから適用）
    private ShootWeaponData currentShootData = null;
    private int shootPower = 0;
    private float shootSpeed = 0;
    public float vanishTime { get; private set; } = 0;
    private float cooldownTime = 1.0f;
    private float wpCost = 0f;
    private int penetrationLimitCount = 0;
    private ShootWeaponData.ShootMoveType moveType = ShootWeaponData.ShootMoveType.None;
    #endregion

    #region 状態管理
    public bool isStarted { get; private set; } = false; // 生成・初期化が完了したかどうか
    private readonly List<HitCooldown> _enemyCooldowns = new List<HitCooldown>(128); // 敵ごとの連続ヒット防止用タイマー
    private int currentPenetrationCount = 0; // 現在の貫通ヒット数
    private bool isMoveRight = true; // 弾の進行方向（true: 右, false: 左）
    private Vector2 initialPosition; // 発射時の初期座標
    private bool isSubBullet = false; // 3-Wayなどで複製されたサブ弾かどうかのフラグ
    private bool _isInBossBattle = false; // ボス戦闘中かどうかのフラグ
    private float _remainingLifetime = 0f;
    private bool _isLifetimeActive = false;
    private bool _isSubBulletMoving = false;
    private float _subBulletYDirection = 0f;
    private bool _isBoomerangMoving = false;
    private float _boomerangElapsedTime = 0f;
    private Vector3 _boomerangStartPosition;
    private Vector3 _boomerangControlPoint1;
    private Vector3 _boomerangControlPoint2;
    private Transform _boomerangReturnTarget;
    private Robot_move _boomerangOwner;
    private bool _isBoomerangOwnerRegistered = false;
    private bool _isBouncingProjectile = false;
    private int _currentBounceCount = 0;
    #endregion

    private void Awake()
    {
        sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _circleCollider = GetComponent<CircleCollider2D>();
        _animator = GetComponent<Animator>();
        _groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );
    }

    private void OnDisable()
    {
        _isLifetimeActive = false;
        _isSubBulletMoving = false;
        _isBoomerangMoving = false;
        ReleaseBoomerangOwner();
    }

    private void Update()
    {
        UpdateEnemyCooldowns(Time.deltaTime);

        if (_isLifetimeActive)
        {
            _remainingLifetime -= Time.deltaTime;
            if (_remainingLifetime <= 0f)
            {
                ReturnProjectile();
                return;
            }
        }

        if (
            _isSubBulletMoving
            && Mathf.Abs(transform.position.y - initialPosition.y) >= height
        )
        {
            _rigidbody.velocity = new Vector2(_rigidbody.velocity.x, 0f);
            _isSubBulletMoving = false;
        }

        if (_isBoomerangMoving)
        {
            UpdateBoomerangMovement();
        }

        if (_isBouncingProjectile && _rigidbody.velocity.sqrMagnitude > 0.01f)
        {
            float angle =
                Mathf.Atan2(_rigidbody.velocity.y, _rigidbody.velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void FixedUpdate()
    {
        if (!_isBouncingProjectile || _rigidbody.velocity.y > 0f)
            return;

        Vector2 boxSize = new Vector2(_circleCollider.bounds.size.x, 0.05f);
        float checkDistance =
            Mathf.Abs(_rigidbody.velocity.y) * Time.fixedDeltaTime + 0.1f;
        RaycastHit2D hit = Physics2D.BoxCast(
            _circleCollider.bounds.center,
            boxSize,
            0f,
            Vector2.down,
            checkDistance,
            _groundLayer
        );

        if (hit.collider == null)
            return;

        if (_currentBounceCount >= currentShootData.bouncingMaxCount)
        {
            ReturnProjectile();
            return;
        }

        _currentBounceCount++;
        float colliderBottomOffset = transform.position.y - _circleCollider.bounds.min.y;
        transform.position = new Vector3(
            transform.position.x,
            hit.point.y + colliderBottomOffset,
            transform.position.z
        );

        float gravity = Mathf.Abs(Physics2D.gravity.y * _rigidbody.gravityScale);
        float bounceVelocityY = Mathf.Sqrt(
            2f * gravity * currentShootData.bouncingHeight
        );
        _rigidbody.velocity = new Vector2(_rigidbody.velocity.x, bounceVelocityY);
    }
    #region 初期化設定

    /// <summary>
    /// 武器データを受け取り、弾の性能やコンポーネントを初期化します。
    /// 実際の軌道計算と発射は ExecuteFire メソッドに委譲します。
    /// </summary>
    /// <param name="data">弾の性能を定義したデータ</param>
    /// <param name="moveRight">右方向に発射する場合は true</param>
    public void InitializeBullet(ShootWeaponData data, bool moveRight)
    {
        InitializeBullet(data, moveRight, false, null, null);
    }

    internal void InitializeBullet(
        ShootWeaponData data,
        bool moveRight,
        Transform boomerangReturnTarget,
        Robot_move boomerangOwner
    )
    {
        InitializeBullet(data, moveRight, false, boomerangReturnTarget, boomerangOwner);
    }

    private void InitializeBullet(
        ShootWeaponData data,
        bool moveRight,
        bool isSubProjectile,
        Transform boomerangReturnTarget,
        Robot_move boomerangOwner
    )
    {
        ResetRuntimeState();
        isSubBullet = isSubProjectile;
        isMoveRight = moveRight;
        _boomerangReturnTarget = boomerangReturnTarget;
        _boomerangOwner = boomerangOwner;

        if (data == null)
        {
            Debug.LogWarning("ShootWeaponDataがnullのため、弾を初期化できません。");
            ReturnProjectile();
            return;
        }

        // --- 1. データの適用 ---
        currentShootData = data;
        _spriteRenderer.sprite = data.itemSprite;
        shootPower = data.power;
        wpCost = data.wpCost;
        vanishTime = data.vanishTime;
        shootSpeed = data.shootSpeed;
        cooldownTime = data.cooldownTime;
        penetrationLimitCount = data.penetrationLimitCount;
        moveType = data.moveType;

        // --- 2. コンポーネントの設定 ---
        if (_circleCollider != null)
        {
            _circleCollider.offset = data.colliderOffset;
            _circleCollider.radius = data.colliderRadius;
        }

        if (_animator != null)
        {
            bool hasShootAnimation = data.shootAnimation != null;
            _animator.enabled = hasShootAnimation;
            if (hasShootAnimation)
            {
                _animator.Play(Animator.StringToHash(data.shootAnimation.name));
            }
        }

        _isInBossBattle = GameUIManager.instance?.IsInBossBattle ?? false;

        // --- 3. 寿命と向きの初期化 ---
        _spriteRenderer.flipX = !moveRight;
        currentPenetrationCount = 0;
        _remainingLifetime = vanishTime;
        _isLifetimeActive = moveType != ShootWeaponData.ShootMoveType.Boomerang;

        // --- 4. 発射処理の呼び出し ---
        ExecuteFire(_rigidbody);
    }

    #endregion

    #region 発射・軌道制御

    /// <summary>
    /// 移動タイプ（moveType）に応じて物理的な力を加え、弾を発射します。
    /// </summary>
    /// <param name="rb">弾のRigidbody2D</param>
    private void ExecuteFire(Rigidbody2D rb)
    {
        // 放物線軌道以外は重力の影響を無効化
        if (moveType != ShootWeaponData.ShootMoveType.Parabola)
        {
            rb.gravityScale = 0f;
        }

        // 移動タイプに応じた発射処理
        if (!isSubBullet && moveType == ShootWeaponData.ShootMoveType.Parallel3Way)
        {
            // 3-Way（メイン弾）の場合、上下にサブ弾を複製して自身は直進する
            CreateSubBullet(1f);
            CreateSubBullet(-1f);
            rb.AddForce(new Vector2((isMoveRight ? 1 : -1) * shootSpeed, 0), ForceMode2D.Impulse);
            isStarted = true;
        }
        else if (moveType == ShootWeaponData.ShootMoveType.Straight)
        {
            // 直線移動の場合
            rb.AddForce(new Vector2((isMoveRight ? 1 : -1) * shootSpeed, 0), ForceMode2D.Impulse);
            isStarted = true;
        }
        else if (moveType == ShootWeaponData.ShootMoveType.Parabola)
        {
            // 放物線移動の場合
            rb.gravityScale = currentShootData.gravityScale;

            // 進行方向の角度を計算（左向きの場合は180度反転）
            float angle = isMoveRight
                ? currentShootData.upwardAngle
                : 180f - currentShootData.upwardAngle;
            Vector2 launchDirection = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            ).normalized;

            rb.AddForce(launchDirection * shootSpeed, ForceMode2D.Impulse);
            isStarted = true;
        }
        else if (isSubBullet && moveType == ShootWeaponData.ShootMoveType.Parallel3Way)
        {
            // 3-Wayのサブ弾はBeginSubBulletMovementで速度を設定する
            isStarted = true;
        }
        else if (moveType == ShootWeaponData.ShootMoveType.Boomerang)
        {
            BeginBoomerangMovement();
            isStarted = true;
        }
        else if (moveType == ShootWeaponData.ShootMoveType.Bouncing)
        {
            BeginBouncingMovement();
            isStarted = true;
        }
        else
        {
            Debug.LogWarning("不明な弾の移動タイプが指定されました: " + moveType);
            ReturnProjectile();
        }
    }

    #endregion

    #region 特殊軌道（サブ弾）処理

    /// <summary>
    /// 3-Way用に自身を複製し、上下に広がるサブ弾を生成します。
    /// </summary>
    /// <param name="yDirection">Y軸方向の向き（1f または -1f）</param>
    private void CreateSubBullet(float yDirection)
    {
        GameObject subBulletGO = SpawnProjectile(transform.position, Quaternion.identity);
        if (subBulletGO == null)
            return;

        FaboProjectileController subBulletScript =
            subBulletGO.GetComponent<FaboProjectileController>();

        subBulletScript.InitializeBullet(currentShootData, isMoveRight, true, null, null);
        subBulletScript.BeginSubBulletMovement(yDirection);
    }

    /// <summary>
    /// サブ弾固有の移動軌道（斜めに広がった後、平行に飛ぶ）を制御します。
    /// </summary>
    private void BeginSubBulletMovement(float yDirection)
    {
        initialPosition = transform.position;
        _subBulletYDirection = yDirection;
        _isSubBulletMoving = true;

        // 指定方向へ斜めに撃ち出す
        float horizontalVelocity = (isMoveRight ? 1 : -1) * shootSpeed;
        _rigidbody.velocity = new Vector2(horizontalVelocity, _subBulletYDirection * shootSpeed / 2f);
    }

    #endregion

    #region ブーメラン軌道

    private void BeginBoomerangMovement()
    {
        _rigidbody.velocity = Vector2.zero;
        _rigidbody.angularVelocity = 0f;
        _boomerangElapsedTime = 0f;
        _boomerangStartPosition = transform.position;

        float facingMultiplier = isMoveRight ? 1f : -1f;
        float topY = _boomerangStartPosition.y + currentShootData.boomerangCurveWidth;
        float bottomY = Mathf.Max(
            _boomerangStartPosition.y - currentShootData.boomerangCurveWidth,
            _boomerangStartPosition.y + currentShootData.boomerangMinYOffset
        );
        float firstControlY = currentShootData.isBoomerangOverhand ? topY : bottomY;
        float secondControlY = currentShootData.isBoomerangOverhand ? bottomY : topY;
        float controlX =
            _boomerangStartPosition.x
            + currentShootData.boomerangDistance * facingMultiplier;

        _boomerangControlPoint1 = new Vector3(controlX, firstControlY, 0f);
        _boomerangControlPoint2 = new Vector3(controlX, secondControlY, 0f);
        _isBoomerangMoving = true;

        if (_boomerangOwner != null)
        {
            _boomerangOwner.NotifyBoomerangLaunched();
            _isBoomerangOwnerRegistered = true;
        }
    }

    private void UpdateBoomerangMovement()
    {
        float flyTime = Mathf.Max(0.01f, currentShootData.boomerangFlyTime);
        _boomerangElapsedTime += Time.deltaTime;
        float t = Mathf.Clamp01(_boomerangElapsedTime / flyTime);
        Vector3 returnPosition =
            _boomerangReturnTarget != null
                ? _boomerangReturnTarget.position
                : _boomerangStartPosition;

        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        Vector3 position = uu * u * _boomerangStartPosition;
        position += 3f * uu * t * _boomerangControlPoint1;
        position += 3f * u * tt * _boomerangControlPoint2;
        position += tt * t * returnPosition;
        transform.position = position;

        transform.Rotate(0f, 0f, currentShootData.boomerangRotationSpeed * Time.deltaTime);

        if (t >= 1f)
        {
            ReturnProjectile();
        }
    }

    #endregion

    #region バウンド軌道

    private void BeginBouncingMovement()
    {
        _isBouncingProjectile = true;
        _currentBounceCount = 0;
        _rigidbody.gravityScale = currentShootData.bouncingGravityScale;

        float angle = currentShootData.bouncingLaunchAngle * Mathf.Deg2Rad;
        float horizontalVelocity = shootSpeed * Mathf.Cos(angle);
        float verticalVelocity = shootSpeed * Mathf.Sin(angle);
        _rigidbody.velocity = new Vector2(
            isMoveRight ? horizontalVelocity : -horizontalVelocity,
            verticalVelocity
        );
    }

    private bool IsGroundBelowProjectile(Collider2D collision)
    {
        bool isGroundLayer = (_groundLayer.value & (1 << collision.gameObject.layer)) != 0;
        return isGroundLayer
            && collision.bounds.max.y <= _circleCollider.bounds.center.y + 0.1f;
    }

    #endregion

    #region 当たり判定

    private void OnTriggerStay2D(Collider2D collision)
    {
        IDamageable hpScript = collision.GetComponent<IDamageable>();

        // --- 敵や破壊可能オブジェクトへのヒット処理 ---
        if (hpScript != null)
        {
            MonoBehaviour mb = hpScript as MonoBehaviour;
            if (mb.enabled == false)
                return;

            GameObject enemy = collision.gameObject;

            // クールタイム中の敵には連続ヒットさせない
            if (IsTargetOnCooldown(enemy))
                return;

            _enemyCooldowns.Add(
                new HitCooldown { target = enemy, remainingTime = cooldownTime }
            );
            currentPenetrationCount++;

            // エフェクトの生成処理
            if (ObjectPooler.PersistentInstance != null && !string.IsNullOrEmpty(hitEffectPoolTag))
            {
                Vector2 hitPosition = this.transform.position;
                ObjectPooler.PersistentInstance.SpawnFromPool(
                    hitEffectPoolTag,
                    hitPosition,
                    Quaternion.identity
                );

                // ボス戦以外なら、周囲に散らばるサブエフェクトを追加生成
                if (!_isInBossBattle && !string.IsNullOrEmpty(subHitEffectPoolTag))
                {
                    for (int i = 0; i < subHitEffectCount; i++)
                    {
                        Vector2 randomOffset = Random.insideUnitCircle * subHitEffectSpawnRadius;
                        ObjectPooler.PersistentInstance.SpawnFromPool(
                            subHitEffectPoolTag,
                            hitPosition + randomOffset,
                            Quaternion.identity
                        );
                    }
                }
            }

            // ダメージ計算と適用
            int damageSumAmount =
                PlayerEffectManager.instance?.CalculateFinalAttackPower(shootPower) ?? 0;
            hpScript.Damage(damageSumAmount);
            sePlayer.Play(SE_EnemyAction.Damage2);

            // WP消費
            if (wpCost > 0)
            {
                PlayerManager.instance?.AddWpConsumptionBuffer(wpCost);
            }

            // 貫通上限に達した場合は弾を破棄
            if (currentPenetrationCount >= penetrationLimitCount)
            {
                ReturnProjectile();
            }

            return;
        }

        // --- 壁や障害物へのヒット処理 ---
        if (!collision.isTrigger)
        {
            // プレイヤー自身には干渉しない
            if (collision.CompareTag(GameConstants.PLAYER_TAG_NAME))
                return;

            // バウンド弾の足元にある地面はFixedUpdate側で処理する
            if (_isBouncingProjectile && IsGroundBelowProjectile(collision))
                return;

            // 物理的な壁（isTriggerがfalseのコライダー）に当たった場合は弾を破棄
            ReturnProjectile();
        }
    }

    #endregion

    #region 命中クールタイム

    private bool IsTargetOnCooldown(GameObject target)
    {
        for (int i = 0; i < _enemyCooldowns.Count; i++)
        {
            if (_enemyCooldowns[i].target == target)
                return true;
        }

        return false;
    }

    private void UpdateEnemyCooldowns(float deltaTime)
    {
        for (int i = _enemyCooldowns.Count - 1; i >= 0; i--)
        {
            HitCooldown cooldown = _enemyCooldowns[i];
            cooldown.remainingTime -= deltaTime;
            if (cooldown.target == null || cooldown.remainingTime <= 0f)
            {
                int lastIndex = _enemyCooldowns.Count - 1;
                _enemyCooldowns[i] = _enemyCooldowns[lastIndex];
                _enemyCooldowns.RemoveAt(lastIndex);
            }
            else
            {
                _enemyCooldowns[i] = cooldown;
            }
        }
    }

    #endregion

    #region Pooling

    private GameObject SpawnProjectile(Vector3 position, Quaternion rotation)
    {
        ObjectPooler pooler = returnToPool == PoolType.Persistent
            ? ObjectPooler.PersistentInstance
            : ObjectPooler.SceneInstance;

        if (pooler != null && !string.IsNullOrEmpty(myPoolTag))
        {
            return pooler.SpawnFromPool(myPoolTag, position, rotation);
        }

        return Instantiate(gameObject, position, rotation);
    }

    private void ResetRuntimeState()
    {
        _isLifetimeActive = false;
        _remainingLifetime = 0f;
        _isSubBulletMoving = false;
        _subBulletYDirection = 0f;
        _isBoomerangMoving = false;
        _boomerangElapsedTime = 0f;
        _isBouncingProjectile = false;
        _currentBounceCount = 0;
        isStarted = false;
        currentPenetrationCount = 0;
        _enemyCooldowns.Clear();
        ReleaseBoomerangOwner();
        _boomerangReturnTarget = null;
        _boomerangOwner = null;

        if (_rigidbody != null)
        {
            _rigidbody.velocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.gravityScale = 0f;
        }
    }

    private void ReturnProjectile()
    {
        _isLifetimeActive = false;
        _isSubBulletMoving = false;
        _isBoomerangMoving = false;
        _isBouncingProjectile = false;
        ReleaseBoomerangOwner();
        ReturnToPool();
    }

    private void ReleaseBoomerangOwner()
    {
        if (!_isBoomerangOwnerRegistered)
            return;

        if (_boomerangOwner != null)
        {
            _boomerangOwner.NotifyBoomerangReturned();
        }

        _isBoomerangOwnerRegistered = false;
    }

    #endregion
}

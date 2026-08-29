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

    #region キャッシュ・外部参照
    private CriWare.Assets.CriAtomSePlayer sePlayer;
    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rigidbody;
    private CircleCollider2D _circleCollider;
    private Animator _animator;
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
    private Dictionary<GameObject, float> enemyCooldowns = new Dictionary<GameObject, float>(); // 敵ごとの連続ヒット防止用タイマー
    private int currentPenetrationCount = 0; // 現在の貫通ヒット数
    private bool isMoveRight = true; // 弾の進行方向（true: 右, false: 左）
    private Vector2 initialPosition; // 発射時の初期座標
    private bool isSubBullet = false; // 3-Wayなどで複製されたサブ弾かどうかのフラグ
    private bool _isInBossBattle = false; // ボス戦闘中かどうかのフラグ
    private float _remainingLifetime = 0f;
    private bool _isLifetimeActive = false;
    private bool _isSubBulletMoving = false;
    private float _subBulletYDirection = 0f;
    #endregion

    private void Awake()
    {
        sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _circleCollider = GetComponent<CircleCollider2D>();
        _animator = GetComponent<Animator>();
    }

    private void Update()
    {
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
        InitializeBullet(data, moveRight, false);
    }

    private void InitializeBullet(ShootWeaponData data, bool moveRight, bool isSubProjectile)
    {
        ResetRuntimeState();
        isSubBullet = isSubProjectile;
        isMoveRight = moveRight;

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
        _isLifetimeActive = true;

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

        subBulletScript.InitializeBullet(currentShootData, isMoveRight, true);
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
            if (enemyCooldowns.ContainsKey(enemy))
                return;

            enemyCooldowns[enemy] = cooldownTime;
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

            // 物理的な壁（isTriggerがfalseのコライダー）に当たった場合は弾を破棄
            ReturnProjectile();
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
        isStarted = false;
        currentPenetrationCount = 0;
        enemyCooldowns.Clear();

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
        ReturnToPool();
    }

    #endregion
}

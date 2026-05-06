using System.Collections;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// ガーゴイルの挙動を制御するコントローラークラス
/// プレイヤーを検知して2種類の弾（大・小）をランダムに発射し、攻撃時にはスプライトを切り替えます。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class GargoyleMoveController : MonoBehaviour, IEnemyResettable
{
    #region 弾の設定クラス
    /// <summary>
    /// 弾ごとの個別の設定をまとめるクラス
    /// </summary>
    [System.Serializable]
    public class BulletSettings
    {
        [Tooltip("発射する弾のプレハブ（BouncingBulletControllerアタッチ済みのもの）")]
        public GameObject prefab;

        [Tooltip("プレイヤーに与えるダメージ")]
        [HideInInspector]
        public int damage = 10;

        [Tooltip("弾を発射する位置のオフセット")]
        public Vector2 offset = new Vector2(1.0f, 0.5f);

        [Tooltip("弾の速度")]
        public float speed = 5.0f;

        [Tooltip("弾を発射する角度（度。0で真横、90で真上）")]
        public float launchAngle = 30.0f;

        [Tooltip("弾が消滅（非アクティブ化）するまでの時間（秒）")]
        public float lifeTime = 3.0f;
    }
    #endregion

    #region 基本・外観設定
    [Header("基本設定")]
    [SerializeField]
    [Tooltip("待機時（通常時）のスプライト")]
    private Sprite idleSprite;

    [SerializeField]
    [Tooltip("攻撃時のスプライト")]
    private Sprite attackSprite;

    [Header("向きの設定")]
    [SerializeField]
    [Tooltip("手動で初期の向きを設定するかどうか")]
    private bool isManualInitialDirection = false;

    [SerializeField]
    [
        Tooltip("初期の向き（trueで右向き、falseで左向き）。"),
        ShowIf(nameof(isManualInitialDirection))
    ]
    private bool initialFacingRight = false;
    #endregion

    #region 配置・初期位置設定
    [Header("敵のタイプ")]
    [SerializeField]
    private EnemyVariant variantType = EnemyVariant.None;

    [Header("初期位置の設定")]
    [SerializeField]
    [Tooltip("手動で初期位置を設定するかどうか")]
    private bool isUseManualInitialPosition = false;

    [SerializeField]
    [Tooltip("親のEnemyActivatorコンポーネント。自動配置の計算時に使用します")]
    private EnemyActivator activator = null;

    [Header("配置調整用の設定")]
    [SerializeField]
    [Tooltip("地面に埋まっていないかチェックする中心点")]
    private Transform overlapCheckPoint;

    [SerializeField]
    [Tooltip("チェック用円の半径")]
    private float overlapCheckRadius = 0.5f;

    [SerializeField]
    [Tooltip("地面から抜け出す速度")]
    private float verticalAdjustSpeed = 10f;
    #endregion

    #region 攻撃・弾設定
    [Header("攻撃範囲・待機時間設定")]
    [SerializeField]
    [Tooltip("攻撃範囲のX距離")]
    private float attackRangeX = 8.0f;

    [SerializeField]
    [Tooltip("攻撃範囲のY距離")]
    private float attackRangeY = 3.0f;

    [SerializeField]
    [Tooltip("攻撃終了後、次の攻撃が可能になるまでの待機時間（秒）")]
    private float attackCooldown = 2.0f;

    [Header("弾の個別設定")]
    [SerializeField]
    [Tooltip("小さい弾の設定")]
    private BulletSettings smallBulletSettings;

    [SerializeField]
    [Tooltip("大きい弾の設定")]
    private BulletSettings largeBulletSettings;

    [Header("弾の発射パターン")]
    [SerializeField]
    [Tooltip("trueの場合、大と小の弾を交互に発射します。falseの場合はランダム（等確率）です。")]
    private bool isAlternateFire = false;
    #endregion

    #region プライベート変数
    private LayerMask groundLayer;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rbody;
    private Transform playerTransform;

    private bool rightFlag = false;
    private bool canAttack = true;

    private enum EnemyVariant
    {
        None = 0,
        Tower = 1,
    }

    // 使い回すための弾インスタンス
    private GameObject pooledSmallBullet;
    private GameObject pooledLargeBullet;

    private bool fireSmallBulletNext = true; // 交互発射時に次に小さい弾を撃つかどうかのフラグ

    // 埋まり判定用プロパティ
    private bool isOverlappingGround =>
        Physics2D.OverlapCircle(overlapCheckPoint.position, overlapCheckRadius, groundLayer);
    #endregion

    #region 初期化処理
    private void Awake()
    {
        // 物理レイヤーの取得
        groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );

        switch (variantType)
        {
            case EnemyVariant.Tower:
                smallBulletSettings.damage = 15;
                largeBulletSettings.damage = 30;
                break;
            default:
                Debug.LogError($"{this.name}のEnemyVariantが設定されていません。", this);
                break;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        rbody = GetComponent<Rigidbody2D>();

        if (activator == null)
        {
            activator = GetComponentInParent<EnemyActivator>();
        }
    }

    private void Start()
    {
        // 弾のインスタンスをそれぞれあらかじめ1つずつ生成し、非アクティブ状態で保持しておく
        if (smallBulletSettings.prefab != null)
        {
            pooledSmallBullet = Instantiate(smallBulletSettings.prefab, this.transform);
            pooledSmallBullet.SetActive(false);
        }
        else
        {
            Debug.LogError("小さい弾のプレハブがアサインされていません。");
        }

        if (largeBulletSettings.prefab != null)
        {
            pooledLargeBullet = Instantiate(largeBulletSettings.prefab, this.transform);
            pooledLargeBullet.SetActive(false);
        }
        else
        {
            Debug.LogError("大きい弾のプレハブがアサインされていません。");
        }

        ResetState();
    }

    /// <summary>
    /// 敵の状態（位置、向き、弾、スプライトなど）を初期化・リセットします
    /// </summary>
    public void ResetState()
    {
        // プレイヤーの取得
        if (PlayerManager.instance != null)
        {
            playerTransform = PlayerManager.instance.PlayerGameObject?.transform;
        }
        else
        {
            playerTransform = GameObject
                .FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME)
                ?.transform;
        }

        // 初期位置の自動設定
        if (!isUseManualInitialPosition)
        {
            if (activator != null)
            {
                var activatorCollider = activator.GetComponent<Collider2D>();
                if (activatorCollider != null)
                {
                    float activatorLeftBound = activatorCollider.bounds.min.x;
                    float activatorRightBound = activatorCollider.bounds.max.x;
                    float randomCenter = Random.Range(activatorLeftBound, activatorRightBound);

                    transform.position = new Vector2(randomCenter, transform.position.y);
                }
                else
                {
                    Debug.LogWarning(
                        $"{this.name}のEnemyActivatorにCollider2Dが見つかりませんでした。初期位置の自動設定は行いません。"
                    );
                }
            }
            else
            {
                Debug.LogWarning(
                    $"{this.name}の親にEnemyActivatorが見つかりませんでした。移動範囲の自動設定は行いません。"
                );
            }
        }

        // 自身の向きを決定
        if (isManualInitialDirection)
        {
            rightFlag = initialFacingRight;
        }
        else
        {
            // 手動設定が無効な場合は左右ランダム
            rightFlag = Random.value > 0.5f;
        }
        UpdateFacingDirection();

        // 弾のリセット
        if (pooledSmallBullet != null)
            pooledSmallBullet.SetActive(false);
        if (pooledLargeBullet != null)
            pooledLargeBullet.SetActive(false);

        // 交互発射の順番をリセット（最初は小さい弾から）
        fireSmallBulletNext = true;

        // スプライトを待機状態にする
        if (spriteRenderer != null && idleSprite != null)
        {
            spriteRenderer.sprite = idleSprite;
        }

        // 攻撃可能状態にリセット
        canAttack = true;

        // 地面に接するように初期座標を調整する処理を開始
        StartCoroutine(CheckAndAdjustPosition());
    }
    #endregion

    #region 更新・判定処理
    private void FixedUpdate()
    {
        if (playerTransform == null || !canAttack)
            return;

        // ポーズ処理の確認
        if (TimeManager.instance != null && TimeManager.instance.isEnemyMovePaused)
        {
            if (rbody.simulated)
                rbody.simulated = false;
            return;
        }
        else if (!rbody.simulated && canAttack)
        {
            rbody.simulated = true;
        }

        // 発射済みの「小」または「大」の弾が画面内に存在している場合は次の攻撃を行わない
        bool isSmallActive = pooledSmallBullet != null && pooledSmallBullet.activeSelf;
        bool isLargeActive = pooledLargeBullet != null && pooledLargeBullet.activeSelf;
        if (isSmallActive || isLargeActive)
            return;

        // 攻撃範囲内にプレイヤーがいれば攻撃を実行
        if (IsPlayerInAttackRange())
        {
            StartCoroutine(AttackRoutine());
        }
    }

    /// <summary>
    /// プレイヤーが自身の攻撃範囲（前方の指定矩形内）にいるか判定します
    /// </summary>
    private bool IsPlayerInAttackRange()
    {
        Vector2 directionToPlayer = playerTransform.position - transform.position;

        // 向いている方向にプレイヤーがいるか確認（右向きならXは正、左向きなら負）
        float horizontalDistance = directionToPlayer.x * (rightFlag ? 1 : -1);

        bool isInRangeX = horizontalDistance >= 0 && horizontalDistance <= attackRangeX;
        bool isInRangeY = Mathf.Abs(directionToPlayer.y) <= attackRangeY;

        return isInRangeX && isInRangeY;
    }
    #endregion

    #region 攻撃処理
    /// <summary>
    /// スプライトの切り替えと、弾（大または小）を発射する一連の処理
    /// </summary>
    private IEnumerator AttackRoutine()
    {
        canAttack = false;

        // 攻撃用スプライトに変更
        if (spriteRenderer != null && attackSprite != null)
        {
            spriteRenderer.sprite = attackSprite;
        }

        bool useSmallBullet;
        if (isAlternateFire)
        {
            // 交互に発射
            useSmallBullet = fireSmallBulletNext;
            fireSmallBulletNext = !fireSmallBulletNext; // 次回のためにフラグを反転させる
        }
        else
        {
            // 50%の確率でランダムに発射
            useSmallBullet = Random.value < 0.5f;
        }

        BulletSettings currentSettings = useSmallBullet ? smallBulletSettings : largeBulletSettings;
        GameObject currentBullet = useSmallBullet ? pooledSmallBullet : pooledLargeBullet;

        if (currentBullet != null)
        {
            // 向きに応じて発射位置のX座標を反転させる
            Vector3 spawnPos =
                transform.position
                + new Vector3(
                    rightFlag ? currentSettings.offset.x : -currentSettings.offset.x,
                    currentSettings.offset.y,
                    0
                );

            // 弾の座標をリセットし、アクティブ化する
            currentBullet.transform.position = spawnPos;
            currentBullet.SetActive(true);

            // ダメージ設定の適用
            ContactDamageController damageController =
                currentBullet.GetComponent<ContactDamageController>();
            if (damageController != null)
            {
                damageController.SetNormalDamage(currentSettings.damage);
            }

            // 弾に指定した角度での速度を与える
            Rigidbody2D bulletRb = currentBullet.GetComponent<Rigidbody2D>();
            if (bulletRb != null)
            {
                // 放物線を描かせるため、弾側の重力を有効にする
                bulletRb.gravityScale = 1f;

                // 角度をラジアンに変換してX方向とY方向の速度成分を計算
                float angleRad = currentSettings.launchAngle * Mathf.Deg2Rad;
                float speedX = currentSettings.speed * Mathf.Cos(angleRad);
                float speedY = currentSettings.speed * Mathf.Sin(angleRad);

                bulletRb.velocity = new Vector2(rightFlag ? speedX : -speedX, speedY);
            }

            // 弾の消滅（バウンド回数や生存時間）の管理は弾自身のスクリプトに任せる
            BouncingBulletController bounceController =
                currentBullet.GetComponent<BouncingBulletController>();
            if (bounceController != null)
            {
                bounceController.Initialize(currentSettings.lifeTime);
            }
        }

        // 次の攻撃までのクールダウン待機
        yield return new WaitForSeconds(attackCooldown);

        // 待機スプライトに戻す
        if (spriteRenderer != null && idleSprite != null)
        {
            spriteRenderer.sprite = idleSprite;
        }

        canAttack = true;
    }
    #endregion

    #region 補助・調整処理
    /// <summary>
    /// 配置時に地面に埋まっている場合、埋まらない位置までY座標を上方向に調整します
    /// </summary>
    private IEnumerator CheckAndAdjustPosition()
    {
        if (overlapCheckPoint == null)
            yield break;

        if (isOverlappingGround)
        {
            rbody.simulated = false; // 物理演算を一時停止して手動で移動

            while (isOverlappingGround)
            {
                transform.position += new Vector3(0, verticalAdjustSpeed * Time.deltaTime, 0);
                yield return null;
            }

            rbody.simulated = true;
        }
    }

    /// <summary>
    /// 自身の向きフラグに合わせてSpriteRendererの左右反転状態を更新します
    /// </summary>
    private void UpdateFacingDirection()
    {
        if (spriteRenderer != null)
        {
            // 左向きならflipX = true、右向きならfalse に設定
            spriteRenderer.flipX = !rightFlag;
        }
    }
    #endregion

    #region Unityイベントハンドラ
    private void OnDisable()
    {
        StopAllCoroutines();

        // 自身が非アクティブになる際に、発射中の弾もリセットする
        if (pooledSmallBullet != null)
            pooledSmallBullet.SetActive(false);
        if (pooledLargeBullet != null)
            pooledLargeBullet.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        // 埋まりチェック用のGizmosをシアン色で表示
        if (overlapCheckPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(overlapCheckPoint.position, overlapCheckRadius);
        }
    }

    private void OnDrawGizmos()
    {
        // 攻撃範囲の描画（赤色の半透明な矩形）
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        float attackCenterX = transform.position.x + (rightFlag ? 1 : -1) * (attackRangeX / 2);
        Vector3 attackCenter = new Vector3(
            attackCenterX,
            transform.position.y,
            transform.position.z
        );
        Vector3 attackSize = new Vector3(attackRangeX, attackRangeY * 2, 0.1f);
        Gizmos.DrawCube(attackCenter, attackSize);

        // 弾の発射位置（オフセット）の描画（黄色の小さい球）
        // ※ここでは小さい弾のオフセットを基準に描画しています
        if (smallBulletSettings != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 gizmoSpawnPos =
                transform.position
                + new Vector3(
                    rightFlag ? smallBulletSettings.offset.x : -smallBulletSettings.offset.x,
                    smallBulletSettings.offset.y,
                    0
                );
            Gizmos.DrawSphere(gizmoSpawnPos, 0.15f);
        }
    }
    #endregion
}

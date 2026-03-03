using System.Collections;
using NaughtyAttributes;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class DesertTempleGolemWalkMoveController : MonoBehaviour, IEnemyResettable
{
    #region Constants

    private const float MOVE_RANGE = 10.0f; // ランダムに設定する場合の移動幅
    private const string ATTACK_ANIMATION_CLIP_NAME = "DesertTempleGolem_Walk_Attack"; // 攻撃アニメーションのクリップ名
    private const float STUCK_CHECK_INTERVAL = 0.5f; // スタック検知の間隔（秒）

    #endregion

    #region Inspector Settings

    [Header("基本設定")]
    [SerializeField]
    private Transform playerTransform = null;

    [SerializeField]
    private EnemyActivator activator = null; // 親のEnemyActivatorコンポーネント

    [Header("横移動の設定")]
    [SerializeField]
    private float speedX = 2.0f;

    [Header("攻撃の設定")]
    [SerializeField, Tooltip("この敵がプレイヤーを攻撃する範囲のX距離")]
    private float attackRangeX = 5.0f;

    [SerializeField, Tooltip("この敵がプレイヤーを攻撃する範囲のY距離")]
    private float attackRangeY = 2.5f;

    [SerializeField, Tooltip("この敵がプレイヤーに与えるダメージ")]
    private int damage = 20;

    [Tooltip("攻撃前の待機時間（秒）")]
    [SerializeField]
    private float beforeAttackTime = 0.5f;

    [Tooltip("攻撃後の待機時間（秒）")]
    [SerializeField]
    private float afterAttackTime = 1.0f;

    [Tooltip("攻撃終了後、次に攻撃可能になるまでのクールダウン時間（秒）")]
    [SerializeField]
    private float attackCooldownTime = 2.0f;

    [Header("移動範囲の設定")]
    [SerializeField]
    [Tooltip("手動で移動範囲を設定するかどうか")]
    private bool isUseManualBounds = false;

    [SerializeField, ShowIf(nameof(isUseManualBounds))]
    private float leftBound;

    [SerializeField, ShowIf(nameof(isUseManualBounds))]
    private float rightBound;

    [Header("地面・壁判定用の設定")]
    [Tooltip("地面に埋まっていないかチェックする中心点")]
    [SerializeField]
    private Transform overlapCheckPoint;

    [SerializeField]
    private float overlapCheckRadius = 0.5f;

    [Tooltip("崖（地面がない場所）を検知するための前方のオフセット距離")]
    [SerializeField]
    private float cliffCheckOffsetX = 0.8f;

    [Tooltip("地面に向かって飛ばすレイの長さ")]
    [SerializeField]
    private float cliffCheckRayLength = 1.0f;

    #endregion

    #region Private Fields

    // --- コンポーネントキャッシュ ---
    private Animator _animator;
    private Rigidbody2D rbody;
    private SpriteRenderer spriteRenderer;
    private EnemyHealth enemyHP;
    private ContactDamageController contactDamageController;
    private CriWare.Assets.CriAtomSePlayer sePlayer;
    private LayerMask groundLayer;

    // --- 状態管理 ---
    private enum GolemState
    {
        None,
        Moving,
        PreparingToAttack,
        Attacking,
        AfterAttackDelay,
        AdjustingPosition,
    }
    private GolemState currentState = GolemState.None;

    // --- 攻撃・移動パラメータ ---
    private float attackAnimationTime = 0.5f; // アニメーションから自動取得
    private float nextAttackPossibleTime = 0f; // 次に攻撃可能になる時間
    private float verticalAdjustSpeed = 5.0f; // 地面から抜け出す速度
    private float vx = 0;
    private bool rightFlag = false;

    // --- スタック検出用変数 ---
    private Vector2 lastCheckedPosition;
    private float timeStuck = 0f;
    private float timeToReverseWhenStuck = 2.0f;
    private float stuckDistanceThreshold = 0.1f;

    // --- プロパティ ---
    private bool isOverlappingGround =>
        Physics2D.OverlapCircle(
            overlapCheckPoint != null ? overlapCheckPoint.position : transform.position,
            overlapCheckRadius,
            groundLayer
        );

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // レイヤーマスクの取得
        groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );

        // コンポーネントの取得
        if (activator == null)
        {
            activator = GetComponentInParent<EnemyActivator>();
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        rbody = GetComponent<Rigidbody2D>();
        sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
        contactDamageController = GetComponent<ContactDamageController>();
        _animator = GetComponent<Animator>();
        enemyHP = GetComponent<EnemyHealth>();

        // エラーチェックと初期設定
        ValidateComponents();
        CalculateAttackAnimationTime();
    }

    private void Start()
    {
        ResetState();
    }

    private void FixedUpdate()
    {
        // ポーズ状態の確認
        if (TimeManager.instance.isEnemyMovePaused)
        {
            PauseMovement();
            return;
        }
        else
        {
            ResumeMovement();
        }

        // 移動状態以外は慣性を殺して停止させる（落下は許可する）
        if (currentState != GolemState.Moving)
        {
            if (currentState != GolemState.AdjustingPosition)
            {
                rbody.velocity = new Vector2(0, rbody.velocity.y);
            }
            return;
        }

        // 移動処理および攻撃判定の更新
        UpdateMovementAndDetectPlayer();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentState != GolemState.Moving)
            return;

        // 衝突相手がGroundLayerかチェック
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            foreach (ContactPoint2D contact in collision.contacts)
            {
                // 法線ベクトルのy成分が小さければ横からの衝突（壁）とみなす
                if (Mathf.Abs(contact.normal.y) < 0.1f)
                {
                    ReverseDirection();
                    return;
                }
            }
        }
    }

    #endregion

    #region Initialization / Reset

    /// <summary>
    /// 必要なコンポーネントが揃っているか確認し、警告を出します。
    /// </summary>
    private void ValidateComponents()
    {
        if (enemyHP == null)
        {
            Debug.LogError($"{this.gameObject.name}にEnemyHealthコンポーネントがありません。");
        }

        if (overlapCheckPoint == null)
        {
            Debug.LogWarning(
                $"{this.gameObject.name}にoverlapCheckPointが設定されていません。自身の位置を使用します。"
            );
        }
    }

    /// <summary>
    /// Animatorから攻撃アニメーションの長さを自動取得します。
    /// </summary>
    private void CalculateAttackAnimationTime()
    {
        bool foundAttackClip = false;

        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == ATTACK_ANIMATION_CLIP_NAME)
                {
                    attackAnimationTime = clip.length;
                    foundAttackClip = true;
                    break;
                }
            }
        }

        if (!foundAttackClip)
        {
            Debug.LogWarning(
                $"{this.name}のAnimatorに攻撃アニメーション({ATTACK_ANIMATION_CLIP_NAME})が見つかりませんでした。デフォルト値を使用します。"
            );
        }
    }

    /// <summary>
    /// 状態を初期化し、移動範囲の計算や配置調整を行います。
    /// </summary>
    public void ResetState()
    {
        // プレイヤーのTransformを取得
        if (playerTransform == null)
        {
            playerTransform = GameObject
                .FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME)
                ?.transform;
        }

        // コンポーネントの状態リセット
        if (enemyHP != null)
        {
            enemyHP.ResetState();
        }
        contactDamageController?.SetNormalDamage(damage);

        if (rbody != null)
        {
            rbody.simulated = true;
            rbody.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        tag = GameConstants.IMMUNE_ENEMY_TAG_NAME;
        StopAllCoroutines(); // 実行中の攻撃やスタック検知をリセット

        // 移動範囲の設定
        SetupMovementBounds();

        // 初期位置をランダムに設定
        Vector3 startPos = transform.position;
        float randomX = Random.Range(leftBound, rightBound);
        transform.position = new Vector2(randomX, startPos.y);

        // 初期向きの設定
        rightFlag = (Random.value > 0.5f);
        ApplyFacingDirection();
        vx = speedX * (rightFlag ? 1 : -1);

        // 攻撃可能時間をリセット
        nextAttackPossibleTime = Time.time;

        // スタック検出用変数の初期化
        lastCheckedPosition = transform.position;
        timeStuck = 0f;

        // 埋まりチェックと移動開始コルーチンの起動
        StartCoroutine(CheckAndAdjustPosition());
        StartCoroutine(CheckIfStuckCoroutine());
    }

    /// <summary>
    /// 外部から移動範囲を指定して状態をリセットします。
    /// </summary>
    /// <param name="minX">左端のX座標</param>
    /// <param name="maxX">右端のX座標</param>
    public void ResetStateWithBounds(float minX, float maxX)
    {
        isUseManualBounds = true;
        leftBound = Mathf.Min(minX, maxX);
        rightBound = Mathf.Max(minX, maxX);
        ResetState();
    }

    /// <summary>
    /// 手動設定が有効でない場合、EnemyActivatorの範囲を元に移動範囲を自動計算します。
    /// </summary>
    private void SetupMovementBounds()
    {
        if (activator != null && !isUseManualBounds)
        {
            var activatorCollider = activator.GetComponent<Collider2D>();
            if (activatorCollider != null)
            {
                float activatorLeftBound = activatorCollider.bounds.min.x;
                float activatorRightBound = activatorCollider.bounds.max.x;
                float randomCenter = Random.Range(activatorLeftBound, activatorRightBound);

                leftBound = randomCenter - MOVE_RANGE / 2f;
                rightBound = randomCenter + MOVE_RANGE / 2f;

                // アクティベーターの範囲を超えないようにクランプ
                leftBound = Mathf.Max(leftBound, activatorLeftBound);
                rightBound = Mathf.Min(rightBound, activatorRightBound);

                // 範囲が狭すぎる場合は調整して最低限の幅を確保
                if (rightBound - leftBound < MOVE_RANGE)
                {
                    if (leftBound == activatorLeftBound)
                    {
                        rightBound = Mathf.Min(activatorRightBound, leftBound + MOVE_RANGE);
                    }
                    else
                    {
                        leftBound = Mathf.Max(activatorLeftBound, rightBound - MOVE_RANGE);
                    }
                }
            }
        }
    }

    #endregion

    #region Movement & Update

    /// <summary>
    /// 物理演算とアニメーションを一時停止します。
    /// </summary>
    private void PauseMovement()
    {
        if (rbody.simulated)
        {
            rbody.simulated = false;
        }
        if (_animator.speed > 0)
        {
            _animator.speed = 0; // アニメーションも一時停止
        }
    }

    /// <summary>
    /// 物理演算とアニメーションを再開します。
    /// </summary>
    private void ResumeMovement()
    {
        if (!rbody.simulated)
        {
            rbody.simulated = true;
        }
        if (_animator.speed == 0)
        {
            _animator.speed = 1;
        }
    }

    /// <summary>
    /// 移動処理を行い、崖や壁を検知し、プレイヤーが攻撃範囲にいるかチェックします。
    /// </summary>
    private void UpdateMovementAndDetectPlayer()
    {
        Vector2 currentPos = transform.position;

        // 1. 移動範囲の端に到達したかチェック
        bool hasReachedBound = (currentPos.x <= leftBound && vx < 0) || 
                               (rightBound <= currentPos.x && vx > 0);

        // 2. 崖っぷち（前方の足元に地面がない）かチェック
        float checkOffsetX = rightFlag ? cliffCheckOffsetX : -cliffCheckOffsetX;
        Vector2 cliffCheckOrigin = new Vector2(currentPos.x + checkOffsetX, currentPos.y);
        bool isCliff = !Physics2D.Raycast(cliffCheckOrigin, Vector2.down, cliffCheckRayLength, groundLayer);

        // 境界到達または崖なら反転
        if (hasReachedBound || isCliff)
        {
            ReverseDirection();
        }

        // 速度を適用
        rbody.velocity = new Vector2(vx, rbody.velocity.y);

        // 3. プレイヤー感知チェック
        if (playerTransform != null && Time.time >= nextAttackPossibleTime)
        {
            Vector2 directionToPlayer = (Vector2)playerTransform.position - currentPos;

            // プレイヤーが前方の攻撃範囲内にいるか、かつ高さが大きく違わないか
            float horizontalDistance = directionToPlayer.x * (rightFlag ? 1 : -1);
            bool isInRangeX = horizontalDistance <= attackRangeX && horizontalDistance >= 0;
            bool isInRangeY = Mathf.Abs(directionToPlayer.y) < attackRangeY;

            if (isInRangeX && isInRangeY)
            {
                StartCoroutine(AttackSequenceCoroutine());
            }
        }
    }

    /// <summary>
    /// 移動方向を反転させます。
    /// </summary>
    private void ReverseDirection()
    {
        rightFlag = !rightFlag;
        vx = speedX * (rightFlag ? 1 : -1);
        ApplyFacingDirection();
    }

    /// <summary>
    /// rightFlag に基づいてスプライト(オブジェクトのスケール)の向きを更新します。
    /// </summary>
    private void ApplyFacingDirection()
    {
        Vector3 currentScale = transform.localScale;
        currentScale.x = Mathf.Abs(currentScale.x) * (rightFlag ? 1 : -1);
        transform.localScale = currentScale;
    }

    /// <summary>
    /// 配置時の埋まりチェックと位置調整を行うコルーチン。
    /// </summary>
    private IEnumerator CheckAndAdjustPosition()
    {
        if (isOverlappingGround)
        {
            currentState = GolemState.AdjustingPosition;
            rbody.simulated = false;

            // 埋まっている間、少しずつ上に移動させる
            while (isOverlappingGround)
            {
                transform.position += new Vector3(0, verticalAdjustSpeed * Time.deltaTime, 0);
                yield return null;
            }

            rbody.simulated = true;
        }

        // 調整完了後に歩行状態へ移行
        currentState = GolemState.Moving;
        _animator.SetBool("Walk", true);
    }

    #endregion

    #region Attack Logic

    /// <summary>
    /// 攻撃の一連のシーケンスを管理するコルーチン。
    /// </summary>
    private IEnumerator AttackSequenceCoroutine()
    {
        // --- 1. 攻撃前待機 ---
        currentState = GolemState.PreparingToAttack;
        rbody.velocity = new Vector2(0, rbody.velocity.y); // 移動停止
        _animator.SetBool("Walk", false); // アイドルアニメーションへ

        tag = GameConstants.DAMAGEABLE_ENEMY_TAG_NAME; // 攻撃中はダメージが通るようにする

        yield return new WaitForSeconds(beforeAttackTime);

        // --- 2. 攻撃アクション ---
        currentState = GolemState.Attacking;
        _animator.SetTrigger("AttackTrigger");

        // TODO: 攻撃のタイミングに合わせてSEを鳴らす場合はここで再生
        // if (sePlayer != null) sePlayer.Play(SE_EnemyAction.Attack_Golem);

        // アニメーションの長さ分待機
        yield return new WaitForSeconds(attackAnimationTime);

        // --- 3. 攻撃後待機 ---
        currentState = GolemState.AfterAttackDelay;
        yield return new WaitForSeconds(afterAttackTime);

        // --- 4. 復帰と歩行再開 ---
        tag = GameConstants.IMMUNE_ENEMY_TAG_NAME; // 無敵状態に戻す
        nextAttackPossibleTime = Time.time + attackCooldownTime; // クールダウン設定

        currentState = GolemState.Moving;
        _animator.SetBool("Walk", true); // 歩行アニメーション再開

        // 再開時にすぐに方向を更新
        vx = speedX * (rightFlag ? 1 : -1);
    }

    #endregion

    #region Physics & Utility

    /// <summary>
    /// 動けていない状態が続いたら強制的に反転させるコルーチン。
    /// 壁に引っかかっている場合などのフェイルセーフとして機能します。
    /// </summary>
    private IEnumerator CheckIfStuckCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(STUCK_CHECK_INTERVAL);

            // 動ける状態でない場合、またはポーズ中はタイマーをリセットしてスキップ
            if (currentState != GolemState.Moving || TimeManager.instance.isEnemyMovePaused)
            {
                timeStuck = 0f;
                lastCheckedPosition = transform.position;
                continue;
            }

            float distanceMoved = Vector2.Distance(transform.position, lastCheckedPosition);

            if (distanceMoved < stuckDistanceThreshold)
            {
                timeStuck += STUCK_CHECK_INTERVAL;
            }
            else
            {
                timeStuck = 0f;
            }

            lastCheckedPosition = transform.position;

            // 一定時間スタックしていたら反転
            if (timeStuck >= timeToReverseWhenStuck)
            {
                timeStuck = 0f;
                ReverseDirection();
            }
        }
    }

    #endregion

    #region Debug / Editor

    private void OnDrawGizmos()
    {
        // 1. 移動範囲の描画
        Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
        Vector3 center = new Vector3(
            (leftBound + rightBound) / 2f,
            transform.position.y + 2.0f, // ゴーレムの中心が地面からどれくらいの高さにあるかに応じて調整
            transform.position.z
        );
        Vector3 size = new Vector3(Mathf.Abs(rightBound - leftBound), 4.5f, 0.1f);
        Gizmos.DrawCube(center, size);

        // 2. 攻撃範囲の描画
        Gizmos.color = new Color(0f, 0f, 1f, 0.3f);
        float attackCenterX = transform.position.x + (rightFlag ? 1 : -1) * (attackRangeX / 2);
        Vector3 attackCenter = new Vector3(
            attackCenterX,
            transform.position.y + 2.0f, // ゴーレムの中心高さ
            transform.position.z
        );
        Vector3 attackSize = new Vector3(attackRangeX, attackRangeY, 0.1f);
        Gizmos.DrawCube(attackCenter, attackSize);

        // 3. 崖っぷち判定用のRaycast描画
        Gizmos.color = Color.yellow;
        float cliffX = transform.position.x + (rightFlag ? cliffCheckOffsetX : -cliffCheckOffsetX);
        Vector2 cliffCheckOrigin = new Vector2(cliffX, transform.position.y);
        Gizmos.DrawLine(cliffCheckOrigin, cliffCheckOrigin + Vector2.down * cliffCheckRayLength);

        // 4. 埋まり検知用の円描画
        if (overlapCheckPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(overlapCheckPoint.position, overlapCheckRadius);
        }
        else
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, overlapCheckRadius);
        }
    }

    #endregion
}
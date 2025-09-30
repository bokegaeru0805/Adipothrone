using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SlimeNormalMoveController : MonoBehaviour, IEnemyResettable
{
    private const float MOVE_RANGE = 10.0f; // ランダムに設定する場合の移動幅

    [Header("敵のタイプ")]
    [SerializeField]
    private EnemyVariant variantType = EnemyVariant.None;

    [Header("設定項目")]
    [SerializeField]
    private Transform playerTransform = null; // PlayerのTransform

    [SerializeField]
    private EnemyActivator activator = null; // 親のEnemyActivatorコンポーネント

    [Header("移動・攻撃の基本設定")]
    [SerializeField]
    private float speedX = 4.0f;

    [SerializeField]
    private float attackRange = 1.5f;

    [SerializeField]
    private float jumpPower = 1.0f;

    [SerializeField]
    private float jumpChargeTime = 0.5f; // ジャンプ前の溜め時間 (秒)

    [Header("必要ならば設定")]
    [SerializeField]
    private float leftBound;

    [SerializeField]
    private float rightBound;

    [Header("地面判定用の設定")]
    [SerializeField]
    private Transform groundCheck;

    [SerializeField]
    private float groundCheckRadius = 0.2f;

    [SerializeField]
    private LayerMask GroundLayer;

    [Header("配置調整用の設定")]
    [SerializeField]
    private Transform overlapCheckPoint; // 地面に埋まっていないかチェックするTransform

    [SerializeField]
    private float overlapCheckRadius = 0.5f; // チェック用円の半径

    // 敵の種類を定義
    private enum EnemyVariant
    {
        None = 0,
        Chapter1 = 1,
    }

    private int damage = 0; // 攻撃力
    private float verticalAdjustSpeed = 100f; // 地面から抜け出す速度
    private float vx = 0;
    private float groundIgnoreAfterJumpTime = 0.1f;
    private float jumpStartTime;
    private float timeToReverseWhenStuck = 2.0f; //動けないと判断してから反転するまでの時間（秒）
    private float stuckDistanceThreshold = 0.1f; //動いていると判断する最低限の移動距離
    private bool isGrounded =>
        Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, GroundLayer);

    //埋まり判定用のbool
    private bool isOverlappingGround =>
        Physics2D.OverlapCircle(overlapCheckPoint.position, overlapCheckRadius, GroundLayer);

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rbody;
    private Animator animator;
    private EnemyHealth enemyHP;
    private int IdleHash;
    private AnimatorStateInfo stateInfo;
    private ContactDamageController contactDamageController;

    // スタック検出用の変数
    private Vector2 lastCheckedPosition;
    private float timeStuck = 0f;
    private const float STUCK_CHECK_INTERVAL = 0.5f; // 位置を確認する間隔（秒）

    private enum SlimeState
    {
        None,
        Idle,
        Moving,
        PreparingToJump,
        Jumping,
        Recovering,
        AdjustingPosition,
    }

    private SlimeState currentState = SlimeState.Idle;
    private bool rightFlag = false;
    private bool isUseAutoBounds = false; // 行動範囲自動設定モードかどうか
    private Vector2 pos = Vector2.zero;

    private void Awake()
    {
        switch (variantType)
        {
            case EnemyVariant.Chapter1:
                damage = 23;
                break;
            default:
                Debug.LogError($"{this.name}のEnemyVariantが設定されていません。");
                break;
        }

        if (groundCheck == null || GroundLayer == 0 || groundCheckRadius <= 0)
        {
            Debug.LogError($"{this.name}の地面判定用の設定が正しくありません。");
        }

        if (overlapCheckPoint == null)
        {
            Debug.LogError($"{this.name}の埋まり判定用のTransformが設定されていません。");
        }

        if (activator == null)
        {
            activator = GetComponentInParent<EnemyActivator>();
            if (activator == null)
            {
                Debug.LogWarning(
                    $"{this.name}の親にEnemyActivatorが見つかりませんでした。移動範囲の自動設定は行いません。"
                );
            }
        }

        spriteRenderer = this.GetComponent<SpriteRenderer>();
        rbody = GetComponent<Rigidbody2D>(); //Rigidbody2Dコンポーネントを取得

        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError($"{this.gameObject.name}にAnimatorコンポーネントがありません。");
            return;
        }

        enemyHP = this.GetComponent<EnemyHealth>();
        {
            if (enemyHP == null)
            {
                Debug.LogError($"{this.gameObject.name}にEnemyHealthコンポーネントがありません。");
                return;
            }
        }

        // 自動設定モードかどうかを判定
        // 境界値が両方とも設定されていない場合、自動モードを有効にする
        isUseAutoBounds = leftBound == 0 && rightBound == 0;
    }

    private void Start()
    {
        IdleHash = Animator.StringToHash("Blue Idle - Animation");

        contactDamageController = GetComponent<ContactDamageController>();
        contactDamageController?.SetDamageAmount(damage);

        ResetState();
    }

    public void ResetState()
    {
        if (playerTransform == null)
        {
            playerTransform = GameObject
                .FindGameObjectWithTag(GameConstants.PlayerTagName)
                ?.transform;
            if (playerTransform == null)
            {
                Debug.LogError($"{this.name}はPlayerTransformを見つけられませんでした");
            }
        }

        if (enemyHP != null)
        {
            // 自分のHPをリセット
            enemyHP.ResetState();
        }
        else
        {
            Debug.LogWarning($"{this.gameObject.name}にenemy_HPコンポーネントがありません。");
        }

        vx = (Random.value < 0.5f ? -1 : 1) * speedX;
        rightFlag = vx > 0;
        spriteRenderer.flipX = !rightFlag;
        if (rbody != null)
        {
            rbody.velocity = new Vector2(vx, 0); // 初速を設定
            rbody.simulated = true; // 物理挙動を再起動
            rbody.constraints = RigidbodyConstraints2D.FreezeRotation; // 回転を停止する
        }
        else
        {
            Debug.LogError($"{this.gameObject.name}にRigidbody2Dコンポーネントがありません。");
            return;
        }

        tag = GameConstants.ImmuneEnemyTagName;
        currentState = SlimeState.Moving;

        // スタック検出用の変数を初期化
        lastCheckedPosition = transform.position;
        timeStuck = 0f;

        //スタック検出コルーチンを開始
        StartCoroutine(CheckIfStuckCoroutine());

        // leftBoundとrightBoundが共に0の場合、ランダムに範囲を設定
        if (activator != null)
        {
            if (isUseAutoBounds) // 自動設定モードの場合
            {
                // activatorが持つCollider2Dの境界を取得する
                var activatorCollider = activator.GetComponent<Collider2D>();
                if (activatorCollider != null)
                {
                    // Colliderのワールド空間での左端と右端を取得
                    float activatorLeftBound = activatorCollider.bounds.min.x;
                    float activatorRightBound = activatorCollider.bounds.max.x;

                    // アクティベーターの検出範囲内でランダムな中心位置を決定
                    float randomCenter = Random.Range(activatorLeftBound, activatorRightBound);

                    // 中心から移動幅(MOVE_RANGE)を基に境界を計算
                    leftBound = randomCenter - MOVE_RANGE / 2f;
                    rightBound = randomCenter + MOVE_RANGE / 2f;

                    // 計算された境界がアクティベーターの範囲を超えないようにクランプ
                    leftBound = Mathf.Max(leftBound, activatorLeftBound);
                    rightBound = Mathf.Min(rightBound, activatorRightBound);

                    // 範囲が狭すぎる場合は調整
                    if (rightBound - leftBound < MOVE_RANGE)
                    {
                        // 範囲が狭い場合は、片方の境界を再調整して最低限の幅を確保
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
        else // activaterが見つからない場合
        {
            Debug.LogWarning(
                $"{this.name}の親にEnemyActivatorが見つかりませんでした。移動範囲の自動設定は行いません。"
            );
        }

        // 初期位置を移動範囲内のランダムな位置に設定
        Vector3 startPos = transform.position;
        transform.position = new Vector2(Random.Range(leftBound, rightBound), startPos.y);

        // 配置時に地面に埋まっていないかチェックし、調整
        StartCoroutine(CheckAndAdjustPosition());
    }

    // 配置時の埋まりチェックと位置調整コルーチン
    private IEnumerator CheckAndAdjustPosition()
    {
        // 重なっている間、上に移動
        if (isOverlappingGround)
        {
            currentState = SlimeState.AdjustingPosition; // ステートを位置調整中に
            rbody.simulated = false; // 物理演算を一時停止して手動で移動

            // 重なりがなくなるまで上に移動
            while (isOverlappingGround)
            {
                transform.position += new Vector3(0, verticalAdjustSpeed * Time.deltaTime, 0);
                yield return null;
            }

            // 位置調整が完了したら、物理演算を再開し、元のステートに戻す
            rbody.simulated = true;
            currentState = SlimeState.Moving;
        }

        animator?.Play("Blue Idle - Animation"); // アイドルアニメーションを強制再生
    }

    private void FixedUpdate()
    {
        if (playerTransform == null)
            return;

        // 位置調整中は他の物理演算や移動ロジックを停止
        if (currentState == SlimeState.AdjustingPosition)
        {
            return;
        }

        //敵の動きがポーズされているかどうかを確認
        // もしポーズされていればRigidbody2Dを無効化する
        if (TimeManager.instance.isEnemyMovePaused)
        {
            if (rbody.simulated)
                rbody.simulated = false;
            return;
        }
        else if (!rbody.simulated)
            rbody.simulated = true;

        pos = transform.position;
        Vector3 dir = (Vector2)playerTransform.position - pos;

        switch (currentState)
        {
            case SlimeState.Moving:
                if ((pos.x <= leftBound && vx <= 0) || (rightBound <= pos.x && 0 <= vx))
                {
                    rightFlag = !rightFlag;
                    vx = speedX * (rightFlag ? 1 : -1);
                    spriteRenderer.flipX = !rightFlag;
                }
                rbody.velocity = new Vector2(vx, rbody.velocity.y);

                bool inRange =
                    dir.x * (rightFlag ? 1 : -1) <= attackRange
                    && dir.x * (rightFlag ? 1 : -1) >= 0;
                if (inRange)
                {
                    // プレイヤーが攻撃範囲に入ったら、溜めステートに移行
                    currentState = SlimeState.PreparingToJump;
                    rbody.velocity = Vector2.zero; // 移動を停止
                    // 溜めアニメーションのトリガーを引く（必要に応じて）
                    // animator.SetTrigger("chargeTriggered");
                    StartCoroutine(JumpChargeCoroutine());
                }
                break;

            case SlimeState.PreparingToJump:
                // コルーチンが完了するまで待機
                break;

            case SlimeState.Jumping:
                if (
                    rbody.velocity.y <= 0
                    && isGrounded
                    && (Time.time - jumpStartTime > groundIgnoreAfterJumpTime)
                )
                {
                    currentState = SlimeState.Recovering;
                }
                break;

            case SlimeState.Recovering:
                stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                tag = GameConstants.ImmuneEnemyTagName;
                if (stateInfo.shortNameHash == IdleHash)
                {
                    currentState = SlimeState.Moving;
                }
                break;
        }

        animator?.SetBool("isGrounded", isGrounded);
        animator?.SetFloat("verticalSpeed", rbody.velocity.y);
    }

    //オブジェクトがColliderにぶつかった時の処理
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 衝突した相手がGroundLayerに含まれているか確認
        if (((1 << collision.gameObject.layer) & GroundLayer) != 0)
        {
            // 衝突点の法線ベクトルをチェックして、横方向からの衝突を判定
            // (法線ベクトルのy成分がほぼ0であれば横方向の衝突とみなす)
            foreach (ContactPoint2D contact in collision.contacts)
            {
                // 法線ベクトルのy成分の絶対値が小さい（水平に近い）かを判定
                if (Mathf.Abs(contact.normal.y) < 0.1f) // 0.1fは許容誤差。必要に応じて調整
                {
                    // 横方向の衝突であれば、移動方向を反転させる
                    rightFlag = !rightFlag;
                    vx = speedX * (rightFlag ? 1 : -1);
                    spriteRenderer.flipX = !rightFlag;

                    // 衝突後の滑り落ちを防ぐために速度をリセット（任意）
                    rbody.velocity = new Vector2(vx, rbody.velocity.y);

                    // 処理を抜ける
                    return;
                }
            }
        }
    }

    // ジャンプ前の溜めを行うコルーチン
    private IEnumerator JumpChargeCoroutine()
    {
        // 溜め中はダメージを受けない敵の状態に
        tag = GameConstants.ImmuneEnemyTagName;

        // 指定された溜め時間待機
        yield return new WaitForSeconds(jumpChargeTime);

        // 待機中にステートが変わっていないか確認
        if (currentState == SlimeState.PreparingToJump)
        {
            // ステートをJumpingに移行
            currentState = SlimeState.Jumping;
            jumpStartTime = Time.time;
            tag = GameConstants.DamageableEnemyTagName;

            // ジャンプアニメーションのトリガーを引く
            animator.SetTrigger("jumpTriggered");

            if (playerTransform != null)
            {
                Vector2 dir = playerTransform.position - transform.position;
                vx = Mathf.Sign(dir.x) * speedX;
                rbody.AddForce(new Vector2(vx, jumpPower), ForceMode2D.Impulse);
                SEManager.instance?.PlayEnemyActionSE(SE_EnemyAction.Attack_slime1); // ジャンプ攻撃の効果音を鳴らす
            }
            else
            {
                rbody.AddForce(new Vector2(0, jumpPower), ForceMode2D.Impulse);
            }

            // ジャンプ後のリカバリー待機コルーチンを開始
            StartCoroutine(JumpInterval());
        }
    }

    private IEnumerator JumpInterval()
    {
        yield return new WaitUntil(() => currentState == SlimeState.Recovering);
    }

    /// <summary>
    /// 一定間隔でオブジェクトの位置をチェックし、動けていない状態が続いたら反転させるコルーチン。
    /// </summary>
    private IEnumerator CheckIfStuckCoroutine()
    {
        // このオブジェクトが存在する限りループ
        while (true)
        {
            // 指定した間隔で待機
            yield return new WaitForSeconds(STUCK_CHECK_INTERVAL);

            // 敵が移動状態でない場合や、ポーズ中はタイマーをリセットして次のチェックへ
            if (currentState != SlimeState.Moving || TimeManager.instance.isEnemyMovePaused)
            {
                timeStuck = 0f;
                lastCheckedPosition = transform.position;
                continue;
            }

            // 前回チェックした位置からの移動距離を計算
            float distanceMoved = Vector2.Distance(transform.position, lastCheckedPosition);

            // ほとんど動いていない場合
            if (distanceMoved < stuckDistanceThreshold)
            {
                // 動かなかった時間を加算
                timeStuck += STUCK_CHECK_INTERVAL;
            }
            else // 十分に動いている場合
            {
                // タイマーをリセット
                timeStuck = 0f;
            }

            // 現在の位置を新しいチェックポイントとして記録
            lastCheckedPosition = transform.position;

            // 動けない状態が指定した時間を超えたら、強制的に反転
            if (timeStuck >= timeToReverseWhenStuck)
            {
                // タイマーをリセット
                timeStuck = 0f;

                // 移動方向を反転
                rightFlag = !rightFlag;
                vx = speedX * (rightFlag ? 1 : -1);
                spriteRenderer.flipX = !rightFlag;
                if (rbody != null)
                {
                    rbody.velocity = new Vector2(vx, rbody.velocity.y);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        // 埋まりチェック用のGizmosも表示
        if (overlapCheckPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(overlapCheckPoint.position, overlapCheckRadius);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
        Vector3 center = new Vector3(
            (leftBound + rightBound) / 2f,
            transform.position.y,
            transform.position.z
        );
        Vector3 size = new Vector3(rightBound - leftBound, 2f, 0.1f);
        Gizmos.DrawCube(center, size);
    }
}

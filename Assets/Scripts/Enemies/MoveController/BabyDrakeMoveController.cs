using System.Collections;
using NaughtyAttributes;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class BabyDrakeMoveController : MonoBehaviour, IEnemyResettable
{
    private const float MOVE_RANGE = 10.0f; // ランダムに設定する場合の移動幅

    [Header("敵のタイプ")]
    [SerializeField]
    private EnemyVariant variantType = EnemyVariant.None;

    [Header("設定項目")]
    [SerializeField]
    private Transform playerTransform = null;

    [SerializeField]
    private EnemyActivator activator = null; // 親のEnemyActivatorコンポーネント

    [Header("移動・攻撃の基本設定")]
    [SerializeField]
    private float speedX = 4.0f;

    [SerializeField]
    private float attackRange = 1.5f;

    [Header("待機・移動時間の設定")]
    [SerializeField]
    [Tooltip("待機時間の最小値（秒）")]
    private float minIdleTime = 1.0f;

    [SerializeField]
    [Tooltip("待機時間の最大値（秒）")]
    private float maxIdleTime = 3.0f;

    [SerializeField]
    [Tooltip("移動時間の最小値（秒）")]
    private float minMoveTime = 2.0f;

    [SerializeField]
    [Tooltip("移動時間の最大値（秒）")]
    private float maxMoveTime = 5.0f;

    [SerializeField]
    [Tooltip("ジャンプ前の溜め時間（秒）")]
    private float jumpChargeTime = 0.5f;

    [Header("移動範囲の設定")]
    [SerializeField]
    [Tooltip("手動で移動範囲を設定するかどうか")]
    private bool isUseManualBounds = false;

    [SerializeField, ShowIf(nameof(isUseManualBounds))]
    private float leftBound;

    [SerializeField, ShowIf(nameof(isUseManualBounds))]
    private float rightBound;

    [Header("配置調整用の設定")]
    [SerializeField]
    private Transform overlapCheckPoint; // 地面に埋まっていないかチェックするTransform

    [SerializeField]
    private float overlapCheckRadius = 0.5f; // チェック用円の半径

    // 敵の種類を定義
    private enum EnemyVariant
    {
        None = 0,
        Desert = 1,
    }

    private int damage = 0; // 攻撃力
    private float verticalAdjustSpeed = 100f; // 地面から抜け出す速度
    private float vx = 0;
    private float groundIgnoreAfterJumpTime = 0.1f;
    private float jumpStartTime;
    private float timeToReverseWhenStuck = 2.0f; //動けないと判断してから反転するまでの時間（秒）
    private float stuckDistanceThreshold = 0.1f; //動いていると判断する最低限の移動距離
    private LayerMask GroundLayer;

    //埋まり判定用のbool
    private bool isOverlappingGround =>
        Physics2D.OverlapCircle(overlapCheckPoint.position, overlapCheckRadius, GroundLayer);

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rbody;
    private Animator animator;
    private EnemyHealth enemyHP;
    private ContactDamageController contactDamageController;
    private CriWare.Assets.CriAtomSePlayer sePlayer;

    // スタック検出用の変数
    private Vector2 lastCheckedPosition;
    private float timeStuck = 0f;
    private const float STUCK_CHECK_INTERVAL = 0.5f; // 位置を確認する間隔（秒）

    // 状態切り替え用のタイマー変数
    private float stateChangeTimer = 0f;
    private float currentStateDuration = 0f;

    private enum DrakeState
    {
        Idle,
        Moving,
        PreparingToJump,
        Jumping,
        Recovering,
        Diving,
        AdjustingPosition,
    }

    private DrakeState currentState = DrakeState.Idle;
    private bool rightFlag = false;
    private Vector2 pos = Vector2.zero;

    private void Awake()
    {
        GroundLayer = LayerMask.GetMask(GameConstants.PhysicsLayerName_Ground); // Groundレイヤーを取得

        switch (variantType)
        {
            case EnemyVariant.Desert:
                //TODO:攻撃力を設定
                // damage = 23;
                break;
            default:
                Debug.LogError($"{this.name}のEnemyVariantが設定されていません。");
                break;
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
        rbody = GetComponent<Rigidbody2D>();
        sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
        animator = GetComponent<Animator>();

        enemyHP = this.GetComponent<EnemyHealth>();
        {
            if (enemyHP == null)
            {
                Debug.LogError($"{this.gameObject.name}にEnemyHealthコンポーネントがありません。");
                return;
            }
        }
    }

    private void Start()
    {
        contactDamageController = GetComponent<ContactDamageController>();
        if (contactDamageController != null)
        {
            contactDamageController?.SetNormalDamage(damage);
        }
        else
        {
            Debug.LogWarning(
                $"{this.gameObject.name}にContactDamageControllerコンポーネントがありません。"
            );
        }

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
                return;
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
        spriteRenderer.flipX = rightFlag;
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

        tag = "Untagged"; // タグをリセット
        // 初期状態をMovingにし、タイマーを設定
        currentState = DrakeState.Moving;
        SetNextStateDuration();

        // スタック検出用の変数を初期化
        lastCheckedPosition = transform.position;
        timeStuck = 0f;

        //スタック検出コルーチンを開始
        StartCoroutine(CheckIfStuckCoroutine());

        // leftBoundとrightBoundが共に0の場合、ランダムに範囲を設定
        if (activator != null)
        {
            if (!isUseManualBounds) // 自動設定モードの場合
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

    /// <summary>
    /// 配置時に地面に埋まっている場合、埋まらない位置まで座標を上方向に調整するコルーチン。
    /// </summary>
    private IEnumerator CheckAndAdjustPosition()
    {
        // 重なっている間、上に移動
        if (isOverlappingGround)
        {
            currentState = DrakeState.AdjustingPosition; // ステートを位置調整中に
            rbody.simulated = false; // 物理演算を一時停止して手動で移動

            // 重なりがなくなるまで上に移動
            while (isOverlappingGround)
            {
                transform.position += new Vector3(0, verticalAdjustSpeed * Time.deltaTime, 0);
                yield return null;
            }

            // 位置調整が完了したら、物理演算を再開し、元のステートに戻す
            rbody.simulated = true;
            currentState = DrakeState.Moving;
        }

        animator?.SetTrigger("HideTrigger"); // 埋まり調整後にHideアニメーションを再生
    }

    private void FixedUpdate()
    {
        if (playerTransform == null)
            return;

        // 位置調整中は他の物理演算や移動ロジックを停止
        if (currentState == DrakeState.AdjustingPosition)
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

        // 状態切り替えのロジック (Idle <-> Moving)
        if (currentState == DrakeState.Idle || currentState == DrakeState.Moving)
        {
            stateChangeTimer += Time.deltaTime;
            if (stateChangeTimer >= currentStateDuration)
            {
                ToggleState();
            }
        }

        pos = transform.position;
        Vector3 dir = (Vector2)playerTransform.position - pos;

        switch (currentState)
        {
            case DrakeState.Idle:
                rbody.velocity = Vector2.zero;
                // Idle中はプレイヤーとの距離チェックを行い、攻撃範囲に入ったら即座に攻撃へ移行
                if (IsPlayerInAttackRange(dir))
                {
                    StartAttack();
                }
                break;
            case DrakeState.Moving:
                if ((pos.x <= leftBound && vx <= 0) || (rightBound <= pos.x && 0 <= vx))
                {
                    rightFlag = !rightFlag;
                    vx = speedX * (rightFlag ? 1 : -1);
                    spriteRenderer.flipX = rightFlag;
                }
                rbody.velocity = new Vector2(vx, rbody.velocity.y);

                if (IsPlayerInAttackRange(dir))
                {
                    StartAttack();
                }
                break;

            case DrakeState.PreparingToJump:
                // コルーチンが完了するまで待機
                break;

            case DrakeState.Jumping:
                if (Time.time - jumpStartTime > groundIgnoreAfterJumpTime)
                {
                    currentState = DrakeState.Recovering;
                }
                break;

            case DrakeState.Recovering:
                // 何もしない（JumpIntervalコルーチンに任せるため）
                break;

            case DrakeState.Diving:
                // アニメーション終了待ちのため、ここでは物理挙動以外の処理を行わない
                break;
        }
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
                    spriteRenderer.flipX = rightFlag;

                    // 衝突後の滑り落ちを防ぐために速度をリセット（任意）
                    rbody.velocity = new Vector2(vx, rbody.velocity.y);

                    // 処理を抜ける
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 次の状態（IdleまたはMoving）の継続時間をランダムに設定する
    /// </summary>
    private void SetNextStateDuration()
    {
        stateChangeTimer = 0f;
        if (currentState == DrakeState.Idle)
        {
            currentStateDuration = Random.Range(minIdleTime, maxIdleTime);
        }
        else if (currentState == DrakeState.Moving)
        {
            currentStateDuration = Random.Range(minMoveTime, maxMoveTime);
        }
    }

    /// <summary>
    /// IdleとMovingの状態を切り替える
    /// </summary>
    private void ToggleState()
    {
        if (currentState == DrakeState.Idle)
        {
            currentState = DrakeState.Moving;
            // 移動再開時に向きに応じた速度を再設定
            vx = speedX * (rightFlag ? 1 : -1);
            //TODO:アニメーションがあればここでWalkなどを再生
        }
        else if (currentState == DrakeState.Moving)
        {
            currentState = DrakeState.Idle;
            rbody.velocity = Vector2.zero;
            //TODO:アニメーションがあればここでIdleなどを再生
        }
        SetNextStateDuration();
    }

    /// <summary>
    /// プレイヤーが攻撃範囲内にいるか判定する
    /// </summary>
    private bool IsPlayerInAttackRange(Vector3 dir)
    {
        return dir.x * (rightFlag ? 1 : -1) <= attackRange && dir.x * (rightFlag ? 1 : -1) >= 0;
    }

    /// <summary>
    /// 攻撃動作を開始する
    /// </summary>
    private void StartAttack()
    {
        // プレイヤーが攻撃範囲に入ったら、溜めステートに移行
        currentState = DrakeState.PreparingToJump;
        rbody.velocity = Vector2.zero; // 移動を停止

        // 攻撃に入ったので、Idle/Movingの切り替えタイマーをリセット
        stateChangeTimer = 0f;

        StartCoroutine(JumpChargeCoroutine());
    }

    /// <summary>
    /// ジャンプ攻撃前の「溜め」動作を管理するコルーチン。
    /// 溜め時間の経過後、ジャンプステートへ移行し、プレイヤーに向かって跳躍します。
    /// </summary>
    private IEnumerator JumpChargeCoroutine()
    {
        // 溜め中はダメージを受けない敵の状態に
        tag = GameConstants.ImmuneEnemyTagName;

        // 指定された溜め時間待機
        yield return new WaitForSeconds(jumpChargeTime);

        // 待機中にステートが変わっていないか確認
        if (currentState == DrakeState.PreparingToJump)
        {
            // ステートをJumpingに移行
            currentState = DrakeState.Jumping;
            jumpStartTime = Time.time;
            tag = GameConstants.DamageableEnemyTagName;

            // ジャンプアニメーションのトリガーを引く
            animator.SetTrigger("JumpTrigger");

            //TODO:効果音の差し替えが必要
            //sePlayer.Play(SE_EnemyAction.Attack_slime1); // ジャンプ攻撃の効果音を鳴らす

            if (playerTransform != null)
            {
                rightFlag = playerTransform.position.x >= transform.position.x;
                spriteRenderer.flipX = rightFlag;
            }

            // ジャンプ後のリカバリー待機コルーチンを開始
            StartCoroutine(JumpInterval());
        }
    }

    /// <summary>
    /// ジャンプ後の着地、待機、ダイブ、移動再開までの一連の流れを管理するコルーチン
    /// </summary>
    private IEnumerator JumpInterval()
    {
        // Recovering（着地）状態になるまで待機
        yield return new WaitUntil(() => currentState == DrakeState.Recovering);

        tag = GameConstants.ImmuneEnemyTagName;

        float idleWaitTime = Random.Range(minIdleTime, maxIdleTime);
        yield return new WaitForSeconds(idleWaitTime);

        // DiveTriggerを実行
        animator.SetTrigger("DiveTrigger");
        currentState = DrakeState.Diving;

        // アニメーションの状態が切り替わるのを1フレーム待つ
        yield return null;

        // 現在のアニメーション（Dive）の長さを取得して待機
        // ※遷移中の場合は遷移先のステート（Dive）の情報を取得する
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (animator.IsInTransition(0))
        {
            stateInfo = animator.GetNextAnimatorStateInfo(0);
        }

        yield return new WaitForSeconds(stateInfo.length);

        // Moving状態に戻る
        currentState = DrakeState.Moving;
        this.tag = "Untagged";
        SetNextStateDuration();
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
            if (currentState != DrakeState.Moving
            //TODO:一時的にコメントアウト
            //|| TimeManager.instance.isEnemyMovePaused
            )
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
                spriteRenderer.flipX = rightFlag;
                if (rbody != null)
                {
                    rbody.velocity = new Vector2(vx, rbody.velocity.y);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // 埋まりチェック用のGizmosを表示
        if (overlapCheckPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(overlapCheckPoint.position, overlapCheckRadius);
        }
    }

    private void OnDrawGizmos()
    {
        // 移動範囲のGizmosを表示
        Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
        Vector3 center = new Vector3(
            (leftBound + rightBound) / 2f,
            transform.position.y + 1f,
            transform.position.z
        );
        Vector3 size = new Vector3(rightBound - leftBound, 2f, 0.1f);
        Gizmos.DrawCube(center, size);
    }
}

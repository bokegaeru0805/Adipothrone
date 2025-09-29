using System.Collections;
using UnityEngine;

/// <summary>
/// 地面を移動し、壁を登り、崖からは重力で落下するキャラクターコントローラー。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class RareEnemyMoveController : MonoBehaviour, IEnemyResettable
{
    private const float SPAWN_MARGIN = 10f; // スポーン時の端からのマージン

    [Header("設定項目")]
    [SerializeField]
    private Transform playerTransform = null; // PlayerのTransform

    [SerializeField]
    private EnemyActivator activator = null; // 親のEnemyActivatorコンポーネント

    [Header("移動設定")]
    [Tooltip("移動速度")]
    [SerializeField]
    private float moveSpeed = 3f;

    [Tooltip("壁を登れる高さの上限")]
    [SerializeField]
    private float maxClimbHeight = 5f;

    [Tooltip("キャラクターに適用される重力の強さ")]
    [SerializeField]
    private float gravityScale = 1f;

    [Tooltip("壁登り上限に達した後の、再登りまでのクールダウン時間（秒）")]
    [SerializeField]
    private float climbCooldown = 1.5f;

    [Header("AI設定")]
    [Tooltip("プレイヤーを検知して行動を開始する距離")]
    [SerializeField]
    private float detectionDistance = 8f;

    [Tooltip("この距離までプレイヤーから離れたら停止する")]
    [SerializeField]
    private float retreatDistance = 10f;

    [Header("地面判定用の設定")]
    [SerializeField]
    private Transform groundCheck;

    [SerializeField]
    private float groundCheckRadius = 0.2f;

    [Header("センサー設定")]
    [Tooltip("地面や壁として認識するレイヤー")]
    [SerializeField]
    private LayerMask groundLayer;

    [Tooltip("センサーの長さ")]
    [SerializeField]
    private float sensorLength = 0.6f; // キャラクターのサイズの半分より少し長いくらいが丁度いい

    [Header("配置調整用の設定")]
    [SerializeField]
    private Transform overlapCheckPoint; // 地面に埋まっていないかチェックするTransform

    [SerializeField]
    private float overlapCheckRadius = 0.5f; // チェック用円の半径

    private EnemyState currentState = EnemyState.Idle; // AIの状態を管理する変数
    private int moveDirection = 1; // 移動方向を動的に変更するための変数
    private float climbStartPositionY; // 壁を登り始めた地点のY座標
    private bool isClimbing = false; // 現在、壁を登っている最中かどうかのフラグ
    private bool isClimbOnCooldown = false; // 壁登りがクールダウン中かどうかのフラグ
    private float verticalAdjustSpeed = 100f; // 地面から抜け出す速度
    private bool hasBeenSeenByCamera = false; // カメラに一度でも映ったかを記録するフラグ
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Rigidbody2D rbody;
    private EnemyHealth enemyHP;

    //埋まり判定用のbool
    private bool isOverlappingGround =>
        Physics2D.OverlapCircle(overlapCheckPoint.position, overlapCheckRadius, groundLayer);

    // ========== 敵のAI状態を定義 ==========
    private enum EnemyState
    {
        Idle, // 待機状態
        Retreating // プレイヤーから退避している状態
        ,
    }

    private void Awake()
    {
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
        animator = this.GetComponent<Animator>();

        enemyHP = this.GetComponent<EnemyHealth>();
        {
            if (enemyHP == null)
            {
                Debug.LogError($"{this.gameObject.name}にEnemyHealthコンポーネントがありません。");
                return;
            }
        }

        tag = GameConstants.ImmuneEnemyTagName;
    }

    private void Start()
    {
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

        // 初期状態を待機に設定
        currentState = EnemyState.Idle;

        //初期状態では、アニメーターを停止
        if (animator != null)
        {
            animator.enabled = false;
        }

        hasBeenSeenByCamera = false; // カメラ視認フラグをリセット
        isClimbing = false;
        isClimbOnCooldown = false;

        // leftBoundとrightBoundが共に0の場合、ランダムに範囲を設定
        if (activator != null)
        {
            // activatorが持つCollider2Dの境界を取得する
            var activatorCollider = activator.GetComponent<Collider2D>();
            if (activatorCollider != null)
            {
                // Colliderのワールド空間での左端と右端を取得
                float activatorLeftBound = activatorCollider.bounds.min.x;
                float activatorRightBound = activatorCollider.bounds.max.x;

                // アクティベーターの検出範囲内でランダムな初期位置を決定
                float startPosX = Random.Range(
                    activatorLeftBound + SPAWN_MARGIN,
                    activatorRightBound - SPAWN_MARGIN
                );

                this.transform.position = new Vector2(startPosX, this.transform.position.y);
            }
        }
        else // activaterが見つからない場合
        {
            Debug.LogWarning(
                $"{this.name}の親にEnemyActivatorが見つかりませんでした。移動範囲の自動設定は行いません。"
            );
        }

        // 配置時に地面に埋まっていないかチェックし、調整
        StartCoroutine(CheckAndAdjustPosition());
    }

    // 配置時の埋まりチェックと位置調整コルーチン
    private IEnumerator CheckAndAdjustPosition()
    {
        // 重なっている間、上に移動
        if (isOverlappingGround)
        {
            rbody.simulated = false; // 物理演算を一時停止して手動で移動

            // 重なりがなくなるまで上に移動
            while (isOverlappingGround)
            {
                transform.position += new Vector3(0, verticalAdjustSpeed * Time.deltaTime, 0);
                yield return null;
            }

            // 位置調整が完了したら、物理演算を再開し、元のステートに戻す
            rbody.simulated = true;
        }
    }

    private void FixedUpdate()
    {
        if (playerTransform == null)
            return;

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

        // AIの状態を判断・更新する
        HandleAIState();

        // 2. 現在の状態に応じた行動を実行する
        switch (currentState)
        {
            case EnemyState.Idle:
                // 待機状態では、横方向の速度を0にしてその場に停止する
                rbody.velocity = new Vector2(0, rbody.velocity.y);
                break;

            case EnemyState.Retreating:
                // 退避状態では、壁登りや落下を含めた移動処理を行う
                PerformMovement();
                break;
        }
    }

    /// <summary>
    /// プレイヤーとの距離を計算し、AIの状態を決定する
    /// </summary>
    private void HandleAIState()
    {
        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        bool isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundLayer
        );

        if (currentState == EnemyState.Idle)
        {
            if (distanceToPlayer <= detectionDistance)
            {
                currentState = EnemyState.Retreating;
                if (animator != null)
                {
                    animator.enabled = true;
                }
            }
        }
        else if (currentState == EnemyState.Retreating)
        {
            if (distanceToPlayer >= retreatDistance && isGrounded)
            {
                currentState = EnemyState.Idle;
                if (animator != null)
                {
                    animator.enabled = false;
                }
            }
        }
    }

    /// <summary>
    /// キャラクターの移動処理（壁登り、歩行、落下）
    /// </summary>
    private void PerformMovement()
    {
        // --- プレイヤーの位置から逃げる方向を決定 ---
        moveDirection = (playerTransform.position.x > transform.position.x) ? -1 : 1;

        // (ここに元のFixedUpdateにあった移動・壁登り・落下のロジックが全て入る)
        bool isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundLayer
        );
        bool isHittingWall = Physics2D.Raycast(
            transform.position,
            Vector2.right * moveDirection,
            sensorLength,
            groundLayer
        );

        // --- 状況に応じた行動の決定 ---
        // 壁にヒットしていて、かつクールダウン中でないか？
        if (isHittingWall && !isClimbOnCooldown)
        {
            // --- 壁登り開始の検知 ---
            if (!isClimbing && isGrounded)
            {
                // このフレームで壁登りが始まった場合
                isClimbing = true;
                climbStartPositionY = transform.position.y; // 登り始めの高さを記録
            }

            // --- 登った高さを計算し、上限を超えていないかチェック ---
            float climbedHeight = transform.position.y - climbStartPositionY;

            if (climbedHeight >= maxClimbHeight)
            {
                // 上限に達したので、落下とクールダウンを開始する
                StartCoroutine(StartClimbCooldown());
            }

            if (isClimbing)
            {
                // 【壁を登る処理（上限に達していない場合）】
                rbody.gravityScale = 0;
                // まっすぐ上に登るようにする
                rbody.velocity = new Vector2(0, moveSpeed);
            }
        }
        else if (isGrounded)
        {
            // 【地面を歩く処理】
            isClimbing = false; // 地面に着いたら登り状態をリセット
            rbody.gravityScale = gravityScale;
            rbody.velocity = new Vector2(moveDirection * moveSpeed, 0);
        }
        else
        {
            // 【崖から落ちる処理】
            isClimbing = false; // 空中にいるなら登り状態をリセット
            rbody.gravityScale = gravityScale;
            rbody.velocity = new Vector2(moveDirection * moveSpeed, rbody.velocity.y);
        }

        // キャラクターの向きを進行方向に合わせる
        // （キャラクターのスプライトが元々右を向いていることを想定）
        transform.localScale = new Vector3(
            Mathf.Abs(transform.localScale.x) * moveDirection,
            transform.localScale.y,
            transform.localScale.z
        );
    }

    /// <summary>
    /// 壁登りのクールダウンを開始するコルーチン
    /// </summary>
    private IEnumerator StartClimbCooldown()
    {
        // 1. 状態を更新
        isClimbing = false; // 登り状態を解除
        isClimbOnCooldown = true; // クールダウン状態に設定
        if (animator != null)
        {
            animator.enabled = false;
        }

        // 2. キャラクターを強制的に落下させる
        rbody.gravityScale = gravityScale;
        rbody.velocity = new Vector2(0, 0);

        // 3. 指定した時間だけ待機（Time.timeScaleの影響を受ける）
        yield return new WaitForSeconds(climbCooldown);

        // 4. クールダウン状態を解除
        isClimbOnCooldown = false;
        if (animator != null)
        {
            animator.enabled = true;
        }
    }

    /// <summary>
    /// Rendererがカメラに映るようになった時に呼び出されるUnityの標準イベント
    /// </summary>
    private void OnBecameVisible()
    {
        // まだ一度もカメラに映ったことがない場合のみ、以下の処理を実行
        if (!hasBeenSeenByCamera)
        {
            // フラグをtrueにして、次回以降はこのDebugログが呼ばれないようにする
            hasBeenSeenByCamera = true;
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

        // AIの範囲をSceneビューに表示
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, retreatDistance);

        // センサーの長さを可視化
        // 実行中でなくても確認できるようにGizmosで描画します。

        // 壁判定センサー (青色の線)
        Gizmos.color = Color.blue;
        // transform.positionから、現在の向き(moveDirection)にsensorLength分の線を引く
        // ※Editor上では、実行前のデフォルト値である右向き(1)で表示されます
        Gizmos.DrawLine(
            transform.position,
            transform.position + new Vector3(moveDirection * sensorLength, 0, 0)
        );
    }
}

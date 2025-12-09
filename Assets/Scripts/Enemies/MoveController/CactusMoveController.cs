using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class CactusMoveController : MonoBehaviour, IEnemyResettable
{
    [Header("敵のタイプ")]
    [SerializeField]
    private EnemyVariant variantType = EnemyVariant.None;

    [Header("基本コンポーネント")]
    [SerializeField]
    private GameObject rightArmObject = null;

    [SerializeField]
    private GameObject leftArmObject = null;

    [SerializeField]
    private GameObject flowerObject = null;

    [Header("設定項目")]
    [SerializeField]
    private Transform playerTransform = null;

    [SerializeField]
    private EnemyActivator activator = null; // 親のEnemyActivatorコンポーネント

    [SerializeField]
    [Tooltip("手動で外観の設定を行うかどうか")]
    private bool isManualAppearanceSetup = false;

    [SerializeField, ShowIf(nameof(isManualAppearanceSetup))]
    private bool isLeftHandEnabled = false;

    [SerializeField, ShowIf(nameof(isManualAppearanceSetup))]
    private bool isFlowerEnabled = false;

    [Header("攻撃の設定")]
    [SerializeField, Range(0f, 1f)]
    private float attack_probability = 0.5f;

    [SerializeField]
    private float attackRange = 1.5f;

    [SerializeField]
    private float ballSpeed = 5.0f;

    [Header("待機時間の設定")]
    [SerializeField]
    [Tooltip("待機時間の最小値（秒）")]
    private float minAfterAttackTime = 1.0f;

    [SerializeField]
    [Tooltip("待機時間の最大値（秒）")]
    private float maxAfterAttackTime = 3.0f;

    [Header("位置の設定")]
    [SerializeField]
    [Tooltip("弾を投げる位置のオフセット")]
    private Vector2 throwPositionOffset = Vector2.zero;

    [Header("初期位置の設定")]
    [SerializeField]
    [Tooltip("手動で初期位置を設定するかどうか")]
    private bool isUseManualInitialPosition = false;

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

    private LayerMask groundLayer;

    //埋まり判定用のbool
    private bool isOverlappingGround =>
        Physics2D.OverlapCircle(overlapCheckPoint.position, overlapCheckRadius, groundLayer);

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rbody;
    private Animator rightArmAnimator;
    private EnemyHealth enemyHP;
    private CriWare.Assets.CriAtomSePlayer sePlayer;

    private enum CactusState
    {
        Idle,
        Attacking,
        AdjustingPosition,
    }

    private CactusState currentState = CactusState.Idle;
    private bool rightFlag = false;
    private bool shouldAttack = false;
    private const float BALL_ATTACK_ANIMATION_TIME = 0.800f; // ボール攻撃のアニメーション時間
    private const string BALL_POOLTAG = "CactusBall"; //ボールのプールタグ名
    private List<SpriteRenderer> allRenderers = new List<SpriteRenderer>(); // 子オブジェクトの位置反転用
    private List<GameObject> spawnedObjects = new List<GameObject>(); //生成したオブジェクトを管理するリスト

    private void Awake()
    {
        groundLayer = LayerMask.GetMask(GameConstants.PhysicsLayerName_Ground); // Groundレイヤーを取得

        if (leftArmObject == null || rightArmObject == null || flowerObject == null)
        {
            Debug.LogError($"{this.name}の基本コンポーネントが設定されていません。");
            return;
        }

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
            return;
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
        allRenderers.Add(spriteRenderer);

        // パーツのレンダラーを登録
        void RegisterPart(GameObject obj)
        {
            if (obj == null)
                return;

            // レンダラー登録
            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null)
                allRenderers.Add(sr);
        }

        RegisterPart(leftArmObject);
        RegisterPart(rightArmObject);
        RegisterPart(flowerObject);

        rbody = GetComponent<Rigidbody2D>();
        sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
        rightArmAnimator = rightArmObject.GetComponent<Animator>();
        if (rightArmAnimator == null)
        {
            Debug.LogError($"{this.name}の右腕のAnimatorが設定されていません。");
            return;
        }

        enemyHP = this.GetComponent<EnemyHealth>();
        if (enemyHP == null)
        {
            Debug.LogError($"{this.gameObject.name}にEnemyHealthコンポーネントがありません。");
            return;
        }
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
            Debug.LogError($"{this.gameObject.name}にenemy_HPコンポーネントがありません。");
            return;
        }

        tag = GameConstants.UntaggedName; // タグをリセット
        currentState = CactusState.Idle; // 初期状態をIdleに設定

        if (!isUseManualInitialPosition) // 自動設定モードの場合
        {
            if (activator != null)
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

                    //初期位置を保存・決定
                    transform.position = new Vector2(randomCenter, transform.position.y);
                }
                else
                {
                    Debug.LogWarning(
                        $"{this.name}のEnemyActivatorにCollider2Dが見つかりませんでした。初期位置の自動設定は行いません。"
                    );
                }
            }
            else // activaterが見つからない場合
            {
                Debug.LogWarning(
                    $"{this.name}の親にEnemyActivatorが見つかりませんでした。移動範囲の自動設定は行いません。"
                );
            }
        }

        rightFlag = IsTargetToRight();
        UpdateFacingDirection(rightFlag); //すべてのパーツの向きを更新

        //手動で外観の設定を行わない場合
        if (!isManualAppearanceSetup)
        {
            // 0.0から1.0までの乱数を生成
            float randomValue = Random.Range(0f, 1f);

            // 各動作の累積確率の境界を計算する

            // 動作 1 の確率 (attack_probability * 0.5)
            float p1 = attack_probability * 0.5f;

            // 動作 2 の確率 (attack_probability * 0.5) + 動作 1 の確率
            float p2 = attack_probability * 1.0f;

            // 動作 3 の確率 ((1 - attack_probability) * 0.75) + 動作 2 の確率
            float p3 = attack_probability + (1f - attack_probability) * 0.75f;

            // 動作 4 の確率 ((1 - attack_probability) * 0.25) + 動作 3 の確率 = 1.0
            // float p4 = 1.0f; // 最後の境界は常に 1.0 なので不要

            if (randomValue < p1)
            {
                // 乱数が p1 未満の場合、左腕のみを生やす
                isLeftHandEnabled = true;
                isFlowerEnabled = false;
            }
            else if (randomValue < p2)
            {
                // 乱数が p1 以上 p2 未満の場合、花のみを生やす
                isLeftHandEnabled = false;
                isFlowerEnabled = true;
            }
            else if (randomValue < p3)
            {
                // 乱数が p2 以上 p3 未満の場合、両方生やさない
                isLeftHandEnabled = false;
                isFlowerEnabled = false;
            }
            else
            {
                // 乱数が p3 以上 1.0 未満の場合、両方生やす
                isLeftHandEnabled = true;
                isFlowerEnabled = true;
            }
        }

        leftArmObject.SetActive(isLeftHandEnabled);
        flowerObject.SetActive(isFlowerEnabled);

        //攻撃をするかどうかは、isLeftHandEnabledとisFlowerEnabledのXORで決定する
        shouldAttack = isLeftHandEnabled ^ isFlowerEnabled;
        if (shouldAttack)
        {
            enemyHP.enabled = true;
        }
        else
        {
            enemyHP.enabled = false;
        }

        // 配置時に地面に埋まっていないかチェックし、調整
        StartCoroutine(CheckAndAdjustPosition());
    }

    private void FixedUpdate()
    {
        if (!shouldAttack || playerTransform == null)
            return;

        // 位置調整中は他の物理演算を停止
        if (currentState == CactusState.AdjustingPosition)
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

        switch (currentState)
        {
            case CactusState.Idle:
                bool isTargetCurrentlyRight = IsTargetToRight();
                if (rightFlag != isTargetCurrentlyRight)
                {
                    rightFlag = isTargetCurrentlyRight;
                    UpdateFacingDirection(rightFlag); //すべてのパーツの向きを更新
                    break;
                }

                // Idle中はプレイヤーとの距離チェックを行い、攻撃範囲に入ったら即座に攻撃へ移行
                if (IsPlayerInAttackRange())
                {
                    StartCoroutine(BallAttack());
                }
                break;
        }
    }

    /// <summary>
    /// ボール攻撃の一連のシーケンス（予備動作、発射、硬直）を制御するコルーチン。
    /// ターゲットの移動予測と重力を考慮した放物線軌道（偏差射撃）を計算し、ボールを投擲します。
    /// </summary>
    private IEnumerator BallAttack()
    {
        if (currentState != CactusState.Idle)
            yield break; // Idle状態でなければ攻撃しない

        currentState = CactusState.Attacking;
        this.tag = GameConstants.ImmuneEnemyTagName;

        float timer = 0f;
        rightArmAnimator.SetTrigger("BallAttackTrigger");
        while (timer < BALL_ATTACK_ANIMATION_TIME)
        {
            yield return null; // 1フレーム待機
            timer += Time.deltaTime; // 時間経過を更新
        }

        Vector3 spawnPos =
            (Vector2)transform.position
            + new Vector2(
                rightFlag ? -throwPositionOffset.x : throwPositionOffset.x,
                throwPositionOffset.y
            );

        GameObject ball = ObjectPooler.SceneInstance.SpawnFromPool(
            BALL_POOLTAG,
            spawnPos,
            Quaternion.identity
        );
        spawnedObjects.Add(ball); //生成したオブジェクトを管理リストに追加

        if (ball == null)
        {
            currentState = CactusState.Idle;
            rightArmAnimator.SetTrigger("IdleTrigger");
            yield break;
        }

        ContactDamageController stateController = ball.GetComponent<ContactDamageController>();
        if (stateController == null)
        {
            Debug.LogError($"{ball.name}にEnemyStateControllerが見つかりません。");
        }
        else
        {
            stateController.SetNormalDamage(damage); // ボールのダメージ量を設定
        }

        Rigidbody2D ballRb = ball.GetComponent<Rigidbody2D>();
        if (ballRb != null)
        {
            Vector3 targetPos =
                playerTransform != null ? playerTransform.position : transform.position;

            // ターゲットの速度を取得
            Vector3 targetVel = Vector3.zero;
            if (playerTransform != null)
            {
                Rigidbody2D playerRb = playerTransform.GetComponent<Rigidbody2D>();
                if (playerRb != null)
                    targetVel = playerRb.velocity;
            }

            // ターゲット速度を渡して計算
            Vector3? finalVelocity = CalculateVelocityFixedSpeed(
                spawnPos,
                targetPos,
                targetVel, // 速度を渡す
                ballSpeed,
                true // false = 低い弾道（直線的）, true = 高い弾道（山なり）
            );

            if (finalVelocity.HasValue)
            {
                ballRb.velocity = finalVelocity.Value;
            }
            else
            {
                // 届かない場合のフォールバック（45度射撃）
                Vector3 dir = (targetPos - spawnPos);
                dir.y = 0;
                Vector3 launchDir = (dir.normalized + Vector3.up).normalized;
                ballRb.velocity = launchDir * ballSpeed;
            }
        }

        //ballの発射後の管理はLimitedContactObjectスクリプトに委ねる

        float afterAttackTime = Random.Range(minAfterAttackTime, maxAfterAttackTime);
        yield return new WaitForSeconds(afterAttackTime);
        rightArmAnimator.SetTrigger("IdleTrigger");
        this.tag = GameConstants.UntaggedName;
        currentState = CactusState.Idle;
    }

    /// <summary>
    /// 初速を固定し、移動するターゲットに偏差を合わせて届くための速度ベクトルを計算します。
    /// （反復計算により予測精度を高めています）
    /// </summary>
    /// <param name="startPos">発射位置</param>
    /// <param name="targetPos">ターゲットの現在位置</param>
    /// <param name="targetVelocity">ターゲットの速度</param>
    /// <param name="speed">初速</param>
    /// <param name="useHighArc">trueなら山なり、falseなら直線的</param>
    /// <returns>速度ベクトル（届かない場合はnull）</returns>
    private Vector3? CalculateVelocityFixedSpeed(
        Vector3 startPos,
        Vector3 targetPos,
        Vector3 targetVelocity,
        float speed,
        bool useHighArc
    )
    {
        float gravity = Mathf.Abs(Physics2D.gravity.y);
        // 重力が0の場合は放物線を描かないため、この計算式は使えない（直線計算などを返すかnull）
        if (gravity <= 0)
            return (targetPos - startPos).normalized * speed;

        // 予測位置の初期値は現在位置
        Vector3 aimPos = targetPos;
        Vector3 finalVelocity = Vector3.zero;

        // 反復計算（イテレーション）
        // 3～5回繰り返すことで、着弾時間と移動予測のズレを修正し、高精度な予測位置に収束させます
        int iterations = 4;

        for (int i = 0; i < iterations; i++)
        {
            Vector3 dir = aimPos - startPos;
            float h = dir.y;
            dir.y = 0;
            dir.z = 0;
            float x = dir.magnitude;
            if (x <= 0.0001f)
            {
                // 水平距離がない場合、垂直に撃ち上げる計算などが必要だが、
                // 簡易的に「わずかにずらす」ことで0除算を回避
                x = 0.0001f;
            }

            // --- 物理計算 ---
            float v2 = speed * speed;
            float v4 = v2 * v2;
            float discriminant = v4 - gravity * (gravity * x * x + 2 * h * v2);

            // 届かない場合
            if (discriminant < 0)
                return null;

            float sqrtD = Mathf.Sqrt(discriminant);
            float tanTheta = (v2 + (useHighArc ? sqrtD : -sqrtD)) / (gravity * x);
            float angle = Mathf.Atan(tanTheta);

            Vector3 horizontalDir = dir.normalized;
            finalVelocity =
                horizontalDir * speed * Mathf.Cos(angle) + Vector3.up * speed * Mathf.Sin(angle);
            // -------------------------------------

            // 今回計算した軌道での「着弾までの時間 (t)」を算出
            // 水平速度 vx = speed * cos(θ)
            // 時間 t = 水平距離 / vx
            float vx = speed * Mathf.Cos(angle);

            // ほぼ垂直発射などでvxが極端に小さい場合の対策
            if (vx < 0.001f)
                break;

            float t = x / vx;

            // ターゲットが時間 t 後にいるはずの場所を再設定して、次のループへ
            aimPos = targetPos + targetVelocity * t;
        }
        return finalVelocity;
    }

    /// <summary>
    /// 配置時に地面に埋まっている場合、埋まらない位置まで座標を上方向に調整するコルーチン。
    /// </summary>
    private IEnumerator CheckAndAdjustPosition()
    {
        // 重なっている間、上に移動
        if (isOverlappingGround)
        {
            currentState = CactusState.AdjustingPosition; // ステートを位置調整中に
            rbody.simulated = false; // 物理演算を一時停止して手動で移動

            // 重なりがなくなるまで上に移動
            while (isOverlappingGround)
            {
                transform.position += new Vector3(0, verticalAdjustSpeed * Time.deltaTime, 0);
                yield return null;
            }

            // 位置調整が完了したら、物理演算を再開し、元のステートに戻す
            rbody.simulated = true;
            currentState = CactusState.Idle;
        }
    }

    /// <summary>
    /// 自身の位置からプレイヤーへのベクトルを取得します
    /// </summary>
    private Vector2 GetVectorToPlayer()
    {
        if (playerTransform != null)
        {
            return (Vector2)playerTransform.position - (Vector2)transform.position;
        }
        return Vector2.zero;
    }

    /// <summary>
    /// 対象が自分より右側にいるか判定します
    /// </summary>
    /// <param name="dir">対象への方向ベクトル</param>
    /// <returns>右側にいるならtrue、左側ならfalse</returns>
    private bool IsTargetToRight()
    {
        Vector2 dir = GetVectorToPlayer();
        return dir.x > 0;
    }

    /// <summary>
    /// プレイヤーが攻撃範囲内にいるか判定する
    /// </summary>
    private bool IsPlayerInAttackRange()
    {
        Vector2 dir = GetVectorToPlayer();
        return dir.x * (rightFlag ? 1 : -1) <= attackRange && dir.x * (rightFlag ? 1 : -1) >= 0;
    }

    /// <summary>
    /// 全てのパーツの向き（flipX）を一括で更新します。
    /// Pivot調整済みのため、位置の反転処理は不要です。
    /// </summary>
    /// <param name="isFacingRight">右を向いているか</param>
    private void UpdateFacingDirection(bool isFacingRight)
    {
        // 右向きなら flipX=true 左向きなら flipX=false

        foreach (var sr in allRenderers)
        {
            if (sr != null)
            {
                sr.flipX = isFacingRight;
            }
        }
    }

    private void OnDisable()
    {
        // 実行中のコルーチンをすべて強制停止
        // (Unityの仕様上、Disableで自動停止しますが、明示的に書くことで意図を明確にします)
        StopAllCoroutines();

        // 管理リストにある弾を全てプールに返却する
        foreach (var obj in spawnedObjects)
        {
            // オブジェクトが存在し、かつアクティブな場合のみ返却
            if (obj != null && obj.activeSelf)
            {
                var poolableObject = obj.GetComponent<PoolableObject>();
                if (poolableObject != null)
                {
                    // LimitedContactObjectがあれば、プールに返却する
                    poolableObject.ReturnToPool();
                }
                else
                {
                    // なければ通常のDestroy
                    Destroy(obj);
                }
            }
        }

        // リストをクリア
        spawnedObjects.Clear();
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
        // 攻撃範囲の中心位置を計算
        // rightFlagがtrue(右向き)なら、自身の位置から右へ attackRange/2 ずらした場所が中心
        // rightFlagがfalse(左向き)なら、自身の位置から左へ attackRange/2 ずらした場所が中心
        // ※ IsPlayerInAttackRange の判定は「向いている方向へ 0 ～ attackRange の距離」であるため

        float direction = rightFlag ? 1f : -1f;

        // 判定エリアの中心座標
        Vector3 center = transform.position + new Vector3(attackRange / 2f * direction, 0f, 0f);

        // 判定エリアのサイズ
        // 幅は attackRange、高さは適当（ここでは2f）、奥行きは0.1f
        Vector3 size = new Vector3(attackRange, 2f, 0.1f);

        Gizmos.color = new Color(1f, 0f, 0f, 0.3f); // 赤色半透明
        Gizmos.DrawCube(center, size);

        Gizmos.color = Color.red; // 外枠
        Gizmos.DrawWireCube(center, size);
    }
}

using System.Collections;
using UnityEngine;

public class BirdNormalMoveController : MonoBehaviour, IEnemyResettable
{
    private const float MOVE_RANGE = 4.0f; // ランダムに設定する場合の移動幅

    [Header("敵のタイプ")]
    [SerializeField]
    private EnemyVariant variantType = EnemyVariant.None; //敵の種類を設定

    [Header("設定項目")]
    [SerializeField]
    private Transform playerTransform = null; // PlayerのTransform

    [SerializeField]
    private EnemyActivator activator = null; // 親のEnemyActivatorコンポーネント

    [SerializeField]
    private GameObject contactDamageObject = null; // ContactDamageControllerのGameObject

    [Header("移動・攻撃の基本設定")]
    [SerializeField]
    private float normalY = 0.0f; //通常時のy座標

    [SerializeField]
    private float speedX = 4.0f; // 移動速度を設定

    [SerializeField]
    private float diveSpeed = 6.0f; // ダイブ時の速度

    [SerializeField]
    private float attackRange = 1.5f; //攻撃範囲

    [SerializeField]
    private float chargeTime = 0.8f; // 溜め時間

    [SerializeField, Tooltip("溜め開始時に上昇するY軸の高さ")]
    private float chargeUpHeight = 0.5f; // 溜め開始時に上昇するY軸の高さ

    [SerializeField, Tooltip("溜め開始時の上昇速度")]
    private float chargeUpSpeed = 1.0f; // 溜め開始時の上昇速度

    [SerializeField, Tooltip("着地後の待機時間")]
    private float recoverDelay = 1.0f; // 着地後の待機時間

    [SerializeField, Tooltip("通常高度への復帰速度（秒）")]
    private float returnDuration = 1.5f; // 高さ復帰にかかる時間（秒）

    [SerializeField, Tooltip("ダイブ時のプレイヤー位置からのオフセット")]
    private float diveOffsetX = 1.0f; // プレイヤー位置からずらして落ちるX座標

    [Header("必要ならば設定")]
    [SerializeField]
    private float leftBound = 0; //行動範囲の左端

    [SerializeField]
    private float rightBound = 0; //行動範囲の右端

    [Header("地面判定用の設定")]
    [SerializeField]
    private Transform groundCheck; // 足元に置くTransform

    [SerializeField]
    private float groundCheckRadius = 0.2f; // 判定用の円の半径

    [SerializeField]
    LayerMask GroundLayer; // 地面として判定するレイヤー

    [Header("スプライト")]
    [SerializeField]
    private Sprite hoverSprite; // ホバリング時のスプライト

    [SerializeField]
    private Sprite glideSprite; // グライド時のスプライト

    [Header("飛行時の微調整")]
    [SerializeField, Tooltip("飛行時のY軸方向の振動量")]
    private float floatAmplitudeY = 0.1f; // Y軸方向の振動の振幅

    [SerializeField, Tooltip("飛行時のY軸方向の振動速度")]
    private float floatSpeedY = 2.0f; // Y軸方向の振動速度

    [SerializeField, Tooltip("飛行時の左右への微細な揺れ幅")]
    private float wobbleAmountX = 0.05f; // 左右への微細な揺れ幅

    [SerializeField, Tooltip("飛行時の左右への微細な揺れ速度")]
    private float wobbleSpeedX = 3.0f; // 左右への微細な揺れ速度

    [SerializeField, Tooltip("飛行時のZ軸回転の揺れ量")]
    private float rotationWobbleZ = 5.0f; // Z軸回転の揺れ量 (度)

    [SerializeField, Tooltip("飛行時のZ軸回転の揺れ速度")]
    private float rotationSpeedZ = 4.0f; // Z軸回転の揺れ速度
    private float vx = 0; //x方法の移動速度
    private float recoverTimer = 0f; // 復帰中の経過時間
    private int damage = 0; //攻撃のダメージ量
    private Vector3 recoverStartPos; // 復帰開始時の位置を保存
    private bool rightFlag = false; //右向きかどうかのフラグ
    private bool isUseAutoBounds = false; // 行動範囲自動設定モードかどうか
    private Vector2 myPos = Vector2.zero; //自分の現在の座標
    private Animator animator = null;
    private Rigidbody2D rbody;
    private SpriteRenderer spriteRenderer;
    private float currentRotationOffset = 0f; // 回転の現在のオフセット
    private float timeElapsedForWobble = 0f; // 微振動の経過時間

    private enum EnemyVariant
    {
        None = 0,
        Chapter1 = 1,
    }

    private enum BirdState
    {
        None,
        Hovering,
        Charging,
        Diving,
        Recovering,
    }

    private BirdState currentState = BirdState.Hovering; // 現在の状態
    private float targetDiveX; // ダイブ時の目標X座標
    private Vector2 diveVelocity; // ダイブ開始時に設定される固定ベクトル

    private void Awake()
    {
        switch (variantType)
        {
            case EnemyVariant.Chapter1:
                damage = 34;
                break;
            default:
                Debug.LogError($"{this.name}のEnemyVariantが設定されていません。");
                break;
        }

        if (normalY == 0.0f)
        {
            Debug.LogWarning($"{this.name}の通常時のy座標が設定されていません。");
        }

        if (contactDamageObject == null)
        {
            Debug.LogError(
                $"{this.name}のContactDamageControllerのGameObjectが設定されていません。"
            );
        }
        else
        {
            ContactDamageController contactDamageController =
                contactDamageObject.GetComponent<ContactDamageController>(); // ContactDamageControllerを子オブジェクトから取得
            if (contactDamageController != null)
            {
                contactDamageController.SetDamageAmount(damage); //敵の攻撃力を設定
            }
            else
            {
                Debug.LogError($"{this.name}にContactDamageControllerがアタッチされていません。");
                return;
            }
        }

        if (groundCheck == null || GroundLayer == 0 || groundCheckRadius <= 0)
        {
            Debug.LogError($"{this.name}の地面判定用の設定が正しくありません。");
        }

        if (hoverSprite == null || glideSprite == null)
        {
            Debug.LogError($"{this.name}のスプライトが設定されていません。");
        }

        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError($"{this.name}にAnimatorコンポーネントがアタッチされていません。");
        }

        //物理挙動の初期化
        rbody = this.GetComponent<Rigidbody2D>();
        if (rbody == null)
        {
            Debug.LogError($"{this.gameObject.name}にRigidbody2Dコンポーネントがありません。");
        }
        else
        {
            rbody.constraints = RigidbodyConstraints2D.FreezeRotation; //回転を停止する
            rbody.gravityScale = 0; //重力を無効化
            rbody.simulated = false; // 初期状態では物理挙動を無効化
        }

        spriteRenderer = this.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError($"{this.gameObject.name}にSpriteRendererコンポーネントがありません。");
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

        // 自動設定モードかどうかを判定
        // 境界値が両方とも設定されていない場合、自動モードを有効にする
        isUseAutoBounds = leftBound == 0 && rightBound == 0;
    }

    private void Start()
    {
        ResetState(); // 敵の状態をリセット
    }

    /// <summary>
    /// 敵の状態をリセットするメソッド
    /// </summary>
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

        EnemyHealth enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            // 自分のHPをリセット
            enemyHealth.ResetState();
        }

        rbody.simulated = true; //物理挙動を有効化
        vx = (Random.value < 0.5f ? -1 : 1) * speedX; //速度の方向をランダムに初期化決定
        rightFlag = vx > 0; //右向きかどうかのフラグを設定
        spriteRenderer.flipX = rightFlag; //スプライトの向きを設定する
        rbody.velocity = new Vector2(vx, 0); //速度を初期化
        currentState = BirdState.Hovering; // 初期状態をホバリングに設定

        this.tag = GameConstants.ImmuneEnemyTagName; // タグをダメージを受けない敵のタグに初期化
        contactDamageObject.tag = GameConstants.ImmuneEnemyTagName; // 子オブジェクトもタグをダメージを受けない敵のタグに初期化
        if (animator != null && !animator.enabled)
        {
            animator.enabled = true; // Animatorを有効化
        }

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
            else // activaterが見つからない場合
            {
                Debug.LogWarning(
                    $"{this.name}の親にEnemyActivatorが見つかりませんでした。移動範囲の自動設定は行いません。"
                );
            }

            this.transform.position = new Vector2(
                UnityEngine.Random.Range(leftBound, rightBound),
                normalY
            ); //自分の初期座標を決定
        }
    }

    private void FixedUpdate()
    {
        if (playerTransform == null)
            return;

        // 時間停止中は物理挙動を停止し、それ以外の処理もスキップ
        if (TimeManager.instance.isEnemyMovePaused)
        {
            if (rbody != null && rbody.simulated)
            {
                rbody.simulated = false; // 物理挙動を停止する
            }
            // 時間停止中はアニメーターも停止させる（Animatorコンポーネントがある場合）
            if (animator != null && animator.enabled)
            {
                animator.enabled = false;
            }
            return;
        }
        else // 時間が動いている場合
        {
            if (rbody != null && !rbody.simulated)
            {
                rbody.simulated = true; // 物理挙動を再起動する
            }
            // 時間再開時にアニメーターも再開させる
            if (animator != null && !animator.enabled)
            {
                // RecoverコルーチンでAnimatorを有効化しているので、ここでは無効化されていない場合のみ有効化する
                // そうでなければ、Recoverコルーチンに任せる
                if (currentState == BirdState.Hovering) // ホバリング状態ならAnimatorを有効化
                {
                    animator.enabled = true;
                }
            }

            myPos = this.transform.position; //自分の現在座標を取得
            Vector3 dir = playerTransform.position - this.transform.position; //自分とプレイヤーの現在座標の差を取得

            // 微振動の経過時間を更新
            timeElapsedForWobble += Time.fixedDeltaTime;

            switch (currentState)
            {
                case BirdState.Hovering:
                    {
                        // 左右に飛ぶ通常行動
                        if ((myPos.x <= leftBound && vx <= 0) || (rightBound <= myPos.x && 0 <= vx))
                        {
                            rightFlag = !rightFlag;
                            vx = speedX * (rightFlag ? 1 : -1);
                            spriteRenderer.flipX = rightFlag;
                            timeElapsedForWobble = 0f; // 方向転換時に微振動の経過時間をリセットすると、動き出しが揃う
                        }

                        // Y軸方向の振動
                        float yOffset =
                            Mathf.Sin(timeElapsedForWobble * floatSpeedY) * floatAmplitudeY;

                        // X軸方向の微細な揺れ（常に左右に揺れる）
                        float xWobble =
                            Mathf.Sin(timeElapsedForWobble * wobbleSpeedX) * wobbleAmountX;

                        // X方向の速度に微振動の速度成分を加える
                        // Rigidbodyの速度に直接影響を与える
                        float currentVx =
                            vx
                            + (
                                Mathf.Cos(timeElapsedForWobble * wobbleSpeedX)
                                * wobbleAmountX
                                * wobbleSpeedX
                            );

                        // RigidbodyのY速度は、通常時のnormalYからのオフセットで制御
                        // current position.y と normalY に yOffset を加えた目標y座標との差を速度に変換
                        float targetY = normalY + yOffset;
                        float vy = (targetY - myPos.y) / Time.fixedDeltaTime; // 次のFixedUpdateで目標に近づく速度

                        rbody.velocity = new Vector2(currentVx, vy);

                        // Z軸回転の揺れ (SpriteRendererに適用)
                        currentRotationOffset =
                            Mathf.Sin(timeElapsedForWobble * rotationSpeedZ) * rotationWobbleZ;
                        transform.rotation = Quaternion.Euler(0, 0, currentRotationOffset);

                        // プレイヤーが攻撃範囲内かチェック
                        Vector2 toPlayer = playerTransform.position - transform.position;
                        bool isInFront = toPlayer.x * (rightFlag ? 1 : -1) > 0; // プレイヤーが自分の前にいるか
                        bool isInRange = Mathf.Abs(toPlayer.x) <= attackRange; // プレイヤーが攻撃範囲内にいるか

                        if (isInFront && isInRange)
                        {
                            currentState = BirdState.Charging; // 攻撃範囲内にいる場合、溜め状態に移行
                            targetDiveX =
                                playerTransform.position.x
                                + (rightFlag ? diveOffsetX : -diveOffsetX); // プレイヤー位置からずらして落ちるX座標
                            StartCoroutine(ChargeAndDive()); // 溜めのコルーチンを開始
                        }
                    }
                    break;

                case BirdState.Diving:
                    {
                        rbody.velocity = diveVelocity; // ダイブ速度を設定
                        // ダイブ中は微振動を適用しない
                        transform.rotation = Quaternion.identity; // ダイブ中は回転をリセット
                    }
                    break;

                case BirdState.Recovering:
                    {
                        // ゆっくり元のY高度へ戻る
                        recoverTimer += Time.fixedDeltaTime; // FixedUpdateなのでfixedDeltaTimeを使用
                        float t = Mathf.Clamp01(recoverTimer / returnDuration);

                        // 線形補間で高さを戻す
                        // current position.y ではなく recoverStartPos.y から normalY へ補間
                        float newY = Mathf.Lerp(recoverStartPos.y, normalY, t);

                        // X軸の移動も考慮に入れる（Recovering中も左右の移動は続ける場合）
                        // ここではHoveringと同じ水平移動ロジックを適用すると仮定
                        if ((myPos.x <= leftBound && vx <= 0) || (rightBound <= myPos.x && 0 <= vx))
                        {
                            rightFlag = !rightFlag;
                            vx = speedX * (rightFlag ? 1 : -1);
                            spriteRenderer.flipX = rightFlag;
                        }

                        // Recovering中の微振動 (オプション: 通常のHoveringと同様の微振動を適用することもできる)
                        float yWobbleRecover =
                            Mathf.Sin(timeElapsedForWobble * floatSpeedY) * floatAmplitudeY;
                        float xWobbleRecover =
                            Mathf.Sin(timeElapsedForWobble * wobbleSpeedX) * wobbleAmountX;
                        float rotationWobbleRecover =
                            Mathf.Sin(timeElapsedForWobble * rotationSpeedZ) * rotationWobbleZ;

                        // Y軸の復帰移動と微振動を組み合わせる
                        float finalY = newY + yWobbleRecover;

                        // Rigidbodyの速度を設定
                        float currentRecoverVx =
                            vx
                            + (
                                Mathf.Cos(timeElapsedForWobble * wobbleSpeedX)
                                * wobbleAmountX
                                * wobbleSpeedX
                            );
                        float currentRecoverVy = (finalY - myPos.y) / Time.fixedDeltaTime;
                        rbody.velocity = new Vector2(currentRecoverVx, currentRecoverVy);

                        // 回転を適用
                        transform.rotation = Quaternion.Euler(0, 0, rotationWobbleRecover);

                        if (t >= 1f)
                        {
                            currentState = BirdState.Hovering;
                            vx = speedX * (rightFlag ? 1 : -1);
                            rbody.velocity = new Vector2(vx, 0); // 微振動の初期化
                            timeElapsedForWobble = 0f; // ホバリングに戻ったら微振動の経過時間をリセット
                            // 念のため、回転もリセットしておくか、微振動に任せる
                            transform.rotation = Quaternion.identity;
                        }
                    }
                    break;
            }

            // 地面判定ロジック
            if (currentState == BirdState.Diving)
            {
                bool isTouchingGround = Physics2D.OverlapCircle(
                    groundCheck.position,
                    groundCheckRadius,
                    GroundLayer
                );
                if (isTouchingGround)
                {
                    rbody.velocity = Vector2.zero; // 着地時に速度をリセット
                    recoverStartPos = transform.position; // 現在位置を記録
                    recoverTimer = 0f; // タイマー初期化
                    currentState = BirdState.None; // ダイブ状態をリセット
                    this.tag = GameConstants.ImmuneEnemyTagName; // タグをダメージを受けない敵のタグに変更
                    contactDamageObject.tag = GameConstants.ImmuneEnemyTagName; // 子オブジェクトもタグをダメージを受けない敵のタグに変更
                    StartCoroutine(Recover()); // 復帰のコルーチンを開始
                }
            }
        }
    }

    // ChargeAndDive() メソッド
    private IEnumerator ChargeAndDive()
    {
        // アニメーターを無効化
        if (animator != null)
        {
            animator.enabled = false;
        }
        // ホバリングスプライトに変更
        spriteRenderer.sprite = hoverSprite;

        // 溜め開始時のY座標上昇
        Vector3 startChargePos = transform.position;
        Vector3 targetChargeUpPos = new Vector3(
            startChargePos.x,
            startChargePos.y + chargeUpHeight,
            startChargePos.z
        );
        float currentChargeUpTime = 0f;

        // 上昇フェーズ
        while (currentChargeUpTime < chargeUpSpeed) // chargeUpSpeedを「上昇にかかる時間」として使用
        {
            if (TimeManager.instance.isEnemyMovePaused)
            {
                rbody.velocity = Vector2.zero; // 時間停止中は停止
                yield return null;
                continue;
            }

            currentChargeUpTime += Time.fixedDeltaTime;
            // 線形補間を使って目標の高さへ移動
            float newY = Mathf.Lerp(
                startChargePos.y,
                targetChargeUpPos.y,
                currentChargeUpTime / chargeUpSpeed
            );
            // RigidbodyのY速度を設定
            rbody.velocity = new Vector2(0, (newY - transform.position.y) / Time.fixedDeltaTime);

            // 溜め中の微振動を適用 (Y軸方向のみ)
            float yOffset = Mathf.Sin(timeElapsedForWobble * floatSpeedY) * floatAmplitudeY;
            transform.position = new Vector3(
                transform.position.x,
                newY + yOffset,
                transform.position.z
            );

            // 回転の微振動も継続したい場合
            currentRotationOffset =
                Mathf.Sin(timeElapsedForWobble * rotationSpeedZ) * rotationWobbleZ;
            transform.rotation = Quaternion.Euler(0, 0, currentRotationOffset);

            timeElapsedForWobble += Time.fixedDeltaTime; // 微振動の経過時間を更新
            yield return null;
        }

        // 上昇が終わったらX方向の速度は0に、Y方向は微振動を維持しながらほぼ停止
        rbody.velocity = new Vector2(0, 0); // 上昇フェーズ終了時にリセット

        // 溜めフェーズ（微振動のみ）
        float currentChargeTime = 0f;
        while (currentChargeTime < chargeTime)
        {
            if (TimeManager.instance.isEnemyMovePaused)
            {
                rbody.velocity = Vector2.zero; // 時間停止中は停止
                yield return null;
                continue;
            }

            currentChargeTime += Time.fixedDeltaTime;

            // 溜め中の微振動を適用
            float yOffset = Mathf.Sin(timeElapsedForWobble * floatSpeedY) * floatAmplitudeY;
            float xWobble = Mathf.Sin(timeElapsedForWobble * wobbleSpeedX) * wobbleAmountX;
            currentRotationOffset =
                Mathf.Sin(timeElapsedForWobble * rotationSpeedZ) * rotationWobbleZ;

            // 溜め中はX方向の移動はなしで、現在のX座標を中心に揺れる
            // transform.position.x を直接操作し、水平移動に影響させない
            transform.position = new Vector3(
                startChargePos.x + xWobble,
                targetChargeUpPos.y + yOffset,
                transform.position.z
            ); // Xは溜め開始時の位置を基準に揺らす
            transform.rotation = Quaternion.Euler(0, 0, currentRotationOffset);

            timeElapsedForWobble += Time.fixedDeltaTime; // 微振動の経過時間を更新
            yield return null;
        }

        // ダイブ開始のロジック
        Vector2 currentPos = transform.position; // 現在のY座標が上昇した位置になっている
        float directionX = Mathf.Sign(targetDiveX - currentPos.x); // プレイヤーの位置に基づいてX方向を決定
        diveVelocity = new Vector2(directionX * diveSpeed, -diveSpeed); // ダイブ速度を設定
        SEManager.instance.PlayEnemyActionSE(SE_EnemyAction.Attack_fly1); // ダイブ開始時のSEを再生
        this.tag = GameConstants.DamageableEnemyTagName; // タグをダメージを受ける敵のタグに変更
        contactDamageObject.tag = GameConstants.DamageableEnemyTagName; // 子オブジェクトもタグをダメージを受ける敵のタグに変更
        spriteRenderer.sprite = glideSprite; // グライドスプライトに変更
        currentState = BirdState.Diving; // 溜めが完了したらダイブ状態に移行

        // ダイブ開始時に微振動の影響をリセット
        transform.rotation = Quaternion.identity;
    }

    // Recover() メソッド
    private IEnumerator Recover()
    {
        spriteRenderer.sprite = hoverSprite; // ホバリングスプライトに変更
        rbody.velocity = Vector2.zero; // 復帰待機中は停止
        yield return new WaitForSeconds(recoverDelay); // 復帰までの待機時間
        if (animator != null)
        {
            animator.enabled = true; // Animatorを有効化してアニメーションを再開
        }
        currentState = BirdState.Recovering; // 復帰状態にする
        // ここで直接rbody.velocityを設定するのではなく、FixedUpdateに任せる
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }

    private void OnDrawGizmos()
    {
        Color semiTransparentRed = new Color(1f, 0f, 0f, 0.15f); // 赤・透過
        Gizmos.color = semiTransparentRed;

        // 四角形の中心座標
        Vector3 center = new Vector3(
            (leftBound + rightBound) / 2f,
            transform.position.y,
            transform.position.z
        );

        // 四角形のサイズ
        Vector3 size = new Vector3(
            rightBound - leftBound,
            2f, // 高さ（Y軸）は2に設定
            0.1f // 厚み（Z軸）は薄く
        );

        Gizmos.DrawCube(center, size);
    }
}

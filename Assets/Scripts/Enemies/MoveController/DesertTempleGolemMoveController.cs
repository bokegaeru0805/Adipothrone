using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NaughtyAttributes;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class DesertTempleGolemMoveController : MonoBehaviour, IEnemyResettable
{
    private const float MOVE_RANGE = 10.0f; // ランダムに設定する場合の移動幅
    private const string SHOOT_POOLTAG = "DesertTempleGolemShoot"; // 弾のプールタグ名
    private const string ATTACK_ANIMATION_CLIP_NAME = "DesertTempleGolem_attack"; // 攻撃アニメーションのクリップ名

    [Header("設定項目")]
    [SerializeField]
    private Transform playerTransform = null;

    [SerializeField]
    private EnemyActivator activator = null; // 親のEnemyActivatorコンポーネント

    [Header("横移動の設定")]
    [SerializeField]
    private float speedX = 2.0f;

    [Header("攻撃の設定")]
    [SerializeField]
    private float attackRange = 1.5f;

    [SerializeField]
    private int damage = 0; // 攻撃力

    [SerializeField]
    private float shootSpeed = 5.0f;

    [Tooltip("攻撃時の高さオフセット")]
    [SerializeField]
    private float targetHeightOffset = 1.0f;

    [Tooltip("攻撃時のY位置の変動幅")]
    [SerializeField]
    private float targetYDelta = 0.5f;

    [Tooltip("弾が発射される間隔（秒）")]
    [SerializeField]
    private float shootIntervalTime = 0.5f;

    [Header("待機時間の設定")]
    [Tooltip("攻撃前の待機時間（秒）")]
    [SerializeField]
    private float beforeAttackTime = 1.0f;

    [Tooltip("攻撃後の待機時間の最小値（秒）")]
    [SerializeField]
    private float minAfterAttackTime = 1.0f;

    [Tooltip("攻撃後の待機時間の最大値（秒）")]
    [SerializeField]
    private float maxAfterAttackTime = 3.0f;

    [Tooltip("移動開始後に再び攻撃可能になるまでの時間（秒）")]
    [SerializeField]
    private float afterMoveAttackCooldown = 2.0f;

    [Header("移動範囲の設定")]
    [SerializeField]
    [Tooltip("手動で移動範囲を設定するかどうか")]
    private bool isUseManualBounds = false;

    [SerializeField, ShowIf(nameof(isUseManualBounds))]
    private float leftBound;

    [SerializeField, ShowIf(nameof(isUseManualBounds))]
    private float rightBound;

    [Header("浮遊の設定")]
    [Tooltip("地面から維持したい高さ")]
    [SerializeField]
    private float targetHeightFromGround = 2.0f;

    [Tooltip("攻撃時の地面からの高さ")]
    [SerializeField]
    private float attackHeightFromGround = 2.5f;

    [Tooltip("高さ調整の追従速度（高いほど素早く高さを合わせる）")]
    [SerializeField]
    private float heightAdjustSpeed = 5.0f;

    [Header("その他の設定")]
    [Tooltip(
        "弾の配置のオフセット(このオフセットは右側の弾に対するものです。中央と左側の弾はそれぞれ-、0のXオフセットになります)"
    )]
    [SerializeField]
    private Vector2 shootOffset = Vector2.zero;

    [Tooltip("弾が発射される前の遅延時間")]
    [SerializeField]
    private float shootStartDelay = 0.2f;

    [Tooltip("弾が発射前に後退する距離")]
    [SerializeField]
    private float shootRecoilDistance = 1f;

    [Tooltip("弾が発射前に後退する時間")]
    [SerializeField]
    private float shootRecoilTime = 0.2f;

    [Tooltip("弾の回転の強さ係数")]
    [SerializeField]
    private float shootRotationMultiplier = 200f;

    private float rayLength = 20.0f; //地面を探すレイの長さ
    private float attackAnimationTime = 0.5f; // 攻撃アニメーションの時間
    private bool rightFlag = false; // 右向きかどうか
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rbody;
    private Animator animator;
    private EnemyHealth enemyHP;
    private ContactDamageController contactDamageController;
    private CriWare.Assets.CriAtomSePlayer sePlayer;
    private LayerMask groundLayer;

    private enum DesertTempleGolemState
    {
        IdleMoving,
        Moving,
        PreparingToAttack,
        Attacking,
    }

    private DesertTempleGolemState currentState = DesertTempleGolemState.Moving;
    private List<GameObject> spawnedObjects = new List<GameObject>(); //生成したオブジェクトを管理するリスト

    private void Awake()
    {
        groundLayer = LayerMask.GetMask(GameConstants.PHYSICS_LAYER_NAME_GROUND); // Groundレイヤーを取得

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
        bool foundAttackClip = false;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                // 定義したクリップ名で検索
                if (clip.name == ATTACK_ANIMATION_CLIP_NAME)
                {
                    attackAnimationTime = clip.length;
                    foundAttackClip = true;
                    // Debug.Log(
                    //     $"{this.name}の攻撃アニメーション時間を{attackAnimationTime}秒として設定しました。"
                    // );
                    break;
                }
            }
        }

        if (!foundAttackClip)
        {
            Debug.LogWarning(
                $"{this.name}のAnimatorに攻撃アニメーション({ATTACK_ANIMATION_CLIP_NAME})が見つかりませんでした。デフォルトの攻撃アニメーション時間({attackAnimationTime}秒)を使用します。"
            );
        }

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
        ResetState();
    }

    public void ResetState()
    {
        if (playerTransform == null)
        {
            playerTransform = GameObject
                .FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME)
                ?.transform;
            if (playerTransform == null)
            {
                Debug.LogError($"{this.name}はPlayerTransformを見つけられませんでした");
                return;
            }
        }

        if (rbody != null)
        {
            rbody.simulated = true; // 物理挙動を再起動
            rbody.constraints = RigidbodyConstraints2D.FreezeRotation; // 回転を停止する
        }
        else
        {
            Debug.LogError($"{this.gameObject.name}にRigidbody2Dコンポーネントがありません。");
            return;
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

        tag = GameConstants.IMMUNE_ENEMY_TAG_NAME; // タグをリセット
        currentState = DesertTempleGolemState.Moving; // 初期状態をMovingに設定

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
                else
                {
                    Debug.LogWarning(
                        $"{this.name}のEnemyActivatorにCollider2Dが見つかりませんでした。移動範囲の自動設定は行いません。"
                    );
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

        //移動方向をランダムに決定
        rightFlag = (Random.value > 0.5f);
        spriteRenderer.flipX = rightFlag;

        animator?.SetTrigger("IdleTrigger");
    }

    private void FixedUpdate()
    {
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
            case DesertTempleGolemState.Moving:
                if (IsPlayerInAttackRange())
                {
                    StartCoroutine(AttackCoroutine());
                }
                else
                {
                    // 速度を計算して適用する
                    rbody.velocity = CalculateVelocity();
                }
                break;
            case DesertTempleGolemState.IdleMoving:
                // 速度を計算して適用する
                rbody.velocity = CalculateVelocity();
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// 攻撃の予備動作（浮遊）、弾の生成と展開、反動演出を伴う発射、および攻撃後のクールダウンまでの一連のシーケンスを制御する。
    /// <para>
    /// 1. 地面からの高さを調整しながらチャージ動作を行う。<br/>
    /// 2. 3つの弾を生成し、自身の周囲（指定オフセット位置）へ滑らかに展開。<br/>
    /// 3. 各弾に対し、発射方向とは逆への予備動作（Recoil）を与えた後、回転を加えてターゲット方向へ順次発射。<br/>
    /// 4. 乱数による攻撃後の硬直を経て、移動状態へ復帰。
    /// </para>
    /// </summary>
    private IEnumerator AttackCoroutine()
    {
        currentState = DesertTempleGolemState.PreparingToAttack;
        animator.SetTrigger("ChargeTrigger");
        rbody.velocity = Vector2.zero; // 移動を停止

        // --- 1. 高さをゆっくり調整するフェーズ ---
        float timer = 0f;
        float startY = transform.position.y;

        while (timer < beforeAttackTime)
        {
            // 地面の位置を再取得（移動床などに対応するため）
            RaycastHit2D hit = Physics2D.Raycast(
                transform.position,
                Vector2.down,
                rayLength,
                groundLayer
            );
            if (hit.collider != null)
            {
                // 目標のY座標
                float targetY = hit.point.y + attackHeightFromGround;

                // 現在のYを滑らかに更新
                // Time.deltaTime を使って beforeAttackTime かけて目標へ遷移させる
                float newY = Mathf.Lerp(startY, targetY, timer / beforeAttackTime);

                // Rigidbodyで位置を更新（Xは維持）
                transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            }

            timer += Time.deltaTime;
            yield return null;
        }

        currentState = DesertTempleGolemState.Attacking;
        animator.SetTrigger("AttackTrigger");

        // --- 2. 弾を生成して拡散させるフェーズ ---

        // 3つの弾を生成してリストに保持
        GameObject[] shoots = new GameObject[3];
        Vector3[] startPositions = new Vector3[3];
        Vector3[] targetPositions = new Vector3[3];
        Vector3 basePos = transform.position;

        // 目標とする相対位置（オフセット）
        // 1つ目: (-x, y), 2つ目: (0, y), 3つ目: (x, y)
        Vector3[] offsets = new Vector3[]
        {
            new Vector3(-shootOffset.x, shootOffset.y, 0),
            new Vector3(0, shootOffset.y, 0),
            new Vector3(shootOffset.x, shootOffset.y, 0),
        };

        // 弾の生成と初期化
        for (int i = 0; i < 3; i++)
        {
            // 自分の位置に生成
            shoots[i] = ObjectPooler.SceneInstance.SpawnFromPool(
                SHOOT_POOLTAG,
                basePos,
                Quaternion.identity
            );

            if (shoots[i] != null)
            {
                spawnedObjects.Add(shoots[i]); // 生成した弾を管理リストに追加

                startPositions[i] = shoots[i].transform.position;

                // 向きを考慮してオフセットを加算（右向きならそのまま、左ならX反転）
                float direction = rightFlag ? 1f : -1f;
                Vector3 adjustedOffset = new Vector3(offsets[i].x * direction, offsets[i].y, 0);

                targetPositions[i] = basePos + adjustedOffset;

                // ダメージ設定などがあればここで行う
                var damageCtrl = shoots[i].GetComponent<ContactDamageController>();
                if (damageCtrl != null)
                    damageCtrl.SetNormalDamage(damage);
            }
        }

        // 時間をかけて徐々に移動させる
        float moveTimer = 0f;
        while (moveTimer < attackAnimationTime)
        {
            float t = moveTimer / attackAnimationTime;
            // イージング（滑らかに動き出す）を入れるならここを調整 (例: t = t * t * (3f - 2f * t))

            for (int i = 0; i < 3; i++)
            {
                if (shoots[i] != null && shoots[i].activeSelf)
                {
                    // Lerpで移動
                    shoots[i].transform.position = Vector3.Lerp(
                        startPositions[i],
                        targetPositions[i],
                        t
                    );
                }
            }

            moveTimer += Time.deltaTime;
            yield return null;
        }

        // 念のため最終位置に合わせる
        for (int i = 0; i < 3; i++)
        {
            if (shoots[i] != null && shoots[i].activeSelf)
            {
                shoots[i].transform.position = targetPositions[i];
            }
        }

        // 少し待機
        yield return new WaitForSeconds(shootStartDelay);

        // --- 3. 反動と発射フェーズ ---

        // ターゲットの基準位置を計算
        // プレイヤーがいればそのY座標、いなければ自分のY座標
        float baseTargetY =
            (playerTransform != null) ? playerTransform.position.y : transform.position.y;

        // 狙う高さのオフセットを加算
        baseTargetY += targetHeightOffset;

        // 3つの弾それぞれの狙うY座標を決定
        // 左: base - delta, 中央: base, 右: base + delta
        float[] targetYPositions = new float[]
        {
            baseTargetY - targetYDelta,
            baseTargetY,
            baseTargetY + targetYDelta,
        };

        // 左から順に発射
        for (int i = 0; i < 3; i++)
        {
            if (shoots[i] != null && shoots[i].activeSelf)
            {
                Rigidbody2D shootRb = shoots[i].GetComponent<Rigidbody2D>();
                if (shootRb != null)
                {
                    // ターゲット方向へのベクトルを計算
                    // X座標はプレイヤーの方向（または向いている方向）へ
                    float targetX =
                        (playerTransform != null)
                            ? playerTransform.position.x
                            : (transform.position.x + (rightFlag ? 10 : -10));

                    Vector3 targetPoint = new Vector3(targetX, targetYPositions[i], 0);
                    Vector3 shootDir = (targetPoint - shoots[i].transform.position).normalized;

                    //弾を発射方向と逆向きに後退させる（予備動作）
                    // 後退先の位置を計算（発射方向の逆ベクトル * 距離）
                    Vector3 recoilTargetPos =
                        shoots[i].transform.position - (shootDir * shootRecoilDistance);

                    // DoTweenで後退 (0.2秒かけて引く)
                    shoots[i]
                        .transform.DOMove(recoilTargetPos, shootRecoilTime)
                        .SetEase(Ease.OutQuad);

                    // 引く動作が終わるまで少し待つ
                    yield return new WaitForSeconds(shootRecoilTime);

                    // 発射
                    shootRb.velocity = shootDir * shootSpeed;

                    // 効果音を再生
                    sePlayer.Play(SE_EnemyAction.Shoot_Water1);

                    // 進行方向と速度に応じた回転（スピン）を与える
                    // 進行方向のXがプラス（右）なら時計回り(-)、マイナス（左）なら反時計回り(+)
                    float rotateDir = (shootDir.x >= 0) ? -1f : 1f;

                    // 弾の速度(shootSpeed)が速いほど、速く回転させる
                    shootRb.angularVelocity = rotateDir * shootSpeed * shootRotationMultiplier;
                }
            }

            // 次の弾まで待機
            yield return new WaitForSeconds(shootIntervalTime);
        }

        // 攻撃後の待機時間
        float afterAttackTime = Random.Range(minAfterAttackTime, maxAfterAttackTime);
        yield return new WaitForSeconds(afterAttackTime);

        // 移動再開
        animator.SetTrigger("IdleTrigger");
        currentState = DesertTempleGolemState.IdleMoving;

        // 攻撃後すぐに再攻撃しないようにクールダウンを設定
        yield return new WaitForSeconds(afterMoveAttackCooldown);
        currentState = DesertTempleGolemState.Moving;
    }

    /// <summary>
    /// 次のフレームで適用すべき速度ベクトルを計算する
    /// </summary>
    private Vector2 CalculateVelocity()
    {
        Vector2 currentPos = this.transform.position;
        float velocityX = rbody.velocity.x;
        float velocityY = 0f; // 地面がない場合は上下移動しない

        // --- 1. 横移動の速度 ---
        if (rightFlag)
        {
            velocityX = speedX;
            if (currentPos.x >= rightBound)
            {
                rightFlag = false;
                spriteRenderer.flipX = rightFlag;
            }
        }
        else
        {
            velocityX = -speedX;
            if (currentPos.x <= leftBound)
            {
                rightFlag = true;
                spriteRenderer.flipX = rightFlag;
            }
        }

        // --- 2. 高さ調整の速度（レイキャスト） ---
        // 足元へ向けてレイを飛ばす
        RaycastHit2D hit = Physics2D.Raycast(currentPos, Vector2.down, rayLength, groundLayer);

        if (hit.collider != null)
        {
            // 目標の高さ（地面のY + 指定高さ）
            float targetY = hit.point.y + targetHeightFromGround;

            // 現在の高さとの差分を計算
            float diffY = targetY - currentPos.y;

            // 差分に係数を掛けて、目標に向かう速度とする（P制御的なアプローチ）
            // これにより、遠いと速く、近づくとゆっくりになり、滑らかに追従します
            velocityY = diffY * heightAdjustSpeed;
        }
        else
        {
            // 地面が見つからない場合は、今のY速度を維持する（またはゆっくり下降させるなど）
            // ここでは維持を採用
            velocityY = rbody.velocity.y;
        }

        return new Vector2(velocityX, velocityY);
    }

    /// <summary>
    /// 自身の位置からプレイヤーへのベクトルを取得します
    /// </summary>
    private Vector2 GetVectorToPlayer()
    {
        if (playerTransform != null)
        {
            return (Vector2)playerTransform.position - (Vector2)this.transform.position;
        }
        return Vector2.zero;
    }

    /// <summary>
    /// プレイヤーが攻撃範囲内にいるか判定する
    /// </summary>
    private bool IsPlayerInAttackRange()
    {
        Vector2 dir = GetVectorToPlayer();
        return dir.x * (rightFlag ? 1 : -1) <= attackRange // プレイヤーがattackRange内の前方にいるか
            && dir.x * (rightFlag ? 1 : -1) >= 0 // プレイヤーが後方にいないか
            && dir.y < 0; // プレイヤーが上にいないか
    }

    private void OnDisable()
    {
        // 実行中のコルーチンをすべて強制停止
        // (Unityの仕様上、Disableで自動停止しますが、明示的に書くことで意図を明確にします)
        StopAllCoroutines();

        // 実行中のDoTweenアニメーション（後退動作など）を完全停止
        // これを行わないと、非アクティブ中や再アクティブ時にTweenが動き続けて位置がおかしくなります
        transform.DOKill();

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

    private void OnDrawGizmos()
    {
        // 移動範囲を示すGizmosを描画（半透明の赤い四角形）
        Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
        Vector3 center = new Vector3(
            (leftBound + rightBound) / 2,
            transform.position.y,
            transform.position.z
        );
        Vector3 size = new Vector3(Mathf.Abs(rightBound - leftBound), 3f, 0.1f);
        Gizmos.DrawCube(center, size);

        //攻撃感知範囲を示すGizmosを描画(青い線)
        Gizmos.color = new Color(0f, 0f, 1f, 0.3f);
        Vector3 attackCenter = new Vector3(
            transform.position.x + (rightFlag ? 1 : -1) * attackRange / 2,
            transform.position.y,
            transform.position.z
        );
        Vector3 attackSize = new Vector3(attackRange, 3f, 0.1f);
        Gizmos.DrawCube(attackCenter, attackSize);
    }
}

using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class DustDevilBossMoveController : MonoBehaviour, IEnemyResettable
{
    private const string DUST_DEVIL_POOL_TAG = "DustDevil";
    private const string DUST_DEVIL_ENEMY_POOL_TAG = "DustDevilEnemy";

    [Header("移動の設定")]
    [Tooltip("移動速度")]
    [SerializeField]
    private float moveSpeedX = 3.0f;

    [Header("移動範囲の設定(必須)")]
    [SerializeField]
    private float leftBound = 0;

    [SerializeField]
    private float rightBound = 0;

    [Header("攻撃力の設定")]
    [Tooltip("通常攻撃の攻撃力")]
    [SerializeField]
    private int normalAttackPower = 10;

    [Tooltip("近距離攻撃の攻撃力")]
    [SerializeField]
    private int closeAttackPower = 15;

    [Header("時間の設定")]
    [Tooltip("通常攻撃の時間間隔の最小値（秒）")]
    [SerializeField]
    private float minNormalAttackTime = 3.0f;

    [Tooltip("通常攻撃の時間間隔の最大値（秒）")]
    [SerializeField]
    private float maxNormalAttackTime = 6.0f;

    [Tooltip("近距離攻撃の時間間隔の最小値（秒）")]
    [SerializeField]
    private float minCloseAttackTime = 5.0f;

    [Tooltip("近距離攻撃の時間間隔の最大値（秒）")]
    [SerializeField]
    private float maxCloseAttackTime = 8.0f;

    [Tooltip("近距離攻撃の発射前待機時間（秒）")]
    [SerializeField]
    private float closeAttackPreWaitTime = 1.0f;

    [Tooltip("近距離攻撃の発射後硬直時間（秒）")]
    [SerializeField]
    private float closeAttackPostWaitTime = 1.0f;

    [Header("攻撃パラメータの設定")]
    [Tooltip("近距離攻撃を行うプレイヤーとの距離")]
    [SerializeField]
    private float closeAttackRange = 5.0f;

    [Tooltip("通常攻撃の弾の速度")]
    [SerializeField]
    private float normalAttackSpeed = 10.0f;

    [Tooltip("通常攻撃の発射する位置のオフセット")]
    [SerializeField]
    private Vector2 normalAttackOffset = new Vector2(0, 1f);

    [Tooltip("近距離攻撃の弾の速度")]
    [SerializeField]
    private float closeAttackSpeed = 8.0f;

    [Tooltip("近距離攻撃の発射する位置のオフセット（xは左右対称に使用）")]
    [SerializeField]
    private Vector2 closeAttackOffset = new Vector2(1.5f, 0.5f);

    [Tooltip("通常時のDustDevil消滅時にDustDevilEnemyを生成する確率 (0.0 ~ 1.0)")]
    [SerializeField, Range(0f, 1f)]
    private float spawnEnemyProbability = 0.1f;

    [Tooltip("HPが半分以下になったときのDustDevil消滅時にDustDevilEnemyを生成する確率 (0.0 ~ 1.0)")]
    [SerializeField, Range(0f, 1f)]
    private float spawnEnemyProbabilityWhenHpBelowHalf = 0.2f;

    [Header("その他の設定")]
    [Tooltip("地面から浮かせたい高さ（Y座標のオフセット）")]
    [SerializeField]
    private float targetHeightFromGround = 0.0f;

    [Tooltip("SpiralWindEffectを持つ子オブジェクト")]
    [SerializeField]
    private GameObject spiralWindEffectObject;

    // --- 内部変数 ---
    private float currentVx = 0; // 現在の移動速度Xを保持
    private float maxCheckDistance = 20.0f; // 地面を探す最大距離
    private bool leftFlag = true;
    private bool isHPbelowHalf = false; // HPが半分以下かどうかのフラグ
    private LayerMask groundLayer;
    private DustDevilBossState currentState = DustDevilBossState.Idle;

    // --- タイマー関連 ---
    private float normalAttackTimer = 0f;
    private float closeAttackTimer = 0f;
    private float currentNormalAttackInterval = 0f;
    private float currentCloseAttackInterval = 0f;
    private float stateTimer = 0f; // ステート内の経過時間計測用

    private enum DustDevilBossState
    {
        Idle, // 移動中・待機中
        PreparingToAttack, // 近距離攻撃前の停止・予備動作
        Recovering, // 攻撃後の硬直
    }

    // --- 内部参照 ---
    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rbody;
    private Animator _animator;
    private CharacterHealth _characterHpScript;
    private CriWare.Assets.CriAtomSePlayer _sePlayer;

    // --- 外部参照 ---
    private Transform playerTransform;

    private void Awake()
    {
        groundLayer = LayerMask.GetMask(GameConstants.PHYSICS_LAYER_NAME_GROUND);

        _spriteRenderer = GetComponent<SpriteRenderer>();
        _rbody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _characterHpScript = GetComponent<CharacterHealth>();
        _characterHpScript.OnHPChanged += HandleHpChanged; // HP変化イベントを購読
        _sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();

        if (leftBound == 0 || rightBound == 0)
        {
            Debug.LogError($"移動範囲が設定されていません。", this);
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
        }

        // 初期の移動方向決定
        currentVx = (Random.value < 0.5f ? -1 : 1) * moveSpeedX;
        leftFlag = currentVx < 0;
        _spriteRenderer.flipX = leftFlag;

        _rbody.velocity = new Vector2(currentVx, 0);

        this.tag = GameConstants.UNTAGGED_TAG_NAME;
        _sePlayer.Play(SE_Field.WindGust_strong);
        AdjustHeight();
        _animator.SetTrigger("IdleTrigger");

        // 攻撃タイマーのリセット
        ResetNormalAttackTimer();
        ResetCloseAttackTimer();

        // 変数リセット
        isHPbelowHalf = false;
        stateTimer = 0f;

        currentState = DustDevilBossState.Idle;

        // 初期状態は不透明（Alpha 1）で表示
        ControlSpiralEffectFade(1f, 0f);
    }

    /// <summary>
    /// 通常攻撃タイマーのリセット
    /// </summary>
    private void ResetNormalAttackTimer()
    {
        normalAttackTimer = 0f;
        currentNormalAttackInterval = Random.Range(minNormalAttackTime, maxNormalAttackTime);
    }

    /// <summary>
    /// 近距離攻撃タイマーのリセット
    /// </summary>
    private void ResetCloseAttackTimer()
    {
        closeAttackTimer = 0f;
        currentCloseAttackInterval = Random.Range(minCloseAttackTime, maxCloseAttackTime);
    }

    /// <summary>
    /// レイキャストを使って地面を検出し、Y座標を調整する処理
    /// </summary>
    public void AdjustHeight()
    {
        Vector2 origin = new Vector2(transform.position.x, transform.position.y);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, maxCheckDistance, groundLayer);

        if (hit.collider != null)
        {
            Vector3 newPos = transform.position;
            newPos.y = hit.point.y + targetHeightFromGround;
            transform.position = newPos;
        }
    }

    private void FixedUpdate()
    {
        // 敵の動きがポーズされているかどうかを確認
        // もしポーズされていればRigidbody2Dを無効化する
        if (TimeManager.instance.isEnemyMovePaused)
        {
            if (_rbody.simulated)
                _rbody.simulated = false;
            return;
        }
        else if (!_rbody.simulated)
            _rbody.simulated = true;

        // 状態ごとの処理
        switch (currentState)
        {
            case DustDevilBossState.Idle:
                HandleIdleState();
                break;

            case DustDevilBossState.PreparingToAttack:
                stateTimer += Time.fixedDeltaTime;
                // 待機時間経過したら攻撃実行
                if (stateTimer >= closeAttackPreWaitTime)
                {
                    ExecuteCloseRangeAttack();
                    currentState = DustDevilBossState.Recovering;
                    stateTimer = 0f;
                }
                break;

            case DustDevilBossState.Recovering:
                stateTimer += Time.fixedDeltaTime;
                // 硬直時間が終わったら移動再開
                if (stateTimer >= closeAttackPostWaitTime)
                {
                    currentState = DustDevilBossState.Idle;
                    _rbody.velocity = new Vector2(currentVx, 0); // 移動再開
                    _animator.SetTrigger("IdleTrigger");

                    // 移動再開時にフェードインで戻す (0.5秒かけて表示)
                    ControlSpiralEffectFade(1f, 0.5f);

                    ResetCloseAttackTimer(); // クールダウン開始
                }
                break;
        }

        // 向きの更新
        bool isTargetCurrentlyLeft = IsTargetToLeft();
        if (leftFlag != isTargetCurrentlyLeft)
        {
            leftFlag = isTargetCurrentlyLeft;
            _spriteRenderer.flipX = leftFlag;
        }
    }

    /// <summary>
    /// アイドル（移動）状態の処理
    /// </summary>
    private void HandleIdleState()
    {
        // --- 移動処理 ---
        // 範囲端に到達したら反転
        if (transform.position.x <= leftBound && currentVx < 0)
        {
            currentVx = Mathf.Abs(moveSpeedX);
        }
        else if (transform.position.x >= rightBound && currentVx > 0)
        {
            currentVx = -Mathf.Abs(moveSpeedX);
        }
        _rbody.velocity = new Vector2(currentVx, 0);

        AdjustHeight(); // 移動中は常に高さを合わせる

        // --- タイマー更新 ---
        float dt = Time.fixedDeltaTime;
        normalAttackTimer += dt;
        closeAttackTimer += dt;

        // --- 攻撃判定 ---
        // 優先度高: 近距離攻撃
        if (closeAttackTimer >= currentCloseAttackInterval)
        {
            float dist = Vector2.Distance(transform.position, playerTransform.position);
            if (dist <= closeAttackRange)
            {
                // 近距離攻撃の準備へ移行
                currentState = DustDevilBossState.PreparingToAttack;
                _animator.SetTrigger("ChargeTrigger");
                _rbody.velocity = Vector2.zero; // 停止
                stateTimer = 0f;

                // 攻撃準備（チャージ）に入ったらエフェクトを消す（もし必要なら）
                // ※ ここで消すか、攻撃実行時(ExecuteCloseRangeAttack)で消すかは演出次第ですが、
                // 今回は「近距離攻撃中」に消すという要望と解釈して、攻撃実行時に消します。
                // もしチャージ中も消したい場合はここに記述してください。
                //攻撃準備時間(closeAttackPreWaitTime)をかけてフェードアウトさせる
                ControlSpiralEffectFade(0f, closeAttackPreWaitTime);
            }
        }

        // 優先度低: 通常攻撃
        if (normalAttackTimer >= currentNormalAttackInterval)
        {
            ExecuteNormalAttack();
            ResetNormalAttackTimer();
        }
    }

    /// <summary>
    /// 通常攻撃：プレイヤーへ向けて放物線を描く弾を発射
    /// </summary>
    private void ExecuteNormalAttack()
    {
        if (playerTransform == null)
            return;

        // 生成位置
        Vector3 spawnPos = transform.position + (Vector3)normalAttackOffset;

        // ObjectPoolから取得
        GameObject dust = ObjectPooler.SceneInstance.SpawnFromPool(
            DUST_DEVIL_POOL_TAG,
            spawnPos,
            Quaternion.identity
        );

        if (dust != null)
        {
            // --- 地面到達時の敵生成処理 ---
            var lifecycle = dust.GetComponent<PoolableObjectLifecycle>();
            if (lifecycle != null)
            {
                // イベントに登録 (PoolableObjectLifecycle側でOnDisable時に解除されるため、登録だけでOK)
                lifecycle.OnContactLimitReached += () =>
                {
                    // 確率判定
                    if (
                        Random.value
                        <= (
                            isHPbelowHalf
                                ? spawnEnemyProbabilityWhenHpBelowHalf
                                : spawnEnemyProbability
                        )
                    )
                    {
                        // 敵生成エフェクトの生成
                        ObjectPooler.PersistentInstance.SpawnFromPool(
                            GameConstants.EFFECT_ENEMY_SPAWN_POOLTAG,
                            dust.transform.position,
                            Quaternion.identity
                        );

                        // 弾が消えた場所(dust.transform.position)に敵を生成
                        ObjectPooler.SceneInstance.SpawnFromPool(
                            DUST_DEVIL_ENEMY_POOL_TAG,
                            dust.transform.position, // 消滅地点
                            Quaternion.identity
                        );
                    }
                };
            }

            // 弾道計算
            Vector3? velocity = CalculateVelocityFixedSpeed(
                spawnPos,
                playerTransform.position,
                Vector3.zero,
                normalAttackSpeed,
                true // 山なり
            );

            Rigidbody2D dustRb = dust.GetComponent<Rigidbody2D>();
            if (dustRb != null)
            {
                dustRb.gravityScale = 1; // 重力有効
                if (velocity.HasValue)
                {
                    dustRb.velocity = velocity.Value;
                }
                else
                {
                    // 届かない場合は単純にプレイヤー方向へ飛ばす
                    Vector2 dir = (playerTransform.position - spawnPos).normalized;
                    dustRb.velocity = dir * normalAttackSpeed;
                }
            }

            var contactDamageController = dust.GetComponent<ContactDamageController>();
            {
                if (contactDamageController != null)
                {
                    contactDamageController.SetNormalDamage(normalAttackPower);
                }
                else
                {
                    Debug.LogError($"{dust.name}にContactDamageControllerが見つかりません。");
                }
            }
        }

        _sePlayer.Play(SE_EnemyAction.Attack_wind1); // 仮のSE
    }

    /// <summary>
    /// 近距離攻撃：左右に弾を生成して水平発射
    /// </summary>
    private void ExecuteCloseRangeAttack()
    {
        // 攻撃アニメーション開始
        _animator.SetTrigger("AttackTrigger");
        // 左側の弾生成
        SpawnCloseRangeBullet(true);
        // 右側の弾生成
        SpawnCloseRangeBullet(false);
        // 攻撃音再生
        _sePlayer.Play(SE_EnemyAction.Attack_wind1);
    }

    /// <summary>
    /// 近距離攻撃用の弾を生成して発射する
    /// </summary>
    private void SpawnCloseRangeBullet(bool isLeft)
    {
        // オフセット計算 (xを反転)
        Vector3 offset = closeAttackOffset;
        if (isLeft)
            offset.x = -Mathf.Abs(offset.x);
        else
            offset.x = Mathf.Abs(offset.x);

        Vector2 spawnPos = transform.position + offset;

        GameObject dust = ObjectPooler.SceneInstance.SpawnFromPool(
            DUST_DEVIL_POOL_TAG,
            spawnPos,
            Quaternion.identity
        );

        if (dust != null)
        {
            Rigidbody2D rb = dust.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0; // 重力無効
            if (rb != null)
            {
                // 指定した速度で左右に飛ばす
                Vector2 dir = isLeft ? Vector2.left : Vector2.right;
                rb.velocity = dir * closeAttackSpeed;
            }

            var contactDamageController = dust.GetComponent<ContactDamageController>();
            {
                if (contactDamageController != null)
                {
                    contactDamageController.SetNormalDamage(closeAttackPower);
                }
                else
                {
                    Debug.LogError($"{dust.name}にContactDamageControllerが見つかりません。");
                }
            }
        }
        else
        {
            Debug.LogError($"{DUST_DEVIL_POOL_TAG}の取得に失敗しました。");
        }
    }

    /// <summary>
    /// SpiralWindEffectの透明度をDoTweenで制御するヘルパーメソッド
    /// </summary>
    /// <param name="targetAlpha">目標の透明度 (0~1)</param>
    /// <param name="duration">変化にかかる時間</param>
    private void ControlSpiralEffectFade(float targetAlpha, float duration)
    {
        if (spiralWindEffectObject == null)
            return;

        // フェードインしようとしているなら、まずはActiveにする
        if (targetAlpha > 0f)
        {
            spiralWindEffectObject.SetActive(true);
        }

        // 子オブジェクトに含まれる全てのSpriteRendererを取得してフェード
        var renderers = spiralWindEffectObject.GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in renderers)
        {
            sr.DOKill(); // 重複動作を防ぐためTweenをリセット
            sr.DOFade(targetAlpha, duration).SetUpdate(false); // TimeScaleの影響を受けるように
        }

        // 完全に消える(0f)設定で、かつ時間が経過した後ならSetActive(false)にしても良いが、
        // アニメーションループを維持したい場合はActiveのままAlpha0にするのが安全。
        // ここではAlpha操作のみ行います。
    }

    /// <summary>
    /// 自身の位置からプレイヤーへのベクトルを取得します
    /// </summary>
    /// <returns>プレイヤーへのベクトル</returns>
    private Vector2 GetVectorToPlayer()
    {
        if (playerTransform != null)
            return (Vector2)playerTransform.position - (Vector2)transform.position;
        return Vector2.zero;
    }

    /// <summary>
    /// プレイヤーが攻撃範囲内にいるか判定する
    /// </summary>
    /// <returns>攻撃範囲内にいる場合はtrue</returns>
    private bool IsTargetToLeft()
    {
        Vector2 dir = GetVectorToPlayer();
        return dir.x < 0;
    }

    private void HandleHpChanged(int _currentHp)
    {
        float normalizedHp = _characterHpScript.NormalizedHP;
        if (!isHPbelowHalf && normalizedHp <= 0.5f)
        {
            // HPが半分以下になった瞬間の処理
            isHPbelowHalf = true;
        }
    }

    /// <summary>
    /// 固定速度で目標に向かう放物線の初速を計算します
    /// </summary>
    /// <param name="startPos">発射位置</param>
    /// <param name="targetPos">目標位置</param>
    /// <param name="targetVelocity">目標の速度</param>
    /// <param name="speed">弾の速度</param>
    /// <param name="useHighArc">高い放物線を使うか
    /// （falseの場合は低い放物線）</param>
    /// <returns>初速ベクトル。到達不可能な場合はnull</returns>
    private Vector3? CalculateVelocityFixedSpeed(
        Vector3 startPos,
        Vector3 targetPos,
        Vector3 targetVelocity,
        float speed,
        bool useHighArc
    )
    {
        float gravity = Mathf.Abs(Physics2D.gravity.y);
        if (gravity <= 0)
            return (targetPos - startPos).normalized * speed;

        Vector3 aimPos = targetPos;
        Vector3 finalVelocity = Vector3.zero;
        int iterations = 4;

        for (int i = 0; i < iterations; i++)
        {
            Vector3 dir = aimPos - startPos;
            float h = dir.y;
            dir.y = 0;
            dir.z = 0;
            float x = dir.magnitude;
            if (x <= 0.0001f)
                x = 0.0001f;

            float v2 = speed * speed;
            float v4 = v2 * v2;
            float discriminant = v4 - gravity * (gravity * x * x + 2 * h * v2);

            if (discriminant < 0)
                return null;

            float sqrtD = Mathf.Sqrt(discriminant);
            float tanTheta = (v2 + (useHighArc ? sqrtD : -sqrtD)) / (gravity * x);
            float angle = Mathf.Atan(tanTheta);

            Vector3 horizontalDir = dir.normalized;
            finalVelocity =
                horizontalDir * speed * Mathf.Cos(angle) + Vector3.up * speed * Mathf.Sin(angle);

            float vx = speed * Mathf.Cos(angle);
            if (vx < 0.001f)
                break;
            float t = x / vx;
            aimPos = targetPos + targetVelocity * t;
        }
        return finalVelocity;
    }

    private void OnDrawGizmosSelected()
    {
        // --- 1. 通常攻撃の生成位置 (赤色) ---
        Gizmos.color = Color.red;
        Vector3 normalPos = transform.position + (Vector3)normalAttackOffset;
        Gizmos.DrawWireSphere(normalPos, 0.3f);
        Gizmos.DrawLine(transform.position, normalPos);

        // --- 2. 近距離攻撃の生成位置 (黄色) ---
        Gizmos.color = Color.yellow;

        // 右側 (絶対値)
        Vector3 rightClosePos =
            transform.position
            + new Vector3(Mathf.Abs(closeAttackOffset.x), closeAttackOffset.y, 0);
        Gizmos.DrawWireSphere(rightClosePos, 0.3f);
        Gizmos.DrawLine(transform.position, rightClosePos);

        // 左側 (x反転)
        Vector3 leftClosePos =
            transform.position
            + new Vector3(-Mathf.Abs(closeAttackOffset.x), closeAttackOffset.y, 0);
        Gizmos.DrawWireSphere(leftClosePos, 0.3f);
        Gizmos.DrawLine(transform.position, leftClosePos);

        // --- 3. 近距離攻撃の「検知範囲」 (シアン色) ---
        // プレイヤーがこの円の中に入ると近距離攻撃モードになる距離
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, closeAttackRange);

        // --- 4. 文字ラベルの表示 (エディタ限定) ---
#if UNITY_EDITOR
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white; // 文字色
        style.alignment = TextAnchor.MiddleCenter; // 中央揃え

        // 近距離検知範囲のラベル（円の上端に表示）
        Vector3 rangeLabelPos = transform.position + Vector3.up * (closeAttackRange + 0.5f);
        UnityEditor.Handles.Label(
            rangeLabelPos,
            $"Close Attack Trigger Range\n({closeAttackRange:F1}m)",
            style
        );
#endif
    }

    private void OnDrawGizmos()
    {
        // --- 移動範囲 ---
        Gizmos.color = new Color(1f, 0f, 0f, 0.15f); // 移動範囲は半透明の赤
        Vector3 center = new Vector3(
            (leftBound + rightBound) / 2f,
            transform.position.y + 7.25f / 2f,
            transform.position.z
        );
        Vector3 size = new Vector3(rightBound - leftBound, 7.25f, 0.1f);
        Gizmos.DrawCube(center, size);
    }
}

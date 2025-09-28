using System.Collections;
using UnityEngine;

public class SlimeBossMoveController : MonoBehaviour
{
    [SerializeField]
    private Transform PlayerTransform = null;

    [Header("移動・攻撃の基本設定")]
    [SerializeField]
    private int damage = 0;

    [SerializeField]
    private float speedX = 4.0f;

    [SerializeField]
    private float startDetectRange = 5.0f; // プレイヤーとの距離がこの値以下で移動を開始

    [SerializeField]
    private float attackRange = 1.5f;

    [SerializeField]
    private float jumpPower = 1.0f;

    [SerializeField]
    private float jumpCooldown = 1.0f; // ジャンプ後の休止時間

    [Header("高ジャンプの設定")]
    [Header("特殊行動の設定")]
    [
        SerializeField,
        Range(0, 1),
        Tooltip("このHP割合以上のダメージを蓄積すると、プレイヤーの方向へ強制的に振り向く")
    ]
    private float forceTurnHpThreshold = 0.1f; // デフォルトは10%

    [SerializeField]
    private float highJumpPower = 2.0f;

    [SerializeField]
    private float highJumpCooldown = 2.0f; // 高ジャンプ後の休止時間

    [SerializeField]
    private int jumpCountUntilPowerAttack = 3; // 高ジャンプを行うまでのジャンプ回数

    [Header("移動範囲の設定(必須)")]
    [SerializeField]
    private float leftBound;

    [SerializeField]
    private float rightBound;

    [Header("最初の位置設定")]
    [SerializeField]
    private float startPosX = 0f; // 初期位置

    [Header("地面判定用の設定")]
    [SerializeField]
    private Transform groundCheck;

    [SerializeField]
    private float groundCheckRadius = 0.2f;

    [SerializeField]
    private LayerMask GroundLayer;

    private float vx = 0;
    private float groundIgnoreAfterJumpTime = 0.1f;
    private float jumpStartTime;
    private int jumpCount = 0; // ジャンプ回数
    private int bossMaxHP; //最大HP
    private int bossHP; //現在のHP
    private int lastHp; // ダメージ計算用に直前のHPを保存
    private int accumulatedDamage = 0; // 振り向いてから蓄積されたダメージ量
    private bool isGrounded =>
        Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, GroundLayer);
    private bool rightFlag = false;
    private bool isHPbelowHalf => ((float)bossHP / (float)bossMaxHP) < 0.5f;
    private bool isMoveStarted = false; // 移動開始フラグ
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rbody;
    private Animator animator;
    private int IdleHash;
    private AnimatorStateInfo stateInfo;
    private ContactDamageController contactDamageController;
    private Vector2 pos = Vector2.zero;
    private CharacterHealth characterHpScript; // CharacterHealthの参照を保持

    private Vector2 centerPosition =>
        new Vector2((leftBound + rightBound) / 2f, transform.position.y);

    private enum SlimeState
    {
        None,
        Idle,
        Moving,
        Jumping,
        Recovering,
    }

    private SlimeState currentState = SlimeState.Idle;

    private void Awake()
    {
        if (groundCheck == null || GroundLayer == 0 || groundCheckRadius <= 0)
        {
            Debug.LogError($"{this.name}の地面判定用の設定が正しくありません。");
        }

        if (leftBound == 0 || rightBound == 0)
        {
            Debug.LogError($"{this.name}の移動範囲が設定されていません。");
        }
        spriteRenderer = GetComponent<SpriteRenderer>();
        rbody = GetComponent<Rigidbody2D>();
        rbody.constraints = RigidbodyConstraints2D.FreezeRotation;

        characterHpScript = this.GetComponent<CharacterHealth>(); //hpのscriptを取得
        if (characterHpScript == null)
        {
            Debug.LogError($"{this.name}はCharacterHealthスクリプトを持っていません");
            return;
        }
        characterHpScript.enabled = false; // CharacterHealthのスクリプトを無効化

        this.tag = "Untagged"; //ダメージ判定を無効化するために、タグを"Untagged"に設定
    }

    private void Start()
    {
        animator = GetComponent<Animator>();
        IdleHash = Animator.StringToHash("Blue Idle - Animation");

        contactDamageController = GetComponent<ContactDamageController>();
        contactDamageController?.SetDamageAmount(damage);

        // HP変化イベントを購読
        characterHpScript.OnHPChanged += HandleHpChanged;

        ResetState();
    }

    private void OnDisable()
    {
        if (characterHpScript != null)
        {
            characterHpScript.OnHPChanged -= HandleHpChanged;
        }
    }

    public void ResetState()
    {
        if (PlayerTransform == null)
        {
            PlayerTransform = GameObject
                .FindGameObjectWithTag(GameConstants.PlayerTagName)
                ?.transform;
            if (PlayerTransform == null)
            {
                Debug.LogError($"{this.name}はPlayerTransformを見つけられませんでした");
            }
        }

        bossMaxHP = characterHpScript.MaxHP; //最大HPを取得
        lastHp = bossMaxHP; // HPの初期値を保存
        accumulatedDamage = 0; // 蓄積ダメージをリセット

        vx = (Random.value < 0.5f ? -1 : 1) * speedX;
        rightFlag = vx > 0;
        spriteRenderer.flipX = !rightFlag;
        rbody.velocity = new Vector2(vx, 0);

        this.tag = "Untagged"; //ダメージ判定を無効化するために、タグを"Untagged"に設定
        animator.Play("Blue Idle - Animation");
        currentState = SlimeState.Moving;
        isMoveStarted = false;

        Vector3 startPos = transform.position;
        if (startPos.x != 0)
        {
            startPos.x = startPosX;
        }
        else
        {
            startPos.x = (leftBound + rightBound) / 2f; // 初期位置を移動範囲の中央に設定
        }

        transform.position = startPos;
    }

    private void Update()
    {
        animator.SetBool("isGrounded", isGrounded);
        animator.SetFloat("verticalSpeed", rbody.velocity.y);
    }

    private void FixedUpdate()
    {
        if (PlayerTransform == null)
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

        pos = transform.position;
        Vector3 dir = (Vector2)PlayerTransform.position - pos;

        if (!isMoveStarted)
        {
            if (dir.magnitude <= startDetectRange)
            {
                isMoveStarted = true;
                tag = GameConstants.ImmuneEnemyTagName; // ダメージ判定を有効化
                this.GetComponent<BossHealth>().enabled = true; // BossHealthのスクリプトを有効化
                BGMManager.instance?.Play(BGMCategory.Boss_Mid);
            }
            else
            {
                return; // プレイヤーが近づくまで何もしない
            }
        }

        switch (currentState)
        {
            case SlimeState.Moving:
                if ((pos.x <= leftBound && vx <= 0) || (rightBound <= pos.x && 0 <= vx))
                {
                    rightFlag = !rightFlag;
                    vx = speedX * (rightFlag ? 1 : -1);
                    spriteRenderer.flipX = !rightFlag;
                    accumulatedDamage = 0; // 壁際で振り向いた時も蓄積ダメージをリセット
                }
                rbody.velocity = new Vector2(vx, rbody.velocity.y);

                bool inRange =
                    dir.x * (rightFlag ? 1 : -1) <= attackRange
                    && dir.x * (rightFlag ? 1 : -1) >= 0;

                if (inRange)
                {
                    currentState = SlimeState.Jumping;
                    jumpStartTime = Time.time;
                    tag = GameConstants.DamageableEnemyTagName;
                    animator.SetTrigger("jumpTriggered");

                    if (isHPbelowHalf)
                    {
                        jumpCount++;
                    }

                    if (isHPbelowHalf && jumpCount >= jumpCountUntilPowerAttack)
                    {
                        Vector2 startPos = transform.position;
                        Vector2 targetPos = centerPosition;

                        // 垂直方向の初速
                        float initialVy = highJumpPower;

                        // 重力加速度
                        float gravity = Mathf.Abs(Physics2D.gravity.y);

                        // 上昇 + 下降にかかる合計時間 = 2Vy / g
                        float totalAirTime = (2f * initialVy) / gravity;

                        // 水平方向の距離
                        float dx = targetPos.x - startPos.x;

                        // 必要な水平方向速度
                        float vx = dx / totalAirTime;

                        // 初速度を設定
                        rbody.velocity = new Vector2(vx, 0f);
                        rbody.AddForce(new Vector2(0f, initialVy), ForceMode2D.Impulse);
                        SEManager.instance?.PlayEnemyActionSE(SE_EnemyAction.Attack_slime1); // 高ジャンプ攻撃の効果音を鳴らす
                    }
                    else
                    {
                        rbody.AddForce(new Vector2(0, jumpPower), ForceMode2D.Impulse);
                        SEManager.instance?.PlayEnemyActionSE(SE_EnemyAction.Attack_slime1); // ジャンプ攻撃の効果音を鳴らす
                    }
                }
                break;

            case SlimeState.Jumping:
                if (
                    rbody.velocity.y <= 0
                    && isGrounded
                    && (Time.time - jumpStartTime > groundIgnoreAfterJumpTime)
                )
                {
                    currentState = SlimeState.Recovering;

                    // HPが半分以下か確認
                    bossHP = characterHpScript.CurrentHP; // 現在のHPを取得

                    if (isHPbelowHalf && jumpCount >= jumpCountUntilPowerAttack)
                    {
                        jumpCount = 0;
                        SEManager.instance?.PlayEnemyActionSE(SE_EnemyAction.Land_enemy1); // 高ジャンプの着地音を鳴らす

                        // 高ジャンプの後に休憩してから通常行動に戻す
                        StartCoroutine(RecoverFromHighJump());
                        break;
                    }

                    StartCoroutine(RecoverFromJump());
                }
                break;
        }
    }

    private IEnumerator RecoverFromJump()
    {
        tag = GameConstants.ImmuneEnemyTagName;
        yield return new WaitForSeconds(jumpCooldown);
        currentState = SlimeState.Moving;
    }

    private IEnumerator RecoverFromHighJump()
    {
        tag = GameConstants.ImmuneEnemyTagName;
        yield return new WaitForSeconds(highJumpCooldown);
        currentState = SlimeState.Moving;
    }

    /// <summary>
    /// boss_HPからHPの変化通知を受け取るイベントハンドラ
    /// </summary>
    private void HandleHpChanged(int newHp)
    {
        // ダメージを受けた場合のみ処理
        if (newHp < lastHp)
        {
            int damageTaken = lastHp - newHp;
            accumulatedDamage += damageTaken;
            CheckForForcedTurnaround(); // 振り向き条件をチェック
        }
        lastHp = newHp; // 現在のHPを保存
    }

    /// <summary>
    /// ダメージ蓄積による強制振り向きの条件をチェックする
    /// </summary>
    private void CheckForForcedTurnaround()
    {
        // 必要なダメージ量の閾値を計算
        float requiredDamage = bossMaxHP * forceTurnHpThreshold;

        // 条件1: 蓄積ダメージが閾値を超えているか
        if (accumulatedDamage < requiredDamage)
            return;

        // 条件2: プレイヤーが自分の背中側にいるか
        bool isPlayerBehind = (PlayerTransform.position.x - transform.position.x > 0) != rightFlag;
        if (!isPlayerBehind)
            return;

        // 条件を満たした場合、強制的に振り向く
        ForceTurnTowardsPlayer();
    }

    /// <summary>
    /// プレイヤーの方向へ即座に振り向く
    /// </summary>
    private void ForceTurnTowardsPlayer()
    {
        // プレイヤーがいる方向を計算
        bool shouldFaceRight = PlayerTransform.position.x > transform.position.x;

        // 向きを更新
        rightFlag = shouldFaceRight;
        spriteRenderer.flipX = !rightFlag;
        vx = speedX * (rightFlag ? 1 : -1);
        rbody.velocity = new Vector2(vx, rbody.velocity.y);

        // 蓄積ダメージをリセット
        accumulatedDamage = 0;
    }

    /// OnDrawGizmosSelectedメソッドに索敵範囲と攻撃範囲の表示を追加
    private void OnDrawGizmosSelected()
    {
        // --- 地面判定の範囲 ---
        if (groundCheck != null)
        {
            Gizmos.color = Color.red; // 地面判定は赤
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        // --- 索敵開始の範囲 ---
        Gizmos.color = Color.yellow; // 索敵範囲は黄色
        Gizmos.DrawWireSphere(transform.position, startDetectRange);

        // --- 攻撃準備の範囲 ---
        Gizmos.color = new Color(1.0f, 0.5f, 0.0f); // 攻撃範囲はオレンジ
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    private void OnDrawGizmos()
    {
        // --- 移動範囲 ---
        Gizmos.color = new Color(1f, 0f, 0f, 0.15f); // 移動範囲は半透明の赤
        Vector3 center = new Vector3(
            (leftBound + rightBound) / 2f,
            transform.position.y,
            transform.position.z
        );
        Vector3 size = new Vector3(rightBound - leftBound, 2f, 0.1f);
        Gizmos.DrawCube(center, size);
    }
}

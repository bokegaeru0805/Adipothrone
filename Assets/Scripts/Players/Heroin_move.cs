using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Heroin_move : MonoBehaviour
{
    #region Inspector Settings

    [Header("必須の子オブジェクト")]
    [SerializeField]
    private GameObject RobotObject;

    [SerializeField]
    private Sprite deathSprite; // 死亡時に表示するスプライト

    [Header("調整用パラメータ")]
    [SerializeField]
    private float bound1SoundIntervalTime = 5.0f; //体形1のときの揺れる効果音の間隔の時間

    [SerializeField]
    private float bound2SoundIntervalTime = 0.2f; //体形2のときの揺れる効果音の間隔の時間

    [SerializeField]
    private Transform groundCheck; // プレイヤーの足元のTransform

    [Header("影の設定")]
    [SerializeField]
    private GameObject shadowObject; // 影のオブジェクト（インスペクターで設定）
    #endregion

    #region Public Properties & Variables
    public bool rightFlag { get; private set; } = false; // 右向きかどうかのフラグ

    [HideInInspector]
    public Vector2 pos = new Vector2(0, 0); //自分の座標
    public Fungus.Flowchart flowchart = null;

    /// <summary>
    /// 外部から現在の無敵状態を読み取るための公開プロパティ
    /// </summary>
    public bool IsImmune => immunity;

    public float m_defaultSpeed { get; private set; } = 4.0f; // 通常の歩行速度

    // プレイヤーの可視状態が変化したときに呼び出されるイベント
    public event Action<bool> OnPlayerVisibilityChanged;

    #endregion

    #region Constants & Internal Parameters

    // --- 調整用パラメータ ---
    private const float BOUND1_SOUND_LENGHT = 3.395f; //体形1のときの揺れる効果音の長さ
    private const float BOUND2_SOUND_LENGHT = 1.384f; //体形2のときの揺れる効果音の長さ
    private const float DEFAULT_WALK_ANIMATION_DURATION = 0.500f; //元の一回の歩行アニメーションの秒数
    private float m_dashDefaultSpeed = 8.0f; //通常のダッシュ速度
    private float jumpHeight = 3.5f; // ジャンプで到達したい高さ

    // private float damageX = 3.0f; //ダメージを食らったときのx軸の移動具合
    private float MoveStart_Sec = 0.5f; //ダメージを食らったときの硬直無敵時間
    private float immunityDuration = 0.75f; //動ける無敵時間
    private float attackMoveSlowRate = 4.0f; //攻撃中の移動速度の減少率
    private float WalkTime = 1.46f; //一回の歩行アニメーションの秒数
    private float DashTime = 0.72f; //一回のダッシュアニメーションの秒数
    private bool isShadowEnabled = false; // 現在のエリアで影が有効かどうか
    private float shadowRayDistance = 20.0f; // 地面を探知する光線の長さ
    private List<EnvironmentArea> activeEnvironments = new List<EnvironmentArea>(); // 現在適用中の環境エリアリスト
    #endregion

    #region Internal State Variables

    // --- 内部状態変数 ---
    private float vx = 0; //実際のx方向の移動速度
    private float walkSpeed = 0; //歩行の速度
    private float dashSpeed = 0; //ダッシュの速度
    private float jumpForce = 0; // 内部的に計算されるジャンプ力
    private float BoundIntervalTime; //揺れる音を鳴らす間を記録する変数

    [SerializeField]
    private Vector2 groundCheckSize = new Vector2(0.8f, 0.2f); // 接地判定のサイズ(幅, 高さ)
    private float gravity; //重力の大きさを保存する変数
    private bool isAttacking = false; // 攻撃中かどうかのフラグ
    private bool immunity = false; //無敵かどうかのフラグ
    private bool isFadingOut = true; //不透明度が減少するかどうかのフラグ
    private bool move = true; //操作できるかどうかのフラグ
    private bool isFirstGetKey = false; //初めてキー入力をしたかどうかのフラグ
    private bool isRobotmove = false; //ロボットが動けるかどうかのフラグ
    private bool isGrounded = false; //接地しているかどうかのフラグ
    private bool wasGroundedLastFrame = true; //前のフレームで接地していたかどうかのフラグ
    private bool jumpRequested = false;
    private bool isDead = false; // プレイヤーが死亡しているかどうかのマスターフラグ
    private bool isPhysicsActive = true; // 外部から物理動作を有効化されているかどうか
    private RigidbodyType2D bodyTypeBeforePhysicsDisabled = RigidbodyType2D.Dynamic;
    private Vector2 currentCarrierVelocity = Vector2.zero; // 現在のリフト速度
    private bool isOnCarrier = false; // リフトに乗っているかどうかのフラグ
    private float currentHorizontalVelocity = 0f; // 現在の自力移動速度（滑る床の慣性計算用）
    private Color defaultCharacterColor; // インスペクターで設定された初期状態のデフォルト色を保存する変数
    #endregion

    #region Component References

    // --- 内部参照 ---
    private Rigidbody2D _rbody; // Rigidbody2Dコンポーネント
    private Animator _animator; // アニメータコンポーネント
    private SpriteRenderer _spriteRenderer;
    private Color _col; //SpriteRendererの色を保存するための変数
    private Robot_move robotMoveScript;
    private CriWare.Assets.CriAtomSePlayer sePlayer; // SE再生用のCriAtomSePlayerコンポーネント
    private LayerMask groundLayer; // 接地判定用のレイヤーマスク

    // --- Managers ---
    private GameManager gameManager;
    private PlayerManager playerManager;
    private PlayerEffectManager playerEffectManager;
    private PlayerBodyManager playerBodyManager;
    private InputManager inputManager;

    #endregion

    #region Unity Lifecycle Methods

    private void Awake()
    {
        InitializeComponents();
    }

    private void Start()
    {
        InitializeSettings();
    }

    private void OnEnable()
    {
        StartCoroutine(DelayedInitialization());
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void Update()
    {
        if (inputManager == null)
            return;

        // 初期化
        vx = 0;

        // ポーズ中、会話中、死亡中、物理停止中は入力を受け付けない
        if (Time.timeScale > 0f && !gameManager.IsTalking && !isDead && isPhysicsActive)
        {
            HandleFirstKeyInput();
            HandleMovementInput();
            HandleJumpInput();
        }
        else
        {
            HandleIdleState();
        }
    }

    private void FixedUpdate()
    {
        UpdateAnimatorParameters();
        UpdateRobotStatus();

        if (Time.timeScale > 0f && isPhysicsActive)
        {
            ApplyEnvironmentEffects();
            ApplyMovement();
            CheckGroundStatus();
            ExecuteJumpPhysics();
            HandleLanding();
            UpdateImmunityBlink();
            UpdateShadowPosition();

            // 座標更新とRobot同期
            pos = this.transform.position;
            RobotObject.SetActive(isRobotmove);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        HandleItemPickup(collision);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
    }

    #endregion

    #region Initialization Logic

    private void InitializeComponents()
    {
        groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );
        _animator = GetComponent<Animator>();
        _rbody = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();

        // 初期状態の色を完全にキャッシュし、現在の色(_col)のベースにする
        defaultCharacterColor = _spriteRenderer.color;
        _col = defaultCharacterColor;

        if (RobotObject == null)
        {
            Debug.LogError("RobotObjectが設定されていません。");
        }
        else
        {
            robotMoveScript = RobotObject.GetComponent<Robot_move>();
            if (robotMoveScript == null)
            {
                Debug.LogError("RobotObjectにRobot_moveスクリプトがアタッチされていません。");
            }
        }
    }

    private void InitializeSettings()
    {
        isFirstGetKey = true;
        gravity = Mathf.Abs(Physics2D.gravity.y * _rbody.gravityScale);

        if (gameObject.name != GameConstants.PLAYER_OBJECT_NAME)
        {
            Debug.LogError(
                $"{gameObject.name}の名前がGameConstants.PLAYER_OBJECT_NAMEと一致しません。"
            );
        }

        if (this.tag != GameConstants.PLAYER_TAG_NAME)
        {
            Debug.LogError(
                $"{this.gameObject.name}のタグがGameConstants.PLAYER_TAG_NAMEと一致しません。"
            );
        }
    }

    /// <summary>
    /// 全てのAwake/Startが完了するのを待ってから、初期化処理を実行するコルーチン
    /// </summary>
    private IEnumerator DelayedInitialization()
    {
        yield return new WaitForEndOfFrame();

        // 各マネージャーの取得
        gameManager = GameManager.instance;
        playerManager = PlayerManager.instance;
        playerEffectManager = PlayerEffectManager.instance;
        playerBodyManager = PlayerBodyManager.instance;
        inputManager = InputManager.instance;

        if (
            gameManager == null
            || playerManager == null
            || playerEffectManager == null
            || playerBodyManager == null
            || inputManager == null
        )
        {
            Debug.LogError("必要なマネージャーが見つかりませんでした。Heroin_moveは機能しません。");
            yield break;
        }

        // イベント購読
        SubscribeEvents();

        // 初期状態の反映
        HandleLoadingStateChanged(false);

        // 変数初期化
        _spriteRenderer.flipX = true;
        rightFlag = true;
        BoundIntervalTime = 0;
        isAttacking = false;
        immunity = false;
        move = true;
        isDead = false;
        OnPlayerVisibilityChanged?.Invoke(true);
    }

    private void SubscribeEvents()
    {
        playerManager.OnDamageReaction += ReactToDamage;
        playerManager.OnPlayerDied += HandlePlayerDeath;
        playerManager.OnPlayerRevived += ResetToLiveState;
        playerManager.OnBoolStatusChanged += OnAnyBoolStatusChanged;
        playerEffectManager.OnSpeedEffectChanged += CalculateMoveSpeed;
        playerBodyManager.OnChangeBodyState += GetBodyStateData;
        SaveLoadManager.OnLoadingStateChanged += HandleLoadingStateChanged;
        CameraMoveArea.OnPlayerEnteredArea += HandleAreaEntered;
        CameraMoveArea.OnPlayerExitedArea += HandleAreaExited;
    }

    private void UnsubscribeEvents()
    {
        if (!GameManager.isFirstGameSceneOpen)
            return;

        if (playerManager != null)
        {
            playerManager.OnBoolStatusChanged -= OnAnyBoolStatusChanged;
            playerManager.OnDamageReaction -= ReactToDamage;
            playerManager.OnPlayerDied -= HandlePlayerDeath;
            playerManager.OnPlayerRevived -= ResetToLiveState;
        }
        if (playerEffectManager != null)
            playerEffectManager.OnSpeedEffectChanged -= CalculateMoveSpeed;
        if (playerBodyManager != null)
            playerBodyManager.OnChangeBodyState -= GetBodyStateData;

        SaveLoadManager.OnLoadingStateChanged -= HandleLoadingStateChanged;
        CameraMoveArea.OnPlayerEnteredArea -= HandleAreaEntered;
        CameraMoveArea.OnPlayerExitedArea -= HandleAreaExited;

        // 状態リセット
        move = true;
        isDead = false;
        immunity = false;
        SetColorWithFixedBrightness(defaultCharacterColor);
        OnPlayerVisibilityChanged?.Invoke(false);
    }

    #endregion

    #region Input & Logic Methods (Called from Update)

    /// <summary>
    /// 初回キー入力時の向き初期化処理
    /// </summary>
    private void HandleFirstKeyInput()
    {
        if (!isFirstGetKey)
            return;

        if (inputManager.GetPlayerMoveRight())
        {
            SetFacingDirection(true);
            isFirstGetKey = false;
        }
        else if (InputManager.instance.GetPlayerMoveLeft())
        {
            SetFacingDirection(false);
            isFirstGetKey = false;
        }
    }

    /// <summary>
    /// 移動入力の処理
    /// </summary>
    private void HandleMovementInput()
    {
        if ((inputManager.GetPlayerMoveRight() || inputManager.GetPlayerMoveLeft()) && move)
        {
            bool movingRight = inputManager.GetPlayerMoveRight();

            // 画像の向きとアニメーション設定
            _spriteRenderer.flipX = movingRight;
            _animator.SetInteger("AnimState", 1);

            // 速度計算（ダッシュ or 歩行）
            bool isDashing = inputManager.GetPlayerDash();
            float currentSpeed = isDashing ? dashSpeed : walkSpeed;
            float direction = movingRight ? 1f : -1f;
            vx = currentSpeed * direction;

            // アニメーション速度調整
            float animDuration = isDashing ? DashTime : WalkTime;
            _animator.SetFloat("WalkSpeed", DEFAULT_WALK_ANIMATION_DURATION / animDuration);

            // 効果音処理
            if (isGrounded)
            {
                sePlayer.Play(SE_PlayerAction.Walk1);
            }
            PlayMoveSoundEffects(isDashing);

            // 向き情報の更新
            if (rightFlag != movingRight)
            {
                SetFacingDirection(movingRight);
            }
        }
        else
        {
            // 待機状態
            _animator.SetInteger("AnimState", 0);
        }
    }

    /// <summary>
    /// ジャンプ入力の受付
    /// </summary>
    private void HandleJumpInput()
    {
        if (inputManager.GetPlayerJump() && isGrounded && move && !GameManager.IsJumpCooldownActive)
        {
            jumpRequested = true;
        }
    }

    /// <summary>
    /// 操作不能時のアイドリング処理
    /// </summary>
    private void HandleIdleState()
    {
        _animator.SetInteger("AnimState", 0);
        if (isDead)
        {
            vx = 0;
            jumpRequested = false;
        }
    }

    /// <summary>
    /// ロボットも含めた向きの設定
    /// </summary>
    public void SetFacingDirection(bool isRight)
    {
        rightFlag = isRight;
        _spriteRenderer.flipX = isRight;
        robotMoveScript.SetRightFlag(isRight);
    }

    /// <summary>
    /// 歩行・ダッシュ中の特殊な効果音（バウンド音など）の再生
    /// </summary>
    private void PlayMoveSoundEffects(bool isDashing)
    {
        BoundIntervalTime += isDashing ? 2 * Time.deltaTime : Time.deltaTime;

        if (
            BoundIntervalTime >= BOUND2_SOUND_LENGHT + bound2SoundIntervalTime
            && playerBodyManager.BodyState == GameConstants.BODY_STATE_ARMED_2
        )
        {
            sePlayer.Play(SE_PlayerAction.Bound2);
            BoundIntervalTime = 0f;
        }
        else if (
            BoundIntervalTime >= BOUND1_SOUND_LENGHT + bound1SoundIntervalTime
            && playerBodyManager.BodyState == GameConstants.BODY_STATE_ARMED_1
        )
        {
            sePlayer.Play(SE_PlayerAction.GichiGichi1);
            BoundIntervalTime = 0f;
        }
    }

    #endregion

    #region Physics Logic Methods (Called from FixedUpdate)

    /// <summary>
    /// プレイヤーの物理動作を有効・無効に切り替えます。
    /// 無効化中はその場で停止し、有効化時は速度ゼロから物理動作を再開します。
    /// </summary>
    /// <param name="isActive">trueで有効化、falseで無効化</param>
    public void SetPhysicsActive(bool isActive)
    {
        if (_rbody == null || isPhysicsActive == isActive)
            return;

        // 死亡状態の物理停止を外部操作で解除しない。
        if (isActive && isDead)
            return;

        if (!isActive)
        {
            bodyTypeBeforePhysicsDisabled = _rbody.bodyType;
            isPhysicsActive = false;

            vx = 0f;
            currentHorizontalVelocity = 0f;
            currentCarrierVelocity = Vector2.zero;
            isOnCarrier = false;
            jumpRequested = false;

            _rbody.velocity = Vector2.zero;
            _rbody.angularVelocity = 0f;
            _rbody.bodyType = RigidbodyType2D.Kinematic;
            return;
        }

        _rbody.bodyType = bodyTypeBeforePhysicsDisabled;
        _rbody.velocity = Vector2.zero;
        _rbody.angularVelocity = 0f;
        isPhysicsActive = true;

        // 復帰直後の誤った着地判定・着地演出を防ぐ。
        CheckGroundStatus();
        wasGroundedLastFrame = isGrounded;
        ApplyEnvironmentEffects();
    }

    private void UpdateAnimatorParameters()
    {
        _animator.SetBool("IsGrounded", isGrounded);
        _animator.SetFloat("VerticalSpeed", _rbody.velocity.y);
    }

    private void UpdateRobotStatus()
    {
        if (RobotObject.activeInHierarchy)
        {
            isAttacking = robotMoveScript.isAttacking;
        }
    }

    /// <summary>
    /// 物理的な移動の適用（風、重力、落下制限、リフトの合成）
    /// </summary>
    private void ApplyMovement()
    {
        if (!move)
            return; // 操作不能時は速度更新しない

        // --- 1. 環境効果の集計 ---
        float globalSpeedMult = 1.0f; // 地形による速度倍率
        Vector2 totalWindVelocity = Vector2.zero; // 風の合成ベクトル
        float restrictFallSpeed = -1f; // 落下制限（-1は未設定を表す）
        float currentSlipAcceleration = 0f; // 滑る床の加速度（0なら滑らない）

        for (int i = 0; i < activeEnvironments.Count; i++)
        {
            var area = activeEnvironments[i];
            if (area == null)
                continue;

            // 速度倍率を乗算
            globalSpeedMult *= area.GlobalSpeedMultiplier;

            // 風ベクトルを加算（X軸だけでなくY軸も合成）
            totalWindVelocity += area.WindVelocity;

            // 落下制限の判定（最も「ゆっくり（値が小さい）」な制限を優先して採用する）
            if (area.MaxFallSpeed > 0f)
            {
                if (restrictFallSpeed < 0f || area.MaxFallSpeed < restrictFallSpeed)
                {
                    restrictFallSpeed = area.MaxFallSpeed;
                }
            }

            // 滑る床の判定（最も加速度が小さい＝一番滑るものを優先して採用する）
            if (area.SlipAcceleration > 0f)
            {
                if (
                    currentSlipAcceleration == 0f
                    || area.SlipAcceleration < currentSlipAcceleration
                )
                {
                    currentSlipAcceleration = area.SlipAcceleration;
                }
            }
        }

        // --- 2. プレイヤー入力に基づく基本速度の計算 (X軸) ---
        float targetVelocityX = 0f; // 目標とする速度

        if (!isAttacking)
        {
            // 通常時: 入力速度(vx) * 環境倍率
            targetVelocityX = vx * globalSpeedMult;
        }
        else
        {
            // 攻撃中: 減速適用 * 環境倍率
            targetVelocityX = (vx / attackMoveSlowRate) * globalSpeedMult;
        }

        // 滑る環境（接地中のみ）の慣性計算
        if (isGrounded && currentSlipAcceleration > 0f)
        {
            // 現在の速度から目標速度へ、指定した加速度で徐々に近づける
            currentHorizontalVelocity = Mathf.MoveTowards(
                currentHorizontalVelocity,
                targetVelocityX,
                currentSlipAcceleration * Time.fixedDeltaTime
            );
        }
        else
        {
            // 滑らない場合（または空中）は即座に目標速度を適用する
            currentHorizontalVelocity = targetVelocityX;
        }

        // --- 3. 最終速度の合成 ---

        // [X軸] 自力移動(慣性込み) + 風(X) + リフト慣性
        float finalVelocityX =
            currentHorizontalVelocity + totalWindVelocity.x + currentCarrierVelocity.x;

        // [Y軸] 現在の物理挙動(重力落下など) + 風(Y)
        // ※風(Y)は、重力とは別に「外力」として加算します（上昇気流ならプラス、吹き下ろしならマイナス）
        float finalVelocityY = _rbody.velocity.y + totalWindVelocity.y;

        // --- 4. ゆっくり落下ギミック（落下速度のクランプ） ---
        // 落下中（Yがマイナス）かつ、制限が設定されている場合
        if (restrictFallSpeed > 0f && finalVelocityY < -restrictFallSpeed)
        {
            // 現在の落下速度が制限を超えていたら、制限速度に書き換える
            finalVelocityY = -restrictFallSpeed;
        }

        // 速度を適用
        _rbody.velocity = new Vector2(finalVelocityX, finalVelocityY);
    }

    /// <summary>
    /// 接地判定
    /// </summary>
    private void CheckGroundStatus()
    {
        isGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);

        // リフトに乗っておらず、かつ地面に着地しているなら、リフト由来の慣性を消す
        if (!isOnCarrier && isGrounded)
        {
            // 徐々に減衰させるか、即座に切るか。
            // ここでは「着地したら慣性終了」として即座にゼロにする
            currentCarrierVelocity = Vector2.zero;
        }
    }

    /// <summary>
    /// ジャンプの物理計算とアニメーショントリガー
    /// </summary>
    private void ExecuteJumpPhysics()
    {
        if (!jumpRequested)
            return;

        jumpRequested = false;

        float finalJumpHeightMultiplier = 1.0f;
        for (int i = 0; i < activeEnvironments.Count; i++)
        {
            if (activeEnvironments[i] != null)
            {
                finalJumpHeightMultiplier *= activeEnvironments[i].JumpHeightMultiplier;
            }
        }

        float finalJumpHeight = jumpHeight * finalJumpHeightMultiplier;
        jumpForce = Mathf.Sqrt(2 * gravity * finalJumpHeight);
        _rbody.velocity = new Vector2(_rbody.velocity.x, jumpForce);

        // 体型に応じたジャンプアニメーション
        switch (playerBodyManager.AnimBodyState)
        {
            case GameConstants.ANIM_BODY_STATE_NORMAL:
                TriggerAnimation("Normal_JumpTrigger");
                break;
            case GameConstants.ANIM_BODY_STATE_ARMED_1:
                TriggerAnimation("Armed1_JumpTrigger");
                break;
            case GameConstants.ANIM_BODY_STATE_ARMED_2:
                TriggerAnimation("Armed2_JumpTrigger");
                break;
        }

        sePlayer.Play(SE_PlayerAction.Jump1);
    }

    /// <summary>
    /// 環境エリアのリストをチェックし、重力倍率などを適用するメソッド (新規追加)
    /// </summary>
    private void ApplyEnvironmentEffects()
    {
        float finalGravityMult = 1.0f;

        // 登録されている全エリアの重力倍率を掛け合わせる
        for (int i = 0; i < activeEnvironments.Count; i++)
        {
            if (activeEnvironments[i] != null)
            {
                finalGravityMult *= activeEnvironments[i].GravityMultiplier;
            }
        }

        // 重力を適用
        _rbody.gravityScale = GameConstants.PLAYER_GRAVITY_SCALE * finalGravityMult;
        // ジャンプ計算用のgravity変数も更新しておく
        gravity = Mathf.Abs(Physics2D.gravity.y * _rbody.gravityScale);
    }

    private void TriggerAnimation(string triggerName)
    {
        _animator.ResetTrigger(triggerName);
        _animator.SetTrigger(triggerName);
    }

    /// <summary>
    /// 着地時の処理
    /// </summary>
    private void HandleLanding()
    {
        if (!wasGroundedLastFrame && isGrounded)
        {
            sePlayer.Play(SE_PlayerAction.Land1);

            if (playerBodyManager.BodyState == GameConstants.BODY_STATE_ARMED_2)
            {
                sePlayer.Play(SE_PlayerAction.Bound1);
            }
            else if (playerBodyManager.BodyState == GameConstants.BODY_STATE_ARMED_1)
            {
                sePlayer.Play(SE_PlayerAction.Bound3);
            }
        }
        wasGroundedLastFrame = isGrounded;
    }

    /// <summary>
    /// 無敵時間の点滅処理
    /// </summary>
    private void UpdateImmunityBlink()
    {
        if (!immunity)
            return;

        if (_col.a <= 0.3f)
            isFadingOut = false;
        else if (_col.a >= 1.0f)
            isFadingOut = true;

        _col.a += isFadingOut ? -0.1f : +0.1f;
        SetColorWithFixedBrightness(_col);
    }

    private void HandleItemPickup(Collider2D collision)
    {
        if (Time.timeScale <= 0f)
            return;

        var script = collision.gameObject.GetComponent<DropItem>();
        if (script != null && !script.isTreasureBox)
        {
            if (script.DropMoney != 0)
            {
                playerManager.ChangeMoney(script.DropMoney);
                sePlayer.Play(SE_Field.CoinGet1);
            }

            // スキルドロップに触れた場合の取得処理
            if (script.isSkillDrop)
            {
                // 既にスキルを入手済みの場合は取得処理やFungusの起動を行わない
                if (!SkillManager.instance.IsSkillUnlocked(script.DropSkillID))
                {
                    SkillManager.instance.UnlockSkill(script.DropSkillID);

                    // Fungusを呼び出して「〇〇を手に入れた」メッセージを表示する
                    GameManager.instance.SkillGetFungus(script.DropSkillID);

                    sePlayer.Play(SE_SystemEvent.ItemGet2);
                }
            }

            if (script.DropID != null)
            {
                gameManager.AddAllTypeIDToInventory(script.DropID);
                sePlayer.Play(SE_SystemEvent.ItemGet2);
            }

            // オブジェクトプールに返却
            script.ReturnToPool();
        }
    }

    #endregion

    #region Damage & Death Logic

    /// <summary>
    /// ダメージリアクション（ノックバック処理）
    /// </summary>
    /// <param name="data">ノックバック情報</param>
    private void ReactToDamage(KnockbackData data)
    {
        if (Time.timeScale <= 0 || IsImmune)
            return;

        // ノックバック力が設定されている場合のみ、移動制御を奪って吹き飛ばす
        if (data.force > 0f)
        {
            move = false; // 操作不能にする

            Vector2 knockbackDir = Vector2.zero;

            // タイプに応じたベクトルの計算
            switch (data.type)
            {
                case KnockbackType.HorizontalFromSource:
                    // 敵(Source)とプレイヤー(pos)のX位置関係を見て、反対方向へ飛ばす
                    // プレイヤーが右にいれば右(1)、左にいれば左(-1)
                    float directionX = Mathf.Sign(pos.x - data.sourcePosition.x);
                    knockbackDir = new Vector2(directionX, 0.5f).normalized; // 0.5fは少し浮かせるため
                    break;

                case KnockbackType.RadialFromSource:
                    // 敵からプレイヤーへ向かうベクトル（全方位）
                    knockbackDir = (pos - data.sourcePosition).normalized;
                    break;

                case KnockbackType.FixedVector:
                    // 指定された固定ベクトル（正規化して使用）
                    knockbackDir = data.fixedDirection.normalized;
                    break;
            }

            // 速度の適用
            _rbody.velocity = knockbackDir * data.force;
        }

        // コルーチンの開始（点滅処理と、硬直解除）
        StartCoroutine(MoveStartCoroutine(data.force > 0f));
    }

    private IEnumerator MoveStartCoroutine(bool hasKnockback)
    {
        _col = defaultCharacterColor;
        immunity = true;

        yield return new WaitForSeconds(MoveStart_Sec); // 硬直時間

        // 死亡していない、かつノックバック発生による操作不能時のみ操作を復帰
        if (!isDead && hasKnockback)
        {
            move = true;
        }

        yield return new WaitForSeconds(immunityDuration); // 無敵時間残り

        immunity = false;
        _col.a = defaultCharacterColor.a;
        SetColorWithFixedBrightness(_col);
    }

    /// <summary>
    /// 外部から無敵時間を強制付与
    /// </summary>
    public void EnableInvincibility(float time)
    {
        StartCoroutine(EnableInvincibilityCoroutine(time));
    }

    private IEnumerator EnableInvincibilityCoroutine(float time)
    {
        _col = defaultCharacterColor;
        immunity = true;
        yield return new WaitForSeconds(time);
        immunity = false;
        _col.a = defaultCharacterColor.a;
        SetColorWithFixedBrightness(_col);
    }

    private void HandlePlayerDeath()
    {
        EnterDeathState();
    }

    public void EnterDeathState()
    {
        isDead = true;
        move = false;

        if (_rbody != null)
            SetPhysicsActive(false);

        if (_animator != null)
            _animator.enabled = false;
        if (_spriteRenderer != null && deathSprite != null)
            _spriteRenderer.sprite = deathSprite;

        immunity = false;
        if (_spriteRenderer != null)
            _spriteRenderer.color = defaultCharacterColor;
    }

    public void ResetToLiveState()
    {
        isDead = false;
        move = true;

        if (_rbody != null)
            SetPhysicsActive(true);
        if (_animator != null)
            _animator.enabled = true;
        if (_spriteRenderer != null)
            _spriteRenderer.color = defaultCharacterColor;
    }

    #endregion

    #region Event Handlers & Helpers

    private void CalculateMoveSpeed()
    {
        walkSpeed = playerEffectManager.CalculateFinalPlayerMoveSpeed(m_defaultSpeed);
        dashSpeed = playerEffectManager.CalculateFinalPlayerMoveSpeed(m_dashDefaultSpeed);
    }

    private void GetBodyStateData()
    {
        _animator.SetInteger("BodyState", playerBodyManager.AnimBodyState);
        UpdateShadowSize(); // 体形が変更されたときに影のサイズも更新する
    }

    /// <summary>
    /// 現在の体形(BodyState)に合わせて影のサイズ(Scale)を変更する
    /// </summary>
    private void UpdateShadowSize()
    {
        if (shadowObject == null)
            return;

        // PlayerBodyManagerから最新の体形状態を取得
        int currentBodyState = playerBodyManager.BodyState;

        // 体形に応じて影のスケールを切り替え
        switch (currentBodyState)
        {
            case GameConstants.BODY_STATE_NORMAL:
                shadowObject.transform.localScale = new Vector2(0.5f, 0.6f);
                break;
            case GameConstants.BODY_STATE_ARMED_1:
                shadowObject.transform.localScale = new Vector2(0.7f, 0.7f);
                break;
            case GameConstants.BODY_STATE_ARMED_2:
                shadowObject.transform.localScale = new Vector2(0.8f, 0.9f);
                break;
            case GameConstants.BODY_STATE_ARMED_3:
                shadowObject.transform.localScale = new Vector2(1.0f, 2.0f);
                break;
            default:
                shadowObject.transform.localScale = new Vector2(0.5f, 0.6f);
                break;
        }
    }

    private void HandleLoadingStateChanged(bool isLoading)
    {
        if (!isLoading)
        {
            GetBodyStateData();
            CalculateMoveSpeed();
            OnAnyBoolStatusChanged(
                PlayerStatusBoolName.isRobotmove,
                playerManager.GetPlayerBoolStatus(PlayerStatusBoolName.isRobotmove)
            );
        }
    }

    private void OnAnyBoolStatusChanged(PlayerStatusBoolName flag, bool isEnabled)
    {
        switch (flag)
        {
            case PlayerStatusBoolName.isRobotmove:
                isRobotmove = isEnabled;
                break;
        }
    }

    /// <summary>
    /// 色や透明度をスプライトに適用する
    /// （HSV変換による明度の強制固定処理を廃止し、初期色をそのまま適用するように変更）
    /// </summary>
    private void SetColorWithFixedBrightness(Color newColor)
    {
        if (_spriteRenderer == null)
            return;

        // 渡された色(_col)をそのまま描画色として適用する
        _spriteRenderer.color = newColor;
    }

    /// <summary>
    /// 環境エリアに入ったときに呼ばれる登録メソッド
    /// </summary>
    public void EnterEnvironmentArea(EnvironmentArea area)
    {
        if (!activeEnvironments.Contains(area))
        {
            activeEnvironments.Add(area);
        }
    }

    /// <summary>
    /// 環境エリアから出たときに呼ばれる解除メソッド
    /// </summary>
    public void ExitEnvironmentArea(EnvironmentArea area)
    {
        if (activeEnvironments.Contains(area))
        {
            activeEnvironments.Remove(area);
        }
    }

    /// <summary>
    /// リフト（PassengerCarrier）から毎フレーム速度を受け取る
    /// </summary>
    public void SetCarrierVelocity(Vector2 velocity)
    {
        currentCarrierVelocity = velocity;
        isOnCarrier = true;
    }

    /// <summary>
    /// リフトから降りた（離れた）時に呼ばれる
    /// </summary>
    public void ExitCarrier()
    {
        isOnCarrier = false;

        // ここで currentCarrierVelocity をゼロにしないことで、
        // 空中にいる間は「慣性」として速度が残り続ける。
        // CheckGroundStatus で着地判定されたときにゼロになる。
    }

    /// <summary>
    /// CameraMoveAreaに入ったときの影の有効/無効の切り替え
    /// </summary>
    private void HandleAreaEntered(CameraMoveArea area)
    {
        isShadowEnabled = area.EnablePlayerShadow;

        // エリアに入った時点で影が無効なら非表示にする
        if (shadowObject != null && !isShadowEnabled)
        {
            shadowObject.SetActive(false);
        }
    }

    /// <summary>
    /// CameraMoveAreaから出たときの処理
    /// </summary>
    private void HandleAreaExited(CameraMoveArea area)
    {
        isShadowEnabled = false;

        if (shadowObject != null)
        {
            shadowObject.SetActive(false);
        }
    }

    /// <summary>
    /// 光線を飛ばして地面を検知し、影の位置を更新する
    /// </summary>
    private void UpdateShadowPosition()
    {
        if (!isShadowEnabled || shadowObject == null)
            return;

        // 光線の起点を設定（groundCheckがあればそれを利用、なければプレイヤーの座標）
        Vector2 rayOrigin =
            groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position;

        // 真下に向かって光線を飛ばし、地面レイヤーと衝突するか確認
        RaycastHit2D hit = Physics2D.Raycast(
            rayOrigin,
            Vector2.down,
            shadowRayDistance,
            groundLayer
        );

        if (hit.collider != null)
        {
            // 地面が見つかった場合は影を表示し、座標を更新する
            shadowObject.SetActive(true);

            // X座標はプレイヤーに追従、Y座標は地面の衝突ポイント、Z座標は影自身のものを維持
            shadowObject.transform.position = new Vector3(
                transform.position.x,
                hit.point.y,
                shadowObject.transform.position.z
            );
        }
        else
        {
            // 底なし穴など、地面が見つからない場合は影を隠す
            shadowObject.SetActive(false);
        }
    }

    #endregion
}

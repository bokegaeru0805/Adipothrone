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

    [SerializeField]
    private float Bound2EffecIntervalTime = 0.2f; //揺れる効果音の間隔の時間

    [SerializeField]
    private Transform groundCheck; // プレイヤーの足元のTransform
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
    private const float BOUND2EFFECT_LENGHT = 1.384f; //揺れる効果音の長さ
    private const float DEFAULT_WALK_ANIMATION_DURATION = 0.500f; //元の一回の歩行アニメーションの秒数
    private float m_dashDefaultSpeed = 8.0f; //通常のダッシュ速度
    private float jumpHeight = 3.5f; // ジャンプで到達したい高さ

    // private float damageX = 3.0f; //ダメージを食らったときのx軸の移動具合
    private float MoveStart_Sec = 0.5f; //ダメージを食らったときの硬直無敵時間
    private float immunityDuration = 0.75f; //動ける無敵時間
    private float attackMoveSlowRate = 4.0f; //攻撃中の移動速度の減少率
    private float WalkTime = 1.46f; //一回の歩行アニメーションの秒数
    private float DashTime = 0.72f; //一回のダッシュアニメーションの秒数
    private List<EnvironmentArea> activeEnvironments = new List<EnvironmentArea>(); // 現在適用中の環境エリアリスト
    #endregion

    #region Internal State Variables

    // --- 内部状態変数 ---
    private float vx = 0; //実際のx方向の移動速度
    private float walkSpeed = 0; //歩行の速度
    private float dashSpeed = 0; //ダッシュの速度
    private float jumpForce = 0; // 内部的に計算されるジャンプ力
    private float BoundIntervalTime; //揺れる音を鳴らす間を記録する変数
    private float groundCheckRadius = 0.2f; // 接地判定の半径
    private float gravity; //重力の大きさを保存する変数
    private int BodyState; //体形の状態を保存する変数
    private int AnimBodyState; //アニメーションの体形の状態を保存する変数
    private bool isAttacking = false; // 攻撃中かどうかのフラグ
    private bool immunity = false; //無敵かどうかのフラグ
    private bool isFadingOut = true; //不透明度が減少するかどうかのフラグ
    private bool move = true; //操作できるかどうかのフラグ
    private bool isFirstGetKey = false; //初めてキー入力をしたかどうかのフラグ
    private bool isRobotmove = false; //ロボットが動けるかどうかのフラグ
    private bool isGrounded = false; //接地しているかどうかのフラグ
    private bool wasGroundedLastFrame = true; //前のフレームで接地していたかどうかのフラグ
    private bool jumpRequested = false;
    private bool isTalking = false; // 会話状態を保存するローカル変数
    private bool isDead = false; // プレイヤーが死亡しているかどうかのマスターフラグ
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

        // ポーズ中、会話中、死亡中は入力を受け付けない
        if (Time.timeScale > 0f && !isTalking && !isDead)
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

        if (Time.timeScale > 0f)
        {
            ApplyEnvironmentEffects();
            ApplyMovement();
            CheckGroundStatus();
            ExecuteJumpPhysics();
            HandleLanding();
            UpdateImmunityBlink();

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
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }

    #endregion

    #region Initialization Logic

    private void InitializeComponents()
    {
        groundLayer = LayerMask.GetMask(GameConstants.PHYSICS_LAYER_NAME_GROUND);
        _animator = GetComponent<Animator>();
        _rbody = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
        _col = _spriteRenderer.color;

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
        GameManager.OnTalkingStateChanged += HandleTalkingStateChanged;
        SaveLoadManager.OnLoadingStateChanged += HandleLoadingStateChanged;
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

        GameManager.OnTalkingStateChanged -= HandleTalkingStateChanged;
        SaveLoadManager.OnLoadingStateChanged -= HandleLoadingStateChanged;

        // 状態リセット
        move = true;
        isDead = false;
        immunity = false;
        _col = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        SetColorWithFixedBrightness(_col);
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
    private void SetFacingDirection(bool isRight)
    {
        rightFlag = isRight;
        robotMoveScript.SetRightFlag(isRight);
    }

    /// <summary>
    /// 歩行・ダッシュ中の特殊な効果音（バウンド音など）の再生
    /// </summary>
    private void PlayMoveSoundEffects(bool isDashing)
    {
        BoundIntervalTime += isDashing ? 2 * Time.deltaTime : Time.deltaTime;

        if (
            BoundIntervalTime >= BOUND2EFFECT_LENGHT + Bound2EffecIntervalTime
            && BodyState == GameConstants.BODY_STATE_ARMED_2
        )
        {
            sePlayer.Play(SE_PlayerAction.Bound2);
            BoundIntervalTime = 0f;
        }
        else if (BoundIntervalTime >= 3.448f && BodyState == GameConstants.BODY_STATE_ARMED_1)
        {
            sePlayer.Play(SE_PlayerAction.GichiGichi1);
            BoundIntervalTime = 0f;
        }
    }

    #endregion

    #region Physics Logic Methods (Called from FixedUpdate)

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
    /// 物理的な移動の適用
    /// </summary>
    private void ApplyMovement()
    {
        if (!move)
            return; // 操作不能時は速度更新しない（ダメージリアクション等で制御されるため）

        // --- 環境効果（速度・風）の計算 ---
        float finalSpeedMult = 1.0f;
        Vector2 totalWindVelocity = Vector2.zero;

        // アクティブな環境エリアすべてから効果を合成
        for (int i = 0; i < activeEnvironments.Count; i++)
        {
            var area = activeEnvironments[i];
            if (area == null)
                continue;

            // 1. 全体速度倍率の適用 (泥沼など)
            finalSpeedMult *= area.GlobalSpeedMultiplier;

            // 2. 風ベクトル（方向と強さ）の合成
            // ここでは風の「ベクトル」を加算していく（複数の風がある場合の合成）
            totalWindVelocity += area.WindVelocity;
        }

        // --- 風による抵抗計算 ---
        // プレイヤーの移動方向(vxの符号)と、風向きの関係を調べる
        if (Mathf.Abs(vx) > 0.01f && totalWindVelocity.sqrMagnitude > 0.01f)
        {
            // 正規化された移動方向ベクトル
            Vector2 moveDir = new Vector2(Mathf.Sign(vx), 0);

            // 風ベクトルとの内積を計算 (正なら追い風、負なら向かい風)
            // WindVelocity自体が強さを持っているので、正規化せずにそのまま使うと強風ほど影響が出る
            float dot = Vector2.Dot(moveDir, totalWindVelocity);

            if (dot < 0)
            {
                // 向かい風の場合: 内積がマイナスになるので、速度を減衰させる
                // 例: dotが-5なら、1 - (5 * 0.1) = 0.5倍になる、等
                // ※ここでは簡易的に、リストの最後のエリアのResistanceFactorを使うか、固定係数を使う
                // 複数のエリアがある場合を考慮し、最も強い抵抗を採用する設計も可能だが、
                // ここではシンプルに「内積値に応じた減速」を行う

                // 抵抗係数を適当に定数化（またはエリアから取得）して調整
                float resistance = 0.1f;
                // 減速率を計算 (最低でも0.1倍は残すクランプ処理)
                float windSlowDown = Mathf.Max(0.1f, 1.0f + (dot * resistance));

                finalSpeedMult *= windSlowDown;
            }
            // 追い風の場合に加速させたい場合は else if (dot > 0) で処理を追加可能
        }

        // --- 最終速度の適用 ---
        if (!isAttacking)
        {
            // 環境倍率を適用して速度決定
            _rbody.velocity = new Vector2(vx * finalSpeedMult, _rbody.velocity.y);
        }
        else
        {
            // 攻撃中は減速 (攻撃減速も環境倍率の影響を受けるようにする)
            float slowedVx = (vx / attackMoveSlowRate) * finalSpeedMult;
            _rbody.velocity = new Vector2(slowedVx, _rbody.velocity.y);
        }
    }

    /// <summary>
    /// 接地判定
    /// </summary>
    private void CheckGroundStatus()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    /// <summary>
    /// ジャンプの物理計算とアニメーショントリガー
    /// </summary>
    private void ExecuteJumpPhysics()
    {
        if (!jumpRequested)
            return;

        jumpRequested = false;
        jumpForce = Mathf.Sqrt(2 * gravity * jumpHeight);
        _rbody.velocity = new Vector2(_rbody.velocity.x, jumpForce);

        // 体型に応じたジャンプアニメーション
        AnimBodyState = playerBodyManager.AnimBodyState;
        switch (AnimBodyState)
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

            if (BodyState == GameConstants.BODY_STATE_ARMED_2)
            {
                sePlayer.Play(SE_PlayerAction.Bound1);
            }
            else if (BodyState == GameConstants.BODY_STATE_ARMED_1)
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
        _col = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        immunity = true;

        yield return new WaitForSeconds(MoveStart_Sec); // 硬直時間

        // 死亡していない、かつノックバック発生による操作不能時のみ操作を復帰
        if (!isDead && hasKnockback)
        {
            move = true;
        }

        yield return new WaitForSeconds(immunityDuration); // 無敵時間残り

        immunity = false;
        _col.a = 1.0f;
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
        _col = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        immunity = true;
        yield return new WaitForSeconds(time);
        immunity = false;
        _col.a = 1.0f;
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
        {
            _rbody.velocity = Vector2.zero;
            _rbody.isKinematic = true;
        }

        if (_animator != null)
            _animator.enabled = false;
        if (_spriteRenderer != null && deathSprite != null)
            _spriteRenderer.sprite = deathSprite;

        immunity = false;
        if (_spriteRenderer != null)
            _spriteRenderer.color = Color.white;
    }

    public void ResetToLiveState()
    {
        isDead = false;
        move = true;

        if (_rbody != null)
            _rbody.isKinematic = false;
        if (_animator != null)
            _animator.enabled = true;
        if (_spriteRenderer != null)
            _spriteRenderer.color = Color.white;
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
        BodyState = playerBodyManager.BodyState;
        AnimBodyState = playerBodyManager.AnimBodyState;
        _animator.SetInteger("BodyState", AnimBodyState);
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

    private void HandleTalkingStateChanged(bool talkState)
    {
        isTalking = talkState;
    }

    private void SetColorWithFixedBrightness(Color newColor)
    {
        if (_spriteRenderer == null)
            return;

        float h,
            s,
            v;
        Color.RGBToHSV(newColor, out h, out s, out v);
        float fixedBrightness = 0.8f;
        Color finalColor = Color.HSVToRGB(h, s, fixedBrightness);
        finalColor.a = newColor.a;

        _spriteRenderer.color = finalColor;
    }

    /// <summary>
    /// 環境エリアに入ったときに呼ばれる登録メソッド (新規追加)
    /// </summary>
    public void EnterEnvironmentArea(EnvironmentArea area)
    {
        if (!activeEnvironments.Contains(area))
        {
            activeEnvironments.Add(area);
        }
    }

    /// <summary>
    /// 環境エリアから出たときに呼ばれる解除メソッド (新規追加)
    /// </summary>
    public void ExitEnvironmentArea(EnvironmentArea area)
    {
        if (activeEnvironments.Contains(area))
        {
            activeEnvironments.Remove(area);
        }
    }

    #endregion
}

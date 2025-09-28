using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class Heroin_move : MonoBehaviour
{
    private GameManager gameManager; // GameManagerのインスタンスを保存する変数
    private PlayerManager playerManager; // PlayerManagerのインスタンスを保存する変数
    private PlayerEffectManager playerEffectManager; // PlayerEffectManagerのインスタンスを保存する変数
    private PlayerBodyManager playerBodyManager; // PlayerBodyManagerのインスタンスを保存する変数
    private InputManager inputManager; // InputManagerのインスタンスを保存する変数
    private SEManager seManager; // SEManagerのインスタンスを保存する変数

    [Header("必須の子オブジェクト")]
    [SerializeField]
    private GameObject RobotObject;

    // public float CameraOffsetY { get; private set; } = 6; //プレイヤーに対してのカメラのy座標の差分
    public bool rightFlag { get; private set; } = false; // 右向きかどうかのフラグ

    [HideInInspector]
    public Vector2 pos = new Vector2(0, 0); //自分の座標
    public Fungus.Flowchart flowchart = null;
    public float m_defaultSpeed { get; private set; } = 4.0f; // 通常の歩行速度
    private float m_dashDefaultSpeed = 8.0f; //通常のダッシュ速度
    private float jumpHeight = 3.5f; // ジャンプで到達したい高さ
    private float damageX = 3.0f; //ダメージを食らったときのx軸の移動具合
    private float MoveStart_Sec = 0.5f; //ダメージを食らったときの硬直時間
    private float immunityDuration = 2f; //動ける無敵時間
    private float attackMoveSlowRate = 4.0f; //攻撃中の移動速度の減少率

    [SerializeField]
    private float Bound2EffecIntervalTime = 0.2f; //揺れる効果音の間隔の時間

    [SerializeField]
    private LayerMask groundLayer; // 接地判定に使うレイヤー

    [SerializeField]
    private Transform groundCheck; // プレイヤーの足元のTransform
    private float vx = 0; //実際のx方向の移動速度
    private float walkSpeed = 0; //歩行の速度
    private float dashSpeed = 0; //ダッシュの速度
    private float jumpForce = 0; // 内部的に計算されるジャンプ力
    private float OriginalWalkTime = 0.500f; //元の一回の歩行アニメーションの秒数
    private float WalkTime = 1.46f; //一回の歩行アニメーションの秒数
    private float DashTime = 0.72f; //一回のダッシュアニメーションの秒数
    private float BoundIntervalTime; //揺れる音を鳴らす間を記録する変数
    private float Bound2EffectLength = 1.384f; //揺れる効果音の長さ
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
    private Rigidbody2D rbody; // Rigidbody2Dコンポーネント
    private Animator m_animator; // アニメータコンポーネント
    private SpriteRenderer spriteRenderer; //SpriteRendererをキャッシュするための変数
    private Color m_col; //SpriteRendererの色を保存するための変数
    private Robot_move robotMoveScript;
    public event Action<bool> OnPlayerVisibilityChanged; // プレイヤーの可視状態が変化したときに呼び出されるイベント

    private void Awake()
    {
        m_animator = GetComponent<Animator>();
        rbody = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        m_col = spriteRenderer.color;

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

    private void Start()
    {
        isFirstGetKey = true;
        gravity = Mathf.Abs(Physics2D.gravity.y * rbody.gravityScale);
        if (gameObject.name != GameConstants.PlayerObjectName)
        {
            Debug.LogError(
                $"{gameObject.name}の名前がGameConstants.PlayerObjectNameと一致しません。"
            ); // プレイヤーのオブジェクト名が一致しない場合のエラーメッセージ
        }

        if (this.tag != GameConstants.PlayerTagName)
        {
            Debug.LogError(
                $"{this.gameObject.name}のタグがGameConstants.PlayerTagNameと一致しません。"
            ); // プレイヤーのタグ名が一致しない場合のエラーメッセージ
        }
    }

    private void Update()
    {
        if (inputManager == null)
        {
            return; // InputManagerがまだ初期化されていない場合は何もしない
        }

        vx = 0; //x方向の速度を初期化

        if (Time.timeScale > 0f && !GameManager.IsTalking)
        {
            if (isFirstGetKey)
            { //向きの初期化の処理
                if (inputManager.GetPlayerMoveRight())
                {
                    rightFlag = true;
                    robotMoveScript.SetRightFlag(true); //Robotの向きを変える
                    isFirstGetKey = false;
                }
                else if (InputManager.instance.GetPlayerMoveLeft())
                {
                    rightFlag = false;
                    robotMoveScript.SetRightFlag(false); //Robotの向きを変える
                    isFirstGetKey = false;
                }
            }

            // -- 入力と移動の処理 --
            if ((inputManager.GetPlayerMoveRight() || inputManager.GetPlayerMoveLeft()) && move)
            {
                //左右の方向を決定
                bool movingRight = inputManager.GetPlayerMoveRight();

                spriteRenderer.flipX = movingRight; //画像の左右の向きを設定
                m_animator.SetInteger("AnimState", 1); //アニメーションの状態を設定

                // ダッシュ判定
                bool isDashing = inputManager.GetPlayerDash();
                vx = isDashing ? dashSpeed : walkSpeed; // x方向の速度の大きさを設定

                float direction = movingRight ? 1f : -1f; // 移動方向の決定
                vx = vx * direction; //vxを方向に合わせる

                //歩行アニメーションの速度を設定
                m_animator.SetFloat(
                    "WalkSpeed",
                    OriginalWalkTime / (isDashing ? DashTime : WalkTime)
                );

                if (isGrounded)
                {
                    if (!seManager.IsPlayingPlayerActionSE(SE_PlayerAction.Walk1))
                    {
                        seManager.PlayPlayerActionSEPitch(
                            SE_PlayerAction.Walk1,
                            isDashing
                                ? UnityEngine.Random.Range(2.0f, 2.5f)
                                : UnityEngine.Random.Range(1.0f, 1.5f)
                        );
                    }
                }

                BoundIntervalTime += isDashing ? 2 * Time.deltaTime : Time.deltaTime;

                // 歩行時の効果音の判定
                if (
                    BoundIntervalTime >= Bound2EffectLength + Bound2EffecIntervalTime
                    && BodyState == GameConstants.BodyState_Armed2
                )
                {
                    seManager?.PlayPlayerActionSE(SE_PlayerAction.Bound2);
                    BoundIntervalTime = 0f;
                }
                else if (BoundIntervalTime >= 3.448f && BodyState == GameConstants.BodyState_Armed1)
                {
                    seManager?.PlayPlayerActionSE(SE_PlayerAction.GichiGichi1);
                    BoundIntervalTime = 0f;
                }

                // 向きフラグとロボット反映
                if (rightFlag != movingRight)
                {
                    rightFlag = movingRight;
                    robotMoveScript.SetRightFlag(movingRight);
                }
            }
            else
            {
                if (seManager.IsPlayingPlayerActionSE(SE_PlayerAction.Walk1))
                    seManager.StopPlayerActionSE(SE_PlayerAction.Walk1); //歩行の効果音を止める
                m_animator.SetInteger("AnimState", 0);
            }

            if (inputManager.GetPlayerJump() && isGrounded && move)
            {
                jumpRequested = true;
            }
        }
        else
        {
            m_animator.SetInteger("AnimState", 0); //自分のanimationをstand状態にする
        }
    }

    private void FixedUpdate()
    {
        m_animator.SetBool("IsGrounded", isGrounded); //接地判定を設定
        m_animator.SetFloat("VerticalSpeed", rbody.velocity.y); //y方向の速度を設定

        if (RobotObject.activeInHierarchy)
        {
            isAttacking = robotMoveScript.isAttacking; //ロボットから攻撃中かどうかのフラグを取得
        }

        if (Time.timeScale > 0f)
        {
            // 移動する（重力をかけたまま）
            if (!isAttacking && move)
            {
                rbody.velocity = new Vector2(vx, rbody.velocity.y);
            }
            else if (isAttacking && move)
            {
                vx /= attackMoveSlowRate; //攻撃中は移動速度を減少させる
                rbody.velocity = new Vector2(vx, rbody.velocity.y);
            }

            // 接地判定
            isGrounded = Physics2D.OverlapCircle(
                groundCheck.position,
                groundCheckRadius,
                groundLayer
            );

            // ジャンプ処理
            if (jumpRequested)
            {
                jumpRequested = false;
                jumpForce = Mathf.Sqrt(2 * gravity * jumpHeight); // ジャンプ力を計算する
                rbody.velocity = new Vector2(rbody.velocity.x, jumpForce);
                AnimBodyState = playerBodyManager.AnimBodyState; //アニメーションの体形の状態を取得する

                switch (AnimBodyState)
                {
                    case GameConstants.AnimBodyState_Normal:
                        m_animator.ResetTrigger("Normal_JumpTrigger");
                        m_animator.SetTrigger("Normal_JumpTrigger");
                        break;
                    case GameConstants.AnimBodyState_Armed1:
                        m_animator.ResetTrigger("Armed1_JumpTrigger");
                        m_animator.SetTrigger("Armed1_JumpTrigger");
                        break;
                    case GameConstants.AnimBodyState_Armed2:
                        m_animator.ResetTrigger("Armed2_JumpTrigger");
                        m_animator.SetTrigger("Armed2_JumpTrigger");
                        break;
                }
                seManager?.PlayPlayerActionSEPitch(
                    SE_PlayerAction.Jump1,
                    UnityEngine.Random.Range(1.0f, 1.5f)
                ); //ジャンプの効果音を鳴らす
            }

            // 着地判定：前のフレームでは空中、今フレームで地面
            if (!wasGroundedLastFrame && isGrounded)
            {
                seManager?.PlayPlayerActionSE(SE_PlayerAction.Land1); //着地の効果音を鳴らす
                if (BodyState == GameConstants.BodyState_Armed2)
                {
                    seManager?.PlayPlayerActionSE(SE_PlayerAction.Bound1); //着地のバウンドの効果音を鳴らす
                }
                else if (BodyState == GameConstants.BodyState_Armed1)
                {
                    seManager?.PlayPlayerActionSE(SE_PlayerAction.Bound3); //着地のバウンドの効果音を鳴らす
                }
            }
            wasGroundedLastFrame = isGrounded; // 前のフレームの接地状態を保存

            if (immunity)
            {
                if (m_col.a <= 0.3f)
                {
                    isFadingOut = false; //不透明度を上げるようにする
                }
                else if (m_col.a >= 1.0f)
                {
                    isFadingOut = true; //不透明度を下げるようにする
                }

                m_col.a += isFadingOut ? -0.1f : +0.1f; //不透明度を変更する
                SetColorWithFixedBrightness(m_col); //ヘルパーメソッドを使って色を設定
            }

            pos = this.transform.position; //現在の自分の座標を保存
            RobotObject.SetActive(isRobotmove);
        }
    }

    /// <summary>
    /// スプライトの色を、明度を固定した状態で設定します。
    /// </summary>
    /// <param name="newColor">設定したい基本の色</param>
    private void SetColorWithFixedBrightness(Color newColor)
    {
        if (spriteRenderer == null)
            return;

        // 1. 設定したい色（newColor）をHSVに変換
        float h,
            s,
            v;
        Color.RGBToHSV(newColor, out h, out s, out v);

        // 2. V（明度）の値を固定値（ここでは80% = 0.8f）に上書き
        float fixedBrightness = 0.8f;

        // 3. 新しいHSVの値からRGBカラーを生成
        Color finalColor = Color.HSVToRGB(h, s, fixedBrightness);

        // 4. 元の色のアルファ値（透明度）を引き継ぐ
        finalColor.a = newColor.a;

        // 5. 最終的な色をスプライトに適用
        spriteRenderer.color = finalColor;
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (Time.timeScale > 0f)
        {
            var script = collision.gameObject.GetComponent<DropItem>();
            if (script != null && !script.isTreasureBox)
            {
                //DropItemのスクリプトが付いていて、かつ宝箱ではないとき
                if (script.DropMoney != 0)
                {
                    //インベントに金を追加
                    playerManager.ChangeMoney(script.DropMoney);
                    seManager?.PlayFieldSEPitch(
                        SE_Field.CoinGet1,
                        UnityEngine.Random.Range(1.0f, 1.5f)
                    ); //効果音を鳴らす
                }

                if (script.DropID != null)
                {
                    gameManager.AddAllTypeIDToInventory(script.DropID);
                    //ドロップ品をインベントに追加
                    seManager?.PlaySystemEventSE(SE_SystemEvent.ItemGet2);
                }

                Destroy(collision.gameObject); //ドロップ品を消去
            }
        }
    }

    /// <summary>
    /// プレイヤーがダメージを受けたときの処理
    /// /// </summary>
    public void DamageHP(int damageAmount)
    {
        if (Time.timeScale > 0)
        {
            int damageReduction = playerEffectManager.CalculateFinalDefensePower(); //ダメージ減少効果を取得する

            damageAmount -= damageReduction; //ダメージから防御減少効果を引く

            if (damageAmount > 0 && !immunity)
            {
                //ダメージを与える処理
                playerManager.DamageHP(damageAmount);
                //自分を動けなくする
                move = false;

                if (rightFlag)
                { //右側からあたったとき
                    rbody.velocity = new Vector2(-damageX, 0); //本来は0の部分にdamageyが入っていた
                    rbody.velocity = new Vector2(-damageX, rbody.velocity.y);
                }
                else
                {
                    rbody.velocity = new Vector2(damageX, 0);
                    rbody.velocity = new Vector2(damageX, rbody.velocity.y);
                }
                StartCoroutine(MoveStart());
            }
        }
    }

    private IEnumerator MoveStart() //velocity再開
    {
        m_col = new Color(1.0f, 1.0f, 1.0f, 1.0f); //色を初期化
        immunity = true; //無敵状態にする
        yield return new WaitForSeconds(MoveStart_Sec); //MoveStart_Secの待つ
        move = true; //velocityを再開する
        yield return new WaitForSeconds(immunityDuration); //immunityDurationの待つ
        immunity = false; //無敵状態を解除する
        m_col.a = 1.0f; //不透明度を初期化する
        SetColorWithFixedBrightness(m_col); // ヘルパーメソッドを使って色を設定
    }

    public void EnableInvincibility(float time)
    {
        StartCoroutine(enableinvincibility(time));
    }

    public IEnumerator enableinvincibility(float time)
    {
        m_col = new Color(1.0f, 1.0f, 1.0f, 1.0f); //色を初期化
        immunity = true; //無敵状態にする
        yield return new WaitForSeconds(time); //time秒待つ
        immunity = false; //無敵状態を解除する
        m_col.a = 1.0f; //不透明度を初期化する
        SetColorWithFixedBrightness(m_col); // ヘルパーメソッドを使って色を設定
    }

    private void OnEnable()
    {
        StartCoroutine(DelayedInitialization());
    }

    /// <summary>
    /// 全てのAwake/Startが完了するのを待ってから、初期化処理を実行するコルーチン
    /// </summary>
    private IEnumerator DelayedInitialization()
    {
        // 最初のフレームの描画が終わるまで待つ
        // これにより、全てのシングルトンが確実に初期化されている状態になる
        yield return new WaitForEndOfFrame();

        // --- ここからが実質的な初期化処理 ---

        // 各マネージャーのインスタンスを取得
        gameManager = GameManager.instance;
        playerManager = PlayerManager.instance;
        playerEffectManager = PlayerEffectManager.instance;
        playerBodyManager = PlayerBodyManager.instance;
        inputManager = InputManager.instance;
        seManager = SEManager.instance; // SEManagerのインスタンスを取得

        // いずれかのマネージャーが見つからなければ、処理を中断
        if (
            gameManager == null
            || playerManager == null
            || playerEffectManager == null
            || playerBodyManager == null
            || inputManager == null
            || seManager == null
        )
        {
            Debug.LogError("必要なマネージャーが見つかりませんでした。Heroin_moveは機能しません。");
            yield break; // コルーチンを終了
        }

        // イベントの購読
        playerManager.OnBoolStatusChanged += OnAnyBoolStatusChanged;
        playerEffectManager.OnSpeedEffectChanged += CalculateMoveSpeed;
        playerBodyManager.OnChangeBodyState += GetBodyStateData;

        // 各状態の初期化
        GetBodyStateData();
        CalculateMoveSpeed();
        OnAnyBoolStatusChanged(
            PlayerStatusBoolName.isRobotmove,
            playerManager.GetPlayerBoolStatus(PlayerStatusBoolName.isRobotmove)
        );
        Bound2EffectLength = seManager.GetPlayerActionSELength(SE_PlayerAction.Bound2); //揺れる効果音の長さを取得

        // その他の初期化
        spriteRenderer.flipX = true; // 初期状態では右向き
        rightFlag = true; // 初期状態では右向き
        BoundIntervalTime = 0; // 効果音の間隔を初期化
        isAttacking = false; // 初期状態では攻撃中ではない
        immunity = false; // 初期状態では無敵ではない
        move = true; // 初期状態では操作可能
        OnPlayerVisibilityChanged?.Invoke(true); // プレイヤーの可視状態を通知
    }

    private void OnDisable()
    {
        if (!GameManager.isFirstGameSceneOpen)
            return;

        // イベントを安全に解除
        if (playerManager != null)
            playerManager.OnBoolStatusChanged -= OnAnyBoolStatusChanged;
        if (playerEffectManager != null)
            playerEffectManager.OnSpeedEffectChanged -= CalculateMoveSpeed;
        if (playerBodyManager != null)
            playerBodyManager.OnChangeBodyState -= GetBodyStateData;

        // その他のリセット処理
        move = true; // 操作可能状態に戻す
        immunity = false; // 無敵状態を解除
        m_col = new Color(1.0f, 1.0f, 1.0f, 1.0f); // 色を初期化
        m_col.a = 1.0f; // 不透明度を初期化
        SetColorWithFixedBrightness(m_col); // ヘルパーメソッドを使って色を設定
        OnPlayerVisibilityChanged?.Invoke(false); // プレイヤーの可視状態を通知
    }

    /// <summary>
    /// 移動速度を計算する
    /// </summary>
    private void CalculateMoveSpeed()
    {
        // 通常の歩行速度とダッシュ速度を計算
        walkSpeed = playerEffectManager.CalculateFinalPlayerMoveSpeed(m_defaultSpeed);
        dashSpeed = playerEffectManager.CalculateFinalPlayerMoveSpeed(m_dashDefaultSpeed);
    }

    private void GetBodyStateData()
    {
        BodyState = playerBodyManager.BodyState; //主人公の体形の状態を取得する
        AnimBodyState = playerBodyManager.AnimBodyState; //アニメーションの体形の状態を取得する
        m_animator.SetInteger("BodyState", AnimBodyState); //体形の状態を設定
    }

    /// <summary>
    /// PlayerManagerのいずれかのbool値が変更されたときに呼び出されます。
    /// </summary>
    /// <param name="flag">どのステータスが変更されたかを示すEnum</param>
    /// <param name="isEnabled">ステータスの新しい値 (true/false)</param>
    private void OnAnyBoolStatusChanged(PlayerStatusBoolName flag, bool isEnabled)
    {
        // どのフラグが変更されたかをswitch文で判定し、対応する変数を更新
        switch (flag)
        {
            // ロボットが移動可能かどうかの状態
            case PlayerStatusBoolName.isRobotmove:
                isRobotmove = isEnabled; //Robotが動けるかどうかを取得する
                break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}

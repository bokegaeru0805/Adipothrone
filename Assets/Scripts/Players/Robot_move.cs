using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Robot_move : MonoBehaviour
{
    #region Public Properties & Events
    // --- 外部から参照されるプロパティやイベント ---

    /// <summary>
    /// ロボットの可視状態が変化したときに発行されるイベント
    /// </summary>
    public event Action<bool> OnRobotVisibilityChanged;

    /// <summary>
    /// プレイヤーに対する追従オフセット
    /// </summary>
    public Vector2 offset = new Vector2(1.5f, 2f);

    /// <summary>
    /// 剣を振っている最中かどうかのフラグ
    /// </summary>
    public bool isBladeSwinging { get; private set; } = false;

    /// <summary>
    /// 現在、右を向いているかどうかのフラグ
    /// </summary>
    public bool rightFlag { get; private set; } = false;

    /// <summary>
    /// 攻撃の硬直中（プレイヤーが動けない）かどうかのフラグ
    /// </summary>
    public bool isAttacking { get; private set; } = false;
    #endregion

    #region Inspector Settings
    // --- Inspectorから設定する項目 ---

    [Header("オブジェクト参照")]
    [SerializeField]
    private GameObject PlayerObject; // Playerのオブジェクト

    [SerializeField]
    private GameObject shoot_prefab; // 弾のプレハブ

    [SerializeField]
    private GameObject blade_prefab; // 剣のプレハブ

    [SerializeField]
    private Sprite RobotSprite_red; // 通常時のスプライト

    [SerializeField]
    private Sprite RobotSprite_blue; // 剣攻撃時のスプライト

    [Header("アニメーション設定")]
    [SerializeField]
    private AnimationCurve bladeEaseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // 剣の攻撃アニメーションの緩急カーブ

    [Header("パラメータ設定")]
    [SerializeField]
    private float changeRightFlag_Sec = 0.5f; // プレイヤーの向き変更からロボットが追従するまでの遅延

    [SerializeField, Tooltip("攻撃後のプレイヤー硬直時間に影響する係数")]
    private float EnableMoveTimeAcjuctment = 0f;
    #endregion

    #region Private Fields
    // --- 内部で管理する変数 ---

    // マネージャー・コンポーネントのキャッシュ
    private PlayerManager playerManager;
    private PlayerEffectManager playerEffectManager;
    private WeaponManager weaponManager;
    private SpriteRenderer spriteRenderer;
    private InputManager inputManager;

    // 移動関連のパラメータ
    private float _smoothTime = 0.1f; // プレイヤー追従の滑らかさ
    private float _maxSpeed = float.PositiveInfinity; // 追従の最大速度
    private float _currentVelocity = 0; // 平滑化移動で使う内部変数
    private Vector3 robot_pos = Vector3.zero; // 計算用の一時的な座標変数

    // 攻撃関連のパラメータ
    private int maxAttackCount = 5; // 剣の最大連続攻撃回数
    private float afterBlade_Sec = 0.4f; // 剣攻撃後の待機時間
    private float inputWindowTime = 0.5f; // 剣の連続攻撃の入力受付時間
    private float bladeSwingOffsetRadius = 1.5f; // 剣の振り子半径

    // プレイヤー関連のパラメータ
    private int playerWP = 0;

    // 内部状態を管理するフラグ
    private PlayerAttackType playerAttackType = PlayerAttackType.Shoot; // 現在のプレイヤーの攻撃方法
    private int attackCount = 0; // 現在の剣の攻撃回数
    private bool isRobotmove = false; // ロボットが動けるかどうかのフラグ
    private bool isRobotattack = false; // ロボットが攻撃できるかどうかのフラグ
    private bool isChangeAttackType = false; // 攻撃方法を変更できるかどうかのフラグ
    private bool queuedAttack = false; // 次の剣攻撃が予約されたか
    private bool isEnable = false; // 表示されているかどうかのフラグ
    private bool isAttackInputWindowOpen = false; // 剣の連続攻撃の入力受付中か
    private bool isEnableNextAttack = true; // 次の攻撃が出来るかどうか

    // 現在装備している武器のデータをキャッシュ
    private BladeWeaponData currentBladeData;
    private float bladeWPCost = 0f; // 剣のWP消費量
    private ShootWeaponData currentShootData;
    private float shootWPCost = 0f; // 弾のWP消費量

    // 外部スクリプトの参照をキャッシュ
    private Robot_blade_move bladeMoveScript;
    private Robot_shoot_move shootMoveScriptPrefab; // 弾はPrefabから生成するため、Prefabのスクリプトを保持
    #endregion

    private void Awake()
    {
        isEnable = false; //初期化の準備
        spriteRenderer = GetComponent<SpriteRenderer>(); // SpriteRendererの取得
        if (this.gameObject.name != GameConstants.RobotObjectName)
        {
            Debug.LogError(
                $"{this.gameObject.name}の名前がGameConstants.RobotObjectNameと一致しません。"
            ); // ロボットのオブジェクト名が一致しない場合のエラーメッセージ
        }

        //剣と弾のスクリプトをキャッシュ
        if (blade_prefab != null)
        {
            bladeMoveScript = blade_prefab.GetComponent<Robot_blade_move>();
        }
        if (shoot_prefab != null)
        {
            shootMoveScriptPrefab = shoot_prefab.GetComponent<Robot_shoot_move>();
        }
    }

    private void Update()
    {
        if (Time.timeScale > 0 && isRobotmove && GameManager.IsTalking == false)
        { //ゲームが進行中で、ロボットが動ける状態で、会話中ではないとき
            if (!isEnable)
            {
                if (inputManager.GetPlayerMoveRight())
                {
                    rightFlag = true;
                    isEnable = true;
                }
                else if (inputManager.GetPlayerMoveLeft())
                {
                    rightFlag = false;
                    isEnable = true;
                }
            }

            if (inputManager.GetPlayerChange())
            {
                if (isChangeAttackType)
                {
                    if (playerAttackType == PlayerAttackType.Shoot)
                    {
                        playerManager.SetPlayerAttackType(PlayerAttackType.Blade); //攻撃方法を剣に変更
                    }
                    else
                    {
                        playerManager.SetPlayerAttackType(PlayerAttackType.Shoot); //攻撃方法を弾に変更
                    }
                }
            }

            if (inputManager.GetRobotAttack())
            {
                // 現在攻撃中の状態で、かつ次の攻撃が可能な場合のみ処理を実行
                if (isRobotattack && isEnableNextAttack)
                {
                    isEnableNextAttack = false; //次の攻撃を出来ないようにする

                    //攻撃方法を取得
                    if (playerAttackType == PlayerAttackType.Blade)
                    {
                        if (playerWP >= bladeWPCost)
                        {
                            // WPが足りる場合のみ攻撃を実行
                            Blade();
                            isAttacking = true; //プレイヤーが動けないようにする
                        }
                        else
                        {
                            // WPが足りない場合は空振り
                            SEManager.instance?.PlayPlayerActionSE(SE_PlayerAction.AttackMiss1);
                            isEnableNextAttack = true; //攻撃を再開する
                            isAttacking = false; //プレイヤーの移動を再開する
                        }
                    }
                    else if (playerAttackType == PlayerAttackType.Shoot)
                    {
                        // WPがコストより大きいかチェック
                        if (playerWP >= shootWPCost)
                        {
                            // WPが足りる場合のみ攻撃を実行
                            Shoot();
                        }
                        else
                        {
                            // WPが足りない場合は空振り
                            SEManager.instance?.PlayPlayerActionSE(SE_PlayerAction.AttackMiss1);
                            isEnableNextAttack = true; //攻撃を再開する
                            isAttacking = false; //プレイヤーの移動を再開する
                        }
                    }
                    else if (playerAttackType == PlayerAttackType.None)
                    {
                        isEnableNextAttack = true; //攻撃を再開する
                        isAttacking = false; //プレイヤーの移動を再開する
                    }
                }
                else if (isAttackInputWindowOpen)
                {
                    queuedAttack = true;
                }
            }
        }
    }

    private void FixedUpdate()
    {
        if (isRobotmove)
        {
            if (!isBladeSwinging)
            {
                spriteRenderer.flipX = !rightFlag; //剣を振っていないときは自分の画像の向きを調整

                robot_pos = this.transform.localPosition; //自分の相対座標を入手
                robot_pos.x = Mathf.SmoothDamp( //プレイヤーに対しての自分のx座標を滑らかに調整
                    robot_pos.x,
                    rightFlag ? offset.x : -offset.x,
                    ref _currentVelocity,
                    _smoothTime,
                    _maxSpeed
                );

                robot_pos.y = offset.y; //プレイヤーに対しての自分のy座標を調整

                this.transform.localPosition = robot_pos; //自分の相対座標を設定
            }
        }
    }

    // 注意: このメソッドを IEnumerator にしてコルーチン化すると、弾が正常に発射されないことがある。
    private void Shoot()
    {
        Vector3 newPos = this.transform.position; //自分の座標を保存
        GameObject newGameObject = Instantiate(shoot_prefab) as GameObject; // 弾1のプレハブを生成
        newGameObject.transform.position = newPos; //弾の位置を設定

        var shootMove = newGameObject.GetComponent<Robot_shoot_move>();
        if (shootMove != null)
        {
            //キャッシュしておいた最新の武器データを渡して初期化
            shootMove.InitializeBullet(currentShootData, rightFlag);
        }

        float EnableMove_Sec =
            newGameObject.GetComponent<Robot_shoot_move>().vanishTime * EnableMoveTimeAcjuctment; //プレイヤーが動けない時間を設定
        StartCoroutine(AttackStart(EnableMove_Sec, currentShootData.shotInterval)); //待機
    }

    private void Blade()
    {
        isBladeSwinging = true; //剣の当たり判定を得る
        InstantsetRightFlag(); //即座にロボットの左右を変更する
        StartCoroutine(BladeAttack());
    }

    // 攻撃ごとの回転角（右向き時）を定義（StartAngle, EndAngle）(プレイヤーが左向きの時)
    private readonly List<Vector2> rightAttackAngles = new List<Vector2>()
    {
        new Vector2(30f, 210f), // 1回目：上から斜めに振り下ろす(時計回り)
        new Vector2(-60f, 240f), // 2回目：下から薙ぎ払う(反時計回り)
        new Vector2(120f, 300f), // 3回目：大回転(時計回り)
        new Vector2(-60f, 240f), // 4回目：再度薙ぎ払い(反時計回り)
        new Vector2(-30f, -210f), // 5回目：背面フィニッシュ(時計回り)
    };

    /// <summary>
    /// 攻撃時の移動タイプを定義します
    /// </summary>
    public enum MovementType
    {
        None, // 移動しない
        Linear, // 直線移動
        Circular // 円周上を移動
        ,
    }

    /// <summary>
    /// 1回の攻撃における移動の設計図
    /// </summary>
    [System.Serializable]
    public class AttackMovementData
    {
        public MovementType type = MovementType.None;

        [Header("--- 直線移動用 ---")]
        public Vector2 startPoint;
        public Vector2 endPoint;

        [Header("--- 円周移動用 ---")]
        public Vector2 center;
        public float radius = 1.0f;
        public float startAngle;
        public float endAngle;
        public bool isClockwise = false;
    }

    [SerializeField]
    private List<AttackMovementData> attackMovements = new List<AttackMovementData>()
    {
        new AttackMovementData
        {
            type = MovementType.Linear,
            startPoint = new Vector2(-1.5f, 1.5f),
            endPoint = new Vector2(-2.5f, 0.5f),
        },
        new AttackMovementData
        {
            type = MovementType.Circular,
            center = new Vector2(-2f, 2f),
            radius = 0.5f,
            startAngle = -60f,
            endAngle = 240,
            isClockwise = true,
        },
        new AttackMovementData
        {
            type = MovementType.Linear,
            startPoint = new Vector2(-2.5f, 0.5f),
            endPoint = new Vector2(-1.5f, 1.5f),
        },
        new AttackMovementData
        {
            type = MovementType.Circular,
            center = new Vector2(-2f, 2f),
            radius = 0.5f,
            startAngle = -60f,
            endAngle = 240,
            isClockwise = true,
        },
        new AttackMovementData
        {
            type = MovementType.Linear,
            startPoint = new Vector2(-1.5f, 0.5f),
            endPoint = new Vector2(-4f, 1.5f),
        },
    };

    /// <summary>
    /// プレイヤーのブレード（剣）による連続攻撃アニメーションと制御を行うコルーチン。
    /// 攻撃回数に応じた角度・方向・入力受付・硬直処理を管理する。
    /// </summary>
    private IEnumerator BladeAttack()
    {
        attackCount = 0; // 攻撃回数を初期化
        if (blade_prefab == null)
        {
            Debug.LogError("Blade prefab is not assigned.");
            yield break; // 剣のプレハブが設定されていない場合は終了
        }
        float bladeAttackTime = blade_prefab.GetComponent<Robot_blade_move>().attackTime;
        bladeAttackTime = playerEffectManager.CalculateFinalBladeMoveSpeed(bladeAttackTime);

        isBladeSwinging = true; // 攻撃中フラグON
        float startAngle = 0; // 攻撃開始角度を記録

        do
        {
            queuedAttack = false; // 入力受付リセット
            attackCount++; // 今回の攻撃回数をカウントアップ

            // 今回の攻撃に対応する移動データをリストから取得
            AttackMovementData movementData = null;
            if (attackCount - 1 < attackMovements.Count)
            {
                movementData = attackMovements[attackCount - 1];
            }

            // プレイヤーの向きに応じて、移動データのX座標を反転させる
            // ※元のデータを書き換えないようにコピーを作成して処理する
            AttackMovementData mirroredMovementData = new AttackMovementData();
            if (movementData != null)
            {
                // まず全ての値をコピー
                mirroredMovementData = new AttackMovementData
                {
                    type = movementData.type,
                    startPoint = movementData.startPoint,
                    endPoint = movementData.endPoint,
                    center = movementData.center,
                    radius = movementData.radius,
                    startAngle = movementData.startAngle,
                    endAngle = movementData.endAngle,
                    isClockwise = movementData.isClockwise,
                };

                // プレイヤーが右向き(rightFlag=true)の場合、X座標を反転
                if (rightFlag)
                {
                    mirroredMovementData.startPoint.x *= -1;
                    mirroredMovementData.endPoint.x *= -1;
                    mirroredMovementData.center.x *= -1;
                }
            }

            // 攻撃回数に対応した攻撃角度（開始と終了）を取得（右向き基準）
            Vector2 angles = rightAttackAngles[
                Mathf.Clamp(attackCount - 1, 0, rightAttackAngles.Count - 1)
            ];

            // プレイヤーが左向きなら左右反転（180度基準で裏返し）
            if (rightFlag)
            {
                angles.x = 180f - angles.x;
                angles.y = 180f - angles.y;
            }

            startAngle = angles.x;
            float endAngle = angles.y;

            // 回転方向の決定：5回目は時計回り（それ以外は反時計回り）
            // プレイヤーの向きによっても回転方向を反転させる
            bool isClockwise = (attackCount == 5);
            if (!rightFlag)
            {
                isClockwise = !isClockwise;
            }

            // 攻撃アニメーションの時間経過処理
            float elapsed = 0f;
            while (elapsed < bladeAttackTime)
            {
                float t = elapsed / bladeAttackTime; // 時間の正規化
                float easedT = bladeEaseCurve.Evaluate(t); // 緩急（Ease）をかける

                // 回転角度を計算（方向によって補間方法が変わる）
                float currentAngle = isClockwise
                    ? LerpAngleClockwise(startAngle, endAngle, easedT)
                    : LerpAngleCounterClockwise(startAngle, endAngle, easedT);

                // 剣の角度を適用
                blade_prefab.transform.rotation = Quaternion.Euler(0f, 0f, currentAngle);

                // ① 角度（Z軸回転）に対して方向ベクトルを算出
                float radians = currentAngle * Mathf.Deg2Rad; // 度→ラジアン変換
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;

                // ② 時間に応じてオフセットを出す（前半で遠ざかり、後半で戻す）
                float offsetT = Mathf.Sin(Mathf.PI * easedT); // 0→1→0 の動き
                Vector2 offset = direction * bladeSwingOffsetRadius * offsetT;

                // ③ 剣の位置を更新（ローカル空間で振り子のように動かす）
                blade_prefab.transform.localPosition = offset;

                if (mirroredMovementData != null && mirroredMovementData.type != MovementType.None)
                {
                    Vector2 robotMovementPos = transform.localPosition;

                    switch (mirroredMovementData.type)
                    {
                        case MovementType.Linear:
                            // 直線移動：始点と終点の間を線形補間
                            robotMovementPos = Vector2.Lerp(
                                mirroredMovementData.startPoint,
                                mirroredMovementData.endPoint,
                                easedT
                            );
                            break;

                        case MovementType.Circular:
                            // 円周上移動
                            float moveAngle = mirroredMovementData.isClockwise
                                ? LerpAngleClockwise(
                                    mirroredMovementData.startAngle,
                                    mirroredMovementData.endAngle,
                                    easedT
                                )
                                : LerpAngleCounterClockwise(
                                    mirroredMovementData.startAngle,
                                    mirroredMovementData.endAngle,
                                    easedT
                                );

                            // プレイヤーが右向きの場合、角度も反転させる
                            if (rightFlag)
                            {
                                moveAngle = 180f - moveAngle;
                            }

                            float moveRadians = moveAngle * Mathf.Deg2Rad;
                            Vector2 localDirection = new Vector2(
                                Mathf.Cos(moveRadians),
                                Mathf.Sin(moveRadians)
                            );
                            robotMovementPos =
                                mirroredMovementData.center
                                + localDirection * mirroredMovementData.radius;
                            break;
                    }

                    // 計算した座標を自身のローカル座標に適用
                    transform.localPosition = robotMovementPos;
                }

                elapsed += Time.deltaTime;
                yield return null; // 次のフレームまで待つ
            }

            // 最大攻撃数に達したらループを抜ける
            if (attackCount >= maxAttackCount)
                break;

            // 次の攻撃入力の受付ウィンドウを開く
            isAttackInputWindowOpen = true;
            float inputElapsed = 0f;
            while (inputElapsed < inputWindowTime)
            {
                if (queuedAttack)
                    break; // 攻撃入力が来たら即座に次へ

                inputElapsed += Time.deltaTime;
                yield return null;
            }
            isAttackInputWindowOpen = false;
        } while (queuedAttack && attackCount < maxAttackCount); // 攻撃入力が継続している限りループ

        isBladeSwinging = false; // 攻撃完了

        // 攻撃後の行動不能時間（ヒットストップのような硬直演出）
        float EnableMove_Sec = bladeAttackTime * EnableMoveTimeAcjuctment;
        StartCoroutine(AttackStart(EnableMove_Sec, afterBlade_Sec));

        // 攻撃終了後、剣を元の角度（startAngle）へ戻す補間アニメーション
        float returnTime = 0.1f;
        float returnElapsed = 0f;
        Vector3 startPos = blade_prefab.transform.localPosition;
        Vector3 endPos = Vector3.zero; // ローカル座標の原点に戻す
        Quaternion startRot = blade_prefab.transform.rotation;
        Quaternion endRot = Quaternion.Euler(0f, 0f, startAngle);
        while (returnElapsed < returnTime)
        {
            float t = returnElapsed / returnTime;
            blade_prefab.transform.rotation = Quaternion.Lerp(startRot, endRot, t);
            blade_prefab.transform.localPosition = Vector3.Lerp(startPos, endPos, t);
            returnElapsed += Time.deltaTime;
            yield return null;
        }

        // 最後に、微小な待機を挟んで演出に余韻を持たせる（AfterBlade）
        yield return new WaitForSeconds(afterBlade_Sec);
    }

    // 時計回り補間（CW）
    private float LerpAngleClockwise(float from, float to, float t)
    {
        float delta = (to - from + 360f) % 360f;
        return from + delta * t;
    }

    // 反時計回り補間（CCW）
    private float LerpAngleCounterClockwise(float from, float to, float t)
    {
        float delta = (from - to + 360f) % 360f;
        return from - delta * t;
    }

    private IEnumerator AttackStart(float Enable_Sec, float after_Sec)
    { //攻撃開始
        yield return new WaitForSeconds(Enable_Sec); //Enable_Secの時間分止める
        isAttacking = false; //プレイヤーの移動を再開する
        if (Enable_Sec < after_Sec)
            yield return new WaitForSeconds(after_Sec - Enable_Sec); //攻撃再開するまで停止
        isEnableNextAttack = true; //attackを再開する
    }

    public void SetRightFlag(bool flag)
    {
        if (isRobotmove)
        {
            StartCoroutine(setRightFlag(flag));
        }
    }

    private IEnumerator setRightFlag(bool flag)
    {
        if (isRobotmove)
        {
            yield return new WaitForSeconds(changeRightFlag_Sec);
            rightFlag = flag;
        }
    }

    private void InstantsetRightFlag()
    {
        if (isRobotmove)
        {
            if (PlayerObject != null)
                rightFlag = PlayerObject.GetComponent<Heroin_move>().rightFlag;
        }
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
        playerManager = PlayerManager.instance;
        playerEffectManager = PlayerEffectManager.instance;
        weaponManager = WeaponManager.instance;
        inputManager = InputManager.instance;

        // いずれかのマネージャーが見つからなければ、処理を中断
        if (
            playerManager == null
            || playerEffectManager == null
            || weaponManager == null
            || inputManager == null
        )
        {
            Debug.LogError("必要なマネージャーが見つかりませんでした。Robot_moveは機能しません。");
            yield break; // コルーチンを終了
        }

        // イベントの購読
        playerManager.OnBoolStatusChanged += OnAnyBoolStatusChanged;
        playerManager.OnChangeAttackType += OnChangeAttackType;
        playerManager.OnChangeWP += OnChangeWP;
        weaponManager.OnWeaponReplaced += OnChangeWeapon;

        // 各状態の初期化
        OnAnyBoolStatusChanged(
            PlayerStatusBoolName.isRobotmove,
            playerManager.GetPlayerBoolStatus(PlayerStatusBoolName.isRobotmove)
        );
        OnAnyBoolStatusChanged(
            PlayerStatusBoolName.isRobotattack,
            playerManager.GetPlayerBoolStatus(PlayerStatusBoolName.isRobotattack)
        );
        OnAnyBoolStatusChanged(
            PlayerStatusBoolName.isChangeAttackType,
            playerManager.GetPlayerBoolStatus(PlayerStatusBoolName.isChangeAttackType)
        );
        OnChangeAttackType(playerManager.GetPlayerAttackType());
        OnChangeWP(playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerCurrentWP));
        InitializeCurrentWeapon();

        // その他の初期化
        isEnableNextAttack = true; //攻撃を再開する
        isAttacking = false; //プレイヤーが動けるようにする
        if (PlayerObject == null)
            PlayerObject = GameObject.FindGameObjectWithTag(GameConstants.PlayerTagName); //Playerを取得
        this.transform.localPosition = new Vector2(0, 0); //ローカル座標を初期化
        rightFlag = PlayerObject.GetComponent<Heroin_move>().rightFlag; //左右の向きを初期化
        OnRobotVisibilityChanged?.Invoke(true); // ロボットの可視状態を表示にする
    }

    private void OnDisable()
    {
        // イベントを安全に解除
        if (playerManager != null)
        {
            playerManager.OnBoolStatusChanged -= OnAnyBoolStatusChanged;
            playerManager.OnChangeAttackType -= OnChangeAttackType;
            playerManager.OnChangeWP -= OnChangeWP;
        }

        if (weaponManager != null)
        {
            weaponManager.OnWeaponReplaced -= OnChangeWeapon;
        }

        // その他のリセット処理
        isEnable = false;
        isBladeSwinging = false; //剣の当たり判定を失くす
        isAttacking = false; //attackを再開する
        OnRobotVisibilityChanged?.Invoke(false); // ロボットの可視状態を非表示にする
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
            // ロボットが攻撃可能かどうかの状態
            case PlayerStatusBoolName.isRobotattack:
                isRobotattack = isEnabled; //Robotが攻撃できるかどうかを取得する
                break;
            //攻撃方法が変更できるかどうかの状態
            case PlayerStatusBoolName.isChangeAttackType:
                isChangeAttackType = isEnabled; //攻撃方法が変更できるかどうかを取得する
                break;
        }
    }

    /// <summary>
    /// 攻撃方法が変更されたときに呼び出されます。
    /// </summary>
    /// <param name="attackType">新しい攻撃方法</param>
    private void OnChangeAttackType(PlayerAttackType attackType)
    {
        playerAttackType = attackType; //攻撃方法を更新

        if (playerAttackType == PlayerAttackType.Blade)
        {
            if (!blade_prefab.activeSelf)
            {
                blade_prefab.SetActive(true); //剣を表示する
            }

            if (spriteRenderer.sprite != RobotSprite_blue)
            {
                spriteRenderer.sprite = RobotSprite_blue; //剣攻撃時のスプライトに変更
            }
        }
        else
        {
            if (blade_prefab.activeSelf)
            {
                blade_prefab.SetActive(false); //剣を非表示にする
            }

            if (spriteRenderer.sprite != RobotSprite_red)
            {
                spriteRenderer.sprite = RobotSprite_red; //通常時のスプライトに変更
            }
        }
    }

    private void OnChangeWP(int currentWP)
    {
        playerWP = currentWP; // WPを更新
    }

    /// <summary>
    /// 武器が変更されたときにWeaponManagerから呼び出されるイベントハンドラ
    /// </summary>
    private void OnChangeWeapon(Enum weaponID)
    {
        // 武器の種類を判別して、新しい武器データを取得・キャッシュする
        if (weaponID is BladeName bladeID)
        {
            // 武器データをキャッシュし、各スクリプトに反映
            currentBladeData = weaponManager.GetBladeByID(bladeID);
            if (bladeMoveScript != null && currentBladeData != null)
            {
                bladeMoveScript.SetBladeData(currentBladeData);
                bladeWPCost = currentBladeData.wpCost; // 剣のWP消費量を更新
            }
            else
            {
                Debug.LogError($"BladeName {bladeID} に対応する武器データが見つかりません。");
            }
        }
        else if (weaponID is ShootName shootID)
        {
            // 武器データをキャッシュし、各スクリプトに反映
            currentShootData = weaponManager.GetShootByID(shootID);
            if (currentShootData != null)
            {
                shootWPCost = currentShootData.wpCost; // 弾のWP消費量を更新
            }
            else
            {
                Debug.LogError($"ShootName {shootID} に対応する武器データが見つかりません。");
            }
        }
        else
        {
            Debug.LogWarning($"{weaponID}は対応していない武器タイプです。");
            return; // 対応していない武器タイプの場合は何もしない
        }
    }

    /// <summary>
    /// 現在装備している武器で各種データを初期化する
    /// </summary>
    private void InitializeCurrentWeapon()
    {
        var shootSaveData = GameManager.instance.savedata.WeaponEquipmentData.GetFirstWeaponByType(
            InventoryWeaponData.WeaponType.shoot
        );

        if (shootSaveData != null)
        {
            // 現在の射撃武器データのWeaponIDを取得
            Enum shootWeaponID = EnumIDUtility.FromID(shootSaveData.WeaponID);
            // 新しい弾の武器データを取得・キャッシュする
            OnChangeWeapon(shootWeaponID);
        }

        var bladeSaveData = GameManager.instance.savedata.WeaponEquipmentData.GetFirstWeaponByType(
            InventoryWeaponData.WeaponType.blade
        );

        if (bladeSaveData != null)
        {
            // 現在の剣武器データのWeaponIDを取得
            Enum bladeWeaponID = EnumIDUtility.FromID(bladeSaveData.WeaponID);
            // 新しい剣の武器データを取得・キャッシュする
            OnChangeWeapon(bladeWeaponID);
        }
    }
}

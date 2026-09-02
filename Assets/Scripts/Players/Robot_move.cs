using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
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
    /// 剣を振っている最中かどうかのフラグ (イベント発行用に変更)
    /// </summary>
    private bool _isBladeSwinging = false;
    public bool isBladeSwinging
    {
        get { return _isBladeSwinging; }
        private set
        {
            // 値が本当に変わった時だけイベントを発行
            if (_isBladeSwinging != value)
            {
                _isBladeSwinging = value;
                OnBladeSwingingChanged?.Invoke(_isBladeSwinging); // イベント発行
            }
        }
    }

    /// <summary>
    /// 剣の振り状態が変更されたときに発行されるイベント (true: 振り始め, false: 振り終わり)
    /// </summary>
    public event Action<bool> OnBladeSwingingChanged;

    /// <summary>
    /// ロボットが攻撃（剣・弾）を実行した瞬間に発行されるイベント
    /// </summary>
    public event Action OnRobotAttackExecuted;

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
    private float floatingAmplitude = 0.25f; //攻撃中でないときの上下の揺れの幅
    private float floatingDuration = 1.5f; //揺れの片道にかかる時間

    // プレイヤー関連のパラメータ
    private int playerWP = 0;

    // 内部状態を管理するフラグ
    private PlayerAttackType playerAttackType = PlayerAttackType.Shoot; // 現在のプレイヤーの攻撃方法
    private BladeAttackActionData currentAttackPattern = null; // 現在の剣の攻撃パターンデータ
    private int attackCount = 0; // 現在の剣の攻撃回数
    private bool isRobotmove = false; // ロボットが動けるかどうかのフラグ
    private bool isRobotattack = false; // ロボットが攻撃できるかどうかのフラグ
    private bool isChangeAttackType = false; // 攻撃方法を変更できるかどうかのフラグ
    private bool queuedAttack = false; // 次の剣攻撃が予約されたか
    private bool isEnable = false; // 表示されているかどうかのフラグ
    private bool isAttackInputWindowOpen = false; // 剣の連続攻撃の入力受付中か
    private bool isEnableNextAttack = true; // 次の攻撃が出来るかどうか
    private bool isTalking = false; // 会話状態を保存するローカル変数
    private Tween floatingTween; // 上下移動のTweenを管理
    private Tweener floatingReturnTween; // 基準位置へ戻るTweenを再利用する
    private bool isShootRecoveryActive = false;
    private float shootMovementUnlockTime = 0f;
    private float shootAttackReadyTime = 0f;
    private int activeBoomerangCount = 0;

    // 現在装備している武器のデータをキャッシュ
    private BladeWeaponData currentBladeData;
    private float bladeWPCost = 0f; // 剣のWP消費量
    private ShootWeaponData currentShootData;
    private float shootWPCost = 0f; // 弾のWP消費量

    // 外部スクリプトの参照をキャッシュ
    private Robot_blade_move bladeMoveScript;
    private FaboProjectileController shootMoveScriptPrefab; // 弾はPrefabから生成するため、Prefabのスクリプトを保持
    #endregion

    private void Awake()
    {
        isEnable = false; //初期化の準備
        spriteRenderer = GetComponent<SpriteRenderer>(); // SpriteRendererの取得
        if (this.gameObject.name != GameConstants.ROBOT_OBJECT_NAME)
        {
            Debug.LogError(
                $"{this.gameObject.name}の名前がGameConstants.ROBOT_OBJECT_NAMEと一致しません。"
            ); // ロボットのオブジェクト名が一致しない場合のエラーメッセージ
        }

        //剣と弾のスクリプトをキャッシュ
        if (blade_prefab != null)
        {
            bladeMoveScript = blade_prefab.GetComponent<Robot_blade_move>();
        }
        if (shoot_prefab != null)
        {
            shootMoveScriptPrefab = shoot_prefab.GetComponent<FaboProjectileController>();
        }
    }

    private void Update()
    {
        UpdateShootRecovery();

        if (Time.timeScale > 0 && isRobotmove && !isTalking)
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
                            if (CanLaunchCurrentShoot())
                            {
                                // WPが足り、同時発射数の上限内の場合のみ攻撃を実行
                                Shoot();
                            }
                            else
                            {
                                isEnableNextAttack = true;
                                isAttacking = false;
                            }
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
                    OnRobotAttackExecuted?.Invoke(); // 攻撃が実行されたことを外部に通知するイベントを発行
                }
            }
        }
    }

    private void FixedUpdate()
    {
        if (isRobotmove)
        {
            ApplySpriteDirection(); //攻撃中を含め、自分の画像の向きをプレイヤーへ追従させる

            if (!isBladeSwinging)
            {
                robot_pos = this.transform.localPosition; //自分の相対座標を入手
                robot_pos.x = Mathf.SmoothDamp( //プレイヤーに対しての自分のx座標を滑らかに調整
                    robot_pos.x,
                    rightFlag ? offset.x : -offset.x,
                    ref _currentVelocity,
                    _smoothTime,
                    _maxSpeed
                );

                // Y座標はDOTweenで制御するため、ここでは更新しない
                // robot_pos.y = offset.y; //プレイヤーに対しての自分のy座標を調整

                this.transform.localPosition = robot_pos; //自分の相対座標を設定
            }
        }
    }

    /// <summary>
    /// 弾を発射するメソッド
    /// 注意: このメソッドを IEnumerator にしてコルーチン化すると、弾が正常に発射されないことがある。
    /// </summary>
    private void Shoot()
    {
        StopFloatingAndReturn(); // ゆらゆらを停止して元の位置へ
        OnRobotAttackExecuted?.Invoke(); // 攻撃が実行されたことを外部に通知するイベントを発行

        Vector3 newPos = this.transform.position; //自分の座標を保存
        GameObject newGameObject = null;
        if (ObjectPooler.PersistentInstance != null)
        {
            newGameObject = ObjectPooler.PersistentInstance.SpawnFromPool(
                FaboProjectileController.RobotShootPoolTag,
                newPos,
                Quaternion.identity
            );
        }

        // プール未設定時にも既存の発射動作を維持する
        if (newGameObject == null)
        {
            newGameObject = Instantiate(shoot_prefab, newPos, Quaternion.identity);
        }

        FaboProjectileController shootMove = newGameObject.GetComponent<FaboProjectileController>();
        if (shootMove != null)
        {
            //キャッシュしておいた最新の武器データを渡して初期化
            shootMove.InitializeBullet(currentShootData, rightFlag, transform, this);
        }

        float enableMoveSec =
            (shootMove != null ? shootMove.vanishTime : currentShootData.vanishTime)
            * EnableMoveTimeAcjuctment;
        StartShootRecovery(enableMoveSec, currentShootData.shotInterval);
    }

    private void StartShootRecovery(float enableMoveSec, float afterSec)
    {
        float currentTime = Time.time;
        shootMovementUnlockTime = currentTime + enableMoveSec;
        shootAttackReadyTime = currentTime + Mathf.Max(enableMoveSec, afterSec);
        isShootRecoveryActive = true;
    }

    private void UpdateShootRecovery()
    {
        if (!isShootRecoveryActive)
            return;

        float currentTime = Time.time;
        if (isAttacking && currentTime >= shootMovementUnlockTime)
        {
            isAttacking = false;
        }

        if (currentTime < shootAttackReadyTime)
            return;

        isShootRecoveryActive = false;
        isEnableNextAttack = true;
        StartFloating();
    }

    private void Blade()
    {
        StopFloatingAndReturn(0.05f); // ゆらゆらをほぼ即座に停止して元の位置へ
        OnRobotAttackExecuted?.Invoke(); // 攻撃が実行されたことを外部に通知するイベントを発行
        InstantsetRightFlag(); //即座にロボットの左右を変更する
        ApplySpriteDirection(); //攻撃開始時の向きを即座に画像へ反映する
        isBladeSwinging = true; //剣の当たり判定を得る
        StartCoroutine(BladeAttack());
    }

    /// <summary>
    /// プレイヤーのブレード（剣）による連続攻撃アニメーションと制御を行うコルーチン。
    /// 攻撃回数に応じた角度・方向・入力受付・硬直処理を管理する。
    /// </summary>
    private IEnumerator BladeAttack()
    {
        // 攻撃データが未設定ならデフォルト動作またはエラー
        if (currentAttackPattern == null)
        {
            Debug.LogError(
                "BladeAttack: 攻撃パターンデータ(BladeAttackActionData)が設定されていません。"
            );
            yield break;
        }

        attackCount = 0; // 攻撃回数を初期化
        if (blade_prefab == null)
        {
            Debug.LogError("BladeAttack: blade_prefabが設定されていません。");
            yield break; // 剣のプレハブが設定されていない場合は終了
        }

        isBladeSwinging = true; // 攻撃中フラグON
        float startAngle = 0; // 攻撃開始角度を記録
        int maxSteps = currentAttackPattern.attackSteps.Count; // 定義されている攻撃ステップ数

        do
        {
            queuedAttack = false; // 入力受付リセット
            attackCount++; // 今回の攻撃回数をカウントアップ

            // 攻撃回数が定義を超えていたら終了（安全策）
            if (attackCount > maxSteps)
                break;

            // ScriptableObjectから現在のステップのデータを取得
            var currentStep = currentAttackPattern.attackSteps[attackCount - 1];
            bladeMoveScript?.BeginAttackStep();
            //ステップごとの時間を取得し、攻撃速度バフなどを適用する
            float baseStepTime = currentStep.attackTime;
            // PlayerEffectManagerで速度補正（バフ等）をかける
            float currentStepTime = playerEffectManager.CalculateFinalBladeMoveSpeed(baseStepTime);

            // 1. 回転角度の計算
            Vector2 angles = new Vector2(currentStep.startAngle, currentStep.endAngle);
            bool isClockwiseRot = currentStep.isClockwiseRotation;

            // プレイヤーが左向きなら左右反転（180度基準で裏返し）
            if (rightFlag)
            {
                angles.x = 180f - angles.x;
                angles.y = 180f - angles.y;
                // 右向きなら回転方向も反転させる必要がある
                isClockwiseRot = !isClockwiseRot;
            }

            startAngle = angles.x;
            float endAngle = angles.y;

            // 2. 移動データの準備 (コピーして反転処理)
            // プレイヤーの向きに応じてX座標を反転
            Vector2 stepStartPoint = currentStep.startPoint;
            Vector2 stepEndPoint = currentStep.endPoint;
            Vector2 stepCenter = currentStep.center;
            float stepMoveStartAngle = currentStep.moveStartAngle;
            float stepMoveEndAngle = currentStep.moveEndAngle;
            bool isClockwiseMove = currentStep.isClockwiseMovement;

            if (rightFlag)
            {
                stepStartPoint.x *= -1;
                stepEndPoint.x *= -1;
                stepCenter.x *= -1;
                // 円運動の角度や回転方向も反転
                stepMoveStartAngle = 180f - stepMoveStartAngle;
                stepMoveEndAngle = 180f - stepMoveEndAngle;
                isClockwiseMove = !isClockwiseMove;
            }

            // 攻撃アニメーションの時間経過処理
            float elapsed = 0f;
            while (elapsed < currentStepTime)
            {
                float t = elapsed / currentStepTime; // 時間の正規化
                // SOから取得したカーブを使用
                float easedT = currentAttackPattern.bladeEaseCurve.Evaluate(t);

                // --- 剣の回転 ---
                float currentAngle = isClockwiseRot
                    ? LerpAngleClockwise(startAngle, endAngle, easedT)
                    : LerpAngleCounterClockwise(startAngle, endAngle, easedT);

                blade_prefab.transform.rotation = Quaternion.Euler(0f, 0f, currentAngle);

                // 剣の位置オフセット（SOの半径設定を使用）
                float radians = currentAngle * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
                float offsetT = Mathf.Sin(Mathf.PI * easedT);
                Vector2 bladeOffset =
                    direction * currentAttackPattern.bladeSwingOffsetRadius * offsetT;
                blade_prefab.transform.localPosition = bladeOffset;

                // --- ロボット本体の移動 ---
                if (currentStep.movementType != BladeAttackActionData.MovementType.None)
                {
                    Vector2 robotMovementPos = transform.localPosition;

                    switch (currentStep.movementType)
                    {
                        case BladeAttackActionData.MovementType.Linear:
                            robotMovementPos = Vector2.Lerp(stepStartPoint, stepEndPoint, easedT);
                            break;

                        case BladeAttackActionData.MovementType.Circular:
                            float moveAngle = isClockwiseMove
                                ? LerpAngleClockwise(stepMoveStartAngle, stepMoveEndAngle, easedT)
                                : LerpAngleCounterClockwise(
                                    stepMoveStartAngle,
                                    stepMoveEndAngle,
                                    easedT
                                );

                            float moveRadians = moveAngle * Mathf.Deg2Rad;
                            Vector2 localDirection = new Vector2(
                                Mathf.Cos(moveRadians),
                                Mathf.Sin(moveRadians)
                            );
                            robotMovementPos = stepCenter + localDirection * currentStep.radius;
                            break;
                    }
                    transform.localPosition = robotMovementPos;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 最大攻撃数に達したらループを抜ける
            if (attackCount >= maxSteps)
            {
                bladeMoveScript?.TrySpawnHammerGroundImpact();
                break;
            }

            // 次の攻撃入力の受付ウィンドウを開く (SOの設定値を使用)
            isAttackInputWindowOpen = true;
            float inputElapsed = 0f;
            while (inputElapsed < currentAttackPattern.inputWindowTime)
            {
                if (queuedAttack)
                    break;

                inputElapsed += Time.deltaTime;
                yield return null;
            }
            isAttackInputWindowOpen = false;
        } while (queuedAttack && attackCount < maxSteps);

        isBladeSwinging = false; // 攻撃完了

        // 攻撃終了後、速やかに基準のY座標に戻る
        transform.DOLocalMoveY(offset.y, 0.2f).SetEase(Ease.OutQuad);

        // 硬直時間計算に、最後のステップの時間を使うか、あるいは固定値を使うか検討が必要です。
        // ここでは「最後の攻撃にかかった時間」をベースにする例とします。
        float lastStepTime = currentAttackPattern.attackSteps[attackCount - 1].attackTime;
        lastStepTime = playerEffectManager.CalculateFinalBladeMoveSpeed(lastStepTime);
        float EnableMove_Sec = lastStepTime * EnableMoveTimeAcjuctment;
        StartCoroutine(AttackStart(EnableMove_Sec, currentAttackPattern.afterBladeSec));

        // 剣を戻すアニメーション
        float returnTime = 0.1f;
        float returnElapsed = 0f;
        Vector3 startPos = blade_prefab.transform.localPosition;
        Vector3 endPos = Vector3.zero;
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

        // 余韻 (SOの設定値を使用)
        yield return new WaitForSeconds(currentAttackPattern.afterBladeSec);
    }

    // 時計回り補間（CW）
    private float LerpAngleClockwise(float from, float to, float t)
    {
        float delta = (from - to + 360f) % 360f;
        return from - delta * t;
    }

    // 反時計回り補間（CCW）
    private float LerpAngleCounterClockwise(float from, float to, float t)
    {
        float delta = (to - from + 360f) % 360f;
        return from + delta * t;
    }

    /// <summary>
    ///  攻撃後の待機処理を行うコルーチン
    /// </summary>
    /// <param name="Enable_Sec">攻撃後の行動不能時間（秒）</param>
    /// <param name="after_Sec">攻撃後の余韻時間（秒）</param>
    /// <returns></returns>
    private IEnumerator AttackStart(float Enable_Sec, float after_Sec)
    { //攻撃開始
        yield return new WaitForSeconds(Enable_Sec); //Enable_Secの時間分止める
        isAttacking = false; //プレイヤーの移動を再開する
        if (Enable_Sec < after_Sec)
            yield return new WaitForSeconds(after_Sec - Enable_Sec); //攻撃再開するまで停止
        isEnableNextAttack = true; //attackを再開する

        // 攻撃の硬直が解けたら、再度ゆらゆらを開始する
        StartFloating();
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

    private bool CanLaunchCurrentShoot()
    {
        if (
            currentShootData == null
            || currentShootData.moveType != ShootWeaponData.ShootMoveType.Boomerang
        )
        {
            return true;
        }

        int maxActiveCount = Mathf.Max(1, currentShootData.maxActiveBoomerangCount);
        return activeBoomerangCount < maxActiveCount;
    }

    internal void NotifyBoomerangLaunched()
    {
        activeBoomerangCount++;
    }

    internal void NotifyBoomerangReturned()
    {
        activeBoomerangCount = Mathf.Max(0, activeBoomerangCount - 1);
    }

    private void ApplySpriteDirection()
    {
        bool isFacingRight = rightFlag;
        if (isBladeSwinging && PlayerObject != null)
        {
            Heroin_move playerMove = PlayerObject.GetComponent<Heroin_move>();
            if (playerMove != null)
            {
                isFacingRight = playerMove.rightFlag;
            }
        }

        spriteRenderer.flipX = !isFacingRight;
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
        GameManager.OnTalkingStateChanged += HandleTalkingStateChanged;

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
            PlayerObject = GameObject.FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME); //Playerを取得
        this.transform.localPosition = new Vector2(0, 0); //ローカル座標を初期化
        rightFlag = PlayerObject.GetComponent<Heroin_move>().rightFlag; //左右の向きを初期化
        OnRobotVisibilityChanged?.Invoke(true); // ロボットの可視状態を表示にする

        // 初期位置に設定してから、ゆらゆらを開始
        this.transform.localPosition = new Vector3(rightFlag ? offset.x : -offset.x, offset.y, 0);
        StartFloating();
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

        GameManager.OnTalkingStateChanged -= HandleTalkingStateChanged;

        // DOTweenを確実に停止させる
        if (floatingTween != null)
        {
            floatingTween.Kill();
        }
        if (floatingReturnTween != null)
        {
            floatingReturnTween.Kill();
        }

        // その他のリセット処理
        isEnable = false;
        isBladeSwinging = false; //剣の当たり判定を失くす
        isAttacking = false; //attackを再開する
        isShootRecoveryActive = false;
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

                if (currentBladeData.attackActionData != null)
                {
                    currentAttackPattern = currentBladeData.attackActionData;
                }
                else
                {
                    Debug.LogWarning(
                        $"BladeID: {bladeID} の BladeWeaponData に BladeAttackActionData が設定されていません。"
                    );
                    currentAttackPattern = null;
                }
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
    /// GameManagerから会話状態の変更通知を受け取る
    /// </summary>
    private void HandleTalkingStateChanged(bool talkState)
    {
        isTalking = talkState;
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

    #region Floating Movement
    // --- ゆらゆら動く処理 ---

    /// <summary>
    /// 攻撃中でないときに、上下にゆらゆら動くTweenを開始します。
    /// </summary>
    private void StartFloating()
    {
        // 既存のTweenがあれば安全に停止
        if (floatingReturnTween != null && floatingReturnTween.IsActive())
        {
            floatingReturnTween.Pause();
        }

        // 攻撃中や剣を振っている最中は開始しない
        if (isAttacking || isBladeSwinging)
            return;

        // Y座標を offset.y を中心に、floatingAmplitude の幅で往復運動させる
        // FixedUpdateのタイミングで更新することで物理挙動との同期が取りやすくなります
        if (floatingTween == null || !floatingTween.IsActive())
        {
            floatingTween = transform
                .DOLocalMoveY(offset.y + floatingAmplitude, floatingDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(UpdateType.Fixed)
                .SetAutoKill(false);
        }
        else
        {
            floatingTween.Restart();
        }
    }

    /// <summary>
    /// ゆらゆら動くTweenを停止し、速やかに基準のY座標に戻します。
    /// </summary>
    private void StopFloatingAndReturn(float duration = 0.1f)
    {
        // 既存のTweenを停止
        if (floatingTween != null && floatingTween.IsActive())
        {
            floatingTween.Pause();
        }

        // 基準となるY座標へ指定時間で移動
        if (floatingReturnTween == null || !floatingReturnTween.IsActive())
        {
            floatingReturnTween = transform
                .DOLocalMoveY(offset.y, duration)
                .SetUpdate(UpdateType.Fixed)
                .SetAutoKill(false);
        }
        else
        {
            floatingReturnTween.ChangeEndValue(offset.y, duration, true).Restart();
        }
    }

    #endregion
}

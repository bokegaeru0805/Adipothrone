using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NaughtyAttributes;
using UnityEngine;

[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class DesertTempleBossMoveController : MonoBehaviour
{
    //TODO: 攻撃力の設定
    //TODO: 攻撃毎の弾のスプライト・オブジェクトの変更
    private const string RIGHT_ARM_BULLET_POOLTAG = "DesertTempleGolemShoot";
    public const string RIGHT_ARM_BULLET_SPAWN_EFFECT_POOLTAG = "DesertTempleBossShootSpawnEffect"; //61D2FF
    public const float LEFTARM_ARMUP_ANIMATION_DURATION = 0.938f;
    public const float LEFTARM_ATTACK_ANIMATION_DURATION = 0.188f;

    [Header("移動の設定")]
    [Tooltip("移動速度")]
    [SerializeField]
    private float moveSpeedX = 3.0f;

    [Header("登場演出の設定")]
    [Tooltip("ResetState時に特定位置まで移動してから行動を開始するか")]
    [SerializeField]
    private bool useIntroMovement = false;

    [Tooltip("登場移動の目標X座標")]
    [SerializeField, ShowIf(nameof(useIntroMovement))]
    private float introTargetX = 0f;

    [Header("移動範囲の設定(必須)")]
    [SerializeField]
    private float leftBound = 0;

    [SerializeField]
    private float rightBound = 0;

    [Header("右腕の攻撃の設定")]
    [Tooltip("攻撃前の溜め時間")]
    [SerializeField]
    private float rightArmAttackChargeTime = 1.0f;

    [Tooltip("弾の速度")]
    [SerializeField]
    private float rightArmAttackBulletSpeed = 5.0f;

    [Tooltip("右腕の攻撃の時間間隔の最小値")]
    [SerializeField]
    private float rightArmAttackIntervalMin = 2.0f;

    [Tooltip("右腕の攻撃の時間間隔の最大値")]
    [SerializeField]
    private float rightArmAttackIntervalMax = 5.0f;

    [Tooltip("弾の地面からの高さ")]
    [SerializeField]
    private float rightArmAttackBulletHeight = 1.0f;

    [Tooltip("発射位置のオフセット(左向き時)")]
    [SerializeField]
    private Vector2 rightArmAttackBulletOffset = new Vector2(4.5f, -5.5f);

    [Header("左腕の攻撃の設定")]
    [Tooltip("攻撃前の溜め時間")]
    [SerializeField]
    private float leftArmAttackChargeTime = 1.0f;

    [Tooltip("弾の速度")]
    [SerializeField]
    private float leftArmAttackBulletSpeed = 5.0f;

    [Tooltip("弾同士の時間間隔")]
    [SerializeField]
    private float leftArmAttackBulletInterval = 0.3f;

    [Tooltip("攻撃時の高さオフセット")]
    [SerializeField]
    private float leftArmAttackBulletHeightOffset = 1.0f;

    [Tooltip("左腕の攻撃の時間間隔の最小値")]
    [SerializeField]
    private float leftArmAttackIntervalMin = 3.0f;

    [Tooltip("左腕の攻撃の時間間隔の最大値")]
    [SerializeField]
    private float leftArmAttackIntervalMax = 6.0f;

    [Tooltip("発射位置のオフセット(左向き時)")]
    [SerializeField]
    private Vector2 leftArmAttackBulletOffset = new Vector2(-3.8f, -5.5f);

    [Header("レーザー攻撃の設定")]
    [Tooltip("レーザー攻撃のチャージ時間")]
    [SerializeField]
    private float laserAttackChargeTime = 2.0f;

    [Tooltip("レーザー攻撃の持続時間")]
    [SerializeField]
    private float laserAttackDuration = 3.0f;

    [Tooltip("レーザー攻撃のクールタイム")]
    [SerializeField]
    private float laserAttackCooldown = 5.0f;

    [Tooltip("レーザー攻撃後の時間間隔の最小値")]
    [SerializeField]
    private float laserAttackIntervalMin = 10.0f;

    [Tooltip("レーザー攻撃後の時間間隔の最大値")]
    [SerializeField]
    private float laserAttackIntervalMax = 15.0f;

    [Tooltip("チャージ前の最初の光輪の回転速度")]
    [SerializeField]
    private float laserChargeInitialHaloRotationSpeed = 30f;

    [Header("分身攻撃の設定")]
    [Tooltip("分身後の消滅までの時間")]
    [SerializeField]
    private float cloneExistDuration = 10.0f;

    [Tooltip("攻撃のチャージ時間")]
    [SerializeField]
    private float cloneAttackChargeTime = 1.0f;

    [Tooltip("弾の速度")]
    [SerializeField]
    private float cloneAttackBulletSpeed = 5.0f;

    [Tooltip("分身攻撃後の時間間隔の最小値")]
    [SerializeField]
    private float cloneAttackIntervalMin = 10.0f;

    [Tooltip("分身攻撃後の時間間隔の最大値")]
    [SerializeField]
    private float cloneAttackIntervalMax = 15.0f;

    [Tooltip("分身の数")]
    [SerializeField]
    private int cloneCount = 3;

    [Tooltip("分身が出現できる端からの最小距離")]
    [SerializeField]
    private float cloneSpawnMinOffsetFromEdge = 2.0f;

    [Header("囲い込み攻撃の設定")]
    [Tooltip("囲い込みを行い続ける時間")]
    [SerializeField]
    private float encirclementDuration = 8.0f;

    [Tooltip("囲い込みの弾の速度")]
    [SerializeField]
    private float encirclementBulletSpeed = 5.0f;

    [Tooltip("囲い込みのプレイヤーからの距離")]
    [SerializeField]
    private float encirclementRadius = 5.0f;

    [Tooltip(" 囲い込みのプレイヤーから引き戻す時間")]
    [SerializeField]
    private float pullBackDuration = 1.0f;

    [Tooltip("囲い込みの攻撃後の時間間隔の最小値")]
    [SerializeField]
    private float encirclementAttackIntervalMin = 15.0f;

    [Tooltip("囲い込みの攻撃後の時間間隔の最大値")]
    [SerializeField]
    private float encirclementAttackIntervalMax = 20.0f;

    [Tooltip("囲い込みの弾の個数(偶数推奨)")]
    [SerializeField]
    private int encirclementBulletCount = 12;

    [Tooltip("囲い込みの弾の生成される始点の角度(度)")]
    [SerializeField]
    private float encirclementStartAngle = 60f;

    [Header("降雨の攻撃の設定")]
    [Tooltip("降雨の攻撃の時間")]
    [SerializeField]
    private float rainAttackDuration = 5.0f;

    [Tooltip("降雨の攻撃の弾同士の時間間隔の最小値")]
    [SerializeField]
    private float rainAttackBulletIntervalMin = 0.2f;

    [Tooltip("降雨の攻撃の弾同士の時間間隔の最大値")]
    [SerializeField]
    private float rainAttackBulletIntervalMax = 0.5f;

    [Tooltip("降雨の攻撃の弾の速度")]
    [SerializeField]
    private float rainAttackBulletSpeed = 5.0f;

    [Tooltip("降雨の攻撃後の時間間隔の最小値")]
    [SerializeField]
    private float rainAttackIntervalMin = 10.0f;

    [Tooltip("降雨の攻撃後の時間間隔の最大値")]
    [SerializeField]
    private float rainAttackIntervalMax = 15.0f;

    [Tooltip("天井のY座標")]
    [SerializeField]
    private float ceilingY = 10.0f;

    [Tooltip("地面のY座標")]
    [SerializeField]
    private float groundY = 0.0f;

    [Space(50)]
    [Header("ゲームオブジェクト設定")]
    [SerializeField]
    private GameObject rightArmObject; // 右腕のオブジェクト

    [SerializeField]
    private GameObject leftArmObject; // 左腕のオブジェクト

    [SerializeField]
    private GameObject haloObject; // 光輪オブジェクト

    [SerializeField]
    private GameObject laserObject; // レーザーオブジェクト

    [SerializeField]
    private GameObject sparkEffectObject; // スパークエフェクトオブジェクト

    [SerializeField]
    private GameObject auraEffectObject; // オーラエフェクトオブジェクト

    [SerializeField]
    private GameObject clonePrefab; // 分身のプレハブ

    [Tooltip("光輪の座標のオフセット(左向き時)")]
    [SerializeField]
    private Vector2 haloOffset = new Vector2(0.25f, -0.2f);

    [Header("浮遊の設定")]
    [Tooltip("浮遊の上下幅")]
    [SerializeField]
    private float floatAmplitude = 1f;

    [Tooltip("浮遊の1周期にかかる時間")]
    [SerializeField]
    private float floatDuration = 2.0f;

    // --- 内部変数 ---
    private bool rightFlag = false;
    private float initialY; // 初期のY座標
    private bool isMovingLeft = false; // trueなら左移動、falseなら右移動
    private bool isHorizontalMoveActive = false; // 横移動が有効か
    private bool isIntroMoving = false; // 登場演出の移動中かどうか
    private Tweener floatTween; // 浮遊アニメーション管理用
    private float haloRotationSpeed = 0f; // 光輪の回転速度
    private float moveStartHpThreshold = 0.8f; // HPがこの割合を下回ったら移動開始
    private const float DEFAULT_HALO_ROTATION_SPEED = 10f; // デフォルトの光輪回転速度
    private bool isHaloRotatingClockwise = false; // true: 時計回り(CW), false: 反時計回り(CCW)
    private Coroutine rightArmAttackCoroutine; // 右腕攻撃用コルーチン
    private bool isFacingLocked = false; // 向きの更新をロックするフラグ
    private Coroutine attackLoopCoroutine; // 本体の攻撃ループ用
    private LayerMask groundLayer; // 地面レイヤー
    private const float MAX_GROUND_DETECT_DISTANCE = 40.0f; // 地面検出の最大距離
    private List<DesertTempleBossClone> activeClones = new List<DesertTempleBossClone>(); // アクティブな分身リスト
    private List<DesertTempleBossClone> clonePool = new List<DesertTempleBossClone>(); // 分身プール
    private DesertTempleBossState currentState = DesertTempleBossState.Idle;

    private enum DesertTempleBossState
    {
        Idle,
        NormalAttacking,
        LaserAttacking,
        CloneAttacking,
        EncirclementAttacking,
        RainAttacking,
    }

    // --- 内部参照 ---
    private SpriteRenderer bodySpriteRenderer;
    private CharacterHealth _characterHpScript;
    private ShieldController _shieldController;

    // --- 外部参照 ---
    private SpriteRenderer rightArmSpriteRenderer;
    private SpriteRenderer leftArmSpriteRenderer;
    private SpriteRenderer haloSpriteRenderer;
    private Transform haloTransform;
    private Transform laserTransform;
    private Transform sparkEffectTransform;
    private Transform playerTransform;
    private Animator rightArmAnimator;
    private Animator leftArmAnimator;
    private Animator sparkEffectAnimator;

    private void Awake()
    {
        groundLayer = LayerMask.GetMask(GameConstants.PHYSICS_LAYER_NAME_GROUND); // Groundレイヤーを取得

        if (leftArmObject != null)
        {
            leftArmSpriteRenderer = leftArmObject.GetComponent<SpriteRenderer>();
            leftArmAnimator = leftArmObject.GetComponent<Animator>();
        }

        if (rightArmObject != null)
        {
            rightArmSpriteRenderer = rightArmObject.GetComponent<SpriteRenderer>();
            rightArmAnimator = rightArmObject.GetComponent<Animator>();
        }

        if (haloObject != null)
        {
            haloTransform = haloObject.transform;
            haloSpriteRenderer = haloObject.GetComponent<SpriteRenderer>();
        }

        if (laserObject != null)
        {
            laserTransform = laserObject.transform;
            laserObject.SetActive(false);
        }

        if (sparkEffectObject != null)
        {
            sparkEffectTransform = sparkEffectObject.transform;
            sparkEffectAnimator = sparkEffectObject.GetComponent<Animator>();
            sparkEffectObject.SetActive(false);
        }

        if (auraEffectObject != null)
        {
            auraEffectObject.SetActive(true);
        }

        bodySpriteRenderer = GetComponent<SpriteRenderer>();
        _characterHpScript = GetComponent<CharacterHealth>();
        _shieldController = GetComponent<ShieldController>();
    }

    private void Start()
    {
        InitializeClonePool(); // 分身プールの初期化

        // --- イベントの購読 ---
        if (_characterHpScript != null)
        {
            _characterHpScript.OnHPChanged += HandleHPChanged;
        }

        if (_shieldController != null)
        {
            _shieldController.OnAllShieldsBroken += HandleAllShieldsBroken;
        }
    }

    /// <summary>
    /// 分身を事前に生成してプールしておく処理
    /// </summary>
    private void InitializeClonePool()
    {
        // 既存のプールがあれば掃除（シーンリロード対策など）
        foreach (var c in clonePool)
        {
            if (c != null)
                Destroy(c.gameObject);
        }
        clonePool.Clear();

        if (clonePrefab == null)
            return;

        // 必要数だけ生成して非アクティブにしておく
        for (int i = 0; i < cloneCount; i++)
        {
            GameObject obj = Instantiate(clonePrefab, transform.position, Quaternion.identity);
            DesertTempleBossClone script = obj.GetComponent<DesertTempleBossClone>();

            if (script != null)
            {
                obj.SetActive(false); // 最初は非表示
                obj.tag = GameConstants.UNTAGGED_TAG_NAME; // Outlineを消すためにタグを外す
                clonePool.Add(script);
            }
            else
            {
                Destroy(obj); // スクリプトがない場合は破棄
            }
        }
    }

    /// <summary>
    /// HPが変化した際に呼び出され、閾値を下回っていたら横移動を有効にする
    /// </summary>
    private void HandleHPChanged(int currentHP)
    {
        if (isHorizontalMoveActive)
            return; // 既に動いているなら無視

        if (_characterHpScript != null && _characterHpScript.MaxHP > 0)
        {
            float ratio = _characterHpScript.NormalizedHP; // 0〜1の割合でHPを取得
            if (ratio <= moveStartHpThreshold)
            {
                isHorizontalMoveActive = true;
            }
        }
    }

    /// <summary>
    /// シールドが全て破壊された際に呼び出され、横移動を有効にする
    /// </summary>
    private void HandleAllShieldsBroken()
    {
        if (!isHorizontalMoveActive)
        {
            isHorizontalMoveActive = true;
        }
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

        // 初期向きを設定
        rightFlag = IsTargetToRight();
        UpdateFacingDirection(rightFlag);
        isMovingLeft = rightFlag; // 最初はプレイヤーの方向へ向かって移動開始

        // アニメーターを初期化
        leftArmAnimator.SetTrigger("IdleTrigger");
        rightArmAnimator.SetTrigger("IdleTrigger");

        // 変数を初期化
        initialY = transform.position.y;
        haloRotationSpeed = DEFAULT_HALO_ROTATION_SPEED;
        isHaloRotatingClockwise = true;
        isFacingLocked = false;
        isHorizontalMoveActive = false;

        // 状態のリセット
        StopFloating(); // 浮遊を停止
        SetHaloRotation(DEFAULT_HALO_ROTATION_SPEED, true); // 光輪回転をリセット
        StopRightArmAttack(); // 右腕攻撃を停止
        laserObject.SetActive(false); // レーザーを非表示
        sparkEffectObject.SetActive(false); // スパークエフェクトを非表示

        StartFloating(); // 移動と浮遊を開始
        StartRightArmAttack(); // 右腕攻撃を開始
        StartBodyAttackLoop(); // 本体の攻撃ループを開始

        // 登場演出の分岐
        if (useIntroMovement)
        {
            isIntroMoving = true;
            // 攻撃ループは開始せず、移動完了を待つ
        }
        else
        {
            isIntroMoving = false;
        }
    }

    private void FixedUpdate()
    {
        // 敵の動きがポーズされているかどうかを確認
        if (TimeManager.instance.isEnemyMovePaused)
        {
            // ポーズ中はTweenも一時停止させる
            if (floatTween != null && floatTween.IsPlaying())
                floatTween.Pause();
            return;
        }
        else
        {
            // ポーズ解除中はTweenを再開させる
            if (floatTween != null && !floatTween.IsPlaying())
                floatTween.Play();
        }

        // --- 登場移動の処理 ---
        if (isIntroMoving)
        {
            UpdateIntroMove();
        }
        // --- 追横移動の処理 ---
        else if (isHorizontalMoveActive)
        {
            UpdateHorizontalMove();
        }

        if (haloTransform != null && haloRotationSpeed > 0f)
        {
            // 時計回り(true)の場合はZ軸マイナス方向、反時計回り(false)の場合はプラス方向
            float directionMultiplier = isHaloRotatingClockwise ? -1f : 1f;

            // 自分が向いている向き(flipX)の影響を受けないよう、Transform.Rotateを使用
            float angle = directionMultiplier * haloRotationSpeed * Time.deltaTime;
            haloTransform.Rotate(0, 0, angle);
        }

        // --- 向きの更新 ---
        if (!isFacingLocked)
        {
            bool isTargetCurrentlyRight = IsTargetToRight();
            if (rightFlag != isTargetCurrentlyRight)
            {
                rightFlag = isTargetCurrentlyRight;
                UpdateFacingDirection(rightFlag); //すべてのパーツの向きを更新
            }
        }
    }

    #region 右腕攻撃の処理
    /// <summary>
    /// 右腕の攻撃ループを開始します
    /// </summary>
    public void StartRightArmAttack()
    {
        if (rightArmAttackCoroutine != null)
        {
            StopCoroutine(rightArmAttackCoroutine);
        }
        rightArmAttackCoroutine = StartCoroutine(RightArmAttackSequence());
    }

    /// <summary>
    /// 右腕の攻撃ループを停止します
    /// </summary>
    public void StopRightArmAttack()
    {
        if (rightArmAttackCoroutine != null)
        {
            StopCoroutine(rightArmAttackCoroutine);
            rightArmAttackCoroutine = null;
        }
    }

    /// <summary>
    /// 右腕攻撃のシーケンス（待機 -> 溜め -> 発射）
    /// </summary>
    private IEnumerator RightArmAttackSequence()
    {
        rightArmAnimator.SetTrigger("IdleTrigger"); // 初期状態に戻す

        while (true)
        {
            // 攻撃間隔の待機 (Min〜Maxのランダム)
            float waitTime = Random.Range(rightArmAttackIntervalMin, rightArmAttackIntervalMax);
            yield return StartCoroutine(WaitForTime(waitTime));

            // 溜め動作
            rightArmAnimator.SetTrigger("AttackTrigger");
            yield return StartCoroutine(WaitForTime(rightArmAttackChargeTime));

            // 弾の生成と発射
            FireRightArmBullet();
            rightArmAnimator.SetTrigger("IdleTrigger");

            // ※ループの末尾で1フレーム待つ（念のため無限ループ防止）
            yield return null;
        }
    }

    /// <summary>
    /// 弾を生成して発射する処理
    /// </summary>
    private void FireRightArmBullet()
    {
        // オブジェクトプールから弾を取得
        GameObject bullet = ObjectPooler.SceneInstance.SpawnFromPool(
            RIGHT_ARM_BULLET_POOLTAG,
            Vector3.zero, // 一旦ゼロで生成
            Quaternion.identity
        );

        if (bullet != null)
        {
            // 地面までの距離を取得
            float distToGround = GetDistanceToGround();
            float spawnY = 0f;

            // 地面が見つかった場合 (距離が最大値未満)
            if (distToGround < MAX_GROUND_DETECT_DISTANCE)
            {
                // 地面のY座標 = 現在のY - 地面までの距離
                float groundY = transform.position.y - distToGround;

                // 生成Y = 地面Y + 指定した高さ
                spawnY = groundY + rightArmAttackBulletHeight;
            }
            else
            {
                // 地面が見つからない場合（穴の上など）は、ボスの位置を基準にするか、
                // あるいは以前のように絶対座標を使う等のフォールバック処理
                // ここでは「ボスの現在位置 - 1.5f + 高さ」などの仮計算にするか、単純に現在位置を使う
                spawnY = transform.position.y - 1.5f + rightArmAttackBulletHeight;
            }

            // X座標の計算
            float offsetX = rightFlag
                ? -rightArmAttackBulletOffset.x
                : rightArmAttackBulletOffset.x;

            Vector3 spawnPos = new Vector3(transform.position.x + offsetX, spawnY, 0f);

            bullet.transform.position = spawnPos;

            // 発射方向の計算
            Vector2 direction = rightFlag ? Vector2.right : Vector2.left;

            // Rigidbody2D設定
            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = direction * rightArmAttackBulletSpeed;
            }

            // エフェクトの生成
            GameObject spawnEffect = ObjectPooler.SceneInstance.SpawnFromPool(
                RIGHT_ARM_BULLET_SPAWN_EFFECT_POOLTAG,
                spawnPos,
                Quaternion.identity
            );

            // SE再生
            var sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
            if (sePlayer != null)
            {
                sePlayer.Play(SE_EnemyAction.Shoot_Water1);
            }
        }
    }
    #endregion

    #region  本体の攻撃ループ
    /// <summary>
    /// 本体の攻撃ループを開始します
    /// </summary>
    public void StartBodyAttackLoop()
    {
        if (attackLoopCoroutine != null)
            StopCoroutine(attackLoopCoroutine);
        attackLoopCoroutine = StartCoroutine(BodyAttackLoopSequence());
    }

    /// <summary>
    /// 攻撃ループシーケンス
    /// 今後攻撃の種類が増えた場合、ここでランダム分岐させます
    /// </summary>
    private IEnumerator BodyAttackLoopSequence()
    {
        while (true)
        {
            // --- 0〜4の範囲でランダム選択 ---
            int attackType = Random.Range(0, 5); // 0:レーザー, 1:分身, 2:囲い込み, 3:降雨, 4:通常攻撃

            // 攻撃種別に応じたインターバル設定用変数
            float minInterval = 0f;
            float maxInterval = 0f;

            switch (attackType)
            {
                case 0: // レーザー攻撃
                    minInterval = laserAttackIntervalMin;
                    maxInterval = laserAttackIntervalMax;
                    yield return StartCoroutine(PerformLaserAttack());
                    break;
                case 1: // 分身攻撃
                    minInterval = cloneAttackIntervalMin;
                    maxInterval = cloneAttackIntervalMax;
                    yield return StartCoroutine(PerformCloneAttack());
                    break;
                case 2: // 囲い込み攻撃
                    minInterval = encirclementAttackIntervalMin;
                    maxInterval = encirclementAttackIntervalMax;
                    yield return StartCoroutine(PerformEncirclementAttack());
                    break;
                case 3: // 降雨攻撃
                    minInterval = rainAttackIntervalMin;
                    maxInterval = rainAttackIntervalMax;
                    yield return StartCoroutine(PerformRainAttack());
                    break;
                case 4: // 通常攻撃
                    // 左腕攻撃用のインターバル変数を流用、または別途定義
                    minInterval = leftArmAttackIntervalMin;
                    maxInterval = leftArmAttackIntervalMax;
                    yield return StartCoroutine(PerformNormalAttack());
                    break;
            }

            // 攻撃後のインターバル待機
            float interval = Random.Range(minInterval, maxInterval);
            yield return StartCoroutine(WaitForTime(interval));
        }
    }

    #region 通常攻撃の処理
    /// <summary>
    /// 通常攻撃（左腕3連射）の一連の動作
    /// </summary>
    private IEnumerator PerformNormalAttack()
    {
        // 1. 準備フェーズ
        currentState = DesertTempleBossState.NormalAttacking;

        // 2. チャージ動作
        leftArmAnimator.SetFloat(
            "ArmUpSpeed",
            LEFTARM_ARMUP_ANIMATION_DURATION / leftArmAttackChargeTime
        );
        leftArmAnimator.SetTrigger("ArmUpTrigger"); // 腕を上げる

        yield return StartCoroutine(WaitForTime(leftArmAttackChargeTime));

        // 3. 向きの固定
        bool isFacingRight = IsTargetToRight();
        UpdateFacingDirection(isFacingRight);
        isFacingLocked = true; // 向き固定

        // 4. 攻撃開始
        leftArmAnimator.SetTrigger("AttackTrigger");
        yield return StartCoroutine(WaitForTime(LEFTARM_ATTACK_ANIMATION_DURATION)); // 攻撃アニメーション待機

        // ターゲットのY座標オフセットリストを作成 (0, +height, -height)
        List<float> targetYOffsets = new List<float>
        {
            0f,
            leftArmAttackBulletHeightOffset,
            -leftArmAttackBulletHeightOffset,
        };

        // ランダムな順番にする (シャッフル)
        targetYOffsets = System.Linq.Enumerable.ToList(
            System.Linq.Enumerable.OrderBy(targetYOffsets, x => System.Guid.NewGuid())
        );

        // 3発発射ループ
        foreach (float yOffset in targetYOffsets)
        {
            // 発射条件チェック (懐に入られていないか)
            bool shouldFire = false;

            if (playerTransform != null)
            {
                float diffX = playerTransform.position.x - transform.position.x;
                float armReachThreshold = Mathf.Abs(leftArmAttackBulletOffset.x);

                if (isFacingRight)
                {
                    // 右向き: ターゲットが右におり、かつ腕の長さより遠い
                    if (diffX >= armReachThreshold)
                    {
                        shouldFire = true;
                    }
                }
                else
                {
                    // 左向き: ターゲットが左におり、かつ腕の長さより遠い
                    if (diffX <= -armReachThreshold)
                    {
                        shouldFire = true;
                    }
                }
            }

            // 条件を満たさない場合は攻撃中断
            if (!shouldFire)
            {
                break;
            }

            // 発射処理
            FireNormalAttackBullet(isFacingRight, yOffset);

            // 次の弾までの間隔待機
            yield return StartCoroutine(WaitForTime(leftArmAttackBulletInterval));
        }

        // 5. 復帰
        leftArmAnimator.SetTrigger("IdleTrigger"); // 腕を戻す
        isFacingLocked = false; // 向き固定解除
        currentState = DesertTempleBossState.Idle;
    }

    /// <summary>
    /// 通常攻撃用の弾を発射する
    /// </summary>
    /// <param name="isFacingRight">発射時の向いている方向</param>
    /// <param name="yOffset">プレイヤーに対するY座標オフセット</param>
    private void FireNormalAttackBullet(bool isFacingRight, float yOffset)
    {
        // 発射位置の計算
        // ボスの現在位置 + オフセット (左向き用オフセットを基準に、右向きならX反転)
        float spawnOffsetX = isFacingRight
            ? -leftArmAttackBulletOffset.x
            : leftArmAttackBulletOffset.x;
        Vector3 spawnPos =
            transform.position + new Vector3(spawnOffsetX, leftArmAttackBulletOffset.y, 0f);

        // ターゲット位置の計算
        Vector3 targetPos = Vector3.zero;
        if (playerTransform != null)
        {
            targetPos = playerTransform.position + new Vector3(0f, yOffset, 0f);
        }
        else
        {
            // プレイヤーがいない場合は前方へ
            targetPos = spawnPos + (isFacingRight ? Vector3.right : Vector3.left) * 10f;
        }

        // プールから弾を取得 (左腕用の弾があればそちらを使用推奨、なければ右腕用を流用)
        // ここでは既存コードに合わせてRIGHT_ARM_BULLET_POOLTAGを使用します
        GameObject bullet = ObjectPooler.SceneInstance.SpawnFromPool(
            RIGHT_ARM_BULLET_POOLTAG,
            spawnPos,
            Quaternion.identity
        );

        if (bullet != null)
        {
            // 進行方向
            Vector2 direction = (targetPos - spawnPos).normalized;

            // 弾の回転
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.Euler(0, 0, angle);

            // 速度
            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = direction * leftArmAttackBulletSpeed;
            }

            // エフェクト生成 (プールがあれば)
            ObjectPooler.SceneInstance.SpawnFromPool(
                RIGHT_ARM_BULLET_SPAWN_EFFECT_POOLTAG,
                spawnPos,
                Quaternion.identity
            );

            // SE再生
            var sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
            if (sePlayer != null)
            {
                // 左腕攻撃用のSEがあれば変更してください
                sePlayer.Play(SE_EnemyAction.Shoot_Water1);
            }
        }
    }
    #endregion

    #region レーザー攻撃の処理
    /// <summary>
    /// レーザー攻撃の一連の動作
    /// </summary>
    private IEnumerator PerformLaserAttack()
    {
        // 準備フェーズ: 移動停止
        isHorizontalMoveActive = false; // 横移動停止
        StopRightArmAttack(); // 演出のため右腕攻撃も一時停止

        sparkEffectObject.SetActive(true); // スパークエフェクトを表示

        // 回転方向をランダムに決定 (0なら反時計、1なら時計)
        bool isClockwise = Random.Range(0, 2) == 0;

        // チャージフェーズ: 光輪の回転演出
        // 光輪の回転向きをセット
        isHaloRotatingClockwise = isClockwise;

        // TODO: チャージSE再生

        // 回転速度を Initial -> 0 へ減衰させる
        // DOVirtual.Floatを使って haloRotationSpeed を操作
        Tween chargeTween = DOVirtual
            .Float(
                laserChargeInitialHaloRotationSpeed,
                0f,
                laserAttackChargeTime,
                (value) =>
                {
                    haloRotationSpeed = value;
                }
            )
            .SetEase(Ease.OutQuad); // 徐々に止まる

        // チャージ時間待機
        yield return StartCoroutine(WaitForTime(laserAttackChargeTime));

        // 念のためTween完了を待つ（ポーズ等でズレた場合用）
        if (chargeTween != null && chargeTween.IsActive())
            chargeTween.Kill();
        haloRotationSpeed = 0f;

        // 攻撃フェーズ: レーザー発射
        isFacingLocked = true; // 向きを固定
        currentState = DesertTempleBossState.LaserAttacking;
        //TODO: 攻撃SE再生

        if (laserObject != null)
        {
            laserObject.SetActive(true);

            // レーザーの初期角度設定: 真下 (-90度)
            float startAngle = -90f;
            laserObject.transform.localRotation = Quaternion.Euler(0, 0, startAngle);

            // 終了角度設定: 方向に応じて ±360度
            float endAngle = startAngle + (isClockwise ? -360f : 360f);

            // 回転アニメーション (遅 -> 早 -> 遅)
            // Transformを直接回すと最短距離を通ってしまうことがあるため、値をTweenして適用する
            Tween laserTween = DOVirtual
                .Float(
                    startAngle,
                    endAngle,
                    laserAttackDuration,
                    (angle) =>
                    {
                        laserObject.transform.localRotation = Quaternion.Euler(0, 0, angle);
                    }
                )
                .SetEase(Ease.InOutCubic); // 初めは遅く中盤は早く最後は遅い

            // レーザー持続時間待機
            yield return StartCoroutine(WaitForTime(laserAttackDuration));

            if (laserTween != null && laserTween.IsActive())
                laserTween.Kill();
            laserObject.SetActive(false);
        }
        sparkEffectAnimator.SetTrigger("EndTrigger"); // スパークエフェクト終了アニメーション再生

        // クールダウンフェーズ
        yield return StartCoroutine(WaitForTime(laserAttackCooldown));
        sparkEffectObject.SetActive(false); // スパークエフェクトを非表示

        // 復帰フェーズ
        isFacingLocked = false; // 向き固定解除
        isHorizontalMoveActive = true; // 移動再開
        currentState = DesertTempleBossState.Idle;

        // 光輪の回転をデフォルトに戻す
        SetHaloRotation(DEFAULT_HALO_ROTATION_SPEED, true);

        StartRightArmAttack(); // 右腕攻撃再開
    }
    #endregion
    #region 分身攻撃の処理
    /// <summary>
    /// 分身攻撃の一連の動作
    /// </summary>
    private IEnumerator PerformCloneAttack()
    {
        // 1. 準備フェーズ
        isHorizontalMoveActive = false;
        StopRightArmAttack();
        currentState = DesertTempleBossState.CloneAttacking;

        SetHaloRotation(DEFAULT_HALO_ROTATION_SPEED, true);
        activeClones.Clear(); // 今回使用するリストをクリア

        // 2. 消失演出
        // Sequenceを使って全パーツ同時にフェードアウトさせる
        Sequence fadeOutSeq = DOTween.Sequence();

        // 本体パーツのフェードアウト登録
        if (bodySpriteRenderer)
            fadeOutSeq.Join(bodySpriteRenderer.DOFade(0f, 0.5f));
        if (leftArmSpriteRenderer)
            fadeOutSeq.Join(leftArmSpriteRenderer.DOFade(0f, 0.5f));
        if (rightArmSpriteRenderer)
            fadeOutSeq.Join(rightArmSpriteRenderer.DOFade(0f, 0.5f));
        if (haloSpriteRenderer)
            fadeOutSeq.Join(haloSpriteRenderer.DOFade(0f, 0.5f));
        this.tag = GameConstants.UNTAGGED_TAG_NAME; // Outlineを消すためにタグを外す
        auraEffectObject.SetActive(false); // オーラを非表示

        // TODO: 消失SE再生
        yield return fadeOutSeq.SetEase(Ease.OutQuad).WaitForCompletion();

        // 3. 配置計算
        int totalCount = 1 + cloneCount;
        float totalWidth = rightBound - leftBound - (cloneSpawnMinOffsetFromEdge * 2);
        float step = totalCount > 1 ? totalWidth / (totalCount - 1) : 0;
        List<float> positionsX = new List<float>();
        for (int i = 0; i < totalCount; i++)
        {
            positionsX.Add(leftBound + cloneSpawnMinOffsetFromEdge + (step * i));
        }
        positionsX = System.Linq.Enumerable.ToList(
            System.Linq.Enumerable.OrderBy(positionsX, x => System.Guid.NewGuid())
        );

        // 4. 配置と起動 (Instantiateではなくプールから取得)

        // --- 本体の配置 ---
        transform.position = new Vector3(positionsX[0], initialY, transform.position.z);
        // 本体の出現 (フェードイン)
        Sequence fadeInSeq = DOTween.Sequence();
        if (bodySpriteRenderer)
            fadeInSeq.Join(bodySpriteRenderer.DOFade(1f, 0.5f));
        if (leftArmSpriteRenderer)
            fadeInSeq.Join(leftArmSpriteRenderer.DOFade(1f, 0.5f));
        if (rightArmSpriteRenderer)
            fadeInSeq.Join(rightArmSpriteRenderer.DOFade(1f, 0.5f));
        if (haloSpriteRenderer)
            fadeInSeq.Join(haloSpriteRenderer.DOFade(1f, 0.5f));
        this.tag = GameConstants.IMMUNE_ENEMY_TAG_NAME; // Outline用にタグを戻す

        fadeInSeq.SetEase(Ease.InQuad); // 再生開始(待機はしない)
        //TODO: 出現SE再生

        // --- 分身の配置 ---
        // プールの数が足りているか確認しつつ使用
        for (int i = 1; i < totalCount; i++)
        {
            int poolIndex = i - 1;
            if (poolIndex < clonePool.Count)
            {
                DesertTempleBossClone cloneScript = clonePool[poolIndex];

                // 位置を設定
                cloneScript.transform.position = new Vector3(
                    positionsX[i],
                    initialY,
                    transform.position.z
                );

                // 初期化して表示 (Setup内でSetActive(true)される)
                cloneScript.Setup(
                    target: playerTransform,
                    initialYPos: initialY,
                    _attackChargeTime: cloneAttackChargeTime,
                    _bulletSpeed: cloneAttackBulletSpeed,
                    _bulletOffset: leftArmAttackBulletOffset,
                    _groundY: groundY
                );

                // 出現演出 (Setupでscale=0にされている前提)
                cloneScript.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);

                // アクティブリストに追加（「攻撃しろ」等の命令を送るため）
                activeClones.Add(cloneScript);
            }
        }

        yield return StartCoroutine(WaitForTime(0.5f));

        // 5. 攻撃命令 (変更なし)
        StartCoroutine(SelfCloneAttackSequence()); // 本体

        foreach (var clone in activeClones)
        {
            float attackActionTime = cloneAttackChargeTime + 0.5f;
            float maxDelay = Mathf.Max(0, cloneExistDuration - attackActionTime);
            float randomDelay = Random.Range(0, maxDelay);
            StartCoroutine(clone.AttackSequence(randomDelay));
        }

        // 6. 存在時間待機
        yield return StartCoroutine(WaitForTime(cloneExistDuration));

        // 7. 終了処理
        CleanupClones();

        // 8. 復帰
        currentState = DesertTempleBossState.Idle;
        auraEffectObject.SetActive(true); // オーラを再表示
        isHorizontalMoveActive = true;
        StartRightArmAttack();
    }

    /// <summary>
    /// 分身攻撃時の「本体」の攻撃シーケンス
    /// </summary>
    private IEnumerator SelfCloneAttackSequence()
    {
        // タイミング計算
        float attackActionTime = cloneAttackChargeTime + 0.5f;
        float maxDelay = Mathf.Max(0, cloneExistDuration - attackActionTime);
        float randomDelay = Random.Range(0, maxDelay);

        yield return StartCoroutine(WaitForTime(randomDelay));

        // --- チャージ前に向きを確定させる ---
        bool isFacingRight = IsTargetToRight();
        UpdateFacingDirection(isFacingRight); // ここで向きをロック

        // チャージ
        leftArmAnimator.SetFloat(
            "ArmUpSpeed",
            LEFTARM_ARMUP_ANIMATION_DURATION / cloneAttackChargeTime
        );
        leftArmAnimator.SetTrigger("ArmUpTrigger");

        yield return StartCoroutine(WaitForTime(cloneAttackChargeTime));
        yield return StartCoroutine(WaitForTime(0.5f)); // アニメーション完了待ち

        // ボスから見たプレイヤーのX方向の相対距離 (右がプラス、左がマイナス)
        float diffX = playerTransform.position.x - transform.position.x;

        // 腕の長さ（オフセットのX絶対値）を射程の閾値とする
        float armReachThreshold = Mathf.Abs(leftArmAttackBulletOffset.x);

        // 発射条件フラグ
        bool shouldFire = false;

        if (isFacingRight)
        {
            // 右を向いている場合:
            // プレイヤーが右側にいて(diffX > 0)、かつ 腕の長さより遠い(diffX >= threshold) なら発射
            // ※つまり、0 〜 threshold の間（懐）や、マイナス（背後）にいたら不発
            if (diffX >= armReachThreshold)
            {
                shouldFire = true;
            }
        }
        else
        {
            // 左を向いている場合:
            // プレイヤーが左側にいて(diffX < 0)、かつ 腕の長さより遠い(diffX <= -threshold) なら発射
            if (diffX <= -armReachThreshold)
            {
                shouldFire = true;
            }
        }

        // 条件を満たしている場合のみ攻撃実行
        if (shouldFire)
        {
            FireLeftArmBulletForClone(isFacingRight); // 確定済みの向きを渡す

            leftArmAnimator.SetTrigger("AttackTrigger");
            yield return StartCoroutine(WaitForTime(LEFTARM_ATTACK_ANIMATION_DURATION)); // 攻撃アニメーション待ち
            yield return StartCoroutine(WaitForTime(0.2f)); // 攻撃後の小休止
        }

        leftArmAnimator.SetTrigger("IdleTrigger");
    }

    /// <summary>
    /// 本体用の左腕攻撃発射 (分身攻撃時用)
    /// </summary>
    private void FireLeftArmBulletForClone(bool isRight)
    {
        // 右腕攻撃と同じ定数を使うか、分身用定数を使うかは要調整
        GameObject bullet = ObjectPooler.SceneInstance.SpawnFromPool(
            RIGHT_ARM_BULLET_POOLTAG,
            Vector3.zero,
            Quaternion.identity
        );

        if (bullet != null)
        {
            // 1. 先に生成位置を確定させる
            // 地面検知を入れるか、簡易計算にするか。ここでは分身に合わせて簡易計算にします。
            float spawnY = transform.position.y - 1.5f + rightArmAttackBulletHeight;
            float offsetX = isRight ? -leftArmAttackBulletOffset.x : leftArmAttackBulletOffset.x;
            Vector3 spawnPos = new Vector3(transform.position.x + offsetX, spawnY, 0f);

            bullet.transform.position = spawnPos;

            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb)
            {
                // 2. プレイヤーへの方向ベクトルを計算
                Vector2 direction;

                if (playerTransform != null)
                {
                    // (ターゲット位置 - 現在位置) の正規化ベクトルを取得
                    direction = ((Vector2)playerTransform.position - (Vector2)spawnPos).normalized;
                }
                else
                {
                    // プレイヤーが見つからない場合のフォールバック（水平発射）
                    direction = isRight ? Vector2.right : Vector2.left;
                }

                // 3. 速度を適用
                rb.velocity = direction * cloneAttackBulletSpeed;

                // 4. エフェクト生成
                ObjectPooler.SceneInstance.SpawnFromPool(
                    RIGHT_ARM_BULLET_SPAWN_EFFECT_POOLTAG,
                    spawnPos,
                    Quaternion.identity
                );
                //TODO: SE再生

                // (オプション) 弾の画像の向きも進行方向に合わせたい場合は以下を追加
                // float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                // bullet.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            var se = GetComponent<CriWare.Assets.CriAtomSePlayer>();
            if (se)
                se.Play(SE_EnemyAction.Shoot_Water1);
        }
    }

    /// <summary>
    /// 分身の退場処理
    /// </summary>
    private void CleanupClones()
    {
        // activeClonesに入っているのは「今回使った分身」
        foreach (var clone in activeClones)
        {
            if (clone != null)
            {
                // Clone側のDespawnメソッド内で、アニメーション後にSetActive(false)される
                clone.Despawn();
            }
        }
        // リストをクリアするが、オブジェクト自体はclonePoolに残っているので次回再利用される
        activeClones.Clear();
    }
    #endregion

    #region 囲い込み攻撃の処理
    /// <summary>
    /// 囲い込み攻撃の一連の動作
    /// </summary>
    private IEnumerator PerformEncirclementAttack()
    {
        // 1. 準備フェーズ
        currentState = DesertTempleBossState.EncirclementAttacking;
        leftArmAnimator.SetFloat("ArmUpSpeed", LEFTARM_ARMUP_ANIMATION_DURATION / 0.2f); // 0.2秒で上げる
        leftArmAnimator.SetTrigger("ArmUpTrigger"); // 左腕を上げる

        // 光輪の回転設定 (反時計回り = false)
        // 初速は高速(例: 720度/秒)から徐々に遅くする
        float initialHaloSpeed = 720f;
        isHaloRotatingClockwise = false;

        // 弾管理用リスト
        List<GameObject> bullets = new List<GameObject>();
        List<Transform> bulletTransforms = new List<Transform>();

        // エフェクト管理用リスト
        List<GameObject> spawnEffects = new List<GameObject>();
        List<Transform> spawnEffectTransforms = new List<Transform>();

        // 2. 弾の生成と配置
        // 左右それぞれの個数
        int halfCount = encirclementBulletCount / 2;
        // 弾をプールから取得してリスト化
        for (int i = 0; i < encirclementBulletCount; i++)
        {
            GameObject b = ObjectPooler.SceneInstance.SpawnFromPool(
                RIGHT_ARM_BULLET_POOLTAG,
                Vector3.zero,
                Quaternion.identity
            );
            if (b != null)
            {
                bullets.Add(b);
                bulletTransforms.Add(b.transform);

                // エフェクトリストのインデックスを合わせるため、nullで枠を確保しておく
                spawnEffects.Add(null);
                spawnEffectTransforms.Add(null);
            }
        }

        // 3. 追従ループ (Halo減速演出含む)
        // Haloのスピード減衰Tween
        Tween haloTween = DOVirtual
            .Float(
                initialHaloSpeed,
                0f,
                encirclementDuration,
                (val) =>
                {
                    haloRotationSpeed = val;
                }
            )
            .SetEase(Ease.OutQuad);

        float timer = 0f;

        // 角度リストの計算
        // グループ1: startAngle ～ -startAngle (右側)
        List<float> angles = new List<float>();
        float angleStep = (halfCount > 1) ? (encirclementStartAngle * 2) / (halfCount - 1) : 0;

        for (int i = 0; i < halfCount; i++)
        {
            angles.Add(encirclementStartAngle - (angleStep * i));
        }

        // グループ2: (180 - startAngle) ～ (180 + startAngle) (左側)
        // ※ 180 - 60 = 120, 180 + 60 = 240
        for (int i = 0; i < halfCount; i++)
        {
            float startLeft = 180f - encirclementStartAngle;
            angles.Add(startLeft + (angleStep * i));
        }

        bool hasShownSpawnEffect = false;

        while (timer < encirclementDuration)
        {
            if (playerTransform != null)
            {
                // 中心座標: プレイヤー位置 + 高さ補正
                Vector2 center =
                    (Vector2)playerTransform.position
                    + new Vector2(0, GameConstants.PLAYER_BASE_HEIGHT / 2.0f);

                for (int i = 0; i < bullets.Count; i++)
                {
                    if (bullets[i] == null || !bullets[i].activeInHierarchy)
                        continue;

                    // 浮遊・振動の計算
                    // 時間(timer)とインデックス(i)を使って、弾ごとに異なる動きを作る
                    float floatFreq = 3.0f; // 振動の速さ
                    float floatAmp = 0.3f; // 振動の幅（浮遊感の強さ）

                    // X軸とY軸で異なる周期の波を作り、有機的な浮遊感を出す
                    float wobbleX = Mathf.Sin(timer * floatFreq + i * 0.5f) * floatAmp;
                    float wobbleY = Mathf.Cos(timer * floatFreq * 0.8f + i * 0.3f) * floatAmp;

                    // 半径方向にも少し呼吸のような伸縮を加える
                    float radiusBreath = Mathf.Sin(timer * 2.0f + i * 0.1f) * 0.2f;

                    // 角度から基本座標を計算 + 半径の伸縮
                    float rad = angles[i] * Mathf.Deg2Rad;
                    Vector2 baseOffset =
                        new Vector2(Mathf.Cos(rad), Mathf.Sin(rad))
                        * (encirclementRadius + radiusBreath);

                    // 最終座標 = 中心 + 基本円周位置 + 浮遊振動
                    bulletTransforms[i].position =
                        center + baseOffset + new Vector2(wobbleX, wobbleY);

                    // --- エフェクトの生成と追従処理 ---
                    if (!hasShownSpawnEffect)
                    {
                        // 最初のフレームでのみ出現エフェクトを表示し、リストに保存
                        GameObject effect = ObjectPooler.PersistentInstance.SpawnFromPool(
                            GameConstants.EFFECT_ENEMY_SPAWN_POOLTAG,
                            bulletTransforms[i].position,
                            Quaternion.identity
                        );

                        // 生成したエフェクトをリストの該当インデックスに格納
                        spawnEffects[i] = effect;
                        spawnEffectTransforms[i] = (effect != null) ? effect.transform : null;
                    }
                    else
                    {
                        // 2回目以降: エフェクトが消えるまで追従させる
                        // インデックス範囲内かつ、エフェクトが存在し、アクティブである場合のみ位置更新
                        if (
                            i < spawnEffects.Count
                            && spawnEffects[i] != null
                            && spawnEffects[i].activeInHierarchy
                        )
                        {
                            spawnEffectTransforms[i].position = bulletTransforms[i].position;
                        }
                    }

                    // 弾の向きを中心に向ける
                    float angle = angles[i] + 180f; // 外側から中心を見る向き
                    bulletTransforms[i].rotation = Quaternion.Euler(0, 0, angle);
                }

                if (!hasShownSpawnEffect)
                {
                    hasShownSpawnEffect = true;
                }
            }

            yield return StartCoroutine(WaitForTime(Time.deltaTime)); // 1フレーム待機(ポーズ対応)
            timer += Time.deltaTime;
        }

        // HaloTweenが残っていたらキル
        if (haloTween != null && haloTween.IsActive())
            haloTween.Kill();
        haloRotationSpeed = 0f;

        // 4. 攻撃予備動作 (弾を引く = 半径を広げる)
        leftArmAnimator.SetTrigger("AttackTrigger"); // 攻撃アニメーション再生
        yield return StartCoroutine(WaitForTime(LEFTARM_ATTACK_ANIMATION_DURATION)); // アニメーション完了待ち
        float pullBackRadius = encirclementRadius * 1.5f; // 1.5倍に広がる

        // 攻撃直前のプレイヤー位置で中心を固定（ロックオン）
        Vector2 lockedCenter = Vector2.zero;
        if (playerTransform != null)
        {
            lockedCenter = (Vector2)playerTransform.position;
        }
        else
        {
            lockedCenter = transform.position; // プレイヤーロスト時は自分中心など
        }

        // 半径を広げるアニメーション
        float pullTimer = 0f;
        while (pullTimer < pullBackDuration)
        {
            float progress = pullTimer / pullBackDuration;
            // 0から1の進行度(progress)を、OutCubicのカーブに変換した値を取得
            float easedProgress = DOVirtual.EasedValue(0f, 1f, progress, Ease.OutCubic);
            float currentRadius = Mathf.Lerp(encirclementRadius, pullBackRadius, easedProgress);

            for (int i = 0; i < bullets.Count; i++)
            {
                if (bullets[i] == null || !bullets[i].activeInHierarchy)
                    continue;

                float rad = angles[i] * Mathf.Deg2Rad;
                Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * currentRadius;
                bulletTransforms[i].position = lockedCenter + offset;
            }

            yield return StartCoroutine(WaitForTime(Time.deltaTime));
            pullTimer += Time.deltaTime;
        }

        // 5. 発射
        // TODO: 発射SE再生 (例: sePlayer.Play(SE_EnemyAction.Shoot_Magic);)
        leftArmAnimator.SetTrigger("IdleTrigger"); // 腕を戻す

        foreach (var b in bullets)
        {
            if (b == null || !b.activeInHierarchy)
                continue;

            b.tag = GameConstants.DAMAGEABLE_ENEMY_TAG_NAME; // タグを戻す

            Rigidbody2D rb = b.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                // ロックした中心位置に向かって発射
                Vector2 direction = (lockedCenter - (Vector2)b.transform.position).normalized;
                rb.velocity = direction * encirclementBulletSpeed;
            }
        }

        // 6. 復帰
        currentState = DesertTempleBossState.Idle;
        SetHaloRotation(DEFAULT_HALO_ROTATION_SPEED, true); // 光輪回転戻す
    }
    #endregion

    #region 降雨攻撃の処理
    /// <summary>
    /// 降雨攻撃の一連の動作
    /// </summary>
    private IEnumerator PerformRainAttack()
    {
        // 1. 準備フェーズ
        // ※この攻撃は移動と右腕攻撃を止めないため、isHorizontalMoveActive等は変更しない
        currentState = DesertTempleBossState.RainAttacking;
        leftArmAnimator.SetTrigger("ArmUpTrigger"); // 左腕を上げる
        yield return StartCoroutine(WaitForTime(LEFTARM_ARMUP_ANIMATION_DURATION)); // 腕上げ完了待ち

        float timer = 0f;

        // 2. 攻撃ループ
        while (timer < rainAttackDuration)
        {
            // 弾の発射
            FireRainBullet();

            // 次の弾までの間隔を決定
            float interval = Random.Range(rainAttackBulletIntervalMin, rainAttackBulletIntervalMax);

            // 待機
            yield return StartCoroutine(WaitForTime(interval));
            timer += interval;
        }

        // 3. 復帰
        leftArmAnimator.SetTrigger("IdleTrigger"); // 左腕を戻す
        currentState = DesertTempleBossState.Idle;
        // 移動等は止められていないため、再開処理は不要
    }

    /// <summary>
    /// 降雨攻撃用の弾を発射する
    /// </summary>
    private void FireRainBullet()
    {
        // 出発地点: 自分のX座標, 天井のY座標
        Vector3 startPos = new Vector3(transform.position.x, ceilingY, 0f);

        // 着地地点: ランダムなX座標, 地面のY座標
        float targetX = Random.Range(leftBound, rightBound);
        Vector3 targetPos = new Vector3(targetX, groundY, 0f);

        // プールから弾を取得
        GameObject bullet = ObjectPooler.SceneInstance.SpawnFromPool(
            RIGHT_ARM_BULLET_POOLTAG,
            startPos,
            Quaternion.identity
        );

        if (bullet != null)
        {
            // 進行方向ベクトル
            Vector2 direction = (targetPos - startPos).normalized;

            // 弾の向きを進行方向に合わせる (Spriteが右向き前提の場合)
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.Euler(0, 0, angle);

            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = direction * rainAttackBulletSpeed;
            }

            // 必要であればSE再生
            /*
            var sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
            if (sePlayer != null) sePlayer.Play(SE_EnemyAction.Shoot_Water1);
            */
        }
    }
    #endregion

    #endregion

    #region 横・浮遊移動と光輪回転の処理
    /// <summary>
    /// x座標を基準とした横移動を行う
    /// </summary>
    private void UpdateHorizontalMove()
    {
        float currentX = transform.position.x;
        float nextX = currentX + (moveSpeedX * (isMovingLeft ? -1 : 1) * Time.deltaTime);

        // 範囲外に出そうになったら方向転換
        if (nextX >= rightBound)
        {
            nextX = rightBound;
            isMovingLeft = true; // 左へ
        }
        else if (nextX <= leftBound)
        {
            nextX = leftBound;
            isMovingLeft = false; // 右へ
        }

        transform.position = new Vector3(nextX, transform.position.y, transform.position.z);
    }

    /// <summary>
    /// 目標X座標まで moveSpeedX で移動する
    /// </summary>
    private void UpdateIntroMove()
    {
        // 現在位置から目標位置への1フレーム分の移動量を計算
        float step = moveSpeedX * Time.deltaTime;

        // YとZは維持したまま、Xだけ目標へ移動
        Vector3 targetPos = new Vector3(introTargetX, transform.position.y, transform.position.z);
        transform.position = Vector3.MoveTowards(transform.position, targetPos, step);

        // 到着判定 (誤差0.01f以内)
        if (Mathf.Abs(transform.position.x - introTargetX) < 0.01f)
        {
            // 移動完了
            isIntroMoving = false;

            // 通常行動の開始
            isHorizontalMoveActive = true;

            // 次の移動方向を決定（エリアの中央より左にいれば右へ、右にいれば左へなど）
            // ここではプレイヤーの方向に合わせて自然に戦い始めるように設定
            isMovingLeft = IsTargetToRight();
        }
    }

    /// <summary>
    /// 上下の浮遊アニメーションを開始します
    /// </summary>
    private void StartFloating()
    {
        // 既に動いている場合は何もしない、またはリセットする
        if (floatTween != null && floatTween.IsActive())
            return;

        // 現在のY座標から開始（あるいは初期位置基準にするなら initialY を使用）
        // ここでは initialY を基準に浮遊させる
        floatTween = transform
            .DOMoveY(initialY + floatAmplitude, floatDuration)
            .SetEase(Ease.InOutSine) // ふわふわした動き
            .SetLoops(-1, LoopType.Yoyo) // 往復ループ
            .SetLink(gameObject); // オブジェクト削除時にTweenも破棄
    }

    /// <summary>
    /// 上下の浮遊アニメーションを停止します
    /// </summary>
    private void StopFloating()
    {
        if (floatTween != null)
        {
            floatTween.Kill();
            floatTween = null;
        }
    }

    /// <summary>
    /// 光輪の回転を設定します
    /// </summary>
    /// <param name="speed">回転速度 (度/秒)</param>
    /// <param name="isClockwise">true: 時計回り, false: 反時計回り</param>
    public void SetHaloRotation(float speed, bool isClockwise)
    {
        haloRotationSpeed = speed;
        isHaloRotatingClockwise = isClockwise;
    }
    #endregion

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
    /// スプライトの向きを更新します
    /// </summary>
    /// <param name="isFacingRight">右を向いているか</param>
    private void UpdateFacingDirection(bool isFacingRight)
    {
        //本体の向きを変更
        if (bodySpriteRenderer != null)
        {
            bodySpriteRenderer.flipX = isFacingRight;
        }

        //腕の向きを変更
        if (leftArmSpriteRenderer != null)
        {
            leftArmSpriteRenderer.flipX = isFacingRight;
        }
        if (rightArmSpriteRenderer != null)
        {
            rightArmSpriteRenderer.flipX = isFacingRight;
        }

        //光輪の位置を調整
        if (haloTransform != null)
        {
            if (isFacingRight)
            {
                haloTransform.localPosition = new Vector2(-haloOffset.x, haloOffset.y);
            }
            else
            {
                haloTransform.localPosition = haloOffset;
            }
        }

        // レーザーの向きを調整
        if (laserTransform != null)
        {
            if (isFacingRight)
            {
                laserTransform.localPosition = new Vector2(-haloOffset.x, haloOffset.y);
            }
            else
            {
                laserTransform.localPosition = haloOffset;
            }
        }

        // スパークエフェクトの向きを調整
        if (sparkEffectTransform != null)
        {
            if (isFacingRight)
            {
                sparkEffectTransform.localPosition = new Vector2(-haloOffset.x, haloOffset.y);
            }
            else
            {
                sparkEffectTransform.localPosition = haloOffset;
            }
        }
    }

    /// <summary>
    /// 現在位置から真下に向かってRayを飛ばし、地面までの距離を計測します。
    /// </summary>
    /// <returns>地面までの距離 (検出できない場合は探索最大距離)</returns>
    private float GetDistanceToGround()
    {
        Vector2 origin = transform.position;

        // moveDistanceの2倍の長さまで下方向を探索
        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            Vector2.down,
            MAX_GROUND_DETECT_DISTANCE,
            groundLayer
        );

        if (hit.collider != null)
        {
            // 現在位置Y - 地面位置Y = 地面までの距離
            return origin.y - hit.point.y;
        }
        else
        {
            // 地面が見つからない＝空中にいると判断し、最大値を返す
            return MAX_GROUND_DETECT_DISTANCE;
        }
    }

    /// <summary>
    /// ポーズを考慮した待機処理
    /// </summary>
    private IEnumerator WaitForTime(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            // ポーズ中は時間を進めずに待機
            if (TimeManager.instance.isEnemyMovePaused)
            {
                yield return null;
                continue;
            }

            timer += Time.deltaTime;
            yield return null;
        }
    }

    private void OnDestroy()
    {
        // 安全のためTweenを破棄
        if (floatTween != null)
        {
            floatTween.Kill();
        }

        // --- イベントの購読解除 ---
        if (_characterHpScript != null)
        {
            _characterHpScript.OnHPChanged -= HandleHPChanged;
        }

        if (_shieldController != null)
        {
            _shieldController.OnAllShieldsBroken -= HandleAllShieldsBroken;
        }
    }

    private void OnDrawGizmosSelected()
    {
        // --- 右腕攻撃の発射位置 ---
        Gizmos.color = Color.red;
        Vector3 normalPos = transform.position + (Vector3)(rightArmAttackBulletOffset);
        Gizmos.DrawWireSphere(normalPos, 0.3f);

        // --- 左腕攻撃の発射位置 ---
        Gizmos.color = Color.blue;
        Vector3 leftPos = transform.position + (Vector3)(leftArmAttackBulletOffset);
        Gizmos.DrawWireSphere(leftPos, 0.3f);
    }

    private void OnDrawGizmos()
    {
        // --- 移動範囲 ---
        Gizmos.color = new Color(1f, 0f, 0f, 0.15f); // 移動範囲は半透明の赤
        Vector3 center = new Vector3(
            (leftBound + rightBound) / 2f,
            transform.position.y - 0.13f / 2f,
            transform.position.z
        );
        Vector3 size = new Vector3(rightBound - leftBound, 13.0f, 0.1f);
        Gizmos.DrawCube(center, size);
    }
}

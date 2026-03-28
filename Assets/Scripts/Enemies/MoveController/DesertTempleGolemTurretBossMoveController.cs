using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DesertTempleGolemTurretのボス版の動作を制御するクラス。
/// プレイヤーとの距離に応じて起動し、複数の攻撃パターン（突飛なレーザー、通常レーザー、連続着弾、壁撃ち）をランダムに実行します。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(UniqueBossHealth))]
[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class DesertTempleGolemTurretBossMoveController : MonoBehaviour, IEnemyResettable
{
    public const string MULTI_SHOOT_BULLET_POOLTAG = "DesertTempleGolemTurretBossMultiShoot";
    public const string WALL_SHOOT_BULLET_POOLTAG = "DesertTempleGolemTurretBossWallShoot";
    public const string LOCKON_MARK_POOLTAG = "LockOnMarkEffect";
    public const string IMPACT_EFFECT_POOLTAG = "DesertTempleGolemTurretBossImpactEffect";
    private const float FACE_ANGLE_THRESHOLD = 25f;

    #region インスペクター設定

    [Header("基本設定")]
    [SerializeField, Tooltip("プレイヤーがこの距離(X軸)に入ったら起動する")]
    private float activationRangeX = 15.0f;

    [SerializeField, Tooltip("右を向いているかどうか")]
    private bool isRightFacing = false;

    [Header("座標・範囲設定")]
    [SerializeField, Tooltip("天井のY座標（攻撃範囲の上限）")]
    private float ceilingY = 0.0f;

    [SerializeField, Tooltip("地面のY座標（攻撃範囲の下限）")]
    private float groundY = 0.0f;

    [SerializeField, Tooltip("左端のX座標（行動範囲の左限）")]
    private float leftBound = 0.0f;

    [SerializeField, Tooltip("右端のX座標（行動範囲の右限）")]
    private float rightBound = 0.0f;

    [Header("レーザー（パターンA＆B）設定")]
    [SerializeField, Tooltip("頭（砲身）のピボット（回転の中心）")]
    private Transform headPivot;

    [SerializeField, Tooltip("照準のピボット（ビームの回転の中心）")]
    private Transform aimPivot;

    [SerializeField, Tooltip("ビームの当たり判定を持つオブジェクト")]
    private GameObject beamObject;

    [SerializeField, Tooltip("予測線を描画するLineRenderer")]
    private LineRenderer predictionLine;

    [Header("パターンA（突飛なレーザー）設定")]
    [SerializeField, Tooltip("初回の突飛なレーザーの予備動作時間（秒）")]
    private float surpriseLaserChargeTime = 0.5f;

    [SerializeField, Tooltip("パターンAのビームが伸びる速度（単位/秒）")]
    private float surpriseLaserBeamExpandSpeed = 60.0f;

    [SerializeField, Tooltip("パターンAのビームを照射している時間（秒）")]
    private float surpriseLaserFiringDuration = 1.0f;

    [SerializeField, Tooltip("攻撃後の時間間隔の最小値（秒）")]
    private float surpriseLaserIntervalMin = 2.0f;

    [SerializeField, Tooltip("攻撃後の時間間隔の最大値（秒）")]
    private float surpriseLaserIntervalMax = 4.0f;

    [Header("パターンB（通常レーザー）設定")]
    [SerializeField, Tooltip("予測線を出してプレイヤーを追尾し続ける時間（秒）")]
    private float aimingDuration = 2.0f;

    [SerializeField, Tooltip("頭の回転がプレイヤーに追従する速度")]
    private float headRotationSpeed = 5.0f;

    [SerializeField, Tooltip("追尾を止め、発射直前の警告を行う時間（秒）")]
    private float lockOnDuration = 0.5f;

    [SerializeField, Tooltip("パターンBのビームが伸びる速度（単位/秒）")]
    private float normalLaserBeamExpandSpeed = 40.0f;

    [SerializeField, Tooltip("パターンBのビームを照射している時間（秒）")]
    private float normalLaserFiringDuration = 1.0f;

    [SerializeField, Tooltip("攻撃後の時間間隔の最小値（秒）")]
    private float normalLaserIntervalMin = 3.0f;

    [SerializeField, Tooltip("攻撃後の時間間隔の最大値（秒）")]
    private float normalLaserIntervalMax = 5.0f;

    [Header("予測線演出設定")]
    [SerializeField, Tooltip("追尾中（Aiming）の予測線の色")]
    private Color aimColor = new Color(1f, 1f, 0f, 0.4f); // 半透明の黄色

    [SerializeField, Tooltip("ロックオン中（発射直前）の予測線の色")]
    private Color lockOnColor = new Color(1f, 0f, 0f, 0.9f); // 濃い赤

    [SerializeField, Tooltip("予測線の点線マテリアルがスクロールする速度")]
    private float lineScrollSpeed = 3.0f;

    [SerializeField, Tooltip("予測線の最大射程距離")]
    private float maxLineLength = 25.0f;

    [SerializeField, Tooltip("ビームの太さ（LineRendererの幅）")]
    private float lineWidth = 0.1f;

    [SerializeField, Tooltip("追尾開始時の照準の最大ブレ角度（度）")]
    private float maxAimNoiseAngle = 15.0f;

    [SerializeField, Tooltip("照準がブレる速さ（ノイズの周波数）")]
    private float aimNoiseSpeed = 10.0f;

    [Header("連続着弾（パターンC）設定")]
    [SerializeField, Tooltip("連続着弾攻撃で狙うターゲットの数")]
    private int multiShootTargetCount = 5;

    [SerializeField, Tooltip("連続着弾攻撃の弾速")]
    private float multiShootBulletSpeed = 15.0f;

    [SerializeField, Tooltip("弾を発射する間隔（秒）")]
    private float multiShootInterval = 0.3f;

    [SerializeField, Tooltip("攻撃後の時間間隔の最小値（秒）")]
    private float multiShootAttackIntervalMin = 3.0f;

    [SerializeField, Tooltip("攻撃後の時間間隔の最大値（秒）")]
    private float multiShootAttackIntervalMax = 5.0f;

    [Header("背後壁撃ち（パターンD）設定")]
    [SerializeField, Tooltip("背後の空間リスト（このY座標を中心に空白を作る）")]
    private List<float> wallShootGapYList = new List<float>();

    [SerializeField, Tooltip("弾のスプライトの高さ（配置時の間隔として使用）")]
    private float bulletSpriteHeight = 1.0f;

    [SerializeField, Tooltip("背後の配置オフセット（ボス中心からのX軸方向の距離）")]
    private float backSpawnOffsetX = 3.0f;

    [SerializeField, Tooltip("壁撃ちの弾速")]
    private float wallShootBulletSpeed = 10.0f;

    [SerializeField, Tooltip("壁撃ちの連射間隔（上から撃つ際の時間間隔）")]
    private float wallShootFireInterval = 0.1f;

    [SerializeField, Tooltip("攻撃後の時間間隔の最小値（秒）")]
    private float wallShootAttackIntervalMin = 3.0f;

    [SerializeField, Tooltip("攻撃後の時間間隔の最大値（秒）")]
    private float wallShootAttackIntervalMax = 5.0f;

    [Header("顔の向き(スプライト)設定")]
    [SerializeField, Tooltip("顔部分のSpriteRenderer")]
    private SpriteRenderer faceSpriteRenderer;

    [SerializeField, Tooltip("通常時の顔スプライト")]
    private Sprite defaultFaceSprite;

    [SerializeField, Tooltip("上向き(LookUp)の顔スプライト")]
    private Sprite lookUpFaceSprite;

    [SerializeField, Tooltip("下向き(LookDown)の顔スプライト")]
    private Sprite lookDownFaceSprite;

    #endregion

    #region 内部変数

    // コンポーネントのキャッシュ
    private Rigidbody2D rbody;
    private Animator animator;
    private UniqueBossHealth bossHealth;
    private CriWare.Assets.CriAtomSePlayer sePlayer;
    private Transform playerTransform;

    // 状態管理フラグ
    private bool isMoveStarted = false; // ボスが起動したかどうか
    private bool hasTriggeredWakeUp = false; // 起動アニメーションを開始したかどうか
    private bool hasDoneSurpriseLaser = false; // 初回の突飛なレーザーを実行済みかどうか
    private Coroutine bossRoutine; // ボスの行動を管理するコルーチン

    // ビーム制御・予測線用の変数
    private LayerMask obstacleLayer; // 障害物のレイヤー（予測線・ビームの貫通防止用）
    private SpriteRenderer beamSpriteRenderer; // ビームのスプライト
    private BoxCollider2D beamCollider; // ビームの当たり判定
    private float defaultBeamHeight; // ビームスプライトの元の高さ（太さ）
    private float targetBeamLength; // 予測線で計算された「ビームの目標の長さ」

    // 顔の向き状態管理用
    private enum FaceType
    {
        Default,
        LookUp,
        LookDown,
    }

    private FaceType currentFaceType = FaceType.Default;
    #endregion

    #region Unityライフサイクル

    /// <summary>
    /// コンポーネントの初期化を行います。
    /// </summary>
    private void Awake()
    {
        // 障害物となるレイヤー（予測線やビームが貫通しないようにするため）
        obstacleLayer = LayerMask.GetMask(GameConstants.PHYSICS_LAYER_NAME_GROUND);

        // 必要なコンポーネントを取得
        rbody = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        bossHealth = GetComponent<UniqueBossHealth>();
        bossHealth.SetRigidbodyControl(false); // ボス戦開始前はRigidbodyを制御しない
        sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();

        // BeamObjectからコンポーネントを取得し、高さを保存する
        if (beamObject != null)
        {
            beamObject.SetActive(false);
            beamSpriteRenderer = beamObject.GetComponent<SpriteRenderer>();
            beamCollider = beamObject.GetComponent<BoxCollider2D>();

            if (beamSpriteRenderer != null)
            {
                defaultBeamHeight = beamSpriteRenderer.size.y;
            }
        }
        else
        {
            Debug.LogError($"{gameObject.name} にBeamObjectが設定されていません。");
        }

        // 予測線の初期化
        if (predictionLine != null)
        {
            predictionLine.gameObject.SetActive(false);
            predictionLine.startWidth = lineWidth;
            predictionLine.endWidth = lineWidth;
        }
        else
        {
            Debug.LogError($"{gameObject.name} にPredictionLineが設定されていません。");
        }
    }

    /// <summary>
    /// ゲーム開始時の処理を行います。
    /// </summary>
    private void Start()
    {
        ResetState();
    }

    /// <summary>
    /// 一定フレームレートで物理演算や状態の更新を行います。
    /// 起動前はプレイヤーとの距離を監視し、範囲内に入ったら起動します。
    /// </summary>
    private void FixedUpdate()
    {
        // 敵の動きがポーズされている場合は処理をスキップ
        if (TimeManager.instance.isEnemyMovePaused)
            return;

        // 起動前の距離チェック（まだ起動アニメーションを開始していない場合）
        if (!isMoveStarted && !hasTriggeredWakeUp && playerTransform != null)
        {
            float distanceX = Mathf.Abs(transform.position.x - playerTransform.position.x);
            if (distanceX <= activationRangeX)
            {
                TriggerWakeUp();
            }
        }
    }

    /// <summary>
    /// Animatorなどの内部処理が終わった後の、フレームの最後に呼び出されます。
    /// ここで強制的にスプライトを適用し続けることで、Animatorによる意図しない上書きを防ぎます。
    /// </summary>
    private void LateUpdate()
    {
        // ポーズ中は処理しない
        if (TimeManager.instance.isEnemyMovePaused)
            return;

        if (faceSpriteRenderer == null)
            return;

        // 現在の顔の状態に応じてスプライトを強制適用する
        switch (currentFaceType)
        {
            case FaceType.LookUp:
                if (lookUpFaceSprite != null)
                    faceSpriteRenderer.sprite = lookUpFaceSprite;
                break;
            case FaceType.LookDown:
                if (lookDownFaceSprite != null)
                    faceSpriteRenderer.sprite = lookDownFaceSprite;
                break;
            case FaceType.Default:
            default:
                if (defaultFaceSprite != null)
                    faceSpriteRenderer.sprite = defaultFaceSprite;
                break;
        }
    }

    #endregion

    #region 初期化・リセット処理

    /// <summary>
    /// ボスの状態を初期化（リセット）します。
    /// 再利用時やゲーム開始時に呼び出されます。
    /// </summary>
    public void ResetState()
    {
        // プレイヤーの参照を最優先で確保
        if (playerTransform == null)
        {
            if (PlayerManager.instance != null)
            {
                // PlayerManagerが持つキャッシュから高速に取得
                playerTransform = PlayerManager.instance.PlayerGameObject?.transform;
            }
            else
            {
                // テスト環境などでPlayerManagerが存在しない場合のフォールバック（従来方式）
                playerTransform = GameObject
                    .FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME)
                    ?.transform;
            }
        }

        // フラグを初期化
        isMoveStarted = false;
        hasTriggeredWakeUp = false;
        hasDoneSurpriseLaser = false;

        // 攻撃オブジェクトを非表示にリセット
        if (beamObject != null)
            beamObject.SetActive(false);
        if (predictionLine != null)
            predictionLine.gameObject.SetActive(false);

        // 実行中の行動コルーチンがあれば停止
        if (bossRoutine != null)
        {
            StopCoroutine(bossRoutine);
            bossRoutine = null;
        }

        // 顔のスプライトを通常時に戻す
        ResetFaceSprite();
    }

    #endregion

    #region メイン行動制御

    /// <summary>
    /// プレイヤーが範囲内に入った際の起動アニメーション開始処理を行います。
    /// </summary>
    private void TriggerWakeUp()
    {
        hasTriggeredWakeUp = true;
        animator.SetTrigger("WakeUpTrigger");
        Debug.Log("起動アニメーション開始");
    }

    /// <summary>
    /// 起動アニメーションの終了時（Animation Event）に呼び出されるメソッドです。
    /// アニメーション側から実行されることを想定しています。
    /// </summary>
    public void OnWakeUpAnimationComplete()
    {
        // 既に起動済みなら何もしない
        if (isMoveStarted)
            return;

        ActivateBoss();
    }

    /// <summary>
    /// 実際のボス戦開始処理を行います。（アニメーション終了後に呼ばれます）
    /// ダメージ判定を有効にし、行動パターンを開始します。
    /// </summary>
    private void ActivateBoss()
    {
        isMoveStarted = true;
        bossHealth.ActivateBattle(); // ボス戦開始（ダメージ受付開始、HPバー表示）

        // ボスの行動ルーチンを開始
        bossRoutine = StartCoroutine(BossBehaviorRoutine());
    }

    /// <summary>
    /// ボスのメイン行動ループを管理するコルーチンです。
    /// 初回は必ず突飛なレーザーを行い、その後は各パターンをランダムに繰り返します。
    /// </summary>
    private IEnumerator BossBehaviorRoutine()
    {
        // 起動時のみ：突飛なレーザー（パターンA）を実行
        if (!hasDoneSurpriseLaser)
        {
            yield return StartCoroutine(PatternA_SurpriseLaser());
            hasDoneSurpriseLaser = true;

            // 攻撃後の隙（最小値から最大値までのランダムな待機時間）
            float interval = Random.Range(surpriseLaserIntervalMin, surpriseLaserIntervalMax);
            yield return new WaitForSeconds(interval);
        }

        // 以降はパターンB, C, D をランダムでループ
        // ※将来的にHPで確率を変える場合はここで条件分岐を追加します
        while (true)
        {
            int nextPattern = Random.Range(0, 3);
            float minInterval = 0f;
            float maxInterval = 0f;

            switch (nextPattern)
            {
                case 0:
                    minInterval = normalLaserIntervalMin;
                    maxInterval = normalLaserIntervalMax;
                    yield return StartCoroutine(PatternB_NormalLaser());
                    break;
                case 1:
                    minInterval = multiShootAttackIntervalMin;
                    maxInterval = multiShootAttackIntervalMax;
                    yield return StartCoroutine(PatternC_MultiTargetShoot());
                    break;
                case 2:
                    minInterval = wallShootAttackIntervalMin;
                    maxInterval = wallShootAttackIntervalMax;
                    yield return StartCoroutine(PatternD_WallShoot());
                    break;
            }

            // 次の攻撃までのインターバル（最小値から最大値までのランダムな待機時間）
            float waitInterval = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(waitInterval);
        }
    }

    #endregion

    #region 攻撃パターン処理

    /// <summary>
    /// パターンA: 予測線なしの突飛なレーザー攻撃を行います。
    /// プレイヤーの現在位置を即座に狙い撃ちます。
    /// </summary>
    private IEnumerator PatternA_SurpriseLaser()
    {
        Debug.Log("パターンA: 予測線なしの突飛なレーザー開始");

        // 発射の瞬間のプレイヤー位置を記録（プレイヤーがいない場合は真下を狙う）
        Vector2 targetPos =
            playerTransform != null
                ? (Vector2)playerTransform.position
                : (Vector2)transform.position + Vector2.down;

        // 照準を即座にプレイヤーに向ける
        if (aimPivot != null)
        {
            Vector2 dir = targetPos - (Vector2)aimPivot.position;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            aimPivot.rotation = Quaternion.Euler(0, 0, angle);

            // 顔の向きを更新
            UpdateFaceSpriteByDirection(dir);
        }

        // 短いチャージ演出（アニメーションとSEの再生）
        //animator.SetTrigger("ChargeTrigger");
        // seplayer.Play(SE_EnemyAction.ChargePower1);
        yield return new WaitForSeconds(surpriseLaserChargeTime);

        // 障害物に向けてRaycastを飛ばし、ビームの目標長さを計算する
        DrawPredictionLine();

        // 即座に発射（予測線なし）
        //animator.SetTrigger("AttackTrigger");
        // seplayer.Play(SE_EnemyAction.LaserAttack1);
        if (beamObject != null)
            beamObject.SetActive(true);

        // スキル名をUIに表示
        GameUIManager.instance?.ShowSkillNameUI("レーザー");

        // ビームを目標の長さ(targetBeamLength)まで指定速度で伸ばす
        float currentLength = 0f;
        UpdateBeamSize(currentLength);
        while (currentLength < targetBeamLength)
        {
            currentLength += surpriseLaserBeamExpandSpeed * Time.deltaTime;
            // 目標長を超えないように制限
            if (currentLength > targetBeamLength)
                currentLength = targetBeamLength;

            UpdateBeamSize(currentLength);
            yield return null; // 1フレーム待機
        }

        // レーザー照射時間分待機
        yield return new WaitForSeconds(surpriseLaserFiringDuration);

        // 攻撃終了処理
        if (beamObject != null)
            beamObject.SetActive(false);
        //animator.SetTrigger("IdleTrigger");

        // 顔を元に戻す
        ResetFaceSprite();
    }

    /// <summary>
    /// パターンB: 通常の追従レーザー攻撃を行います。
    /// 予測線を表示してプレイヤーを一定時間追尾した後に発射します。
    /// </summary>
    private IEnumerator PatternB_NormalLaser()
    {
        Debug.Log("パターンB: 通常の追従レーザー開始");

        // 予測線を表示し、色を追尾中の色（黄色など）に設定
        if (predictionLine != null)
            predictionLine.gameObject.SetActive(true);
        SetLineColor(aimColor);

        // --- 1. 追尾・予測線照射フェーズ ---
        float timer = 0f;
        while (timer < aimingDuration)
        {
            // 追尾の進行度（0.0～1.0）を計算してメソッドに渡す
            float progress = timer / aimingDuration;
            TrackPlayerWithAimPivot(progress);

            // 顔の向きを現在の照準（aimPivot）の向きに合わせてリアルタイムに更新
            if (aimPivot != null)
                UpdateFaceSpriteByDirection(aimPivot.right);

            // 予測線の描画とスクロールアニメーション
            DrawPredictionLine();
            AnimatePredictionLine();

            timer += Time.deltaTime;
            yield return null;
        }

        // --- 2. ロックオン・発射警告フェーズ ---
        SetLineColor(lockOnColor);
        timer = 0f;
        while (timer < lockOnDuration)
        {
            // プレイヤーが壁裏に逃げることも考慮し、線の長さだけは継続して計算する
            DrawPredictionLine();

            // 警告演出：線の太さを脈打たせる (PingPong)
            float pulseWidth = lineWidth + Mathf.PingPong(Time.time * 5f, 0.15f);
            predictionLine.startWidth = pulseWidth;
            predictionLine.endWidth = pulseWidth;

            timer += Time.deltaTime;
            yield return null;
        }

        // --- 3. ビーム発射フェーズ ---
        if (predictionLine != null)
        {
            predictionLine.gameObject.SetActive(false);
            predictionLine.startWidth = lineWidth; // 太さを元に戻す
        }

        //animator.SetTrigger("AttackTrigger");
        // seplayer.Play(SE_EnemyAction.LaserAttack1);
        if (beamObject != null)
            beamObject.SetActive(true);

        // ビームを目標の長さ(targetBeamLength)まで指定速度で伸ばす
        float currentLength = 0f;
        UpdateBeamSize(currentLength);
        while (currentLength < targetBeamLength)
        {
            currentLength += normalLaserBeamExpandSpeed * Time.deltaTime;
            if (currentLength > targetBeamLength)
                currentLength = targetBeamLength;
            UpdateBeamSize(currentLength);
            yield return null;
        }

        // 照射時間
        yield return new WaitForSeconds(normalLaserFiringDuration);

        // 攻撃終了処理
        if (beamObject != null)
            beamObject.SetActive(false);
        // animator.SetTrigger("IdleTrigger");

        // 顔を元に戻す
        ResetFaceSprite();
    }

    /// <summary>
    /// パターンC: 連続着弾攻撃を行います。
    /// 指定範囲内にランダムな予告マークを出し、その順番に従って弾を発射します。
    /// </summary>
    private IEnumerator PatternC_MultiTargetShoot()
    {
        Debug.Log("パターンC: 連続着弾攻撃開始");

        List<Vector2> targetPositions = new List<Vector2>();
        List<GameObject> warningMarks = new List<GameObject>(); // 予告マークを保持するリスト

        // 1. ターゲット座標の決定と予告マークの表示フェーズ
        for (int i = 0; i < multiShootTargetCount; i++)
        {
            // 範囲内からランダムな座標を生成
            float randX = Random.Range(leftBound, rightBound);
            float randY = Random.Range(groundY, ceilingY);
            Vector2 targetPos = new Vector2(randX, randY);
            targetPositions.Add(targetPos);

            // 予告マークを生成し、リストに保持
            GameObject mark = ObjectPooler.SceneInstance.SpawnFromPool(
                LOCKON_MARK_POOLTAG,
                targetPos,
                Quaternion.identity
            );
            warningMarks.Add(mark);

            // 予告を出す間隔
            yield return new WaitForSeconds(0.2f);
        }

        // 撃ち始める前のタメ
        yield return new WaitForSeconds(0.5f);

        // 2. 予告した順番に弾を発射するフェーズ
        for (int i = 0; i < targetPositions.Count; i++)
        {
            Vector2 target = targetPositions[i];
            GameObject mark = warningMarks[i]; // 対応する予告マークを取得

            // 弾の生成位置（照準ピボットがあればそこから、なければ自身から）
            Vector2 spawnPos = aimPivot != null ? aimPivot.position : transform.position;

            // 顔の向きを現在のターゲット方向へ更新する
            Vector2 dirToTarget = target - spawnPos;
            UpdateFaceSpriteByDirection(dirToTarget);

            // パターンC用のプールタグを使用
            GameObject bullet = ObjectPooler.SceneInstance.SpawnFromPool(
                MULTI_SHOOT_BULLET_POOLTAG,
                spawnPos,
                Quaternion.identity
            );

            if (bullet != null)
            {
                // sePlayer.Play(SE_EnemyAction.Shoot_Magic);
                // 弾の追従と消去を管理するコルーチンを個別に起動（予告マークも渡す）
                StartCoroutine(TrackAndDestroyBullet(bullet, target, mark));
            }
            else
            {
                // 万が一弾が生成できなかった場合は、PoolableObjectを通じて予告マークをプールへ返却
                if (mark != null && mark.activeInHierarchy)
                {
                    mark.GetComponent<PoolableObject>()?.ReturnToPool();
                }
            }

            // 次の弾を発射するまでの間隔
            yield return new WaitForSeconds(multiShootInterval);
        }

        // 全て撃ち終わったら顔を元に戻す
        ResetFaceSprite();
    }

    /// <summary>
    /// パターンD: 背後からの壁撃ち攻撃を行います。
    /// 自身の背後に弾を縦一列に配置し、プレイヤーの高さ分の隙間を空けてから順に発射します。
    /// </summary>
    private IEnumerator PatternD_WallShoot()
    {
        Debug.Log("パターンD: 背後からの壁撃ち開始");

        // 1. 基準となるX座標（背後）を決定
        float spawnX = isRightFacing
            ? transform.position.x - backSpawnOffsetX
            : transform.position.x + backSpawnOffsetX;

        // 2. 空白（安全地帯）を作るY座標をリストからランダムに決定
        float gapCenterY = 0f;
        if (wallShootGapYList.Count > 0)
        {
            gapCenterY = wallShootGapYList[Random.Range(0, wallShootGapYList.Count)];
        }

        List<GameObject> readyBullets = new List<GameObject>();

        // 3. 地面から天井に向けて弾を等間隔に配置するフェーズ
        for (float y = groundY; y <= ceilingY; y += bulletSpriteHeight)
        {
            // 空白地帯（gapCenterY ± playerHeight/2）の範囲内なら弾の配置をスキップ
            if (
                y >= gapCenterY - (GameConstants.PLAYER_BASE_HEIGHT / 2f)
                && y <= gapCenterY + (GameConstants.PLAYER_BASE_HEIGHT / 2f)
            )
            {
                continue;
            }

            // 弾を生成
            Vector3 spawnPos = new Vector3(spawnX, y, 0f);
            GameObject bullet = ObjectPooler.SceneInstance.SpawnFromPool(
                WALL_SHOOT_BULLET_POOLTAG,
                spawnPos,
                Quaternion.identity
            );

            if (bullet != null)
            {
                // 弾の出現時にエフェクトを生成
                ObjectPooler.PersistentInstance.SpawnFromPool(
                    GameConstants.EFFECT_ENEMY_SPAWN_POOLTAG,
                    spawnPos,
                    Quaternion.identity
                );

                // 弾の出現SEを再生
                var bulletSePlayer = bullet.GetComponent<CriWare.Assets.CriAtomSePlayer>();
                if (bulletSePlayer != null)
                {
                    bulletSePlayer.Play(SE_EnemyAction.Spawn1);
                }

                // 発射までは静止させておく
                Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
                if (rb != null)
                    rb.velocity = Vector2.zero;

                readyBullets.Add(bullet);
            }
        }

        // 全ての弾が並んだら少しタメを作る（予備動作）
        // animator.SetTrigger("ArmUpTrigger");
        // seplayer.Play(SE_EnemyAction.MagicWave1);
        yield return new WaitForSeconds(1.0f);

        // 4. 上から順番に発射するフェーズ
        // リストをY座標の高い順にソート（上から順に発射するため）
        readyBullets.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));

        // 発射方向の決定
        Vector2 fireDirection = isRightFacing ? Vector2.right : Vector2.left;

        foreach (var bullet in readyBullets)
        {
            if (bullet != null && bullet.activeInHierarchy)
            {
                Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    // 速度を与えて発射
                    rb.velocity = fireDirection * wallShootBulletSpeed;

                    // 弾の向きを進行方向に合わせる
                    float angle = Mathf.Atan2(fireDirection.y, fireDirection.x) * Mathf.Rad2Deg;
                    bullet.transform.rotation = Quaternion.Euler(0, 0, angle);
                }
                // seplayer.Play(SE_EnemyAction.Shoot_Water1);
            }

            // 順番に発射するための時間間隔
            yield return new WaitForSeconds(wallShootFireInterval);
        }

        // 攻撃終了処理
        // animator.SetTrigger("IdleTrigger");
    }

    #endregion

    #region 弾制御用ローカルコルーチン

    /// <summary>
    /// パターンC用のローカルコルーチン。
    /// 弾を目標地点まで動かし、到達したら着弾エフェクトを出して、弾と予告マークを消去（プールへ返却）します。
    /// </summary>
    /// <param name="bullet">移動させる弾のGameObject</param>
    /// <param name="targetPos">着弾目標の座標</param>
    /// <param name="warningMark">着弾地点に表示されている予告マークのGameObject</param>
    private IEnumerator TrackAndDestroyBullet(
        GameObject bullet,
        Vector2 targetPos,
        GameObject warningMark
    )
    {
        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // 目標への方向を計算し、速度を設定
            Vector2 dir = (targetPos - (Vector2)bullet.transform.position).normalized;
            rb.velocity = dir * multiShootBulletSpeed;

            // 弾の向きを進行方向に合わせる
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        // 弾が目標地点に到達するか、一定時間経過するまで監視
        float timeout = 5.0f; // タイムアウト時間
        while (bullet.activeInHierarchy && timeout > 0)
        {
            float dist = Vector2.Distance(bullet.transform.position, targetPos);
            // 目標付近（誤差範囲内）に到達したら返却処理
            if (dist < 0.5f)
            {
                // 着弾エフェクトの生成
                ObjectPooler.SceneInstance.SpawnFromPool(
                    IMPACT_EFFECT_POOLTAG,
                    targetPos,
                    Quaternion.identity
                );

                // 弾と予告マークをPoolableObjectを通じてプールに戻す
                bullet.GetComponent<PoolableObject>()?.ReturnToPool();
                if (warningMark != null && warningMark.activeInHierarchy)
                {
                    warningMark.GetComponent<PoolableObject>()?.ReturnToPool();
                }

                yield break; // コルーチンを終了
            }
            timeout -= Time.deltaTime;
            yield return null; // 次のフレームまで待機
        }

        // タイムアウトした場合は安全のためPoolableObjectを通じてプールに戻す
        if (bullet.activeInHierarchy)
        {
            bullet.GetComponent<PoolableObject>()?.ReturnToPool();
        }
        if (warningMark != null && warningMark.activeInHierarchy)
        {
            warningMark.GetComponent<PoolableObject>()?.ReturnToPool();
        }
    }

    #endregion

    #region ビーム制御・予測線用ヘルパーメソッド

    /// <summary>
    /// 照準（AimPivot）をプレイヤーの方向へ滑らかに回転させ、ブレ（ノイズ）を加えます。
    /// </summary>
    /// <param name="aimProgress">追尾の進行度（0.0～1.0）。1.0に近づくほどブレが収束して正確になる</param>
    private void TrackPlayerWithAimPivot(float aimProgress)
    {
        if (playerTransform == null || aimPivot == null)
            return;

        // ターゲットへの方向ベクトルとワールド角度の計算
        Vector2 targetDir = (
            (Vector2)playerTransform.position - (Vector2)aimPivot.position
        ).normalized;
        float targetAngle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;

        // 照準のブレ（ノイズ）の計算と加算
        // 進行度が進むにつれて(1.0に近づくにつれて)減衰率(damping)が0になり、ブレが完全に収束する
        float damping = 1.0f - aimProgress;

        // 滑らかで不規則な揺れ(-1.0 ～ 1.0)を作る
        float noise = Mathf.PerlinNoise(Time.time * aimNoiseSpeed, transform.position.x) * 2f - 1f;

        // ベースの角度に、減衰させたブレ角度を加算
        targetAngle += noise * maxAimNoiseAngle * damping;

        Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);

        // 照準のワールド回転を滑らかに補間
        aimPivot.rotation = Quaternion.Lerp(
            aimPivot.rotation,
            targetRotation,
            Time.deltaTime * headRotationSpeed
        );
    }

    /// <summary>
    /// レイキャストを飛ばし、障害物に当たったらそこで予測線（とビームの目標長）を止めます。
    /// </summary>
    private void DrawPredictionLine()
    {
        if (aimPivot == null || predictionLine == null)
            return;

        Vector2 direction = aimPivot.right;
        Vector2 origin = aimPivot.position;

        // 地面や壁に向かってレイを飛ばす
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, maxLineLength, obstacleLayer);

        Vector3 endPosition;
        if (hit.collider != null)
        {
            endPosition = hit.point; // 壁に当たった場所まで
            targetBeamLength = hit.distance; // 当たった距離をビームの目標長として保持
        }
        else
        {
            endPosition = origin + direction * maxLineLength; // 当たらなければ最大射程まで
            targetBeamLength = maxLineLength; //最大射程をビームの目標長として保持
        }

        predictionLine.SetPosition(0, origin);
        predictionLine.SetPosition(1, endPosition);
    }

    /// <summary>
    /// LineRendererのマテリアルのオフセットを動かし、エネルギーが流れるように見せます。
    /// </summary>
    private void AnimatePredictionLine()
    {
        if (predictionLine.material != null)
        {
            predictionLine.material.mainTextureOffset -= new Vector2(
                lineScrollSpeed * Time.deltaTime,
                0
            );
        }
    }

    /// <summary>
    /// 予測線の色を設定します。
    /// </summary>
    private void SetLineColor(Color color)
    {
        if (predictionLine == null)
            return;
        predictionLine.startColor = color;
        predictionLine.endColor = color;
    }

    /// <summary>
    /// ビームの長さ（スプライトとコライダー）を更新します。
    /// ピボットが左端にあることを前提としています。
    /// </summary>
    private void UpdateBeamSize(float length)
    {
        if (beamSpriteRenderer != null)
        {
            // Tiled設定のスプライトサイズを変更
            beamSpriteRenderer.size = new Vector2(length, defaultBeamHeight);
        }

        if (beamCollider != null)
        {
            // コライダーのサイズを変更
            beamCollider.size = new Vector2(length, beamCollider.size.y);

            // コライダーのオフセット位置を調整（左端を基準にするため半分だけ右にずらす）
            beamCollider.offset = new Vector2(length / 2f, beamCollider.offset.y);
        }
    }

    #endregion

    #region 顔の向き変更用ヘルパーメソッド

    /// <summary>
    /// 指定された方向ベクトルに基づいて、現在の顔の状態を更新します。
    /// 実際の画像の切り替えは LateUpdate で行われます。
    /// </summary>
    /// <param name="direction">狙っている方向のベクトル</param>
    private void UpdateFaceSpriteByDirection(Vector2 direction)
    {
        if (faceSpriteRenderer == null)
            return;

        // 左右の向きを無視して、上下の傾き角度（水平0度として、上がプラス・下がマイナス）を計算
        float angle = Mathf.Atan2(direction.y, Mathf.Abs(direction.x)) * Mathf.Rad2Deg;

        if (angle > FACE_ANGLE_THRESHOLD && lookUpFaceSprite != null)
        {
            // 上に閾値以上傾いている場合
            currentFaceType = FaceType.LookUp;
        }
        else if (angle < -FACE_ANGLE_THRESHOLD && lookDownFaceSprite != null)
        {
            // 下に閾値以上傾いている場合
            currentFaceType = FaceType.LookDown;
        }
        else
        {
            // それ以外（通常範囲）の場合
            currentFaceType = FaceType.Default;
        }
    }

    /// <summary>
    /// 顔の状態を通常時（デフォルト）に戻します。
    /// </summary>
    private void ResetFaceSprite()
    {
        currentFaceType = FaceType.Default;
    }

    #endregion

    #region デバッグ表示 (Gizmos)

    /// <summary>
    /// エディタのSceneビューに、常に表示されるデバッグ用の図形を描画します。
    /// </summary>
    private void OnDrawGizmos()
    {
        // --- 行動範囲の描画（半透明の赤） ---
        Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
        // 左右の端と天井・地面の中心座標を計算
        Vector3 center = new Vector3(
            (leftBound + rightBound) / 2f,
            (ceilingY + groundY) / 2f,
            transform.position.z
        );
        // 設定された範囲のサイズを計算（Z軸は視認用の薄さ）
        Vector3 size = new Vector3(rightBound - leftBound, ceilingY - groundY, 0.1f);
        Gizmos.DrawCube(center, size);

        // --- 天井と地面の線の描画 ---
        // 天井 (ceilingY) を緑色の線で描画
        Gizmos.color = Color.green;
        Gizmos.DrawLine(
            new Vector3(leftBound, ceilingY, transform.position.z),
            new Vector3(rightBound, ceilingY, transform.position.z)
        );

        // 地面 (groundY) を黄色の線で描画
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            new Vector3(leftBound, groundY, transform.position.z),
            new Vector3(rightBound, groundY, transform.position.z)
        );

        // --- 起動範囲 (activationRangeX) の描画（半透明の青） ---
        Gizmos.color = new Color(0f, 0f, 1f, 0.2f);
        // 自身を中心に、左右に activationRangeX 分の幅を持つ判定エリアを描画
        Vector3 activationCenter = new Vector3(
            transform.position.x,
            center.y,
            transform.position.z
        );
        Vector3 activationSize = new Vector3(activationRangeX * 2f, ceilingY - groundY, 0.1f);
        Gizmos.DrawCube(activationCenter, activationSize);
    }

    /// <summary>
    /// オブジェクトが選択されている時のみ表示されるデバッグ用の図形を描画します。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // --- 背後からの壁撃ち（パターンD）の生成ライン描画 ---
        // 自身が右を向いている時の背後（左側）の生成ライン（シアン）
        Gizmos.color = Color.cyan;
        float spawnXRightFacing = transform.position.x - backSpawnOffsetX;
        Gizmos.DrawLine(
            new Vector3(spawnXRightFacing, groundY, transform.position.z),
            new Vector3(spawnXRightFacing, ceilingY, transform.position.z)
        );

        // 自身が左を向いている時の背後（右側）の生成ライン（マゼンタ）
        Gizmos.color = Color.magenta;
        float spawnXLeftFacing = transform.position.x + backSpawnOffsetX;
        Gizmos.DrawLine(
            new Vector3(spawnXLeftFacing, groundY, transform.position.z),
            new Vector3(spawnXLeftFacing, ceilingY, transform.position.z)
        );
    }

    #endregion
}

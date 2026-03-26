using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class DesertTempleGolemTurretMoveController : MonoBehaviour, IEnemyResettable
{
    #region 定数
    private const float IDLE_LOOK_INTERVAL = 3.0f; // 索敵時に左右を見渡す間隔（秒）
    #endregion

    #region インスペクター設定

    [Header("基本設定")]
    [SerializeField]
    private EnemyActivator activator = null;

    [Header("砲台パーツ設定")]
    [Tooltip("回転させる頭（砲身）のピボット（中心点）となるオブジェクト")]
    [SerializeField]
    private Transform headPivot;

    [Tooltip("ビームと予測線を実際に回転させるための中心点となる空オブジェクト")]
    [SerializeField]
    private Transform aimPivot;

    [Tooltip("実際のビーム攻撃判定を持つ子オブジェクト")]
    [SerializeField]
    private GameObject beamObject;

    [Tooltip("予測線を描画するためのLineRenderer")]
    [SerializeField]
    private LineRenderer predictionLine;

    [Header("索敵・待機設定")]
    [Tooltip("この敵がプレイヤーを検知する範囲のX距離")]
    [SerializeField]
    private float attackRangeX = 15.0f;

    [Tooltip("この敵がプレイヤーを検知する範囲のY距離")]
    [SerializeField]
    private float attackRangeY = 8.0f;

    [Tooltip("プレイヤーが射程内に留まる必要がある時間（秒）")]
    [SerializeField]
    private float detectionTimeRequired = 1.0f;

    [Tooltip("頭の回転がプレイヤーに追従する速度")]
    [SerializeField]
    private float headRotationSpeed = 5.0f;

    [Header("攻撃フェーズの時間設定")]
    [Tooltip("予測線を出してプレイヤーを追尾し続ける時間（秒）")]
    [SerializeField]
    private float aimingDuration = 2.0f;

    [Tooltip("追尾を止め、発射直前の警告を行う時間（秒）")]
    [SerializeField]
    private float lockOnDuration = 0.5f;

    [Tooltip("ビームが伸びる速度（単位/秒）")]
    [SerializeField]
    private float beamExpandSpeed = 40.0f;

    [Tooltip("実際にビームを照射している時間（秒）")]
    [SerializeField]
    private float firingDuration = 1.0f;

    [Tooltip("ビーム終了後、次の索敵に戻るまでの隙（秒）")]
    [SerializeField]
    private float cooldownDuration = 2.0f;

    [Header("予測線演出設定")]
    [Tooltip("追尾中（Aiming）の予測線の色")]
    [SerializeField]
    private Color aimColor = new Color(1f, 1f, 0f, 0.4f); // 半透明の黄色

    [Tooltip("ロックオン中（発射直前）の予測線の色")]
    [SerializeField]
    private Color lockOnColor = new Color(1f, 0f, 0f, 0.9f); // 濃い赤

    [Tooltip("予測線の点線マテリアルがスクロールする速度")]
    [SerializeField]
    private float lineScrollSpeed = 3.0f;

    [Tooltip("予測線の最大射程距離")]
    [SerializeField]
    private float maxLineLength = 25.0f;

    [Tooltip("ビームの太さ（LineRendererの幅）")]
    [SerializeField]
    private float lineWidth = 0.1f;

    #endregion

    #region プライベート変数

    private Rigidbody2D rbody;
    private Animator animator;
    private EnemyHealth enemyHP;
    private CriWare.Assets.CriAtomSePlayer sePlayer;
    private LayerMask obstacleLayer;
    private Transform playerTransform = null;
    private float idleTimer = 0f;
    private float playerDetectionTimer = 0f; // プレイヤー検知用のタイマー
    private bool rightFlag = false; // 現在右を向いているか
    private Quaternion defaultAimRotation; // 初期状態の照準の角度

    // ビーム制御用の変数
    private SpriteRenderer beamSpriteRenderer;
    private BoxCollider2D beamCollider;
    private float defaultBeamHeight; // ビームスプライトの元の高さ（太さ）
    private float targetBeamLength; // 予測線で計算された「ビームの目標の長さ」
    #endregion

    #region 状態管理

    private enum TurretState
    {
        Idle, // 索敵中（キョロキョロ）
        Aiming, // 予測線を出して追尾中
        LockOnDelay, // 追尾停止、発射警告
        Firing, // ビーム発射中
        Cooldown // 発射後の硬直
        ,
    }

    private TurretState currentState = TurretState.Idle;

    #endregion

    #region Unityライフサイクル

    private void Awake()
    {
        // 障害物となるレイヤー（予測線が貫通しないようにするため）
        obstacleLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );

        if (activator == null)
            activator = GetComponentInParent<EnemyActivator>();

        rbody = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
        enemyHP = GetComponent<EnemyHealth>();

        if (aimPivot != null)
            defaultAimRotation = aimPivot.localRotation;
        else
            Debug.LogError(
                $"{gameObject.name} にAimPivotが設定されていません。予測線やビームが回転しません。"
            );

        if (predictionLine != null)
        {
            predictionLine.gameObject.SetActive(false);
            predictionLine.startWidth = lineWidth;
            predictionLine.endWidth = lineWidth;
        }
        else
            Debug.LogError(
                $"{gameObject.name} にPredictionLine(LineRenderer)が設定されていません。"
            );

        // BeamObjectからコンポーネントを取得し、高さを保存する ---
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
            Debug.LogError($"{gameObject.name} にBeamObjectが設定されていません。");
    }

    private void Start()
    {
        ResetState(); // Debug用に開始時に状態をリセットする（必要に応じて削除してもOK）
    }

    private void FixedUpdate()
    {
        // ポーズ中の処理
        if (TimeManager.instance.isEnemyMovePaused)
        {
            if (rbody.simulated)
                rbody.simulated = false;
            return;
        }
        else if (!rbody.simulated)
        {
            rbody.simulated = true;
        }

        // 状態に応じた毎フレームの更新処理
        switch (currentState)
        {
            case TurretState.Idle:
                UpdateIdleBehavior();
                break;
            case TurretState.Cooldown:
                // クールダウン中は照準（AimPivot）をゆっくり正面に戻す ---
                if (aimPivot != null)
                {
                    aimPivot.localRotation = Quaternion.Lerp(
                        aimPivot.localRotation,
                        defaultAimRotation,
                        Time.deltaTime * 2f
                    );
                }
                break;
        }
    }

    #endregion

    #region 初期化・リセット処理

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

        if (enemyHP != null)
            enemyHP.ResetState();

        if (rbody != null)
        {
            rbody.simulated = true;
            rbody.velocity = Vector2.zero;
            rbody.constraints = RigidbodyConstraints2D.FreezeAll; // 固定砲台なので全軸固定
        }

        tag = GameConstants.IMMUNE_ENEMY_TAG_NAME;

        // 初期向きの設定
        rightFlag = (Random.value > 0.5f);
        ApplyFacingDirection();

        // 攻撃パーツの初期化
        if (predictionLine != null)
            predictionLine.gameObject.SetActive(false);
        if (beamObject != null)
            beamObject.SetActive(false);
        if (aimPivot != null)
            aimPivot.localRotation = defaultAimRotation;

        StopAllCoroutines();
        currentState = TurretState.Idle;
        idleTimer = 0f;
        playerDetectionTimer = 0f;
    }
    #endregion

    #region 索敵・更新処理

    /// <summary>
    /// 待機中の索敵（一定間隔での振り向き）と、プレイヤー検知を行います。
    /// </summary>
    private void UpdateIdleBehavior()
    {
        // 1. 一定間隔で左右をキョロキョロ見渡す
        idleTimer += Time.deltaTime;
        if (idleTimer >= IDLE_LOOK_INTERVAL)
        {
            idleTimer = 0f;
            ReverseDirection();
        }

        // 2. プレイヤーが範囲内に一定時間いるかチェック
        if (IsPlayerInAttackRange())
        {
            playerDetectionTimer += Time.deltaTime;
            if (playerDetectionTimer >= detectionTimeRequired)
            {
                playerDetectionTimer = 0f; // 発射決定時にリセット
                StartCoroutine(AttackSequenceCoroutine());
            }
        }
        else
        {
            // 範囲外に出たらタイマーをリセット
            playerDetectionTimer = 0f;
        }
    }

    private bool IsPlayerInAttackRange()
    {
        if (playerTransform == null)
            return false;

        Vector2 dir = (Vector2)playerTransform.position - (Vector2)transform.position;

        // 向いている方向（右なら1、左なら-1）を掛けることで、前方への距離を計算する
        float forwardDistance = dir.x * (rightFlag ? 1 : -1);

        // 1. プレイヤーが前方にいるか (forwardDistance >= 0)
        // 2. 前方の射程内か (forwardDistance <= attackRangeX)
        // 3. 上下の射程内か (Mathf.Abs(dir.y) <= attackRangeY)
        return forwardDistance >= 0
            && forwardDistance <= attackRangeX
            && Mathf.Abs(dir.y) <= attackRangeY;
    }

    private void ReverseDirection()
    {
        rightFlag = !rightFlag;
        ApplyFacingDirection();
    }

    private void ApplyFacingDirection()
    {
        if (headPivot != null)
        {
            Vector3 currentScale = headPivot.localScale;
            currentScale.x = Mathf.Abs(currentScale.x) * (rightFlag ? 1 : -1);
            headPivot.localScale = currentScale;
        }

        if (aimPivot != null)
        {
            // 砲口のX座標のみを反転させ、頭部の左右（目の位置など）へ移動させる
            Vector3 currentPos = aimPivot.localPosition;
            currentPos.x = Mathf.Abs(currentPos.x) * (rightFlag ? 1 : -1);
            aimPivot.localPosition = currentPos;

            // 重要: aimPivot自体のスケールは常に (1, 1, 1) に保つ
            // これにより、ワールド回転との干渉を防ぎ、左右どちらにいても計算が狂わなくなります
            aimPivot.localScale = Vector3.one;
        }
    }

    #endregion

    #region 攻撃・ビーム制御

    /// <summary>
    /// 追尾 ➔ 警告 ➔ 発射 ➔ クールダウン の一連の攻撃シーケンス
    /// </summary>
    private IEnumerator AttackSequenceCoroutine()
    {
        // --- 1. 追尾・予測線照射 (Aiming) ---
        currentState = TurretState.Aiming;
        predictionLine.gameObject.SetActive(true);
        SetLineColor(aimColor);

        float timer = 0f;
        while (timer < aimingDuration)
        {
            TrackPlayerWithAimPivot();
            DrawPredictionLine();
            AnimatePredictionLine();

            timer += Time.deltaTime;
            yield return null;
        }

        // --- 2. ロックオン・発射警告 (LockOnDelay) ---
        currentState = TurretState.LockOnDelay;
        // 色を赤にし、追尾（角度の更新）を停止する
        SetLineColor(lockOnColor);

        timer = 0f;
        while (timer < lockOnDuration)
        {
            // プレイヤーが壁裏に逃げることも考慮し、線の長さと貫通チェックだけは続ける
            DrawPredictionLine();

            // 警告演出：線の太さを脈打たせる (PingPong)
            float pulseWidth = lineWidth + Mathf.PingPong(Time.time * 5f, 0.15f);
            predictionLine.startWidth = pulseWidth;
            predictionLine.endWidth = pulseWidth;

            timer += Time.deltaTime;
            yield return null;
        }

        // --- 3. ビーム発射 (Firing) ---
        currentState = TurretState.Firing;
        predictionLine.gameObject.SetActive(false);
        predictionLine.startWidth = lineWidth; // 太さを元に戻す

        // ビーム判定をオン
        beamObject.SetActive(true);

        // TODO: SEの設定
        // sePlayer.Play(SE_EnemyAction.Shoot_Water1);

        // ビームを目標の長さ(targetBeamLength)まで指定速度で伸ばす
        float currentLength = 0f;
        UpdateBeamSize(currentLength);

        while (currentLength < targetBeamLength)
        {
            currentLength += beamExpandSpeed * Time.deltaTime;
            // 目標長を超えないように制限
            if (currentLength > targetBeamLength)
                currentLength = targetBeamLength;

            UpdateBeamSize(currentLength);
            yield return null; // 1フレーム待機
        }

        yield return new WaitForSeconds(firingDuration);

        // --- 4. クールダウン (Cooldown) ---
        currentState = TurretState.Cooldown;
        beamObject.SetActive(false);

        yield return new WaitForSeconds(cooldownDuration);

        // 索敵に戻る
        currentState = TurretState.Idle;
        idleTimer = 0f;
    }

    /// <summary>
    /// AimPivotをプレイヤーの方向へ滑らかに回転させる（Head自体は回転させない）
    /// </summary>
    private void TrackPlayerWithAimPivot()
    {
        if (playerTransform == null || aimPivot == null)
            return;

        // プレイヤーの方向を向くように本体を左右反転させる（基準位置は headPivot または本体）
        Vector2 referencePos = headPivot != null ? headPivot.position : transform.position;
        Vector2 dirToPlayer = (Vector2)playerTransform.position - referencePos;

        if (dirToPlayer.x < 0 && rightFlag)
            ReverseDirection();
        else if (dirToPlayer.x > 0 && !rightFlag)
            ReverseDirection();

        // ターゲットへの方向ベクトルとワールド角度の計算
        Vector2 targetDir = (
            (Vector2)playerTransform.position - (Vector2)aimPivot.position
        ).normalized;
        float targetAngle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;

        Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);

        // aimPivotのワールド回転(rotation)を滑らかに補間
        // 親の反転(localScale.x = -1)の影響があっても、ワールド回転で上書きされるため正確に追尾します
        aimPivot.rotation = Quaternion.Lerp(
            aimPivot.rotation,
            targetRotation,
            Time.deltaTime * headRotationSpeed
        );
    }

    /// <summary>
    /// ビームの長さ（スプライトとコライダー）を更新する
    /// ピボットが「左（Left）」にあることを前提とした計算です。
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

            // コライダーのオフセット位置を調整
            // (長さが変わると中心位置が変わるため、左端を基準にするなら 半分だけ右にずらす)
            beamCollider.offset = new Vector2(length / 2f, beamCollider.offset.y);
        }
    }

    #endregion

    #region 予測線演出

    /// <summary>
    /// レイキャストを飛ばし、障害物に当たったらそこで線を止める
    /// </summary>
    private void DrawPredictionLine()
    {
        if (headPivot == null || predictionLine == null)
            return;

        // 角度補正付きの回転により、aimPivot.right が常に正しいターゲット方向を向くようになります
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
    /// LineRendererのマテリアルのオフセットを動かし、エネルギーが流れるように見せる
    /// </summary>
    private void AnimatePredictionLine()
    {
        if (predictionLine.material != null)
        {
            // X方向のオフセットを減算し続けることで、根本から先端へ流れるアニメーションになる
            predictionLine.material.mainTextureOffset -= new Vector2(
                lineScrollSpeed * Time.deltaTime,
                0
            );
        }
    }

    private void SetLineColor(Color color)
    {
        if (predictionLine == null)
            return;
        predictionLine.startColor = color;
        predictionLine.endColor = color;
    }

    #endregion

    #region デバッグ表示

    private void OnDrawGizmos()
    {
        // 攻撃感知範囲を示すGizmosを描画 (青い半透明の箱)
        Gizmos.color = new Color(0f, 0f, 1f, 0.2f);
        Gizmos.DrawCube(transform.position, new Vector3(attackRangeX * 2, attackRangeY * 2, 0.1f));
    }

    #endregion
}

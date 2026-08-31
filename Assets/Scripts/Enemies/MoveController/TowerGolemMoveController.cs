using System.Collections;
using NaughtyAttributes;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class TowerGolemMoveController : MonoBehaviour, IEnemyResettable
{
    #region 定数・列挙型

    private const string ATTACK_ANIMATION_CLIP_NAME = "TowerGolem_Attack"; // アニメーションクリップ名の定数
    private const string DUSH_DUST_POOLTAG = "DushDustEffect"; // ダストエフェクトのプールタグ

    /// <summary>
    /// 敵の種類を定義する列挙型
    /// </summary>
    private enum EnemyVariant
    {
        None = 0,
        Tower = 1,
    }

    /// <summary>
    /// ゴーレムの行動状態を定義する列挙型
    /// </summary>
    private enum GolemState
    {
        None,
        Walk,
        Tackle,
        Attack,
        Idle,
        AdjustingPosition,
    }

    #endregion

    #region インスペクター設定

    [Header("敵のタイプ")]
    [SerializeField]
    private EnemyVariant variantType = EnemyVariant.None;

    [Header("基本設定")]
    [SerializeField]
    private EnemyActivator _activator = null;

    [Header("移動の設定 (通常時)")]
    [SerializeField, Tooltip("通常時の横移動速度")]
    private float walkSpeedX = 2.0f;

    [SerializeField, Tooltip("ランダムに設定する場合の基準となる移動幅")]
    private float moveRange = 10.0f;

    [Header("移動範囲の設定")]
    [SerializeField, Tooltip("手動で移動範囲を設定するかどうか")]
    private bool isUseManualBounds = false;

    [
        SerializeField,
        ShowIf(nameof(isUseManualBounds)),
        Tooltip("leftBoundとrightBoundを現在位置からの相対座標として扱うかどうか")
    ]
    private bool isUseRelativeBounds = false;

    [SerializeField, ShowIf(nameof(isUseManualBounds))]
    private float leftBound;

    [SerializeField, ShowIf(nameof(isUseManualBounds))]
    private float rightBound;

    [SerializeField, Tooltip("行動範囲外にタックルできる余白分の距離")]
    private float tackleMargin = 2.0f;

    [
        SerializeField,
        Tooltip("初期配置をランダムにせず、シーンに配置した座標をそのまま使用するかどうか")
    ]
    private bool keepInitialPosition = false;

    [Header("攻撃（タックル）の設定")]
    [SerializeField, Tooltip("プレイヤーを検知するY軸の距離")]
    private float attackRangeY = 2.5f;

    [SerializeField, Tooltip("タックルを開始する最小X距離")]
    private float tackleDetectMinX = 3.0f;

    [SerializeField, Tooltip("タックルを開始する最大X距離")]
    private float tackleDetectMaxX = 8.0f;

    [SerializeField, Tooltip("タックル時の高速移動速度")]
    private float tackleSpeedX = 6.0f;

    [SerializeField, Tooltip("タックル中にダストエフェクトを生成する間隔（秒）")]
    private float dashDustSpawnInterval = 0.1f;

    [SerializeField, Tooltip("タックル中に生成するダストのX座標のオフセット（後方への距離）")]
    private float dashDustOffsetX = 0.5f;

    [SerializeField, Tooltip("タックル予兆時の発光色")]
    private Color tackleFlashColor = Color.red;

    [SerializeField, Tooltip("タックル予兆の発光時間（秒）")]
    private float tackleFlashDuration = 0.15f;

    [SerializeField, Tooltip("攻撃アクションを実行するプレイヤーとのX距離")]
    private float attackDistanceX = 1.5f;

    [SerializeField, Tooltip("攻撃後、Idle状態で待機する時間（秒）")]
    private float idleTime = 1.5f;

    [Header("地面・壁判定用の設定")]
    [SerializeField, Tooltip("地面に埋まっていないかチェックする中心点")]
    private Transform overlapCheckPoint;

    [SerializeField]
    private float overlapCheckRadius = 0.5f;

    [SerializeField, Tooltip("崖（地面がない場所）を検知するための前方のオフセット距離")]
    private float cliffCheckOffsetX = 0.8f;

    [SerializeField, Tooltip("地面に向かって飛ばすレイの長さ")]
    private float cliffCheckRayLength = 1.0f;

    #endregion

    #region 内部変数

    // キャッシュ用コンポーネント
    private Animator _animator;
    private Rigidbody2D _rbody;
    private EnemyHealth _enemyHP;
    private CriWare.Assets.CriAtomSePlayer _sePlayer;
    private ContactDamageController _contactDamageController;
    private Transform _playerTransform;

    // システム・状態管理変数
    private LayerMask _groundLayer;
    private int damage = 30;
    private GolemState currentState = GolemState.None;

    // アニメーターパラメータ名
    private string walkParam = "Walk";
    private string tackleParam = "Tackle";
    private string attackTriggerParam = "AttackTrigger";

    // 移動・判定用パラメータ
    private float attackAnimationTime = 0.5f;
    private float verticalAdjustSpeed = 5.0f;
    private float currentVx = 0f;
    private bool rightFlag = false;
    private float dashDustTimer = 0f;
    private float _resolvedLeftBound;
    private float _resolvedRightBound;

    #endregion

    #region プロパティ

    /// <summary>
    /// 地面に埋まっているかどうかを判定するプロパティ
    /// </summary>
    private bool IsOverlappingGround =>
        Physics2D.OverlapCircle(
            overlapCheckPoint != null ? overlapCheckPoint.position : transform.position,
            overlapCheckRadius,
            _groundLayer
        );

    #endregion

    #region Unityイベント

    private void Awake()
    {
        _groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );

        // EnemyVariantによるダメージの設定
        switch (variantType)
        {
            case EnemyVariant.Tower:
                damage = 114;
                break;
            default:
                Debug.LogError($"{this.name}のEnemyVariantが設定されていません。", this);
                break;
        }

        if (_activator == null)
        {
            _activator = GetComponentInParent<EnemyActivator>();
        }

        // コンポーネントのキャッシュ取得
        _rbody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
        _enemyHP = GetComponent<EnemyHealth>();
        _contactDamageController = GetComponent<ContactDamageController>();

        CalculateAttackAnimationTime();
    }

    private void Start()
    {
        ResetState();
    }

    private void FixedUpdate()
    {
        if (TimeManager.instance.isEnemyMovePaused)
        {
            PauseMovement();
            return;
        }
        else
        {
            ResumeMovement();
        }

        if (currentState == GolemState.Walk || currentState == GolemState.Tackle)
        {
            UpdateMovementAndLogic();
        }
        else if (currentState != GolemState.AdjustingPosition)
        {
            // 移動状態以外は慣性を殺して停止させる（落下は許可）
            _rbody.velocity = new Vector2(0f, _rbody.velocity.y);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentState != GolemState.Walk && currentState != GolemState.Tackle)
        {
            return;
        }

        if (((1 << collision.gameObject.layer) & _groundLayer) != 0)
        {
            foreach (ContactPoint2D contact in collision.contacts)
            {
                // 横からの衝突（壁）を検知
                if (Mathf.Abs(contact.normal.y) < 0.1f && IsMovingTowardContact(contact))
                {
                    HandleObstacleEncountered();
                    return;
                }
            }
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (currentState != GolemState.Walk && currentState != GolemState.Tackle)
        {
            return;
        }

        // 既に壁に接触している状態でタックル状態になり、身動きが取れなくなった場合も確実に検知
        if (((1 << collision.gameObject.layer) & _groundLayer) != 0)
        {
            foreach (ContactPoint2D contact in collision.contacts)
            {
                // 横からの接触（壁）を検知
                if (Mathf.Abs(contact.normal.y) < 0.1f && IsMovingTowardContact(contact))
                {
                    HandleObstacleEncountered();
                    return;
                }
            }
        }
    }

    #endregion

    #region 初期化処理

    /// <summary>
    /// 必要な初期化・リセット処理を行います。
    /// </summary>
    public void ResetState()
    {
        if (_playerTransform == null)
        {
            if (PlayerManager.instance != null)
            {
                // PlayerManagerが持つキャッシュから高速に取得
                _playerTransform = PlayerManager.instance.PlayerGameObject?.transform;
            }
            else
            {
                // テスト環境などでPlayerManagerが存在しない場合のフォールバック（従来方式）
                _playerTransform = GameObject
                    .FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME)
                    ?.transform;
            }
        }

        if (_enemyHP != null)
        {
            _enemyHP.ResetState();
        }

        _contactDamageController?.SetNormalDamage(damage);

        if (_rbody != null)
        {
            _rbody.simulated = true;
            _rbody.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        // 本体のタグを免疫状態に設定
        gameObject.tag = GameConstants.IMMUNE_ENEMY_TAG_NAME;

        StopAllCoroutines();
        SetupMovementBounds();

        // 初期位置の設定
        Vector3 startPos = transform.position;
        if (!keepInitialPosition)
        {
            float randomX = Random.Range(_resolvedLeftBound, _resolvedRightBound);
            transform.position = new Vector2(randomX, startPos.y);
        }

        // 初期向きの設定
        rightFlag = Random.value > 0.5f;
        ApplyFacingDirection();

        StartCoroutine(CheckAndAdjustPosition());
    }

    /// <summary>
    /// Activatorを元に自動で移動範囲（Bounds）を計算・設定します。
    /// </summary>
    private void SetupMovementBounds()
    {
        if (isUseManualBounds)
        {
            float originX = isUseRelativeBounds ? transform.position.x : 0f;
            float firstBound = originX + leftBound;
            float secondBound = originX + rightBound;
            _resolvedLeftBound = Mathf.Min(firstBound, secondBound);
            _resolvedRightBound = Mathf.Max(firstBound, secondBound);
            return;
        }

        _resolvedLeftBound = Mathf.Min(leftBound, rightBound);
        _resolvedRightBound = Mathf.Max(leftBound, rightBound);

        if (_activator != null)
        {
            var activatorCollider = _activator.GetComponent<Collider2D>();
            if (activatorCollider != null)
            {
                float activatorLeftBound = activatorCollider.bounds.min.x;
                float activatorRightBound = activatorCollider.bounds.max.x;
                float randomCenter = Random.Range(activatorLeftBound, activatorRightBound);

                _resolvedLeftBound = randomCenter - moveRange / 2.0f;
                _resolvedRightBound = randomCenter + moveRange / 2.0f;

                _resolvedLeftBound = Mathf.Max(_resolvedLeftBound, activatorLeftBound);
                _resolvedRightBound = Mathf.Min(_resolvedRightBound, activatorRightBound);

                // 範囲が狭すぎる場合の補正
                if (_resolvedRightBound - _resolvedLeftBound < moveRange)
                {
                    if (_resolvedLeftBound == activatorLeftBound)
                    {
                        _resolvedRightBound = Mathf.Min(
                            activatorRightBound,
                            _resolvedLeftBound + moveRange
                        );
                    }
                    else
                    {
                        _resolvedLeftBound = Mathf.Max(
                            activatorLeftBound,
                            _resolvedRightBound - moveRange
                        );
                    }
                }
            }
        }
    }

    /// <summary>
    /// Animatorから攻撃アニメーションの長さを自動取得します。
    /// </summary>
    private void CalculateAttackAnimationTime()
    {
        bool foundAttackClip = false;

        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == ATTACK_ANIMATION_CLIP_NAME)
                {
                    attackAnimationTime = clip.length;
                    foundAttackClip = true;
                    break;
                }
            }
        }

        if (!foundAttackClip)
        {
            Debug.LogWarning(
                $"{this.name}のAnimatorに攻撃アニメーション({ATTACK_ANIMATION_CLIP_NAME})が見つかりませんでした。デフォルト値を使用します。"
            );
        }
    }

    #endregion

    #region 移動・ロジック制御

    /// <summary>
    /// 移動処理および崖・プレイヤーの検知ロジックを実行します。
    /// </summary>
    private void UpdateMovementAndLogic()
    {
        Vector2 currentPos = transform.position;

        // 崖の検知
        float checkOffsetX = rightFlag ? cliffCheckOffsetX : -cliffCheckOffsetX;
        Vector2 cliffCheckOrigin = new Vector2(currentPos.x + checkOffsetX, currentPos.y);
        bool isCliff = !Physics2D.Raycast(
            cliffCheckOrigin,
            Vector2.down,
            cliffCheckRayLength,
            _groundLayer
        );

        if (isCliff)
        {
            HandleObstacleEncountered();
            return;
        }

        if (currentState == GolemState.Walk)
        {
            // Walk状態: 範囲端での反転チェック
            bool hasReachedBound =
                (currentPos.x <= _resolvedLeftBound && currentVx < 0f)
                || (_resolvedRightBound <= currentPos.x && currentVx > 0f);
            if (hasReachedBound)
            {
                ReverseDirection();
            }

            currentVx = walkSpeedX * (rightFlag ? 1.0f : -1.0f);
            _rbody.velocity = new Vector2(currentVx, _rbody.velocity.y);

            // プレイヤー検知（タックル開始判定）
            DetectPlayerForTackle(currentPos);
        }
        else if (currentState == GolemState.Tackle)
        {
            // Tackle状態: 範囲+余白の超過チェック
            float limitLeft = _resolvedLeftBound - tackleMargin;
            float limitRight = _resolvedRightBound + tackleMargin;
            bool isOverMargin =
                (currentPos.x <= limitLeft && currentVx < 0f)
                || (limitRight <= currentPos.x && currentVx > 0f);

            // プレイヤーとの距離チェック
            float distanceToPlayerX = float.MaxValue;
            if (_playerTransform != null)
            {
                Vector2 directionToPlayer = (Vector2)_playerTransform.position - currentPos;
                distanceToPlayerX = directionToPlayer.x * (rightFlag ? 1.0f : -1.0f);
            }

            if (isOverMargin || (distanceToPlayerX >= 0f && distanceToPlayerX <= attackDistanceX))
            {
                StartCoroutine(AttackSequenceCoroutine());
                return;
            }

            currentVx = tackleSpeedX * (rightFlag ? 1.0f : -1.0f);
            _rbody.velocity = new Vector2(currentVx, _rbody.velocity.y);

            // 一定間隔でダストエフェクトを生成
            dashDustTimer += Time.deltaTime;
            if (dashDustTimer >= dashDustSpawnInterval)
            {
                dashDustTimer = 0f;
                if (ObjectPooler.SceneInstance != null)
                {
                    // 進行方向の少し後ろ（逆方向）の座標を計算
                    float offsetX = dashDustOffsetX * (rightFlag ? -1.0f : 1.0f);
                    Vector3 spawnPos = transform.position + new Vector3(offsetX, 0f, 0f);

                    // 計算した位置にダストを生成し、生成されたオブジェクトを取得
                    GameObject dustObj = ObjectPooler.SceneInstance.SpawnFromPool(
                        DUSH_DUST_POOLTAG,
                        spawnPos,
                        Quaternion.identity
                    );

                    if (dustObj != null)
                    {
                        // 自身の向き(rightFlag)に合わせてダストの左右スケールを反転させる
                        Vector3 dustScale = dustObj.transform.localScale;
                        dustScale.x = Mathf.Abs(dustScale.x) * (rightFlag ? 1.0f : -1.0f);
                        dustObj.transform.localScale = dustScale;
                    }
                }
            }
        }
    }

    /// <summary>
    /// プレイヤーがタックル範囲内にいるか検知します。
    /// </summary>
    private void DetectPlayerForTackle(Vector2 currentPos)
    {
        if (_playerTransform == null)
            return;

        Vector2 directionToPlayer = (Vector2)_playerTransform.position - currentPos;
        float horizontalDistance = directionToPlayer.x * (rightFlag ? 1.0f : -1.0f);

        bool isInTackleRangeX =
            horizontalDistance >= tackleDetectMinX && horizontalDistance <= tackleDetectMaxX;
        bool isInRangeY = Mathf.Abs(directionToPlayer.y) < attackRangeY;

        if (isInTackleRangeX && isInRangeY)
        {
            ChangeStateToTackle();
        }
    }

    /// <summary>
    /// 壁や崖などの障害物に到達した際の処理を行います。
    /// </summary>
    private void HandleObstacleEncountered()
    {
        if (currentState == GolemState.Walk)
        {
            ReverseDirection();
        }
        else if (currentState == GolemState.Tackle)
        {
            // タックル中に壁や崖に到達したら即座に攻撃
            StartCoroutine(AttackSequenceCoroutine());
        }
    }

    /// <summary>
    /// 現在の水平方向の移動が接触面に向かっているかを判定します。
    /// </summary>
    private bool IsMovingTowardContact(ContactPoint2D contact)
    {
        Vector2 horizontalVelocity = new Vector2(currentVx, 0f);
        return Vector2.Dot(horizontalVelocity, contact.normal) < 0f;
    }

    /// <summary>
    /// ステートをWalk(歩行)に変更します。
    /// </summary>
    private void ChangeStateToWalk()
    {
        currentState = GolemState.Walk;
        _animator.SetBool(walkParam, true);
        _animator.SetBool(tackleParam, false);
        currentVx = walkSpeedX * (rightFlag ? 1.0f : -1.0f);
    }

    /// <summary>
    /// ステートをTackle(高速移動)に変更し、予兆エフェクトを発生させます。
    /// </summary>
    private void ChangeStateToTackle()
    {
        currentState = GolemState.Tackle;
        _animator.SetBool(walkParam, false);
        _animator.SetBool(tackleParam, true);
        currentVx = tackleSpeedX * (rightFlag ? 1.0f : -1.0f);
        dashDustTimer = 0f;

        // タックル開始時に指定した色でフラッシュさせる
        if (_enemyHP != null)
        {
            _enemyHP.TriggerCustomFlash(tackleFlashColor, 0.85f, tackleFlashDuration);
        }
    }

    /// <summary>
    /// 進行方向を反転させます。
    /// </summary>
    private void ReverseDirection()
    {
        rightFlag = !rightFlag;
        currentVx = walkSpeedX * (rightFlag ? 1.0f : -1.0f);
        ApplyFacingDirection();
    }

    /// <summary>
    /// rightFlagに基づいてスプライトの向き（スケール）を更新します。
    /// </summary>
    private void ApplyFacingDirection()
    {
        Vector3 currentScale = transform.localScale;
        currentScale.x = Mathf.Abs(currentScale.x) * (rightFlag ? 1.0f : -1.0f);
        transform.localScale = currentScale;
    }

    /// <summary>
    /// 地面に埋まっている場合、抜け出すまで上に位置調整を行うコルーチン。
    /// </summary>
    private IEnumerator CheckAndAdjustPosition()
    {
        if (IsOverlappingGround)
        {
            currentState = GolemState.AdjustingPosition;
            _rbody.simulated = false;

            while (IsOverlappingGround)
            {
                transform.position += new Vector3(0f, verticalAdjustSpeed * Time.deltaTime, 0f);
                yield return null;
            }

            _rbody.simulated = true;
        }

        ChangeStateToWalk();
    }

    #endregion

    #region 攻撃処理

    /// <summary>
    /// タックル終了から攻撃アクション、Idle待機、復帰までの一連のシーケンスを管理します。
    /// </summary>
    private IEnumerator AttackSequenceCoroutine()
    {
        // 攻撃状態へ移行
        currentState = GolemState.Attack;
        _rbody.velocity = new Vector2(0f, _rbody.velocity.y);
        _animator.SetBool(walkParam, false);
        _animator.SetBool(tackleParam, false);
        _animator.SetTrigger(attackTriggerParam);

        // 本体自身のタグを攻撃判定用に変更
        gameObject.tag = GameConstants.DAMAGEABLE_ENEMY_TAG_NAME;

        // 攻撃アニメーションの長さ分待機
        yield return new WaitForSeconds(attackAnimationTime);

        // 本体のタグを元に戻す
        gameObject.tag = GameConstants.IMMUNE_ENEMY_TAG_NAME;

        // 攻撃後のIdle待機状態
        currentState = GolemState.Idle;

        yield return new WaitForSeconds(idleTime);

        // 待機完了後、反転してWalkへ復帰
        ReverseDirection();
        ChangeStateToWalk();
    }

    #endregion

    #region ユーティリティ処理

    /// <summary>
    /// 物理演算とアニメーションを一時停止します。
    /// </summary>
    private void PauseMovement()
    {
        if (_rbody.simulated)
        {
            _rbody.simulated = false;
        }
        if (_animator.speed > 0f)
        {
            _animator.speed = 0f;
        }
    }

    /// <summary>
    /// 物理演算とアニメーションを再開します。
    /// </summary>
    private void ResumeMovement()
    {
        if (!_rbody.simulated)
        {
            _rbody.simulated = true;
        }
        if (_animator.speed == 0f)
        {
            _animator.speed = 1f;
        }
    }

    #endregion

    #region デバッグ描画

    private void OnDrawGizmos()
    {
        // 1. 移動範囲の描画
        Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 0.15f);
        Vector3 center = Vector3.zero;
        Vector3 size = Vector3.zero;

        if (isUseManualBounds)
        {
            float gizmoLeftBound;
            float gizmoRightBound;

            if (Application.isPlaying)
            {
                gizmoLeftBound = _resolvedLeftBound;
                gizmoRightBound = _resolvedRightBound;
            }
            else
            {
                float originX = isUseRelativeBounds ? transform.position.x : 0f;
                gizmoLeftBound = originX + leftBound;
                gizmoRightBound = originX + rightBound;
            }

            center = new Vector3(
                (gizmoLeftBound + gizmoRightBound) / 2.0f,
                transform.position.y + 2.0f,
                transform.position.z
            );
            size = new Vector3(Mathf.Abs(gizmoRightBound - gizmoLeftBound), 4.5f, 0.1f);
        }
        else
        {
            center = new Vector3(
                transform.position.x,
                transform.position.y + 2.0f,
                transform.position.z
            );
            size = new Vector3(moveRange, 4.5f, 0.1f);
        }
        Gizmos.DrawCube(center, size);

        // 2. タックル検知範囲の描画
        Gizmos.color = new Color(1.0f, 0.5f, 0.0f, 0.3f);
        float minDetectX = transform.position.x + (rightFlag ? 1.0f : -1.0f) * tackleDetectMinX;
        float maxDetectX = transform.position.x + (rightFlag ? 1.0f : -1.0f) * tackleDetectMaxX;
        Vector3 detectCenter = new Vector3(
            (minDetectX + maxDetectX) / 2.0f,
            transform.position.y + 2.0f,
            transform.position.z
        );
        Vector3 detectSize = new Vector3(
            Mathf.Abs(tackleDetectMaxX - tackleDetectMinX),
            attackRangeY * 2.0f,
            0.1f
        );
        Gizmos.DrawCube(detectCenter, detectSize);

        // 3. 崖っぷち判定用のRaycast描画
        Gizmos.color = Color.yellow;
        float cliffX = transform.position.x + (rightFlag ? cliffCheckOffsetX : -cliffCheckOffsetX);
        Vector2 cliffCheckOrigin = new Vector2(cliffX, transform.position.y);
        Gizmos.DrawLine(cliffCheckOrigin, cliffCheckOrigin + Vector2.down * cliffCheckRayLength);
    }

    #endregion
}

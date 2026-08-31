using System.Collections;
using UnityEngine;

/// <summary>
/// 雪原ゴーレムの挙動を制御するコントローラークラス。
/// プレイヤーの位置に応じて向きを変え、ブーメランとブレス（FreezeMist）による攻撃を行います。
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class SnowFieldGolemNormalMoveController : MonoBehaviour, IEnemyResettable
{
    #region 定数・列挙型

    // プールタグを定数で管理
    private const string MIST_POOL_TAG = "FreezeMist";

    private enum EnemyVariant
    {
        None = 0,
        SnowField = 1, // 雪原タイプ
        SnowMan = 2,
    }

    #endregion

    #region インスペクター設定

    [Header("基本設定")]
    [SerializeField]
    private EnemyVariant _variantType = EnemyVariant.SnowField;

    [SerializeField]
    private EnemyActivator _activator = null;

    [Header("配置・初期位置設定")]
    [SerializeField]
    [Tooltip(
        "手動で初期位置を設定するかどうか。falseの場合はActivatorの範囲内でランダム配置されます"
    )]
    private bool _isUseManualInitialPosition = false;

    [SerializeField]
    [Tooltip("配置調整用の地面チェック中心点")]
    private Transform _overlapCheckPoint;

    [SerializeField, Min(0f)]
    [Tooltip("地面表面から意図的に浮かせる高さ")]
    private float _groundClearance = 0.5f;

    [Header("攻撃範囲")]
    [SerializeField]
    private float _attackRangeX = 6.0f;

    [SerializeField]
    private float _attackRangeY = 2.0f;

    [SerializeField]
    [Tooltip("自身の足元から下方向への攻撃検知範囲（浮いている場合の補正用）")]
    private float _attackRangeYDown = 1.0f;

    [Header("攻撃確率・クールダウン")]
    [SerializeField, Range(0, 1)]
    private float _boomerangProbability = 0.5f;

    [SerializeField]
    [Tooltip("ブーメラン攻撃終了後、次の攻撃が可能になるまでの待機時間")]
    private float _boomerangAttackCooldown = 3.0f;

    [SerializeField]
    [Tooltip("ブレス攻撃開始後、次の攻撃が可能になるまでの待機時間")]
    private float _breathAttackCooldown = 3.0f;

    [Header("ブーメラン設定")]
    [SerializeField]
    private Transform _boomerangTransform;

    [SerializeField]
    [Tooltip("攻撃準備（AttackReady）から発射までの待機時間")]
    private float _boomerangAttackReadyTime = 1.0f;

    [SerializeField]
    private float _boomerangFlyTime = 2.0f;

    [SerializeField]
    [Tooltip("ブーメランが飛んでいくX方向の最小距離")]
    private float _boomerangDistanceMin = 3.0f;

    [SerializeField]
    [Tooltip("ブーメランが飛んでいくX方向の最大距離")]
    private float _boomerangDistanceMax = 6.0f;

    [SerializeField]
    [Tooltip("trueなら上回り、falseなら下回りの軌道になります")]
    private bool _isBoomerangOverhand = true;

    [SerializeField]
    [Tooltip("ブーメランが迂回するY方向の幅")]
    private float _boomerangCurveWidth = 3.0f;

    [SerializeField]
    [Tooltip("地面貫通防止用の最低高さ（キャラクターのPivotからの相対Y座標）")]
    private float _boomerangMinYOffset = 0.5f;

    [SerializeField]
    [Tooltip("背中に背負っている表示用のブーメラン")]
    private Transform _backBoomerangTransform;

    [Header("ブレス設定")]
    [SerializeField]
    [Tooltip("ブレスアニメーション開始から実際にミストが発生するまでの待機時間")]
    private float _breathAttackReadyTime = 0.5f;

    [SerializeField]
    private float _mistDuration = 2.5f;

    [SerializeField]
    private float _mistMoveSpeed = 2.0f;

    [SerializeField]
    private Vector2 _breathOffset = new Vector2(1.2f, 0.5f);

    [SerializeField]
    [Tooltip("プレイヤーを狙う際のY軸方向のランダムなブレ幅")]
    private float _mistYVariance = 0.2f;

    #endregion

    #region プライベート変数

    // コンポーネントキャッシュ
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rbody;
    private Transform _playerTransform;

    // 状態管理フラグ
    private bool _isFacingRight = true;
    private bool _canAttack = true;
    private bool _isAttacking = false;
    private Coroutine _attackCoroutine;

    // ブーメランの状態記憶用
    private Vector3 _boomerangLocalPosition;
    private Vector3 _backBoomerangLocalPosition;
    private Vector3 _backBoomerangLocalScale;

    // 接地判定・ステータス用
    private LayerMask _groundLayer;
    private const float POSITION_ADJUST_STEP = 0.01f;
    private const int MAX_POSITION_ADJUST_ATTEMPTS = 1000;
    private int _boomerangDamage = 20;

    // Animatorパラメータハッシュ
    private static readonly int AnimIsAttacking = Animator.StringToHash("IsAttacking");
    private static readonly int AnimAttackReady = Animator.StringToHash("AttackReady");
    private static readonly int AnimWeaponAttack = Animator.StringToHash("WeaponAttack");
    private static readonly int AnimBreathAttack = Animator.StringToHash("BreathAttack");

    #endregion

    #region Unity ライフサイクル

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _rbody = GetComponent<Rigidbody2D>();

        // 親のEnemyActivatorを取得
        _activator = GetComponentInParent<EnemyActivator>();

        // 地面レイヤーの設定（GameConstants等から取得を想定）
        _groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );

        // Variantに応じたステータス調整（例）
        switch (_variantType)
        {
            case EnemyVariant.SnowField:
                _boomerangDamage = 107;
                break;
            case EnemyVariant.SnowMan:
                _boomerangDamage = 80;
                break;
            default:
                Debug.LogError($"{this.name}のEnemyVariantが設定されていません。", this);
                break;
        }

        // 攻撃用ブーメランの初期設定と記憶
        if (_boomerangTransform != null)
        {
            _boomerangLocalPosition = _boomerangTransform.localPosition;
            _boomerangTransform.gameObject.SetActive(false);
        }

        // 背負い用のブーメランの初期設定と記憶
        if (_backBoomerangTransform != null)
        {
            _backBoomerangLocalPosition = _backBoomerangTransform.localPosition;
            _backBoomerangLocalScale = _backBoomerangTransform.localScale;
            _backBoomerangTransform.gameObject.SetActive(true);
        }
    }

    private void Start()
    {
        ResetState();
    }

    private void FixedUpdate()
    {
        if (_playerTransform == null || _isAttacking)
            return;

        // ポーズチェック（TimeManager等がある場合）
        if (TimeManager.instance != null && TimeManager.instance.isEnemyMovePaused)
        {
            _rbody.simulated = false;
            return;
        }
        _rbody.simulated = true;

        UpdateFacingDirection();

        // 攻撃可能かつプレイヤーが範囲内にいる場合に攻撃を実行
        if (_canAttack && IsPlayerInAttackRange())
        {
            _attackCoroutine = StartCoroutine(AttackRoutine());
        }
    }

    private void OnDisable()
    {
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }

        _canAttack = true;
        _isAttacking = false;
        CleanupBoomerangState();
    }

    #endregion

    #region インターフェース実装 (IEnemyResettable)

    /// <summary>
    /// 敵の状態（位置、向き、装備表示、アニメーションなど）を初期化・リセットします。
    /// </summary>
    public void ResetState()
    {
        // プレイヤーの取得
        if (PlayerManager.instance != null)
        {
            _playerTransform = PlayerManager.instance.PlayerGameObject?.transform;
        }
        else
        {
            _playerTransform = GameObject
                .FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME)
                ?.transform;
        }

        // 初期位置の自動設定（Activatorの範囲内）
        if (!_isUseManualInitialPosition && _activator != null)
        {
            var coll = _activator.GetComponent<Collider2D>();
            if (coll != null)
            {
                float randomX = Random.Range(coll.bounds.min.x, coll.bounds.max.x);
                transform.position = new Vector2(randomX, transform.position.y);
            }
        }

        _canAttack = true;
        _isAttacking = false;

        CleanupBoomerangState();

        _animator.SetBool(AnimIsAttacking, false);

        // 地面への接地調整
        StartCoroutine(CheckAndAdjustPosition());
    }

    #endregion

    #region 状態後処理

    /// <summary>
    /// 攻撃中・非表示化後に残ったブーメランを初期状態へ戻します。
    /// </summary>
    private void CleanupBoomerangState()
    {
        if (_boomerangTransform != null)
        {
            _boomerangTransform.SetParent(transform);
            _boomerangTransform.localPosition = _boomerangLocalPosition;
            _boomerangTransform.gameObject.SetActive(false);
        }

        if (_backBoomerangTransform != null)
        {
            _backBoomerangTransform.SetParent(transform);
            _backBoomerangTransform.localPosition = _backBoomerangLocalPosition;
            _backBoomerangTransform.localScale = _backBoomerangLocalScale;
            _backBoomerangTransform.gameObject.SetActive(true);
        }
    }

    #endregion

    #region 行動・向き制御

    /// <summary>
    /// プレイヤーの位置に合わせてスプライトおよび背中の装備品の向きを更新します。
    /// </summary>
    private void UpdateFacingDirection()
    {
        float direction = _playerTransform.position.x - transform.position.x;

        // 右向きがデフォルト。プレイヤーが左にいれば反転。
        _isFacingRight = direction >= 0;
        _spriteRenderer.flipX = !_isFacingRight;

        // 背負い用ブーメランの反転処理
        if (_backBoomerangTransform != null)
        {
            float flipMultiplier = _isFacingRight ? 1f : -1f;

            // ローカルのX座標を反転させて、背中の正しい位置（左右）に移動させる
            _backBoomerangTransform.localPosition = new Vector3(
                _backBoomerangLocalPosition.x * flipMultiplier,
                _backBoomerangLocalPosition.y,
                _backBoomerangLocalPosition.z
            );

            // ローカルのXスケールを反転させて、ブーメランの画像自体を反転させる
            _backBoomerangTransform.localScale = new Vector3(
                _backBoomerangLocalScale.x * flipMultiplier,
                _backBoomerangLocalScale.y,
                _backBoomerangLocalScale.z
            );
        }
    }

    /// <summary>
    /// プレイヤーが攻撃範囲内にいるかを判定します。
    /// PivotがBottomであることを考慮し、足元からの上下の範囲で判定を行います。
    /// </summary>
    /// <returns>攻撃範囲内にいる場合は true</returns>
    private bool IsPlayerInAttackRange()
    {
        Vector2 diff = _playerTransform.position - transform.position;
        float horizontalDist = diff.x * (_isFacingRight ? 1 : -1);

        // X軸の判定（向いている方向かつ指定距離以内）
        bool isWithinRangeX = horizontalDist > 0 && horizontalDist <= _attackRangeX;

        // Y軸の判定（足元から上方向 _attackRangeY、下方向 _attackRangeYDown の範囲内かチェック）
        bool isWithinRangeY = diff.y >= -_attackRangeYDown && diff.y <= _attackRangeY;

        return isWithinRangeX && isWithinRangeY;
    }

    #endregion

    #region 攻撃判定・実行ロジック

    /// <summary>
    /// 攻撃の抽選を行い、各種攻撃ルーチンを実行します。
    /// </summary>
    private IEnumerator AttackRoutine()
    {
        _canAttack = false;
        _isAttacking = true;
        _animator.SetBool(AnimIsAttacking, true);

        float currentCooldown = 0f;

        // 攻撃の抽選とクールダウンの決定
        if (Random.value <= _boomerangProbability)
        {
            yield return StartCoroutine(BoomerangAttackRoutine());
            currentCooldown = _boomerangAttackCooldown;
        }
        else
        {
            yield return StartCoroutine(BreathAttackRoutine());
            currentCooldown = _breathAttackCooldown;
        }

        _isAttacking = false;
        _animator.SetBool(AnimIsAttacking, false);

        // 指定されたクールダウン時間を待機
        yield return new WaitForSeconds(currentCooldown);
        _canAttack = true;
        _attackCoroutine = null;
    }

    /// <summary>
    /// ブーメランによる攻撃を実行し、ベジェ曲線を用いた軌道を制御します。
    /// </summary>
    private IEnumerator BoomerangAttackRoutine()
    {
        _animator.SetTrigger(AnimAttackReady);
        yield return new WaitForSeconds(_boomerangAttackReadyTime);

        _animator.SetTrigger(AnimWeaponAttack);

        if (_boomerangTransform != null)
        {
            // 飛ばす瞬間に背中の表示用を非表示にする
            if (_backBoomerangTransform != null)
            {
                _backBoomerangTransform.gameObject.SetActive(false);
            }

            var damageController = _boomerangTransform.GetComponent<ContactDamageController>();
            if (damageController != null)
            {
                damageController.SetNormalDamage(_boomerangDamage);
            }

            _boomerangTransform.gameObject.SetActive(true);
            _boomerangTransform.SetParent(null); // 一時的に親を離れる

            Vector3 p0 = _boomerangTransform.position;
            Vector3 p3 = p0;

            // 指定した範囲からランダムな飛距離を決定
            float randomDistance = Random.Range(_boomerangDistanceMin, _boomerangDistanceMax);
            float facingMultiplier = _isFacingRight ? 1f : -1f;

            // 軌道のY座標の最大値と最小値を計算
            float topY = p0.y + _boomerangCurveWidth;
            float bottomY = p0.y - _boomerangCurveWidth;

            // キャラクターの足元（Pivot）を基準に、地面を貫通しない最低のY座標を計算
            float groundMinY = transform.position.y + _boomerangMinYOffset;

            // 下側を通るルートのY座標を、最低の高さで制限（下限補正）
            bottomY = Mathf.Max(bottomY, groundMinY);

            // インスペクターのフラグを使用して上下の制御点を決定
            float p1Y = _isBoomerangOverhand ? topY : bottomY;
            float p2Y = _isBoomerangOverhand ? bottomY : topY;

            // 制御点の計算
            Vector3 p1 = new Vector3(p0.x + randomDistance * facingMultiplier, p1Y, 0);
            Vector3 p2 = new Vector3(p0.x + randomDistance * facingMultiplier, p2Y, 0);

            float timer = 0;

            while (timer < _boomerangFlyTime)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / _boomerangFlyTime);

                float u = 1f - t;
                float tt = t * t;
                float uu = u * u;
                float uuu = uu * u;
                float ttt = tt * t;

                Vector3 position = uuu * p0;
                position += 3f * uu * t * p1;
                position += 3f * u * tt * p2;
                position += ttt * p3;

                _boomerangTransform.position = position;
                yield return null;
            }

            // 手元に戻して再設定
            _boomerangTransform.SetParent(this.transform);
            _boomerangTransform.localPosition = _boomerangLocalPosition;
            _boomerangTransform.gameObject.SetActive(false);

            // キャッチしたので背中の表示用を再表示する
            if (_backBoomerangTransform != null)
            {
                _backBoomerangTransform.gameObject.SetActive(true);
            }
        }
    }

    /// <summary>
    /// オブジェクトプールを利用してブレス攻撃（FreezeMist）を生成し、発射します。
    /// </summary>
    private IEnumerator BreathAttackRoutine()
    {
        _animator.SetTrigger(AnimBreathAttack);

        yield return new WaitForSeconds(_breathAttackReadyTime);

        Vector3 spawnPos =
            transform.position
            + new Vector3(_isFacingRight ? _breathOffset.x : -_breathOffset.x, _breathOffset.y, 0);

        // 定数化したタグを使用してプールから生成
        GameObject mist = ObjectPooler.SceneInstance.SpawnFromPool(
            MIST_POOL_TAG,
            spawnPos,
            Quaternion.identity
        );

        if (mist != null)
        {
            var controller = mist.GetComponent<FreezeMistController>();
            if (controller != null)
            {
                Vector2 aimDirection = _isFacingRight ? Vector2.right : Vector2.left;
                if (_playerTransform != null)
                {
                    // プレイヤーの中心（足元 + PLAYER_BASE_HEIGHT / 2）を狙うように補正を加える
                    Vector3 targetPos =
                        _playerTransform.position
                        + new Vector3(0, GameConstants.PLAYER_BASE_HEIGHT / 2f, 0);
                    aimDirection = (targetPos - spawnPos).normalized;
                }

                // インスペクターで設定した _mistYVariance を渡す
                controller.Initialize(aimDirection, _mistDuration, _mistMoveSpeed, _mistYVariance);
            }
        }
    }

    #endregion

    #region 補助機能

    /// <summary>
    /// 配置時に地面に埋まっている場合、埋まらない位置までY座標を上方向に調整します。
    /// </summary>
    private IEnumerator CheckAndAdjustPosition()
    {
        if (_overlapCheckPoint == null)
            yield break;

        // チェック地点を中心とする円が地面から離れるまで上昇させ、指定した浮遊量を確保する。
        if (Physics2D.OverlapCircle(_overlapCheckPoint.position, _groundClearance, _groundLayer))
        {
            _rbody.simulated = false;
            int adjustAttempts = 0;
            while (
                Physics2D.OverlapCircle(
                    _overlapCheckPoint.position,
                    _groundClearance,
                    _groundLayer
                )
                && adjustAttempts < MAX_POSITION_ADJUST_ATTEMPTS
            )
            {
                transform.position += Vector3.up * POSITION_ADJUST_STEP;
                adjustAttempts++;
                yield return null;
            }

            if (adjustAttempts >= MAX_POSITION_ADJUST_ATTEMPTS)
            {
                Debug.LogWarning($"{name}の地面からの位置補正が上限回数に達しました。", this);
            }

            _rbody.simulated = true;
        }
    }

    #endregion

    #region デバッグ描画

    private void OnDrawGizmosSelected()
    {
        // 実行中（Playモード）でない場合は、SpriteRendererのflipXから現在の向きを判定する
        bool currentFacingRight = _isFacingRight;
        if (!Application.isPlaying)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                // flipXがfalseなら右向き、trueなら左向き
                currentFacingRight = !sr.flipX;
            }
        }

        float facingMultiplier = currentFacingRight ? 1f : -1f;

        // 攻撃範囲の描画
        Gizmos.color = new Color(0, 1, 0, 0.2f);

        // サイズは上方向と下方向の合計値
        Vector3 size = new Vector3(_attackRangeX, _attackRangeY + _attackRangeYDown, 0.1f);

        // 中心位置のY軸を、上下の範囲の中間に調整
        float centerYOffset = (_attackRangeY - _attackRangeYDown) / 2f;
        Vector3 center =
            transform.position
            + new Vector3(facingMultiplier * (_attackRangeX / 2), centerYOffset, 0);

        Gizmos.DrawCube(center, size);

        // 地面チェック範囲
        if (_overlapCheckPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_overlapCheckPoint.position, _groundClearance);
        }

        // ブレスの発生位置（オフセット）の描画（黄色の小さい球）
        Gizmos.color = Color.yellow;
        Vector3 breathSpawnPos =
            transform.position
            + new Vector3(
                currentFacingRight ? _breathOffset.x : -_breathOffset.x,
                _breathOffset.y,
                0
            );
        Gizmos.DrawSphere(breathSpawnPos, 0.15f);

        // ブーメランの軌跡（ベジェ曲線）の描画
        Gizmos.color = Color.magenta;
        Vector3 p0 = transform.position;
        Vector3 p3 = p0;

        // Gizmo描画時も同じ下限補正を適用
        float topY = p0.y + _boomerangCurveWidth;
        float bottomY = p0.y - _boomerangCurveWidth;
        float groundMinY = transform.position.y + _boomerangMinYOffset;
        bottomY = Mathf.Max(bottomY, groundMinY);

        float p1Y = _isBoomerangOverhand ? topY : bottomY;
        float p2Y = _isBoomerangOverhand ? bottomY : topY;

        // 最小距離と最大距離の2つの軌道を描画して範囲を可視化
        DrawBezierGizmo(p0, p3, p0.x + _boomerangDistanceMin * facingMultiplier, p1Y, p2Y, 1.0f);
        DrawBezierGizmo(p0, p3, p0.x + _boomerangDistanceMax * facingMultiplier, p1Y, p2Y, 0.4f);
    }

    /// <summary>
    /// 指定されたパラメータでベジェ曲線のGizmosを描画する補助関数
    /// </summary>
    private void DrawBezierGizmo(
        Vector3 p0,
        Vector3 p3,
        float targetX,
        float p1Y,
        float p2Y,
        float alpha
    )
    {
        Gizmos.color = new Color(1f, 0f, 1f, alpha);
        Vector3 p1 = new Vector3(targetX, p1Y, 0);
        Vector3 p2 = new Vector3(targetX, p2Y, 0);
        Vector3 prevPos = p0;

        for (int i = 1; i <= 20; i++)
        {
            float t = i / 20f;
            float u = 1f - t;
            Vector3 pos =
                (u * u * u * p0) + (3f * u * u * t * p1) + (3f * u * t * t * p2) + (t * t * t * p3);
            Gizmos.DrawLine(prevPos, pos);
            prevPos = pos;
        }
    }

    #endregion
}

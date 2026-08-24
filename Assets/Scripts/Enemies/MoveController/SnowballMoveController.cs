using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// 地面を横方向へ転がり、時間経過に応じて成長・減速する雪玉を制御します。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(ContactDamageController))]
public class SnowballMoveController : MonoBehaviour, IEnemyResettable
{
    #region 列挙型

    private enum EnemyVariant
    {
        None = 0,
        SnowField = 1,
    }

    private enum InitialSizeMode
    {
        Fixed = 0,
        Random = 1,
    }

    private enum InitialDirection
    {
        Left = 0,
        Right = 1,
        Random = 2,
    }

    #endregion

    #region インスペクター設定

    [BoxGroup("基本設定")]
    [SerializeField, Tooltip("攻撃力等の初期化に使用する敵のバリエーションタイプ")]
    private EnemyVariant _variantType = EnemyVariant.SnowField;

    [BoxGroup("スプライト設定")]
    [SerializeField, Tooltip("1倍以上2倍未満で使用する16×16ピクセルのスプライト")]
    private Sprite _sprite16 = null;

    [BoxGroup("スプライト設定")]
    [SerializeField, Tooltip("2倍以上3倍未満で使用する32×32ピクセルのスプライト")]
    private Sprite _sprite32 = null;

    [BoxGroup("スプライト設定")]
    [SerializeField, Tooltip("3倍到達時に使用する48×48ピクセルのスプライト")]
    private Sprite _sprite48 = null;

    [BoxGroup("移動・成長設定")]
    [SerializeField, Min(0f), Tooltip("1倍時の横移動速度")]
    private float _initialSpeed = 6f;

    [BoxGroup("移動・成長設定")]
    [SerializeField, Min(0f), Tooltip("3倍時の横移動速度")]
    private float _finalSpeed = 2f;

    [BoxGroup("移動・成長設定")]
    [SerializeField, Min(0f), Tooltip("1秒間に増加する大きさの倍率")]
    private float _growthRate = 0.2f;

    [BoxGroup("物理設定")]
    [SerializeField, Min(0f), Tooltip("雪玉へ適用するRigidbody2Dの重力倍率")]
    private float _gravityScale = 1f;

    [BoxGroup("物理設定")]
    [SerializeField, Tooltip("EnemyPhysicsレイヤーの子オブジェクトに配置する地形衝突用Collider")]
    private CircleCollider2D _physicsCollider = null;

    [BoxGroup("リセット時の大きさ")]
    [SerializeField, Tooltip("固定倍率または1～3倍のランダム倍率から開始します")]
    private InitialSizeMode _initialSizeMode = InitialSizeMode.Fixed;

    [BoxGroup("リセット時の大きさ")]
    [
        SerializeField,
        ShowIf(nameof(_initialSizeMode), InitialSizeMode.Fixed),
        Range(1f, 3f),
        Tooltip("固定開始時に使用する大きさの倍率")
    ]
    private float _fixedInitialSize = 1f;

    [BoxGroup("リセット時の位置")]
    [SerializeField, Tooltip("有効な場合、指定したX座標範囲内へランダムに配置します")]
    private bool _isUseRandomXRange = false;

    [BoxGroup("リセット時の位置")]
    [
        SerializeField,
        ShowIf(nameof(_isUseRandomXRange)),
        Tooltip("ランダム配置に使用するX座標の最小値（ワールド座標）")
    ]
    private float _minimumInitialX = 0f;

    [BoxGroup("リセット時の位置")]
    [
        SerializeField,
        ShowIf(nameof(_isUseRandomXRange)),
        Tooltip("ランダム配置に使用するX座標の最大値（ワールド座標）")
    ]
    private float _maximumInitialX = 0f;

    [BoxGroup("リセット時の進行方向")]
    [SerializeField, Tooltip("ResetState時に雪玉が転がり始める方向")]
    private InitialDirection _initialDirection = InitialDirection.Random;

    [BoxGroup("壁反射設定")]
    [SerializeField, Range(0f, 1f), Tooltip("壁と判定する接触法線のX成分の最小値")]
    private float _wallNormalThreshold = 0.5f;

    #endregion

    #region 内部変数

    private const float MINIMUM_SIZE = 1f;
    private const float MIDDLE_SIZE = 2f;
    private const float MAXIMUM_SIZE = 3f;
    private const float BASE_SPRITE_PIXEL_SIZE = 16f;
    private static readonly float BASE_COLLIDER_RADIUS =
        BASE_SPRITE_PIXEL_SIZE / GameConstants.PIXELS_PER_UNIT / 2f;

    private Rigidbody2D _rbody;
    private CircleCollider2D _circleCollider;
    private SpriteRenderer _spriteRenderer;
    private ContactDamageController _contactDamageController;
    private LayerMask _groundLayer;
    private Vector3 _initialPosition;
    private Vector3 _initialLocalScale;
    private float _currentSize = MINIMUM_SIZE;
    private float _moveDirection = 1f;
    private int _damage = 20;
    private bool _isStarted = false;

    #endregion

    #region Unityイベント関数

    private void Awake()
    {
        _rbody = GetComponent<Rigidbody2D>();
        _circleCollider = GetComponent<CircleCollider2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _contactDamageController = GetComponent<ContactDamageController>();
        _initialLocalScale = transform.localScale;

        if (_physicsCollider == null)
        {
            Debug.LogError(
                $"{name}の地形衝突用CircleCollider2Dが設定されていません。",
                this
            );
        }

        _groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );

        switch (_variantType)
        {
            case EnemyVariant.SnowField:
                _damage = 20;
                break;
            default:
                Debug.LogError($"{name}のEnemyVariantが設定されていません。", this);
                break;
        }
    }

    private void Start()
    {
        _initialPosition = transform.position;
        _isStarted = true;
        ResetState();
    }

    private void OnEnable()
    {
        // Startはインスタンス生成後の1回しか呼ばれないため、
        // プールから再取得された際は明示的に状態を初期化します。
        if (_isStarted)
        {
            ResetState();
        }
    }

    private void FixedUpdate()
    {
        if (TimeManager.instance != null && TimeManager.instance.isEnemyMovePaused)
        {
            _rbody.simulated = false;
            return;
        }
        _rbody.simulated = true;

        UpdateGrowth();

        float currentSpeed = GetCurrentSpeed();
        _rbody.velocity = new Vector2(_moveDirection * currentSpeed, _rbody.velocity.y);
        UpdateRotation(currentSpeed);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        ReflectFromWall(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        ReflectFromWall(collision);
    }

    #endregion

    #region IEnemyResettable

    /// <summary>
    /// 位置、大きさ、スプライト、回転、進行方向および接触ダメージを初期状態へ戻します。
    /// </summary>
    public void ResetState()
    {
        _rbody.bodyType = RigidbodyType2D.Dynamic;
        _rbody.simulated = true;
        _rbody.gravityScale = Mathf.Max(0f, _gravityScale);
        _rbody.velocity = Vector2.zero;
        _rbody.angularVelocity = 0f;
        _rbody.rotation = 0f;

        float minimumX = Mathf.Min(_minimumInitialX, _maximumInitialX);
        float maximumX = Mathf.Max(_minimumInitialX, _maximumInitialX);
        float initialX = _isUseRandomXRange ? Random.Range(minimumX, maximumX) : _initialPosition.x;
        transform.position = new Vector3(initialX, _initialPosition.y, _initialPosition.z);

        _currentSize =
            _initialSizeMode == InitialSizeMode.Random
                ? Random.Range(MINIMUM_SIZE, MAXIMUM_SIZE)
                : Mathf.Clamp(_fixedInitialSize, MINIMUM_SIZE, MAXIMUM_SIZE);

        switch (_initialDirection)
        {
            case InitialDirection.Left:
                _moveDirection = -1f;
                break;
            case InitialDirection.Right:
                _moveDirection = 1f;
                break;
            case InitialDirection.Random:
                _moveDirection = Random.value < 0.5f ? -1f : 1f;
                break;
        }

        ApplySizeAndSprite();
        _contactDamageController.SetNormalDamage(_damage);
    }

    #endregion

    #region 移動・成長処理

    private void UpdateGrowth()
    {
        if (_currentSize >= MAXIMUM_SIZE || _growthRate <= 0f)
            return;

        _currentSize = Mathf.Min(MAXIMUM_SIZE, _currentSize + (_growthRate * Time.fixedDeltaTime));
        ApplySizeAndSprite();
    }

    private float GetCurrentSpeed()
    {
        float normalizedSize = Mathf.InverseLerp(MINIMUM_SIZE, MAXIMUM_SIZE, _currentSize);
        return Mathf.Lerp(Mathf.Max(0f, _initialSpeed), Mathf.Max(0f, _finalSpeed), normalizedSize);
    }

    private void ApplySizeAndSprite()
    {
        Sprite targetSprite;
        float spriteBaseSize;

        if (_currentSize >= MAXIMUM_SIZE)
        {
            targetSprite = _sprite48;
            spriteBaseSize = MAXIMUM_SIZE;
        }
        else if (_currentSize >= MIDDLE_SIZE)
        {
            targetSprite = _sprite32;
            spriteBaseSize = MIDDLE_SIZE;
        }
        else
        {
            targetSprite = _sprite16;
            spriteBaseSize = MINIMUM_SIZE;
        }

        if (targetSprite != null)
        {
            _spriteRenderer.sprite = targetSprite;
        }

        float displayScale = _currentSize / spriteBaseSize;
        transform.localScale = Vector3.Scale(
            _initialLocalScale,
            new Vector3(displayScale, displayScale, 1f)
        );

        // 16px・PPU 16では1倍時の直径が1 Unity unit（半径0.5）になります。
        // Transformの表示倍率補正を考慮し、ワールド上の半径が常に現在倍率の半分になるよう、
        // 使用中スプライトの基準倍率に合わせてColliderのローカル半径を更新します。
        float colliderRadius = BASE_COLLIDER_RADIUS * spriteBaseSize;
        _circleCollider.radius = colliderRadius;

        if (_physicsCollider != null)
        {
            _physicsCollider.radius = colliderRadius;
        }
    }

    private void UpdateRotation(float currentSpeed)
    {
        if (currentSpeed <= 0f || _spriteRenderer.sprite == null)
            return;

        float worldRadius =
            _spriteRenderer.sprite.bounds.extents.x * Mathf.Abs(transform.lossyScale.x);
        if (worldRadius <= Mathf.Epsilon)
            return;

        float rotationDelta = (currentSpeed / worldRadius) * Mathf.Rad2Deg * Time.fixedDeltaTime;
        _rbody.MoveRotation(_rbody.rotation - (_moveDirection * rotationDelta));
    }

    #endregion

    #region 衝突処理

    private void ReflectFromWall(Collision2D collision)
    {
        if (!IsGroundLayer(collision.gameObject.layer))
            return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 normal = collision.GetContact(i).normal;
            if (Mathf.Abs(normal.x) < _wallNormalThreshold)
                continue;

            // 接触面から離れる方向を採用することで、同じ壁での連続反転を防ぎます。
            _moveDirection = normal.x > 0f ? 1f : -1f;
            return;
        }
    }

    private bool IsGroundLayer(int layer)
    {
        return (_groundLayer.value & (1 << layer)) != 0;
    }

    #endregion

    #region デバッグ描画

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!_isUseRandomXRange)
            return;

        float minimumX = Mathf.Min(_minimumInitialX, _maximumInitialX);
        float maximumX = Mathf.Max(_minimumInitialX, _maximumInitialX);
        float y = Application.isPlaying ? _initialPosition.y : transform.position.y;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(
            new Vector3(minimumX, y, transform.position.z),
            new Vector3(maximumX, y, transform.position.z)
        );
    }
#endif

    #endregion
}

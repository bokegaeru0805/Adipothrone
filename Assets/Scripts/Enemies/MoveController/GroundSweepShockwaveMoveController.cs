using System.Collections;
using UnityEngine;

/// <summary>
/// 地面に沿って直進し、寿命経過または地形への衝突で非表示になる衝撃波です。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class GroundSweepShockwaveMoveController : MonoBehaviour
{
    private const float WALL_NORMAL_THRESHOLD = 0.5f;
    private const float START_INSIDE_HIT_EPSILON = 0.001f;

    [Header("壁検知")]
    [
        SerializeField,
        Min(0f),
        Tooltip("1FixedUpdate分の移動距離に加算する、前方壁検知Raycastの余裕距離。")
    ]
    private float _wallCheckMargin = 0.05f;

    [
        SerializeField,
        Tooltip("ColliderのBounds中心を基準とした、前方壁検知Raycast開始位置のOffset。")
    ]
    private Vector2 _wallCheckOffset = Vector2.zero;

    [
        SerializeField,
        Range(0f, 1f),
        Tooltip("Collider下端を0、上端を1とした、前方壁検知Raycastの高さ。床の誤検知を避けるため中央より上を推奨します。")
    ]
    private float _wallCheckHeightRatio = 0.75f;

    private Rigidbody2D _rbody;
    private Collider2D _collider;
    private ContactDamageController _contactDamageController;
    private Animator _animator;
    private Renderer[] _renderers;
    private SpriteRenderer[] _spriteRenderers;
    private LayerMask _groundLayer;
    private Coroutine _lifeCoroutine;
    private float _moveSpeedX;

    private void Awake()
    {
        CacheComponents();

        _groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );
    }

    private void CacheComponents()
    {
        _rbody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        if (_collider == null)
            _collider = GetComponentInChildren<Collider2D>(true);

        _contactDamageController = GetComponent<ContactDamageController>();
        if (_contactDamageController == null)
            _contactDamageController = GetComponentInChildren<ContactDamageController>(true);

        _animator = GetComponent<Animator>();
        _renderers = GetComponentsInChildren<Renderer>(true);
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void FixedUpdate()
    {
        bool isPaused = TimeManager.instance != null && TimeManager.instance.isEnemyMovePaused;
        if (isPaused)
        {
            _rbody.velocity = Vector2.zero;
            return;
        }

        if (IsWallAhead())
        {
            Hide();
            return;
        }

        _rbody.velocity = new Vector2(_moveSpeedX, 0f);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsGroundLayer(collision.gameObject.layer))
            return;

        foreach (ContactPoint2D contact in collision.contacts)
        {
            // 床の上向き法線は無視し、横向き法線を持つ壁との衝突だけで消去します。
            if (Mathf.Abs(contact.normal.x) < WALL_NORMAL_THRESHOLD)
                continue;

            Hide();
            return;
        }
    }

    /// <summary>
    /// 衝撃波を発射可能な状態へ初期化します。
    /// </summary>
    public void Launch(bool isFacingRight, float speed, float lifeTime, int damage)
    {
        // 非アクティブな原本から生成された場合はAwakeが未実行なので、先に有効化して参照を解決します。
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        if (_rbody == null)
            CacheComponents();

        if (_lifeCoroutine != null)
            StopCoroutine(_lifeCoroutine);

        _moveSpeedX = Mathf.Abs(speed) * (isFacingRight ? 1f : -1f);
        _contactDamageController?.SetNormalDamage(damage);
        gameObject.tag = GameConstants.DAMAGEABLE_ENEMY_TAG_NAME;
        if (_contactDamageController != null)
            _contactDamageController.gameObject.tag = GameConstants.DAMAGEABLE_ENEMY_TAG_NAME;

        if (_rbody != null)
        {
            _rbody.simulated = true;
            _rbody.velocity = new Vector2(_moveSpeedX, 0f);
        }

        if (_renderers != null)
            foreach (Renderer targetRenderer in _renderers)
                if (targetRenderer != null)
                    targetRenderer.enabled = true;

        // 元画像は右向きのため、左へ進む場合だけSpriteRendererを水平反転します。
        if (_spriteRenderers != null)
            foreach (SpriteRenderer spriteRenderer in _spriteRenderers)
                if (spriteRenderer != null)
                    spriteRenderer.flipX = !isFacingRight;

        if (_animator != null)
        {
            _animator.enabled = true;
            _animator.Rebind();
            _animator.Update(0f);
        }

        _lifeCoroutine = StartCoroutine(HideAfterDelayRoutine(Mathf.Max(0f, lifeTime)));
    }

    private IEnumerator HideAfterDelayRoutine(float delay)
    {
        float elapsed = 0f;
        while (elapsed < delay)
        {
            if (TimeManager.instance == null || !TimeManager.instance.isEnemyMovePaused)
                elapsed += Time.deltaTime;
            yield return null;
        }

        Hide();
    }

    private bool IsGroundLayer(int layer)
    {
        return ((1 << layer) & _groundLayer.value) != 0;
    }

    /// <summary>
    /// ダメージ用Triggerとは別に、進行方向だけをRaycastして壁を検知します。
    /// 床への接触は消去条件に含めません。
    /// </summary>
    private bool IsWallAhead()
    {
        if (_collider == null || Mathf.Approximately(_moveSpeedX, 0f))
            return false;

        float facingMultiplier = Mathf.Sign(_moveSpeedX);
        Bounds bounds = _collider.bounds;
        float originY = Mathf.Lerp(bounds.min.y, bounds.max.y, _wallCheckHeightRatio);
        Vector2 origin =
            new Vector2(bounds.center.x, originY)
            + new Vector2(_wallCheckOffset.x * facingMultiplier, _wallCheckOffset.y);
        Vector2 direction = Vector2.right * facingMultiplier;
        float distance =
            Mathf.Abs(_moveSpeedX) * Time.fixedDeltaTime + Mathf.Max(0f, _wallCheckMargin);

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, distance, _groundLayer);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
                continue;

            // Ray開始点が床Collider内部にある場合に返る距離0のヒットは、壁判定に使用しません。
            if (hit.distance <= START_INSIDE_HIT_EPSILON)
                continue;

            // 平地の上下向き法線を除外し、横向きの面だけを壁として扱います。
            if (Mathf.Abs(hit.normal.x) >= WALL_NORMAL_THRESHOLD)
                return true;
        }

        return false;
    }

    private void Hide()
    {
        if (_rbody != null)
            _rbody.velocity = Vector2.zero;
        _lifeCoroutine = null;
        gameObject.SetActive(false);
    }
}

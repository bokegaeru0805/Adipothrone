using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 旧ハンマー衝撃波Prefabとの参照互換を維持するための移動衝撃波です。
/// 現在のハンマー着弾攻撃からは使用しません。
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerGroundSweepShockwaveMoveController : MonoBehaviour
{
    private const float WallNormalThreshold = 0.5f;
    private const float StartInsideHitEpsilon = 0.001f;

    [SerializeField, Min(0f)]
    private float _wallCheckMargin = 0.05f;

    [SerializeField]
    private Vector2 _wallCheckOffset = Vector2.zero;

    [SerializeField, Range(0f, 1f)]
    private float _wallCheckHeightRatio = 0.75f;

    private readonly HashSet<int> _damagedTargetIds = new HashSet<int>();
    private Rigidbody2D _rbody;
    private Collider2D _collider;
    private SpriteRenderer[] _spriteRenderers;
    private LayerMask _groundLayer;
    private Coroutine _lifeCoroutine;
    private float _moveSpeedX;
    private int _damage;

    private void Awake()
    {
        _rbody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        _groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );
    }

    private void FixedUpdate()
    {
        if (IsWallAhead())
        {
            Destroy(gameObject);
            return;
        }

        _rbody.velocity = new Vector2(_moveSpeedX, 0f);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryDamage(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        TryDamage(collision);
    }

    public void Launch(bool isFacingRight, float speed, float lifeTime, int damage)
    {
        _damagedTargetIds.Clear();
        _damage = Mathf.Max(0, damage);
        _moveSpeedX = Mathf.Abs(speed) * (isFacingRight ? 1f : -1f);
        _rbody.velocity = new Vector2(_moveSpeedX, 0f);

        foreach (SpriteRenderer spriteRenderer in _spriteRenderers)
        {
            if (spriteRenderer != null)
                spriteRenderer.flipX = !isFacingRight;
        }

        if (_lifeCoroutine != null)
            StopCoroutine(_lifeCoroutine);
        _lifeCoroutine = StartCoroutine(DestroyAfterDelay(Mathf.Max(0f, lifeTime)));
    }

    private void TryDamage(Collider2D collision)
    {
        IDamageable damageable = collision.GetComponent<IDamageable>();
        if (damageable == null)
            return;

        MonoBehaviour damageableBehaviour = damageable as MonoBehaviour;
        if (damageableBehaviour == null || !damageableBehaviour.enabled)
            return;

        int targetId = damageableBehaviour.gameObject.GetInstanceID();
        if (_damagedTargetIds.Add(targetId))
            damageable.Damage(_damage);
    }

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
            if (hit.collider == null || hit.distance <= StartInsideHitEpsilon)
                continue;
            if (Mathf.Abs(hit.normal.x) >= WallNormalThreshold)
                return true;
        }

        return false;
    }

    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }
}

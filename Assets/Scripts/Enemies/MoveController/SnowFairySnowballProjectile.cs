using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(ContactDamageController))]
public class SnowFairySnowballProjectile : MonoBehaviour
{
    private Rigidbody2D _rbody;
    private Collider2D _collider;
    private ContactDamageController _contactDamageController;
    private LayerMask _groundLayer;
    private float _fallSpeed;
    private float _lifeTime;
    private float _elapsedTime;
    private bool _isFalling;
    private Action _onFinished;

    private void Awake()
    {
        _rbody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _contactDamageController = GetComponent<ContactDamageController>();
        _rbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );
        PrepareForDisplay();
    }

    private void FixedUpdate()
    {
        if (!_isFalling)
            return;

        if (TimeManager.instance != null && TimeManager.instance.isEnemyMovePaused)
        {
            _rbody.velocity = Vector2.zero;
            return;
        }

        _elapsedTime += Time.fixedDeltaTime;
        if (_elapsedTime >= _lifeTime)
        {
            Finish();
            return;
        }

        float fallDistance = (_fallSpeed * Time.fixedDeltaTime) + 0.05f;
        RaycastHit2D groundHit = Physics2D.BoxCast(
            _collider.bounds.center,
            _collider.bounds.size * 0.9f,
            transform.eulerAngles.z,
            Vector2.down,
            fallDistance,
            _groundLayer
        );
        if (groundHit.collider != null)
        {
            Finish();
            return;
        }

        _rbody.velocity = Vector2.down * _fallSpeed;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_isFalling && IsGroundLayer(collision.gameObject.layer))
        {
            Finish();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isFalling)
            return;

        if (IsGroundLayer(other.gameObject.layer))
        {
            Finish();
        }
    }

    public void PrepareForDisplay()
    {
        _isFalling = false;
        _elapsedTime = 0f;
        if (_rbody != null)
        {
            _rbody.velocity = Vector2.zero;
            _rbody.gravityScale = 0f;
            _rbody.simulated = false;
        }
        if (_collider != null)
            _collider.enabled = false;
        gameObject.tag = GameConstants.UNTAGGED_TAG_NAME;
    }

    public void Launch(float fallSpeed, float lifeTime, int damage, Action onFinished)
    {
        _fallSpeed = Mathf.Max(0f, fallSpeed);
        _lifeTime = Mathf.Max(0.01f, lifeTime);
        _elapsedTime = 0f;
        _onFinished = onFinished;
        _isFalling = true;

        gameObject.tag = GameConstants.DAMAGEABLE_ENEMY_TAG_NAME;
        _contactDamageController.SetNormalDamage(damage);
        _collider.enabled = true;
        _rbody.simulated = true;
    }

    public void Cancel()
    {
        _onFinished = null;
        PrepareForDisplay();
    }

    private bool IsGroundLayer(int layer)
    {
        return (_groundLayer.value & (1 << layer)) != 0;
    }

    private void Finish()
    {
        Action callback = _onFinished;
        _onFinished = null;
        PrepareForDisplay();
        gameObject.SetActive(false);
        callback?.Invoke();
    }
}

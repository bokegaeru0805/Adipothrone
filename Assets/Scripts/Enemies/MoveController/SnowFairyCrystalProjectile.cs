using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(ContactDamageController))]
public class SnowFairyCrystalProjectile : MonoBehaviour
{
    private Rigidbody2D _rbody;
    private ContactDamageController _contactDamageController;
    private Vector2 _forwardDirection;
    private Vector2 _perpendicularDirection;
    private Vector2 _startPosition;
    private Vector3 _initialScale;
    private float _currentSpeed;
    private float _minimumSpeed;
    private float _deceleration;
    private float _forwardDistance;
    private float _driftStrength;
    private float _driftFrequency;
    private float _driftNoiseOffset;
    private float _initialDriftNoise;
    private float _baseRotationSpeed;
    private float _rotationFluctuation;
    private float _rotationNoiseSpeed;
    private float _rotationNoiseOffset;
    private float _minimumXScaleRate;
    private float _scaleFlipFrequency;
    private float _yScaleFluctuation;
    private float _yScaleFrequency;
    private float _scalePhase;
    private float _lifeTime;
    private float _elapsedTime;

    private void Awake()
    {
        _rbody = GetComponent<Rigidbody2D>();
        _contactDamageController = GetComponent<ContactDamageController>();
        _rbody.gravityScale = 0f;
        _initialScale = transform.localScale;
    }

    private void FixedUpdate()
    {
        if (TimeManager.instance != null && TimeManager.instance.isEnemyMovePaused)
        {
            _rbody.velocity = Vector2.zero;
            _rbody.angularVelocity = 0f;
            return;
        }

        _elapsedTime += Time.fixedDeltaTime;
        if (_elapsedTime >= _lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        _currentSpeed = Mathf.Max(
            _minimumSpeed,
            _currentSpeed - (_deceleration * Time.fixedDeltaTime)
        );
        _forwardDistance += _currentSpeed * Time.fixedDeltaTime;

        float driftNoise = Mathf.PerlinNoise(
            _driftNoiseOffset + (_elapsedTime * _driftFrequency),
            0f
        );
        float driftOffset = (driftNoise - _initialDriftNoise) * 2f * _driftStrength;
        Vector2 targetPosition =
            _startPosition
            + (_forwardDirection * _forwardDistance)
            + (_perpendicularDirection * driftOffset);

        _rbody.MovePosition(targetPosition);
        UpdateRotationAndScale();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(GameConstants.PLAYER_TAG_NAME))
        {
            Destroy(gameObject);
        }
    }

    public void Launch(
        Vector2 direction,
        float speed,
        float minimumSpeed,
        float deceleration,
        float driftStrength,
        float driftFrequency,
        float minimumRotationSpeed,
        float maximumRotationSpeed,
        float rotationFluctuation,
        float rotationNoiseSpeed,
        float minimumXScaleRate,
        float scaleFlipFrequency,
        float yScaleFluctuation,
        float yScaleFrequency,
        float lifeTime,
        int damage
    )
    {
        _forwardDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
        _perpendicularDirection = new Vector2(-_forwardDirection.y, _forwardDirection.x);
        _startPosition = transform.position;
        _initialScale = transform.localScale;
        _currentSpeed = Mathf.Max(0f, speed);
        _minimumSpeed = Mathf.Clamp(minimumSpeed, 0f, _currentSpeed);
        _deceleration = Mathf.Max(0f, deceleration);
        _forwardDistance = 0f;
        _driftStrength = Mathf.Max(0f, driftStrength);
        _driftFrequency = Mathf.Max(0f, driftFrequency);
        _driftNoiseOffset = Random.Range(0f, 1000f);
        _initialDriftNoise = Mathf.PerlinNoise(_driftNoiseOffset, 0f);

        float minRotation = Mathf.Min(minimumRotationSpeed, maximumRotationSpeed);
        float maxRotation = Mathf.Max(minimumRotationSpeed, maximumRotationSpeed);
        _baseRotationSpeed = Random.Range(minRotation, maxRotation);
        if (Random.value < 0.5f)
            _baseRotationSpeed *= -1f;
        _rotationFluctuation = Mathf.Max(0f, rotationFluctuation);
        _rotationNoiseSpeed = Mathf.Max(0f, rotationNoiseSpeed);
        _rotationNoiseOffset = Random.Range(0f, 1000f);

        _minimumXScaleRate = Mathf.Clamp(minimumXScaleRate, 0.05f, 1f);
        _scaleFlipFrequency = Mathf.Max(0f, scaleFlipFrequency)
            * Random.Range(0.85f, 1.15f);
        _yScaleFluctuation = Mathf.Clamp(yScaleFluctuation, 0f, 0.9f);
        _yScaleFrequency = Mathf.Max(0f, yScaleFrequency) * Random.Range(0.85f, 1.15f);
        _scalePhase = Random.Range(0f, Mathf.PI * 2f);
        _lifeTime = Mathf.Max(0.01f, lifeTime);
        _elapsedTime = 0f;
        _rbody.velocity = Vector2.zero;
        _rbody.angularVelocity = 0f;

        gameObject.tag = GameConstants.DAMAGEABLE_ENEMY_TAG_NAME;
        _contactDamageController.SetNormalDamage(damage);
    }

    private void UpdateRotationAndScale()
    {
        float rotationNoise =
            (Mathf.PerlinNoise(
                    _rotationNoiseOffset + (_elapsedTime * _rotationNoiseSpeed),
                    0f
                )
                - 0.5f)
            * 2f;
        float rotationSpeed = _baseRotationSpeed + (rotationNoise * _rotationFluctuation);
        _rbody.MoveRotation(_rbody.rotation + (rotationSpeed * Time.fixedDeltaTime));

        float xFlipWave = Mathf.Abs(
            Mathf.Cos(
                (_elapsedTime * _scaleFlipFrequency * Mathf.PI * 2f) + _scalePhase
            )
        );
        float xScaleRate = Mathf.Lerp(_minimumXScaleRate, 1f, xFlipWave);
        float yScaleRate =
            1f
            + (
                Mathf.Sin(
                    (_elapsedTime * _yScaleFrequency * Mathf.PI * 2f)
                        + (_scalePhase * 0.73f)
                )
                * _yScaleFluctuation
            );
        transform.localScale = new Vector3(
            _initialScale.x * xScaleRate,
            _initialScale.y * yScaleRate,
            _initialScale.z
        );
    }
}

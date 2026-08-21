using UnityEngine;

/// <summary>
/// PassablePlatformに乗っているプレイヤーの位置に応じてシーソーを傾けます。
/// </summary>
public class SeesawController : MonoBehaviour
{
    private const float MinimumDistanceDifference = 0.0001f;

    [Header("参照設定")]
    [SerializeField, Tooltip("プレイヤーの乗車状態を管理する子PassablePlatformのPassengerCarrier")]
    private PassengerCarrier _passengerCarrier;

    [SerializeField, Tooltip("Pivotを原点として回転させるシーソー本体。未設定の場合はこのTransformを使用します")]
    private Transform _rotationTarget;

    [SerializeField, Tooltip("板と同じ傾斜角度を反映するPivotのSpriteRenderer。未設定の場合は子のPivotVisualから取得します")]
    private SpriteRenderer _pivotSpriteRenderer;

    [Header("傾斜設定")]
    [SerializeField, Min(0f), Tooltip("初期角度を基準とした左右共通の最大傾斜角度")]
    private float maxTiltAngle = 20f;

    [SerializeField, Min(0f), Tooltip("Pivot中央で傾斜を発生させない範囲（ローカル座標）")]
    private float centerDeadZone = 0.1f;

    [SerializeField, Min(MinimumDistanceDifference), Tooltip("最高回転速度になるPivotからの横距離")]
    private float maxEffectDistance = 3f;

    [SerializeField, Min(0f), Tooltip("デッドゾーンの外側で使用する最低回転速度（度/秒）")]
    private float minimumTiltSpeed = 5f;

    [SerializeField, Min(0f), Tooltip("最大効果距離で使用する最高回転速度（度/秒）")]
    private float maximumTiltSpeed = 45f;

    [SerializeField, Tooltip("Pivotからの距離率を回転速度率へ変換するカーブ")]
    private AnimationCurve distanceSpeedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [SerializeField, Min(0f), Tooltip("プレイヤーが連続して乗ってから傾斜を開始するまでの時間（秒）。0の場合は即座に傾斜します")]
    private float tiltStartDelay = 0f;

    [Header("Reset設定")]
    [SerializeField, Min(0f), Tooltip("プレイヤーが降りてから初期角度へ戻り始めるまでの時間（秒）")]
    private float resetDelay = 0.3f;

    [SerializeField, Min(0f), Tooltip("初期角度へ戻る回転速度（度/秒）")]
    private float resetSpeed = 30f;

    private Vector3 _initialLocalEulerAngles;
    private float _initialLocalAngle;
    private Vector3 _pivotInitialLocalEulerAngles;
    private float _occupiedElapsedTime;
    private float _unoccupiedElapsedTime;
    private bool _isInitialized;

    private void Awake()
    {
        SanitizeSettings();

        if (_rotationTarget == null)
            _rotationTarget = transform;

        if (_passengerCarrier == null)
            _passengerCarrier = GetComponentInChildren<PassengerCarrier>();

        if (_pivotSpriteRenderer == null)
        {
            Transform pivotVisual = transform.Find("PivotVisual");
            if (pivotVisual != null)
                _pivotSpriteRenderer = pivotVisual.GetComponent<SpriteRenderer>();
        }

        if (_passengerCarrier == null)
        {
            Debug.LogError($"{name}: 子PassablePlatformのPassengerCarrierが設定されていません。", this);
            enabled = false;
            return;
        }

        _initialLocalEulerAngles = _rotationTarget.localEulerAngles;
        _initialLocalAngle = _initialLocalEulerAngles.z;

        if (_pivotSpriteRenderer != null)
            _pivotInitialLocalEulerAngles = _pivotSpriteRenderer.transform.localEulerAngles;

        _isInitialized = true;
    }

    private void FixedUpdate()
    {
        if (!_isInitialized)
            return;

        Heroin_move player = _passengerCarrier.CurrentPlayerPassenger;
        if (player == null)
        {
            _occupiedElapsedTime = 0f;
            UpdateReset();
            return;
        }

        _unoccupiedElapsedTime = 0f;

        _occupiedElapsedTime += Time.fixedDeltaTime;
        if (_occupiedElapsedTime < tiltStartDelay)
            return;

        UpdateTilt(player.transform.position);
    }

    private void UpdateTilt(Vector3 playerWorldPosition)
    {
        float localPlayerPositionX = _rotationTarget.InverseTransformPoint(playerWorldPosition).x;
        float distanceFromPivot = Mathf.Abs(localPlayerPositionX);

        if (distanceFromPivot <= centerDeadZone)
            return;

        float distanceRange = Mathf.Max(
            MinimumDistanceDifference,
            maxEffectDistance - centerDeadZone
        );
        float distanceRate = Mathf.Clamp01((distanceFromPivot - centerDeadZone) / distanceRange);
        float speedRate = Mathf.Clamp01(distanceSpeedCurve.Evaluate(distanceRate));
        float tiltSpeed = Mathf.Lerp(minimumTiltSpeed, maximumTiltSpeed, speedRate);

        // 右側に乗ると時計回り、左側に乗ると反時計回りに傾ける。
        float direction = Mathf.Sign(localPlayerPositionX);
        float targetAngle = _initialLocalAngle - (direction * maxTiltAngle);
        MoveTowardsAngle(targetAngle, tiltSpeed);
    }

    private void UpdateReset()
    {
        _unoccupiedElapsedTime += Time.fixedDeltaTime;
        if (_unoccupiedElapsedTime < resetDelay)
            return;

        MoveTowardsAngle(_initialLocalAngle, resetSpeed);
    }

    private void MoveTowardsAngle(float targetAngle, float speed)
    {
        float currentAngle = _rotationTarget.localEulerAngles.z;
        float nextAngle = Mathf.MoveTowardsAngle(
            currentAngle,
            targetAngle,
            speed * Time.fixedDeltaTime
        );

        Vector3 nextEulerAngles = _initialLocalEulerAngles;
        nextEulerAngles.z = nextAngle;
        _rotationTarget.localRotation = Quaternion.Euler(nextEulerAngles);

        SynchronizePivotRotation(nextAngle);
    }

    private void SynchronizePivotRotation(float rotationTargetAngle)
    {
        if (_pivotSpriteRenderer == null)
            return;

        Transform pivotTransform = _pivotSpriteRenderer.transform;

        // 同一Transformまたは回転対象の子なら、既に回転が反映されるため追加処理しない。
        if (pivotTransform == _rotationTarget || pivotTransform.IsChildOf(_rotationTarget))
            return;

        float tiltAngle = Mathf.DeltaAngle(_initialLocalAngle, rotationTargetAngle);
        Vector3 pivotEulerAngles = _pivotInitialLocalEulerAngles;
        pivotEulerAngles.z += tiltAngle;
        pivotTransform.localRotation = Quaternion.Euler(pivotEulerAngles);
    }

    private void SanitizeSettings()
    {
        maxTiltAngle = Mathf.Max(0f, maxTiltAngle);
        centerDeadZone = Mathf.Max(0f, centerDeadZone);
        maxEffectDistance = Mathf.Max(centerDeadZone + MinimumDistanceDifference, maxEffectDistance);
        minimumTiltSpeed = Mathf.Max(0f, minimumTiltSpeed);
        maximumTiltSpeed = Mathf.Max(minimumTiltSpeed, maximumTiltSpeed);
        tiltStartDelay = Mathf.Max(0f, tiltStartDelay);
        resetDelay = Mathf.Max(0f, resetDelay);
        resetSpeed = Mathf.Max(0f, resetSpeed);

        if (distanceSpeedCurve == null || distanceSpeedCurve.length == 0)
            distanceSpeedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }

    private void OnValidate()
    {
        SanitizeSettings();
    }
}

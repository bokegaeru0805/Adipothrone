using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// チェーンに繋がれた鉄球を振り子のように滑らかに動かすオブジェクトの制御クラス。
/// 右真横（X軸の正の向き）を基準の0度として動作します。
/// </summary>
public class PendulumBallMoveController : MonoBehaviour, IEnemyResettable
{
    #region 列挙型

    private enum EnemyVariant
    {
        None = 0,
        Tower = 1,
    }

    private enum MoveType
    {
        Pendulum = 0,
        ContinuousRotation = 1,
    }

    private enum RotationDirection
    {
        Clockwise = 0,
        Counterclockwise = 1,
    }

    #endregion

    #region インスペクター設定

    [Header("敵のタイプ")]
    [SerializeField]
    private EnemyVariant _variantType = EnemyVariant.Tower;

    [Header("ダメージ設定")]
    [SerializeField, Tooltip("接触ダメージを与える鉄球のコントローラー")]
    private ContactDamageController _contactDamageController = null;

    [SerializeField, Min(0f), Tooltip("親SpriteRendererの幅から差し引く鉄球位置の補正値")]
    private float _contactDamagePositionOffset = 1.25f;

    [Header("回転運動の設定")]
    [SerializeField, Tooltip("振り子運動または360度の連続回転を選択します")]
    private MoveType _moveType = MoveType.Pendulum;

    [SerializeField, Tooltip("運動の起点となる角度（度数法：右真横が0度）")]
    [ShowIf(nameof(IsPendulumMovement))]
    private float _startAngle = -45f;

    [SerializeField, Tooltip("運動の終点となる角度（度数法：右真横が0度）")]
    [ShowIf(nameof(IsPendulumMovement))]
    private float _endAngle = 45f;

    [SerializeField, Tooltip("回転運動の速度（度/秒）")]
    private float _moveSpeed = 90f;

    [
        SerializeField,
        Range(0f, 3f),
        Tooltip(
            "端点での減速の強さ（0: 一定速度で折り返し, 1: 標準的な加減速, 大きいほど端で長く止まる）"
        )
    ]
    [ShowIf(nameof(IsPendulumMovement))]
    private float _decelerationPower = 1.0f;

    [SerializeField, ShowIf(nameof(IsContinuousRotation))]
    [Tooltip("360度回転時の回転方向")]
    private RotationDirection _rotationDirection = RotationDirection.Clockwise;

    [Header("初期位置タイミング設定")]
    [SerializeField, Tooltip("有効にすると、配置時やリセット時の開始位置を完全にランダムにします")]
    [ShowIf(nameof(IsPendulumMovement))]
    private bool _isRandomStart = false;

    [SerializeField, Range(0f, 1f)]
    [ShowIf(nameof(ShowNormalizedStartTime))]
    [Tooltip(
        "1周期内における開始位置のタイミング（0:起点角度, 0.25:中間から終点へ, 0.5:終点角度, 0.75:中間から起点へ）"
    )]
    private float _normalizedStartTime = 0f;

    #endregion

    #region 内部変数
    private int _damageValue = 20;
    private float _initialOffsetAngle;
    private float _elapsedTime;
    private SpriteRenderer _spriteRenderer;

    private bool IsPendulumMovement => _moveType == MoveType.Pendulum;
    private bool IsContinuousRotation => _moveType == MoveType.ContinuousRotation;
    private bool ShowNormalizedStartTime => IsPendulumMovement && !_isRandomStart;

    #endregion

    #region Unityイベント関数

    private void Awake()
    {
        // EnemyActivatorはStartより先にResetStateを呼ぶ場合があるため、配置時の角度はAwakeで保存する
        _initialOffsetAngle = transform.eulerAngles.z;

        _spriteRenderer = GetComponent<SpriteRenderer>();
        UpdateContactDamagePosition();

        // バリアントに基づく設定の再現（必要に応じて拡張可能）
        switch (_variantType)
        {
            case EnemyVariant.Tower:
                _damageValue = 20;
                break;
            default:
                Debug.LogError($"{name} の EnemyVariant が正しく設定されていません。");
                break;
        }
    }

    private void Start()
    {
        ResetState();
    }

    private void FixedUpdate()
    {
        UpdateContactDamagePosition();

        // ゲーム内時間停止マネージャーの状況を確認
        if (TimeManager.instance != null && TimeManager.instance.isEnemyMovePaused)
        {
            return;
        }

        // 速度が0以下の場合は計算をスキップして停止させる
        if (_moveSpeed <= 0f)
        {
            return;
        }

        // 物理更新に合わせて経過時間を進める
        _elapsedTime += Time.fixedDeltaTime;

        // 現在の角度を計算して適用
        UpdateMovementRotation();
    }

    #endregion

    #region 初期化・リセット処理

    /// <summary>
    /// オブジェクトの状態を指定された初期位置タイミングへリセットし、ダメージ値を再適用します。
    /// </summary>
    public void ResetState()
    {
        UpdateContactDamagePosition();

        // 鉄球部分の ContactDamageController にダメージ値を適用
        if (_contactDamageController != null)
        {
            _contactDamageController.SetNormalDamage(_damageValue);
        }
        else
        {
            Debug.LogError($"{name} に ContactDamageController が設定されていません。");
        }

        float fullCycleDuration = GetFullCycleDuration();

        // 速度が0、または移動距離がない場合は処理をスキップ
        if (fullCycleDuration <= 0f)
        {
            _elapsedTime = 0f;
            UpdateMovementRotation();
            return;
        }

        if (IsContinuousRotation)
        {
            _elapsedTime = 0f;
            UpdateMovementRotation();
            return;
        }

        // ランダム設定が有効な場合は 0.0 〜 1.0 のランダム値を採用
        float startTimeRatio = _isRandomStart ? Random.Range(0f, 1f) : _normalizedStartTime;

        // 選択されたタイミングに対応する経過時間を設定
        _elapsedTime = startTimeRatio * fullCycleDuration;

        // 即座に計算上の回転角度を適用
        UpdateMovementRotation();
    }

    #endregion

    #region 鉄球位置の更新

    /// <summary>
    /// 親SpriteRendererの幅に合わせて、接触ダメージを持つ鉄球のローカルX座標を更新します。
    /// </summary>
    private void UpdateContactDamagePosition()
    {
        if (_contactDamageController == null)
        {
            return;
        }

        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (_spriteRenderer == null)
        {
            return;
        }

        Transform contactDamageTransform = _contactDamageController.transform;
        Vector3 localPosition = contactDamageTransform.localPosition;
        localPosition.x = _spriteRenderer.size.x - _contactDamagePositionOffset;
        contactDamageTransform.localPosition = localPosition;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        UpdateContactDamagePosition();
    }
#endif

    #endregion

    #region 振り子計算・更新処理

    /// <summary>
    /// 選択されている移動タイプに応じたZ軸回転を適用します。
    /// </summary>
    private void UpdateMovementRotation()
    {
        if (IsContinuousRotation)
        {
            UpdateContinuousRotation();
            return;
        }

        UpdatePendulumRotation();
    }

    /// <summary>
    /// 指定方向へ360度回転し続ける角度を適用します。
    /// </summary>
    private void UpdateContinuousRotation()
    {
        float direction = _rotationDirection == RotationDirection.Clockwise ? -1f : 1f;
        float angle = Mathf.Repeat(_elapsedTime * _moveSpeed, 360f) * direction;
        ApplyZRotation(angle);
    }

    /// <summary>
    /// 現在の経過時間から正確な回転角度を算出し、Transformに適用します。
    /// </summary>
    private void UpdatePendulumRotation()
    {
        float fullCycleDuration = GetFullCycleDuration();

        // 0割りを防ぎ、距離や速度がない場合は起点に固定する
        if (fullCycleDuration <= 0f)
        {
            ApplyZRotation(_startAngle);
            return;
        }

        // 現在の1周期内での進行度（0.0 〜 1.0）
        float cycleProgress = (_elapsedTime % fullCycleDuration) / fullCycleDuration;

        // 進行度をもとに現在の角度を取得
        float currentPendulumAngle = GetAngleAtProgress(cycleProgress);

        ApplyZRotation(currentPendulumAngle);
    }

    /// <summary>
    /// Z軸の回転をオフセットを加味して適用します。
    /// </summary>
    /// <param name="localAngle">適用するローカルの角度</param>
    private void ApplyZRotation(float localAngle)
    {
        float finalZRotation = _initialOffsetAngle + localAngle;
        transform.rotation = Quaternion.Euler(0f, 0f, finalZRotation);
    }

    /// <summary>
    /// 指定された移動速度と距離から、往復（1周期）にかかる合計時間を算出します。
    /// </summary>
    /// <returns>1周期の合計時間（秒）</returns>
    private float GetFullCycleDuration()
    {
        float totalDistance = Mathf.Abs(_endAngle - _startAngle);
        if (totalDistance <= 0.001f || _moveSpeed <= 0.001f)
        {
            return 0f;
        }

        // 片道の時間 = 距離 / 速度。往復はその2倍
        return (totalDistance / _moveSpeed) * 2f;
    }

    /// <summary>
    /// 1周期の進行度（0.0～1.0）から、イージングを加味した現在の角度を算出します。
    /// </summary>
    /// <param name="cycleProgress">1周期内の進行度</param>
    /// <returns>現在の計算上の角度</returns>
    private float GetAngleAtProgress(float cycleProgress)
    {
        // 0.0~0.5は起点から終点へ、0.5~1.0は終点から起点へ
        bool isMovingToEnd = cycleProgress < 0.5f;

        // 現在の方向に対するローカルな進行度 (0.0 〜 1.0) に変換
        float localTime = isMovingToEnd ? (cycleProgress * 2f) : ((cycleProgress - 0.5f) * 2f);

        // 減速の強さを計算（スライダーの 0〜3 をべき乗の 1〜4 に変換）
        float power = 1f + _decelerationPower;
        float easedProgress = EaseInOut(localTime, power);

        // イージング結果をもとに角度を補間
        if (isMovingToEnd)
        {
            return Mathf.Lerp(_startAngle, _endAngle, easedProgress);
        }
        else
        {
            return Mathf.Lerp(_endAngle, _startAngle, easedProgress);
        }
    }

    /// <summary>
    /// 進行度（0.0～1.0）に対して、指定された累乗による滑らかな加減速（Ease-In-Out）を適用します。
    /// </summary>
    /// <param name="t">進行度</param>
    /// <param name="power">累乗の強さ</param>
    /// <returns>イージングが適用された進行度</returns>
    private float EaseInOut(float t, float power)
    {
        if (t < 0.5f)
        {
            return 0.5f * Mathf.Pow(2f * t, power);
        }
        else
        {
            return 1f - 0.5f * Mathf.Pow(2f * (1f - t), power);
        }
    }

    #endregion

    #region デバッグ描画

#if UNITY_EDITOR
    /// <summary>
    /// インスペクターでオブジェクトが選択されたときに、振り子の可動範囲と初期位置をギズモとして描画します。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // SpriteRenderer側のWidth変更はOnValidateで検知できない場合があるため、
        // 選択中はSceneビューの再描画に合わせて鉄球位置を同期する
        UpdateContactDamagePosition();

        // ゲーム実行中はAwake時にキャッシュした初期オフセットを使い、エディタ編集時は現在のZ軸回転を基準にする
        float offset = Application.isPlaying ? _initialOffsetAngle : transform.eulerAngles.z;

        // SpriteRendererのTiledのWidthを、TransformのScaleを含むワールド距離へ変換する
        float localRadius = 1.0f;
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            localRadius = spriteRenderer.size.x;
        }

        float outerRadius = GetWorldRadius(localRadius);
        float contactCenterRadius = GetWorldRadius(
            Mathf.Max(0f, localRadius - _contactDamagePositionOffset)
        );
        float innerRadius = GetWorldRadius(
            Mathf.Max(0f, localRadius - _contactDamagePositionOffset * 2f)
        );

        Vector3 center = transform.position;

        if (IsContinuousRotation)
        {
            DrawContinuousRotationGizmo(
                center,
                contactCenterRadius,
                innerRadius,
                outerRadius
            );
            return;
        }

        float finalStartAngle = offset + _startAngle;
        float totalAngle = _endAngle - _startAngle;

        // 1. 振り子の可動範囲を扇形（緑色の半透明）で描画
        UnityEditor.Handles.color = new Color(0f, 1f, 0f, 0.1f);
        UnityEditor.Handles.DrawSolidArc(
            center,
            Vector3.forward,
            GetDirectionFromAngle(finalStartAngle),
            totalAngle,
            outerRadius
        );

        // 2. 扇形の外枠（円弧）を描画
        UnityEditor.Handles.color = new Color(0f, 1f, 0f, 0.6f);
        UnityEditor.Handles.DrawWireArc(
            center,
            Vector3.forward,
            GetDirectionFromAngle(finalStartAngle),
            totalAngle,
            outerRadius
        );

        DrawContactDamageRangeGizmo(
            center,
            finalStartAngle,
            totalAngle,
            innerRadius,
            contactCenterRadius,
            outerRadius
        );

        // 起点と終点の境界線を描画
        Gizmos.color = new Color(0f, 1f, 0f, 0.6f);
        Gizmos.DrawLine(center, center + GetDirectionFromAngle(finalStartAngle) * outerRadius);
        Gizmos.DrawLine(
            center,
            center + GetDirectionFromAngle(offset + _endAngle) * outerRadius
        );

        // 3. ランダム開始でない場合、ResetState時の初期角度を赤線で目立たせて描画
        if (!_isRandomStart)
        {
            // 進行度に応じた角度を算出し、オフセットを加える
            float initialAngle = GetAngleAtProgress(_normalizedStartTime);
            float finalInitialAngle = offset + initialAngle;

            Vector3 initialDir = GetDirectionFromAngle(finalInitialAngle);

            // 初期位置を太い赤線で強調描画
            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.DrawLine(center, center + initialDir * outerRadius, 3f);
        }
    }

    /// <summary>
    /// 度数法の角度から方向ベクトルを算出します（右真横が0度）。
    /// </summary>
    /// <param name="angleDegrees">度数法の角度</param>
    /// <returns>計算された方向のベクトル</returns>
    private Vector3 GetDirectionFromAngle(float angleDegrees)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
    }

    /// <summary>
    /// ローカルX軸上の半径を、TransformのScaleを含むワールド距離へ変換します。
    /// </summary>
    private float GetWorldRadius(float localRadius)
    {
        return transform.TransformVector(Vector3.right * localRadius).magnitude;
    }

    /// <summary>
    /// 360度回転の軌道と回転方向を示す矢印を描画します。
    /// </summary>
    private void DrawContinuousRotationGizmo(
        Vector3 center,
        float contactCenterRadius,
        float innerRadius,
        float outerRadius
    )
    {
        Color gizmoColor =
            _rotationDirection == RotationDirection.Clockwise
                ? new Color(1f, 0.6f, 0f, 0.9f)
                : new Color(0f, 0.7f, 1f, 0.9f);

        UnityEditor.Handles.color = gizmoColor;
        UnityEditor.Handles.DrawWireDisc(center, Vector3.forward, outerRadius);

        DrawContactDamageRangeGizmo(
            center,
            0f,
            360f,
            innerRadius,
            contactCenterRadius,
            outerRadius
        );

        // 円の右端に、回転方向を示す接線方向の矢印を描く
        Vector3 arrowTip = center + Vector3.right * contactCenterRadius;
        Vector3 tangent =
            _rotationDirection == RotationDirection.Clockwise ? Vector3.down : Vector3.up;
        float arrowLength = Mathf.Max(contactCenterRadius * 0.2f, 0.25f);
        Vector3 arrowBase = arrowTip - tangent * arrowLength;
        Vector3 arrowSideA = Quaternion.Euler(0f, 0f, 30f) * -tangent;
        Vector3 arrowSideB = Quaternion.Euler(0f, 0f, -30f) * -tangent;

        UnityEditor.Handles.DrawLine(arrowBase, arrowTip, 3f);
        UnityEditor.Handles.DrawLine(
            arrowTip,
            arrowTip + arrowSideA * arrowLength * 0.5f,
            3f
        );
        UnityEditor.Handles.DrawLine(
            arrowTip,
            arrowTip + arrowSideB * arrowLength * 0.5f,
            3f
        );
    }

    /// <summary>
    /// 鉄球本体が通過する範囲を、内周から外周までの半透明の帯として描画します。
    /// </summary>
    private void DrawContactDamageRangeGizmo(
        Vector3 center,
        float startAngle,
        float sweepAngle,
        float innerRadius,
        float contactCenterRadius,
        float outerRadius
    )
    {
        const int FullCircleSegments = 72;
        int segmentCount = Mathf.Max(
            1,
            Mathf.CeilToInt(Mathf.Abs(sweepAngle) / 360f * FullCircleSegments)
        );
        Color rangeColor = new Color(1f, 0.2f, 0.1f, 0.18f);

        UnityEditor.Handles.color = rangeColor;
        for (int i = 0; i < segmentCount; i++)
        {
            float angleA = startAngle + sweepAngle * i / segmentCount;
            float angleB = startAngle + sweepAngle * (i + 1) / segmentCount;
            Vector3 directionA = GetDirectionFromAngle(angleA);
            Vector3 directionB = GetDirectionFromAngle(angleB);

            UnityEditor.Handles.DrawAAConvexPolygon(
                center + directionA * innerRadius,
                center + directionA * outerRadius,
                center + directionB * outerRadius,
                center + directionB * innerRadius
            );
        }

        UnityEditor.Handles.color = new Color(1f, 0.2f, 0.1f, 0.8f);
        UnityEditor.Handles.DrawWireArc(
            center,
            Vector3.forward,
            GetDirectionFromAngle(startAngle),
            sweepAngle,
            innerRadius
        );
        UnityEditor.Handles.DrawWireArc(
            center,
            Vector3.forward,
            GetDirectionFromAngle(startAngle),
            sweepAngle,
            outerRadius
        );

        UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.9f);
        UnityEditor.Handles.DrawWireArc(
            center,
            Vector3.forward,
            GetDirectionFromAngle(startAngle),
            sweepAngle,
            contactCenterRadius
        );
    }
#endif

    #endregion
}

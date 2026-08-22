using UnityEngine;
using NaughtyAttributes;

/// <summary>
/// Tiledスプライトで表現された間欠泉の伸縮と、子オブジェクトの範囲を管理します。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class GeyserController : MonoBehaviour, IEnemyResettable
{
    private const float MinimumSize = 0.0001f;
    private const float MinimumLoweredHeight = 2f;
    private const float MinimumEnvironmentAreaHeight = 0.01f;
    private static readonly Color RaisedGizmoColor = new Color(1f, 0.85f, 0.1f, 1f);
    private static readonly Color LoweredGizmoColor = new Color(1f, 0.2f, 0.65f, 1f);

    private enum OperationMode
    {
        Normal = 0,
        Raised = 1,
        Lowered = 2
    }

    private enum InitialState
    {
        Raised = 0,
        Lowered = 1
    }

    [Header("動作設定")]
    [SerializeField, Tooltip("通常動作、上昇位置で停止、下降位置で停止から選択します")]
    private OperationMode operationMode = OperationMode.Normal;

    [SerializeField, ShowIf(nameof(IsNormalMode)), Tooltip("通常動作を開始する基準状態")]
    private InitialState initialState = InitialState.Lowered;

    [SerializeField, ShowIf(nameof(IsNormalMode)), Min(0f), Tooltip("開始状態を基準に、周期を何秒進めた位置から開始するか")]
    private float initialElapsedTime;

    [Header("高さ・速度設定")]
    [SerializeField, ShowIf(nameof(UsesRaisedHeight)), Min(MinimumLoweredHeight), Tooltip("上昇状態でのSpriteRendererの高さ")]
    private float raisedHeight = 5f;

    [SerializeField, ShowIf(nameof(UsesLoweredHeight)), Min(MinimumLoweredHeight), Tooltip("下降状態でのSpriteRendererの高さ（最小2）")]
    private float loweredHeight = MinimumLoweredHeight;

    [SerializeField, ShowIf(nameof(IsNormalMode)), Min(MinimumSize), Tooltip("1秒あたりの上昇量")]
    private float risingSpeed = 1f;

    [SerializeField, ShowIf(nameof(IsNormalMode)), Min(0f), Tooltip("上昇位置で停止する秒数")]
    private float raisedWaitDuration = 1f;

    [SerializeField, ShowIf(nameof(IsNormalMode)), Min(MinimumSize), Tooltip("1秒あたりの下降量")]
    private float loweringSpeed = 1f;

    [SerializeField, ShowIf(nameof(IsNormalMode)), Min(0f), Tooltip("下降位置で停止する秒数")]
    private float loweredWaitDuration = 1f;

    [Header("子オブジェクト設定")]
    [SerializeField, Tooltip("横幅を間欠泉に一度だけ合わせるPassablePlatformのSpriteRenderer")]
    private SpriteRenderer _passablePlatformSpriteRenderer;

    [SerializeField, Tooltip("間欠泉の範囲に追従させるEnvironmentAreaのBoxCollider2D")]
    private BoxCollider2D _environmentAreaCollider;

    [SerializeField, Min(0f), Tooltip("EnvironmentAreaの上端を、間欠泉の上端から下へずらす距離")]
    private float environmentTopOffset;

    [Header("覆いスプライト設定")]
    [SerializeField, Tooltip("本体と同じ形状・アニメーションを常時反映する子SpriteRenderer")]
    private SpriteRenderer _overlaySpriteRenderer;

    [Header("Gizmo設定")]
    [SerializeField, Tooltip("Sceneビューに動作時の高さを表示するか")]
    private bool isShowingGizmos = true;

    private SpriteRenderer _spriteRenderer;
    private Animator _animator;
    private Animator _overlayAnimator;
    private Vector3 _fixedBottomPosition;
    private float _cycleElapsedTime;
    private bool _isInitialized;

    private void Awake()
    {
        SanitizeSettings();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _animator = GetComponent<Animator>();
        if (_overlaySpriteRenderer != null)
            _overlayAnimator = _overlaySpriteRenderer.GetComponent<Animator>();

        // Top Pivotからローカル下方向へ現在の高さ分進んだ位置を、伸縮時に動かさない底辺として保存する。
        _fixedBottomPosition = transform.TransformPoint(Vector3.down * _spriteRenderer.size.y);

        UpdateChildWidths();
        InitializeOverlayAnimator();
        SynchronizeOverlay();
        _isInitialized = true;
        ResetState();
    }

    private void Update()
    {
        if (!_isInitialized || operationMode != OperationMode.Normal)
            return;

        _cycleElapsedTime = NormalizeCycleTime(_cycleElapsedTime + Time.deltaTime);
        ApplyHeight(EvaluateNormalHeight(_cycleElapsedTime));
    }

    private void LateUpdate()
    {
        if (!_isInitialized)
            return;

        SynchronizeOverlay();
    }

    /// <summary>
    /// Inspectorで設定した開始状態と経過秒数へ間欠泉を戻します。
    /// PassablePlatformへ適用済みの横幅は元に戻しません。
    /// </summary>
    public void ResetState()
    {
        if (!_isInitialized)
            return;

        switch (operationMode)
        {
            case OperationMode.Raised:
                _cycleElapsedTime = 0f;
                ApplyHeight(raisedHeight);
                break;

            case OperationMode.Lowered:
                _cycleElapsedTime = 0f;
                ApplyHeight(loweredHeight);
                break;

            default:
                _cycleElapsedTime = NormalizeCycleTime(initialElapsedTime);
                ApplyHeight(EvaluateNormalHeight(_cycleElapsedTime));
                break;
        }
    }

    private float EvaluateNormalHeight(float elapsedTime)
    {
        float heightDifference = Mathf.Abs(raisedHeight - loweredHeight);
        float risingDuration = heightDifference / Mathf.Max(risingSpeed, MinimumSize);
        float loweringDuration = heightDifference / Mathf.Max(loweringSpeed, MinimumSize);
        float cycleDuration = raisedWaitDuration + loweringDuration
            + loweredWaitDuration + risingDuration;

        if (cycleDuration <= 0f || heightDifference <= 0f)
            return initialState == InitialState.Raised ? raisedHeight : loweredHeight;

        float cycleTime = Mathf.Repeat(elapsedTime, cycleDuration);

        if (initialState == InitialState.Raised)
        {
            return EvaluateFromRaised(
                cycleTime,
                risingDuration,
                loweringDuration
            );
        }

        return EvaluateFromLowered(
            cycleTime,
            risingDuration,
            loweringDuration
        );
    }

    private float EvaluateFromRaised(
        float cycleTime,
        float risingDuration,
        float loweringDuration
    )
    {
        if (cycleTime < raisedWaitDuration)
            return raisedHeight;

        cycleTime -= raisedWaitDuration;
        if (cycleTime < loweringDuration)
            return Mathf.MoveTowards(raisedHeight, loweredHeight, loweringSpeed * cycleTime);

        cycleTime -= loweringDuration;
        if (cycleTime < loweredWaitDuration)
            return loweredHeight;

        cycleTime -= loweredWaitDuration;
        if (cycleTime < risingDuration)
            return Mathf.MoveTowards(loweredHeight, raisedHeight, risingSpeed * cycleTime);

        return raisedHeight;
    }

    private float EvaluateFromLowered(
        float cycleTime,
        float risingDuration,
        float loweringDuration
    )
    {
        if (cycleTime < loweredWaitDuration)
            return loweredHeight;

        cycleTime -= loweredWaitDuration;
        if (cycleTime < risingDuration)
            return Mathf.MoveTowards(loweredHeight, raisedHeight, risingSpeed * cycleTime);

        cycleTime -= risingDuration;
        if (cycleTime < raisedWaitDuration)
            return raisedHeight;

        cycleTime -= raisedWaitDuration;
        if (cycleTime < loweringDuration)
            return Mathf.MoveTowards(raisedHeight, loweredHeight, loweringSpeed * cycleTime);

        return loweredHeight;
    }

    private float NormalizeCycleTime(float elapsedTime)
    {
        float heightDifference = Mathf.Abs(raisedHeight - loweredHeight);
        float risingDuration = heightDifference / Mathf.Max(risingSpeed, MinimumSize);
        float loweringDuration = heightDifference / Mathf.Max(loweringSpeed, MinimumSize);
        float cycleDuration = raisedWaitDuration + loweringDuration
            + loweredWaitDuration + risingDuration;

        return cycleDuration > 0f ? Mathf.Repeat(Mathf.Max(0f, elapsedTime), cycleDuration) : 0f;
    }

    private void ApplyHeight(float height)
    {
        height = Mathf.Max(MinimumSize, height);

        Vector2 size = _spriteRenderer.size;
        size.y = height;
        _spriteRenderer.size = size;

        // Rotation.zにかかわらず、保存した底辺からローカル上方向へ伸びるように本体位置を補正する。
        Vector3 bottomOffset = transform.TransformVector(Vector3.down * height);
        transform.position = _fixedBottomPosition - bottomOffset;

        SynchronizeEnvironmentArea(height);
    }

    private void SynchronizePassablePlatformWidth()
    {
        if (_passablePlatformSpriteRenderer == null)
            return;

        Vector2 platformSize = _passablePlatformSpriteRenderer.size;
        platformSize.x = _spriteRenderer.size.x;
        _passablePlatformSpriteRenderer.size = platformSize;

        Vector3 platformPosition = _passablePlatformSpriteRenderer.transform.localPosition;
        platformPosition.x = 0f;
        _passablePlatformSpriteRenderer.transform.localPosition = platformPosition;
    }

    /// <summary>
    /// PassablePlatformとEnvironmentAreaの横幅を間欠泉に合わせ、ローカルX座標を中央へ戻します。
    /// </summary>
    [Button("子オブジェクトの横幅を更新")]
    private void UpdateChildWidths()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_spriteRenderer == null)
            return;

        SynchronizePassablePlatformWidth();
        SynchronizeEnvironmentAreaWidth();
    }

    private void SynchronizeEnvironmentAreaWidth()
    {
        if (_environmentAreaCollider == null)
            return;

        Transform areaTransform = _environmentAreaCollider.transform;
        Vector3 areaPosition = areaTransform.localPosition;
        areaPosition.x = 0f;
        areaTransform.localPosition = areaPosition;

        float colliderWidth = Mathf.Abs(_environmentAreaCollider.size.x);
        if (Mathf.Approximately(colliderWidth, 0f))
            return;

        Vector3 areaScale = areaTransform.localScale;
        areaScale.x = _spriteRenderer.size.x / colliderWidth;
        areaTransform.localScale = areaScale;
    }

    private void SynchronizeEnvironmentArea(float geyserHeight)
    {
        if (_environmentAreaCollider == null)
            return;

        Transform areaTransform = _environmentAreaCollider.transform;
        Vector3 areaPosition = areaTransform.localPosition;
        areaPosition.x = 0f;
        areaTransform.localPosition = areaPosition;

        Vector2 colliderSize = _environmentAreaCollider.size;
        if (Mathf.Approximately(colliderSize.x, 0f) || Mathf.Approximately(colliderSize.y, 0f))
            return;

        float areaHeight = geyserHeight - environmentTopOffset;
        Vector3 areaScale = areaTransform.localScale;
        areaScale.x = _spriteRenderer.size.x / Mathf.Abs(colliderSize.x);

        if (areaHeight < MinimumEnvironmentAreaHeight)
        {
            _environmentAreaCollider.enabled = false;
            areaScale.y = MinimumEnvironmentAreaHeight / Mathf.Abs(colliderSize.y);
            areaTransform.localScale = areaScale;

            return;
        }

        _environmentAreaCollider.enabled = true;
        areaScale.y = areaHeight / Mathf.Abs(colliderSize.y);
        areaTransform.localScale = areaScale;

        float areaCenterY = -(environmentTopOffset + geyserHeight) * 0.5f;
        areaPosition.y = areaCenterY - (_environmentAreaCollider.offset.y * areaScale.y);
        areaTransform.localPosition = areaPosition;
    }

    private void InitializeOverlayAnimator()
    {
        if (_animator == null || _overlayAnimator == null)
            return;

        _overlayAnimator.runtimeAnimatorController = _animator.runtimeAnimatorController;
    }

    private void SynchronizeOverlay()
    {
        if (_overlaySpriteRenderer != null)
        {
            _overlaySpriteRenderer.sprite = _spriteRenderer.sprite;
            _overlaySpriteRenderer.drawMode = _spriteRenderer.drawMode;
            _overlaySpriteRenderer.tileMode = _spriteRenderer.tileMode;
            _overlaySpriteRenderer.adaptiveModeThreshold = _spriteRenderer.adaptiveModeThreshold;
            _overlaySpriteRenderer.size = _spriteRenderer.size;
            _overlaySpriteRenderer.flipX = _spriteRenderer.flipX;
            _overlaySpriteRenderer.flipY = _spriteRenderer.flipY;

            Transform overlayTransform = _overlaySpriteRenderer.transform;
            Vector3 overlayPosition = overlayTransform.localPosition;
            overlayPosition.x = 0f;
            overlayPosition.y = 0f;
            overlayTransform.localPosition = overlayPosition;
            overlayTransform.localRotation = Quaternion.identity;
            overlayTransform.localScale = Vector3.one;
        }

        SynchronizeOverlayAnimator();
    }

    private void SynchronizeOverlayAnimator()
    {
        if (_animator == null || _overlayAnimator == null
            || _animator.runtimeAnimatorController == null)
        {
            return;
        }

        if (_overlayAnimator.runtimeAnimatorController != _animator.runtimeAnimatorController)
            _overlayAnimator.runtimeAnimatorController = _animator.runtimeAnimatorController;

        _overlayAnimator.speed = _animator.speed;

        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Float:
                    _overlayAnimator.SetFloat(parameter.nameHash, _animator.GetFloat(parameter.nameHash));
                    break;

                case AnimatorControllerParameterType.Int:
                    _overlayAnimator.SetInteger(parameter.nameHash, _animator.GetInteger(parameter.nameHash));
                    break;

                case AnimatorControllerParameterType.Bool:
                    _overlayAnimator.SetBool(parameter.nameHash, _animator.GetBool(parameter.nameHash));
                    break;
            }
        }

        int layerCount = Mathf.Min(_animator.layerCount, _overlayAnimator.layerCount);
        for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
        {
            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(layerIndex);
            _overlayAnimator.Play(state.fullPathHash, layerIndex, state.normalizedTime);
            _overlayAnimator.SetLayerWeight(layerIndex, _animator.GetLayerWeight(layerIndex));
        }
    }

    private void SanitizeSettings()
    {
        raisedHeight = Mathf.Max(MinimumLoweredHeight, raisedHeight);
        loweredHeight = Mathf.Clamp(loweredHeight, MinimumLoweredHeight, raisedHeight);
        risingSpeed = Mathf.Max(MinimumSize, risingSpeed);
        loweringSpeed = Mathf.Max(MinimumSize, loweringSpeed);
        raisedWaitDuration = Mathf.Max(0f, raisedWaitDuration);
        loweredWaitDuration = Mathf.Max(0f, loweredWaitDuration);
        initialElapsedTime = Mathf.Max(0f, initialElapsedTime);
        environmentTopOffset = Mathf.Max(0f, environmentTopOffset);
    }

    private bool IsNormalMode()
    {
        return operationMode == OperationMode.Normal;
    }

    private bool UsesRaisedHeight()
    {
        return operationMode == OperationMode.Normal || operationMode == OperationMode.Raised;
    }

    private bool UsesLoweredHeight()
    {
        return operationMode == OperationMode.Normal || operationMode == OperationMode.Lowered;
    }

    private void OnDrawGizmos()
    {
        if (!isShowingGizmos)
            return;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Vector3 bottomPosition = transform.TransformPoint(Vector3.down * renderer.size.y);
        Gizmos.matrix = Matrix4x4.TRS(bottomPosition, transform.rotation, transform.lossyScale);

        if (operationMode == OperationMode.Normal || operationMode == OperationMode.Raised)
            DrawHeightGizmo(renderer.size.x, raisedHeight, RaisedGizmoColor);

        if (operationMode == OperationMode.Normal || operationMode == OperationMode.Lowered)
            DrawHeightGizmo(renderer.size.x, loweredHeight, LoweredGizmoColor);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private static void DrawHeightGizmo(float width, float height, Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawWireCube(
            new Vector3(0f, height * 0.5f, 0f),
            new Vector3(width, height, 0f)
        );
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SanitizeSettings();
    }
#endif
}

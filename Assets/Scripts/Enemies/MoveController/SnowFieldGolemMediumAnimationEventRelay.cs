using UnityEngine;

/// <summary>
/// 子オブジェクト側のAnimatorから発生したAnimation Eventを、
/// 親のSnowFieldGolemMediumMoveControllerへ転送します。
/// Animatorと同じGameObjectへ追加してください。
/// </summary>
public class SnowFieldGolemMediumAnimationEventRelay : MonoBehaviour
{
    [SerializeField]
    [Tooltip("転送先の移動コントローラー。未設定の場合は親階層から自動取得します。")]
    private SnowFieldGolemMediumMoveController _moveController = null;

    private void Awake()
    {
        if (_moveController == null)
            _moveController = GetComponentInParent<SnowFieldGolemMediumMoveController>();

        if (_moveController == null)
        {
            Debug.LogError(
                $"{name}: SnowFieldGolemMediumMoveControllerを親階層から取得できませんでした。",
                this
            );
        }
    }

    /// <summary>
    /// Attack_Upper、Attack_Lower、JumpStartのAnimation Eventから呼び出します。
    /// </summary>
    public void OnSpearAttackAnimationEvent()
    {
        _moveController?.OnSpearAttackAnimationEvent();
    }
}

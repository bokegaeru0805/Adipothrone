using UnityEngine;

/// <summary>
/// 子のAnimatorに登録されたAnimation Eventを物理ルートのControllerへ中継します。
/// </summary>
public class SnowFieldGolemLargeAnimationEventRelay : MonoBehaviour
{
    [SerializeField, Tooltip("Animation Eventの通知先となるLarge GolemのMoveController。")]
    private SnowFieldGolemLargeMoveController _moveController = null;

    public void OnMeleeAttackImpactAnimationEvent()
    {
        _moveController?.OnMeleeAttackImpactAnimationEvent();
    }
}

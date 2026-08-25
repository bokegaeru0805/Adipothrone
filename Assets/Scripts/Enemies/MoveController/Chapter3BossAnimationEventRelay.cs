using UnityEngine;

/// <summary>
/// Chapter3BossのAnimatorから発生した攻撃判定・演出用Animation Eventを、
/// Chapter3BossMoveControllerへ転送します。
/// Animatorと同じGameObjectへ追加してください。
/// </summary>
public class Chapter3BossAnimationEventRelay : MonoBehaviour
{
    [SerializeField]
    [Tooltip("転送先の移動コントローラー。未設定の場合は同一または親階層から自動取得します。")]
    private Chapter3BossMoveController _moveController = null;

    private void Awake()
    {
        if (_moveController == null)
            _moveController = GetComponentInParent<Chapter3BossMoveController>();

        if (_moveController == null)
        {
            Debug.LogError(
                $"{name}: Chapter3BossMoveControllerを同一または親階層から取得できませんでした。",
                this
            );
        }
    }

    public void BeginLowAttackDamage() => SetAttackDamageEnabled(
        Chapter3BossMoveController.AttackDamageType.Low,
        true
    );

    public void EndLowAttackDamage() => SetAttackDamageEnabled(
        Chapter3BossMoveController.AttackDamageType.Low,
        false
    );

    public void BeginHighAttackDamage() => SetAttackDamageEnabled(
        Chapter3BossMoveController.AttackDamageType.High,
        true
    );

    public void EndHighAttackDamage() => SetAttackDamageEnabled(
        Chapter3BossMoveController.AttackDamageType.High,
        false
    );

    public void BeginHorizontalAttackDamage() => SetAttackDamageEnabled(
        Chapter3BossMoveController.AttackDamageType.Horizontal,
        true
    );

    public void EndHorizontalAttackDamage() => SetAttackDamageEnabled(
        Chapter3BossMoveController.AttackDamageType.Horizontal,
        false
    );

    public void BeginThrustAttackDamage() => SetAttackDamageEnabled(
        Chapter3BossMoveController.AttackDamageType.Thrust,
        true
    );

    public void EndThrustAttackDamage() => SetAttackDamageEnabled(
        Chapter3BossMoveController.AttackDamageType.Thrust,
        false
    );

    public void BeginUpperAttackDamage() => SetAttackDamageEnabled(
        Chapter3BossMoveController.AttackDamageType.Upper,
        true
    );

    public void EndUpperAttackDamage() => SetAttackDamageEnabled(
        Chapter3BossMoveController.AttackDamageType.Upper,
        false
    );

    /// <summary>
    /// PowerUpアニメーションの表示タイミングでスキル名UIを表示します。
    /// </summary>
    public void ShowPowerUpSkillNameUI()
    {
        _moveController?.ShowPowerUpSkillNameUI();
    }

    private void SetAttackDamageEnabled(
        Chapter3BossMoveController.AttackDamageType attackType,
        bool isEnabled
    )
    {
        _moveController?.SetAttackDamageEnabled(attackType, isEnabled);
    }
}

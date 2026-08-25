using UnityEngine;

/// <summary>
/// スキルポイントのアイコン（クリスタル）1つを制御するクラス
/// </summary>
public class PointIconUI : MonoBehaviour
{
    [SerializeField]
    private Animator animator;

    // アニメーションのステート名（Unityエディタ上で作成するAnimation名と一致させる）
    private const string ANIM_DEFAULT = "SkillCrystal";
    private const string ANIM_STOPPED = "SkillCrystal_Stopped";

    private bool isAnimated;
    private bool hasState;

    /// <summary>
    /// 装備状態を受け取り、アニメーションを切り替える
    /// </summary>
    public void SetState(bool isEquipped, float normalizedTime = 0f)
    {
        if (animator == null)
            return;

        if (hasState && isAnimated == isEquipped && animator.gameObject.activeInHierarchy)
            return;

        hasState = true;
        isAnimated = isEquipped;
        if (isEquipped)
        {
            animator.Play(ANIM_DEFAULT, 0, Mathf.Repeat(normalizedTime, 1f));
        }
        else
        {
            animator.Play(ANIM_STOPPED);
        }
    }

    /// <summary>
    /// 同じSkillPointView内のアイコン同期に使う再生位置を返します。
    /// </summary>
    public bool TryGetAnimationNormalizedTime(out float normalizedTime)
    {
        normalizedTime = 0f;
        if (animator == null || !isAnimated || !animator.gameObject.activeInHierarchy)
            return false;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName(ANIM_DEFAULT))
        {
            return false;
        }

        normalizedTime = Mathf.Repeat(stateInfo.normalizedTime, 1f);
        return true;
    }

    private void OnDisable()
    {
        hasState = false;
    }
}

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

    /// <summary>
    /// 装備状態を受け取り、アニメーションを切り替える
    /// </summary>
    public void SetState(bool isEquipped)
    {
        if (animator == null)
            return;

        if (isEquipped)
        {
            animator.Play(ANIM_DEFAULT); // 装備中：回転アニメーション
        }
        else
        {
            animator.Play(ANIM_STOPPED); // 未装備：停止アニメーション
        }
    }
}

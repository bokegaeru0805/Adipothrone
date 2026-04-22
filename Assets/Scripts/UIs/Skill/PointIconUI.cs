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

    // 【同期用】すべてのクリスタルがタイミングを合わせるための「基準」となるAnimator
    // static修飾子をつけることで、すべてのPointIconUIインスタンスで1つの変数を共有します
    private static Animator referenceAnimator;

    /// <summary>
    /// 装備状態を受け取り、アニメーションを切り替える
    /// </summary>
    public void SetState(bool isEquipped)
    {
        if (animator == null)
            return;

        if (isEquipped)
        {
            // 1. 基準となるリーダーがまだいない、またはリーダーが非表示になった場合は、自分がリーダーになる
            if (referenceAnimator == null || !referenceAnimator.gameObject.activeInHierarchy)
            {
                referenceAnimator = animator;
                animator.Play(ANIM_DEFAULT);
            }
            // 2. すでにリーダーが存在する場合は、その再生位置（タイミング）をコピーする
            else if (referenceAnimator != animator)
            {
                AnimatorStateInfo stateInfo = referenceAnimator.GetCurrentAnimatorStateInfo(0);

                // リーダーが現在「Default」アニメーションを再生しているか確認
                if (stateInfo.IsName(ANIM_DEFAULT))
                {
                    // normalizedTime（0.0〜1.0で表される再生進捗）を取得し、同じ位置から再生を開始する
                    // % 1.0f をかけることで、何周ループしていても現在のコマにピタリと合わせます
                    animator.Play(ANIM_DEFAULT, 0, stateInfo.normalizedTime % 1.0f);
                }
                else
                {
                    animator.Play(ANIM_DEFAULT);
                }
            }
        }
        else
        {
            animator.Play(ANIM_STOPPED);

            // もし自分がリーダーだったのに止まることになった場合、リーダーを辞任する
            if (referenceAnimator == animator)
            {
                referenceAnimator = null;
            }
        }
    }

    /// <summary>
    /// オブジェクトが非アクティブになったり破棄されたりした際の処理
    /// </summary>
    private void OnDisable()
    {
        // 自分がリーダーのまま画面外に消えた場合、次のクリスタルが困らないようにリーダーを辞任する
        if (referenceAnimator == animator)
        {
            referenceAnimator = null;
        }
    }
}

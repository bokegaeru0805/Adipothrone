using UnityEngine;

#region 死亡アニメーション検知ビヘイビア
/// <summary>
/// ボスの死亡アニメーション（Deathステート）にアタッチするビヘイビアスクリプト。
/// 再生開始時にクリップの長さを読み取ってBGMを自動フェードアウトし、
/// 再生終了時に本体の演出スクリプトへ非アクティブ化処理を依頼します。
/// </summary>
public class BossDeathStateBehaviour : StateMachineBehaviour
{
    #region ステート制御
    /// <summary>
    /// アニメーション（ステート）の再生が開始された瞬間に呼ばれます。
    /// </summary>
    /// <param name="animator">このステートを持つAnimator</param>
    /// <param name="stateInfo">現在のステート情報</param>
    /// <param name="layerIndex">レイヤーインデックス</param>
    public override void OnStateEnter(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex
    )
    {
        // stateInfo.length には「現在のアニメーションクリップの正確な秒数」が入っています。
        // これを利用して、アニメーションが終わるのと同時にBGMが消えるようにフェードアウトさせます。
        if (BGMManager.instance != null)
        {
            BGMManager.instance.FadeOut(stateInfo.length);
        }
    }

    /// <summary>
    /// アニメーション（ステート）の再生が完全に終了し、他のステートへ遷移（または停止）する瞬間に呼ばれます。
    /// </summary>
    /// <param name="animator">このステートを持つAnimator</param>
    /// <param name="stateInfo">現在のステート情報</param>
    /// <param name="layerIndex">レイヤーインデックス</param>
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Animatorがついているオブジェクトから演出管理スクリプトを取得して、終了後の処理を依頼します
        var presentation = animator.GetComponent<UniqueBossPresentation>();

        if (presentation != null)
        {
            // スクリプトが見つかれば、BGMの復帰と非アクティブ化を依頼
            presentation.OnDeathAnimationFinished();
        }
        else
        {
            // フォールバック：万が一演出スクリプトが外れていた場合でも、敵の死体が残り続けないように強制的に消す
            animator.gameObject.SetActive(false);
        }
    }
    #endregion
}
#endregion

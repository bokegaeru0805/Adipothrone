using System.Collections;
using UnityEngine;

/// <summary>
/// アニメーションが付いたエフェクトプレハブにアタッチします。
/// 再生（OnEnable）されたら、アニメーションの長さの分だけ待機し、
/// 終了後に自動で ObjectPooler (永続インスタンス) に返却されます。
/// </summary>
[RequireComponent(typeof(Animator))]
public class AutoPoolReturn : MonoBehaviour
{
    private Animator animator;

    [Header("ObjectPooler 設定")]
    [SerializeField]
    [Tooltip("このオブジェクトの返却先となる ObjectPooler の「タグ」")]
    private string myPoolTag;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (string.IsNullOrEmpty(myPoolTag))
        {
            Debug.LogError(
                $"'{this.gameObject.name}' に myPoolTag が設定されていません！プールに返却できません。",
                this
            );
        }

        this.transform.localScale = Vector3.one; // スケールをリセット
    }

    /// <summary>
    /// ObjectPooler によって SetActive(true) にされた瞬間に呼び出されます。
    /// </summary>
    private void OnEnable()
    {
        // 実行中のアニメーションの長さを取得
        float animationLength = animator.GetCurrentAnimatorStateInfo(0).length;

        // アニメーションの長さが0（または取得失敗）の場合は、安全のため1秒後に返却
        // (注: OnEnableの瞬間に0.0fを返すことがあるため、確実な長さを設定するのが望ましい)
        if (animationLength <= 0)
        {
            animationLength = 1.0f;
            Debug.LogWarning(
                $"'{gameObject.name}'のアニメーション長が0でした。1秒後に返却します。",
                this
            );
        }

        // アニメーションの長さだけ待機してからプールに返却する
        StartCoroutine(ReturnAfterDelay(animationLength));
    }

    /// <summary>
    /// 指定時間後に ObjectPooler.PersistentInstance.ReturnToPool を呼び出すコルーチン
    /// </summary>
    private IEnumerator ReturnAfterDelay(float delay)
    {
        // アニメーションの長さだけ待つ
        yield return new WaitForSeconds(delay);

        // 永続プーラー(PersistentInstance) が存在し、タグが設定されていれば
        if (ObjectPooler.PersistentInstance != null && !string.IsNullOrEmpty(myPoolTag))
        {
            // 自分自身を、指定されたタグのプールに返却（非アクティブ化）
            ObjectPooler.PersistentInstance.ReturnToPool(myPoolTag, this.gameObject);
        }
        else
        {
            // マネージャーがいない、またはタグが未設定の場合は、破棄する
            Destroy(gameObject);
        }
    }
}

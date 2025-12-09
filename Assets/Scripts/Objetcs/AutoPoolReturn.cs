using System.Collections;
using DG.Tweening;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// アニメーションが付いたエフェクトプレハブにアタッチします。
/// OnEnable時にエフェクトの初期化（アルファ値のリセットや、ボス戦に応じたスケール変更など）を行い、
/// アニメーション終了後に自動で ObjectPooler に返却されます。
/// (注：スケール変更機能は、OnEnable時の初期化処理の一部として、
/// 他スクリプトとの実行順序の問題を避けるためにこのスクリプトに含まれています。)
/// </summary>
[RequireComponent(typeof(Animator))]
public class AutoPoolReturn : PoolableObject
{
    [Header("フェードアウト設定")]
    [SerializeField]
    [Tooltip("trueにすると、アニメーション終了後に徐々に消えます")]
    private bool useFadeOut = false;

    [SerializeField, ShowIf(nameof(useFadeOut))]
    [Tooltip("フェードアウトにかける時間（秒）")]
    private float fadeOutDuration = 0.5f;

    [Header("ボス戦スケール設定")]
    [SerializeField]
    [Tooltip("trueにすると、ボス戦中にスケールを変更します")]
    private bool scaleOnBossBattle = false;

    [SerializeField, ShowIf(nameof(scaleOnBossBattle))]
    [Tooltip("ボス戦中に適用するスケール")]
    private float bossScale = 1f;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private float originalScale;
    private float defaultFadeOutDuration;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        defaultFadeOutDuration = fadeOutDuration; // デフォルト値を保存
        originalScale = this.transform.localScale.x; // 元のスケールを保存
    }

    /// <summary>
    /// ObjectPooler によって SetActive(true) にされた瞬間に呼び出されます。
    /// </summary>
    private void OnEnable()
    {
        // OnEnable時に、値をデフォルト（インスペクター設定値）に戻す
        // これにより、プールから再利用されるたびに、
        // 前回の上書き設定（SetFadeDurationOverride）がリセットされます。
        fadeOutDuration = defaultFadeOutDuration;

        // プールから再利用されたときのために、アルファ値を元に戻す
        if (useFadeOut && spriteRenderer != null)
        {
            // DOTween の Kill() でアルファが0のまま止まっている可能性があるので、
            // 即座にアルファを 1 (不透明) に戻す
            Color resetColor = spriteRenderer.color;
            resetColor.a = 1f;
            spriteRenderer.color = resetColor;
        }

        // スケール変更が有効な場合、現在のボス戦状態に応じてスケールを設定
        if (scaleOnBossBattle)
        {
            // GameUIManager から現在のボス戦状態を取得
            bool isBossBattle = GameUIManager.instance?.IsInBossBattle ?? false;
            float targetScale = isBossBattle ? bossScale : originalScale;

            // ボス戦中ならボススケール、そうでなければ元のスケールを適用
            this.transform.localScale = new Vector2(targetScale, targetScale);
        }
        else
        {
            // スケール変更がOFFの場合は、常に元のスケールに戻す
            this.transform.localScale = new Vector2(originalScale, originalScale);
        }

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
        StartCoroutine(ReturnWithFade(animationLength));
    }

    /// <summary>
    /// 指定時間後に ObjectPooler.PersistentInstance.ReturnToPool を呼び出すコルーチン
    /// </summary>
    private IEnumerator ReturnWithFade(float delay)
    {
        // アニメーションの長さだけ待つ
        yield return new WaitForSeconds(delay);

        // フェードアウト処理
        if (useFadeOut && spriteRenderer != null)
        {
            // DOTween でフェードアウトさせ、完了するまで待機する
            yield return spriteRenderer.DOFade(0f, fadeOutDuration).WaitForCompletion();
        }

        // プールに返却
        ReturnToPool();
    }

    /// <summary>
    /// 外部からフェードアウト時間を一時的に上書きします。
    /// （プールに返却され、次にOnEnableされるとデフォルト値にリセットされます）
    /// </summary>
    /// <param name="newDuration">新しいフェードアウト時間（秒）</param>
    public void SetFadeDurationOverride(float newDuration)
    {
        // フェードアウトが有効になっていない場合は、この設定を無視
        if (!useFadeOut)
        {
            return;
        }

        // 0秒未満は設定しない
        if (newDuration < 0f)
        {
            newDuration = 0f;
        }

        this.fadeOutDuration = newDuration;
    }

    /// <summary>
    /// オブジェクトが無効化（プールに返却）される際に、実行中のTweenを停止する
    /// </summary>
    private void OnDisable()
    {
        // 安全のため、実行中のDOTweenを停止
        if (spriteRenderer != null)
        {
            spriteRenderer.DOKill();
        }
    }
}

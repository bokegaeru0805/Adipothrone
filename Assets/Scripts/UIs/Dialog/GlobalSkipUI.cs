using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SayDialogの子オブジェクトにアタッチし、TimelineSkipManagerの状態を監視して
/// GlobalSkip用の円形ゲージを表示制御するクラス。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class GlobalSkipUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("長押し進捗を表示するImage (Image TypeをFilledに設定してください)")]
    [SerializeField]
    private Image progressFillImage;

    [Header("Appearance")]
    [Tooltip("表示・非表示のフェード時間")]
    [SerializeField]
    private float fadeDuration = 0.2f;

    private CanvasGroup canvasGroup;
    private float currentAlpha = 0f;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        // 初期状態は非表示
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false; // 操作を阻害しないように
    }

    private void Update()
    {
        // マネージャーが存在しない場合は非表示にして終了
        if (TimelineSkipManager.instance == null)
        {
            UpdateAlpha(0f);
            return;
        }

        var manager = TimelineSkipManager.instance;

        // スキップ可能な状態かチェック
        if (manager.IsSkipAvailable)
        {
            // 表示目標アルファを1に
            UpdateAlpha(1f);

            // ゲージの更新
            if (progressFillImage != null)
            {
                progressFillImage.fillAmount = manager.SkipProgress;
            }
        }
        else
        {
            // スキップ不可（または実行中）なら非表示
            UpdateAlpha(0f);
            
            // 非表示時はゲージを0に戻しておく
            if (progressFillImage != null)
            {
                progressFillImage.fillAmount = 0f;
            }
        }
    }

    /// <summary>
    /// CanvasGroupのアルファ値を滑らかに変更する
    /// </summary>
    private void UpdateAlpha(float target)
    {
        if (Mathf.Abs(currentAlpha - target) > 0.01f)
        {
            float delta = Time.unscaledDeltaTime / fadeDuration;
            currentAlpha = Mathf.MoveTowards(currentAlpha, target, delta);
            canvasGroup.alpha = currentAlpha;
        }
        else
        {
            currentAlpha = target;
            canvasGroup.alpha = currentAlpha;
        }
    }
}
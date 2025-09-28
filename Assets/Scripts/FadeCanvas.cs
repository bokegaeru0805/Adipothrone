using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class FadeCanvas : MonoBehaviour
{
    public static FadeCanvas instance;

    [SerializeField]
    [Tooltip("フェードに使用するImageコンポーネントを持つUI要素")]
    private Image fadeImage;

    private Canvas canvas;

    private void Awake()
    {
        // シングルトン設定
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
            return;
        }

        canvas = this.GetComponent<Canvas>();
        if (fadeImage == null)
        {
            Debug.LogError("Fade ImageがInspectorから設定されていません。", this);
            return;
        }

        // 初期状態を設定
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// 画面を暗転させます（フェードアウト）
    /// </summary>
    /// <param name="duration">フェードにかかる時間</param>
    public void FadeOut(float duration)
    {
        if (fadeImage == null)
            return;

        fadeImage.gameObject.SetActive(true);
        // 既存のTweenを停止してから新しいTweenを開始
        fadeImage.DOKill();
        fadeImage.DOFade(1.0f, duration).SetUpdate(true); // Time.timeScale=0でも動作
    }

    /// /// <summary>
    /// 画面を明転させます（フェードイン）
    /// </summary>
    /// <param name="duration">フェードにかかる時間</param>
    public void FadeIn(float duration)
    {
        if (fadeImage == null)
            return;

        fadeImage.gameObject.SetActive(true);
        fadeImage.DOKill();
        // フェード完了後（OnComplete）に自動で非表示にする
        fadeImage
            .DOFade(0.0f, duration)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                fadeImage.gameObject.SetActive(false);
            });
    }
}

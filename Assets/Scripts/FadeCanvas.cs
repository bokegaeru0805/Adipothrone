using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class FadeCanvas : MonoBehaviour
{
    public static FadeCanvas instance;

    [Header("Images")]
    [SerializeField]
    [Tooltip("暗転用の黒画像 (Hierarchyの上の方＝奥に表示)")]
    private Image fadeImage;

    [SerializeField]
    [Tooltip("発光用の白画像 (Hierarchyの下の方＝手前に表示)")]
    private Image flashImage;

    /// <summary>
    /// 現在のフェードのアルファ値（不透明度）を取得します。
    /// Timelineのスキップ判定などで使用します。
    /// </summary>
    public float CurrentAlpha
    {
        get
        {
            if (fadeImage != null)
            {
                return fadeImage.color.a;
            }
            return 0f;
        }
    }

    /// <summary>
    /// 現在の白フェード（フラッシュ）のアルファ値を取得します。
    /// </summary>
    public float CurrentFlashAlpha
    {
        get
        {
            if (flashImage != null)
            {
                return flashImage.color.a;
            }
            return 0f;
        }
    }

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

        // --- 黒画像の初期化 ---
        if (fadeImage != null)
        {
            fadeImage.color = new Color(0f, 0f, 0f, 0f);
            fadeImage.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("Fade Image (Black) が設定されていません。", this);
        }

        // --- 白画像の初期化 ---
        if (flashImage != null)
        {
            flashImage.color = new Color(1f, 1f, 1f, 0f);
            flashImage.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("Flash Image (White) が設定されていません。", this);
        }
    }

    #region  Fade Image (Black) Methods
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

    /// <summary>
    /// フェードの透明度を直接設定します
    /// /// </summary>
    public void SetAlpha(float alpha)
    {
        if (fadeImage != null)
        {
            // 念のためDOTweenを止める（Timeline優先）
            fadeImage.DOKill();

            Color c = fadeImage.color;
            c.a = alpha;
            fadeImage.color = c;
            // Alphaが0より大きければ表示、0なら非表示（最適化）
            fadeImage.gameObject.SetActive(alpha > 0);

            //Debug.Log($"SetAlpha: {alpha}", this);
        }
    }

    #endregion

    #region Flash Image (White) Methods

    /// <summary>
    /// 白フェードの透明度を直接設定します (Timeline用)
    /// </summary>
    public void SetFlashAlpha(float alpha)
    {
        if (flashImage != null)
        {
            flashImage.DOKill(); // Tweenが走っていたら止める
            Color c = flashImage.color;
            c.a = alpha;
            flashImage.color = c;
            flashImage.gameObject.SetActive(alpha > 0);
        }
    }

    /// <summary>
    /// 画面を白く飛ばします（フラッシュアウト）
    /// </summary>
    public void FlashOut(float duration)
    {
        if (flashImage == null)
            return;
        flashImage.gameObject.SetActive(true);
        flashImage.DOKill();
        flashImage.DOFade(1.0f, duration).SetUpdate(true);
    }

    /// <summary>
    /// 白い画面から通常に戻ります（フラッシュイン）
    /// </summary>
    public void FlashIn(float duration)
    {
        if (flashImage == null)
            return;
        flashImage.gameObject.SetActive(true);
        flashImage.DOKill();
        flashImage
            .DOFade(0.0f, duration)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                flashImage.gameObject.SetActive(false);
            });
    }

    #endregion
}

using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public enum EventCGSizeMode
{
    [InspectorName("画面内に収める")]
    FitScreen = 0,

    [InspectorName("画面全体を覆う")]
    FillScreen = 1,

    [InspectorName("画像の元サイズ")]
    NativeSize = 2,

    [InspectorName("幅・高さを指定")]
    CustomSize = 3,

    [InspectorName("元サイズに対する倍率")]
    NativeScale = 4,
}

public struct EventCGDisplayOptions
{
    public EventCGSizeMode SizeMode;
    public Vector2 CustomSize;
    public float NativeScale;
    public Vector2 PositionOffset;
}

/// <summary>
/// 会話ウィンドウの後ろ、立ち絵の前にEventCGを表示します。
/// </summary>
public class EventCGController : MonoBehaviour
{
    private const int EventCGSortingOrder = 20;
    private static EventCGController _instance;

    private RectTransform _canvasRect;
    private RectTransform _imageRect;
    private CanvasGroup _canvasGroup;
    private Image _image;
    private Sequence _activeSequence;

    public static EventCGController EnsureInstance()
    {
        if (_instance != null)
        {
            return _instance;
        }

        _instance = FindObjectOfType<EventCGController>();
        if (_instance != null)
        {
            return _instance;
        }

        GameObject canvasObject = new GameObject(
            "EventCGCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(CanvasGroup),
            typeof(EventCGController)
        );
        _instance = canvasObject.GetComponent<EventCGController>();
        _instance.InitializeRuntimeUI();
        return _instance;
    }

    /// <summary>
    /// EventCG用Canvasが生成済みの場合だけ、表示中のCGを即座に隠します。
    /// </summary>
    public static void HideExistingImmediate()
    {
        if (_instance != null)
        {
            _instance.HideImmediate();
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        InitializeRuntimeUI();
        HideImmediate();
    }

    private void OnDestroy()
    {
        _activeSequence?.Kill();
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void InitializeRuntimeUI()
    {
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = EventCGSortingOrder;

        CanvasScaler canvasScaler = GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        _canvasRect = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        Transform existingImage = transform.Find("EventCGImage");
        if (existingImage == null)
        {
            GameObject imageObject = new GameObject(
                "EventCGImage",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            imageObject.transform.SetParent(transform, false);
            existingImage = imageObject.transform;
        }

        _imageRect = existingImage.GetComponent<RectTransform>();
        _imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        _imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        _imageRect.pivot = new Vector2(0.5f, 0.5f);

        _image = existingImage.GetComponent<Image>();
        _image.raycastTarget = false;
        _image.preserveAspect = true;
    }

    public void Show(
        Sprite sprite,
        EventCGDisplayOptions options,
        float fadeInDuration,
        Action onComplete = null
    )
    {
        CancelActiveSequence();
        if (!PrepareImage(sprite, options))
        {
            onComplete?.Invoke();
            return;
        }

        _canvasGroup.alpha = 0f;
        if (fadeInDuration <= 0f)
        {
            _canvasGroup.alpha = 1f;
            onComplete?.Invoke();
            return;
        }

        _activeSequence = DOTween.Sequence();
        _activeSequence
            .Append(_canvasGroup.DOFade(1f, fadeInDuration))
            .SetUpdate(true)
            .OnComplete(() =>
            {
                _activeSequence = null;
                onComplete?.Invoke();
            });
    }

    public void ShowForDuration(
        Sprite sprite,
        EventCGDisplayOptions options,
        float fadeInDuration,
        float displayDuration,
        float fadeOutDuration,
        Action onComplete
    )
    {
        CancelActiveSequence();
        if (!PrepareImage(sprite, options))
        {
            onComplete?.Invoke();
            return;
        }

        _canvasGroup.alpha = 0f;
        _activeSequence = DOTween.Sequence();
        if (fadeInDuration > 0f)
        {
            _activeSequence.Append(_canvasGroup.DOFade(1f, fadeInDuration));
        }
        else
        {
            _activeSequence.AppendCallback(() => _canvasGroup.alpha = 1f);
        }

        if (displayDuration > 0f)
        {
            _activeSequence.AppendInterval(displayDuration);
        }

        if (fadeOutDuration > 0f)
        {
            _activeSequence.Append(_canvasGroup.DOFade(0f, fadeOutDuration));
        }
        else
        {
            _activeSequence.AppendCallback(() => _canvasGroup.alpha = 0f);
        }

        _activeSequence
            .SetUpdate(true)
            .OnComplete(() =>
            {
                _activeSequence = null;
                ClearImage();
                onComplete?.Invoke();
            });
    }

    public void Hide(float fadeOutDuration, Action onComplete = null)
    {
        CancelActiveSequence();
        if (_image == null || _image.sprite == null || _canvasGroup.alpha <= 0f)
        {
            HideImmediate();
            onComplete?.Invoke();
            return;
        }

        if (fadeOutDuration <= 0f)
        {
            HideImmediate();
            onComplete?.Invoke();
            return;
        }

        _activeSequence = DOTween.Sequence();
        _activeSequence
            .Append(_canvasGroup.DOFade(0f, fadeOutDuration))
            .SetUpdate(true)
            .OnComplete(() =>
            {
                _activeSequence = null;
                ClearImage();
                onComplete?.Invoke();
            });
    }

    public void HideImmediate()
    {
        CancelActiveSequence();
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
        }
        ClearImage();
    }

    private bool PrepareImage(Sprite sprite, EventCGDisplayOptions options)
    {
        if (sprite == null)
        {
            Debug.LogWarning("EventCGController: 表示するSpriteが設定されていません。", this);
            HideImmediate();
            return false;
        }

        _image.sprite = sprite;
        _image.enabled = true;
        ApplyLayout(sprite, options);
        return true;
    }

    private void ApplyLayout(Sprite sprite, EventCGDisplayOptions options)
    {
        Canvas.ForceUpdateCanvases();

        Vector2 nativeSize = sprite.rect.size;
        Vector2 canvasSize = _canvasRect.rect.size;
        if (canvasSize.x <= 0f || canvasSize.y <= 0f)
        {
            canvasSize = new Vector2(1920f, 1080f);
        }

        Vector2 targetSize = nativeSize;
        switch (options.SizeMode)
        {
            case EventCGSizeMode.FitScreen:
                targetSize = nativeSize * Mathf.Min(
                    canvasSize.x / nativeSize.x,
                    canvasSize.y / nativeSize.y
                );
                break;

            case EventCGSizeMode.FillScreen:
                targetSize = nativeSize * Mathf.Max(
                    canvasSize.x / nativeSize.x,
                    canvasSize.y / nativeSize.y
                );
                break;

            case EventCGSizeMode.CustomSize:
                targetSize = new Vector2(
                    Mathf.Max(1f, options.CustomSize.x),
                    Mathf.Max(1f, options.CustomSize.y)
                );
                break;

            case EventCGSizeMode.NativeScale:
                targetSize = nativeSize * Mathf.Max(0.01f, options.NativeScale);
                break;

            case EventCGSizeMode.NativeSize:
            default:
                break;
        }

        _imageRect.sizeDelta = targetSize;
        _imageRect.anchoredPosition = options.PositionOffset;
    }

    private void CancelActiveSequence()
    {
        _activeSequence?.Kill();
        _activeSequence = null;
    }

    private void ClearImage()
    {
        if (_image == null)
        {
            return;
        }

        _image.enabled = false;
        _image.sprite = null;
    }
}

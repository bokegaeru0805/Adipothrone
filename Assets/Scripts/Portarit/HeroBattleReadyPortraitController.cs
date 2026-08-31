using System;
using System.Collections.Generic;
using DG.Tweening;
using Fungus;
using UnityEngine;

/// <summary>
/// Flowchartから明示的に要求された時だけ表示する、全身一枚絵の立ち絵を管理します。
/// Sayコマンドによる通常の動的立ち絵表示では表示されません。
/// </summary>
public class HeroBattleReadyPortraitController : BasePortraitController
{
    [Serializable]
    public class PortraitVariant
    {
        [Tooltip("FlowchartのControl Portraitで指定する識別子（例: BattleReady_Normal）")]
        public string id;

        [Tooltip("表示する全身立ち絵スプライト")]
        public Sprite sprite;
    }

    [Header("Full Body Portrait Settings")]
    [Tooltip("表情・ポーズごとの全身立ち絵。識別子を増やすことでポーズを追加できます。")]
    [SerializeField]
    private List<PortraitVariant> portraitVariants = new List<PortraitVariant>();

    private Dictionary<string, Sprite> _variantDictionary;
    private bool _isExplicitlyVisible;

    protected override void Awake()
    {
        _variantDictionary = new Dictionary<string, Sprite>();
        foreach (PortraitVariant variant in portraitVariants)
        {
            if (variant == null || string.IsNullOrEmpty(variant.id) || variant.sprite == null)
            {
                continue;
            }

            if (_variantDictionary.ContainsKey(variant.id))
            {
                Debug.LogWarning($"立ち絵の識別子が重複しています: {variant.id}", this);
                continue;
            }

            _variantDictionary.Add(variant.id, variant.sprite);
        }

        base.Awake();
    }

    /// <summary>
    /// Sayコマンドからの通常表示は受け付けません。
    /// </summary>
    public override void HandleShowRequest(string portraitString) { }

    /// <summary>
    /// Control Portraitコマンドから要求された全身立ち絵を表示します。
    /// </summary>
    public override void HandleExplicitShowRequest(string portraitString)
    {
        if (_variantDictionary.TryGetValue(portraitString, out Sprite sprite))
        {
            _isExplicitlyVisible = true;

            // 明示表示ではCanvasGroupを確実に復帰させる。
            // 非表示時のalpha = 0 が残ったまま、子Imageだけが描画されない状態を防ぐ。
            _portraitCanvasGroup.alpha = 1f;
            UpdateScreenPosition();
            ShowPortrait(portraitString, string.Empty, string.Empty);
            return;
        }

        Debug.LogWarning($"全身立ち絵が見つかりません: {portraitString}", this);
    }

    /// <summary>
    /// Control PortraitのHide要求で、明示表示状態を終了します。
    /// </summary>
    public override void HandleExplicitHideRequest()
    {
        _isExplicitlyVisible = false;
        HidePortrait();
    }

    /// <summary>
    /// 新しい会話ブロックの開始時は、前のブロックの明示表示状態を引き継ぎません。
    /// </summary>
    protected override void HandleBlockStart(BlockType blockType)
    {
        _isExplicitlyVisible = false;
        base.HandleBlockStart(blockType);
    }

    public override void SetPortraitColorTween(Color targetColor, float duration)
    {
        if (bodyImage != null)
        {
            bodyImage.DOColor(targetColor, duration).SetUpdate(true);
        }
    }

    protected override void SetAllSprites(
        string bodySpriteName,
        string faceSpriteName,
        string expressionSpriteName
    )
    {
        _currentBodySpriteName = bodySpriteName;

        if (_variantDictionary.TryGetValue(bodySpriteName, out Sprite sprite))
        {
            bodyImage.sprite = sprite;
            bodyImage.enabled = true;
        }
        else
        {
            Debug.LogError($"全身立ち絵が見つかりません: {bodySpriteName}", this);
            bodyImage.enabled = false;
        }

        if (faceImage != null)
        {
            faceImage.enabled = false;
        }

        if (expressionImage != null)
        {
            expressionImage.enabled = false;
        }
    }

    public override void HidePortrait()
    {
        // 明示表示中はSay等の共通非表示要求を無視し、Control PortraitのHideまで維持する。
        if (_isExplicitlyVisible)
        {
            return;
        }

        _activeTweenAnimation?.Kill();
        _activeTweenAnimation = null;

        _portraitCanvasGroup.alpha = 0f;
        if (bodyImage != null)
        {
            // CanvasGroupで完全に隠れるため、Imageコンポーネントは有効のまま維持する。
            // Flowchartからの明示表示時に、Imageの有効状態が表示を妨げないようにする。
            bodyImage.enabled = true;
        }
    }

    public override void FadeOutPortrait(float duration, Action onComplete = null)
    {
        if (_isExplicitlyVisible)
        {
            onComplete?.Invoke();
            return;
        }

        base.FadeOutPortrait(duration, onComplete);
    }

    public override void ResetToInitialState()
    {
        _activeTweenAnimation?.Kill();
        _activeTweenAnimation = null;

        _portraitContainerRect.anchoredPosition = _initialPosition;
        _temporaryOffset = Vector2.zero;
        _portraitContainerRect.localScale = _initialScale;
        ApplyDefaultDirection();
        _portraitCanvasGroup.alpha = _initialAlpha;

        if (_portraitCanvas != null)
        {
            _portraitCanvas.sortingOrder = _defaultSortOrder;
        }

        SetPortraitColorTween(Color.white, 0f);
        if (bodyImage != null)
        {
            bodyImage.enabled = true;
        }
    }
}

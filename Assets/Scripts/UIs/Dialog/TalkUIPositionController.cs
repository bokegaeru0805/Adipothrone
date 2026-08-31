using System;
using DG.Tweening;
using UnityEngine;

public enum TalkUIPosition
{
    [InspectorName("上側（通常位置）")]
    Top = 0,

    [InspectorName("下側")]
    Bottom = 1,
}

/// <summary>
/// SayDialog全体の上下位置と、スキップアイコンの表示側を管理します。
/// </summary>
public class TalkUIPositionController : MonoBehaviour
{
    private const string DialogPanelName = "Panel_SayDialog";
    private const string SkipIconName = "SkipIcon";
    private const string GlobalSkipIconName = "GlobalSkipIcon";

    private RectTransform _dialogPanel;
    private RectTransform _skipIcon;
    private RectTransform _globalSkipIcon;
    private Vector2 _topPanelPosition;
    private Vector2 _topSkipIconPosition;
    private Vector2 _topGlobalSkipIconPosition;
    private Tween _moveTween;
    private bool _isInitialized;

    private void OnDestroy()
    {
        _moveTween?.Kill();
    }

    public void CancelMove()
    {
        _moveTween?.Kill();
        _moveTween = null;
    }

    private void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        RectTransform[] rectTransforms = GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform rectTransform in rectTransforms)
        {
            switch (rectTransform.name)
            {
                case DialogPanelName:
                    _dialogPanel = rectTransform;
                    break;
                case SkipIconName:
                    _skipIcon = rectTransform;
                    break;
                case GlobalSkipIconName:
                    _globalSkipIcon = rectTransform;
                    break;
            }
        }

        if (_dialogPanel == null)
        {
            Debug.LogError(
                $"TalkUIPositionController: {DialogPanelName} が見つかりません。",
                this
            );
            return;
        }

        Canvas.ForceUpdateCanvases();
        _topPanelPosition = ResolveTopPanelPosition(_dialogPanel.anchoredPosition);
        bool isCurrentlyOnBottom =
            !Mathf.Approximately(
                _topPanelPosition.y,
                _dialogPanel.anchoredPosition.y
            );
        if (_skipIcon != null)
        {
            _topSkipIconPosition = ResolveTopIconPosition(
                _skipIcon.anchoredPosition,
                isCurrentlyOnBottom
            );
        }
        if (_globalSkipIcon != null)
        {
            _topGlobalSkipIconPosition = ResolveTopIconPosition(
                _globalSkipIcon.anchoredPosition,
                isCurrentlyOnBottom
            );
        }

        _isInitialized = true;
    }

    public void MoveTo(
        TalkUIPosition position,
        float duration,
        Ease ease,
        Action onComplete
    )
    {
        Initialize();
        if (!_isInitialized)
        {
            onComplete?.Invoke();
            return;
        }

        _moveTween?.Kill();
        ApplyIconPosition(position);

        Vector2 targetPosition = GetPanelPosition(position);
        if (duration <= 0f || _dialogPanel.anchoredPosition == targetPosition)
        {
            _dialogPanel.anchoredPosition = targetPosition;
            onComplete?.Invoke();
            return;
        }

        _moveTween = _dialogPanel
            .DOAnchorPos(targetPosition, duration)
            .SetEase(ease)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                _moveTween = null;
                onComplete?.Invoke();
            });
    }

    public void ResetImmediate()
    {
        Initialize();
        if (!_isInitialized)
        {
            return;
        }

        _moveTween?.Kill();
        _moveTween = null;
        _dialogPanel.anchoredPosition = GetPanelPosition(TalkUIPosition.Top);
        ApplyIconPosition(TalkUIPosition.Top);
    }

    private Vector2 GetPanelPosition(TalkUIPosition position)
    {
        if (position == TalkUIPosition.Top)
        {
            return _topPanelPosition;
        }

        RectTransform parentRect = _dialogPanel.parent as RectTransform;
        float parentHeight = parentRect != null ? parentRect.rect.height : Screen.height;
        if (parentHeight <= 0f)
        {
            parentHeight = 1080f;
        }

        return new Vector2(_topPanelPosition.x, -parentHeight - _topPanelPosition.y);
    }

    private Vector2 ResolveTopPanelPosition(Vector2 currentPosition)
    {
        RectTransform parentRect = _dialogPanel.parent as RectTransform;
        float parentHeight = parentRect != null ? parentRect.rect.height : Screen.height;
        if (parentHeight <= 0f)
        {
            parentHeight = 1080f;
        }

        // 既に下側へ移動済みの状態から生成された場合も、鏡映した上側を基準値にする。
        if (currentPosition.y < -parentHeight * 0.5f)
        {
            return new Vector2(currentPosition.x, -parentHeight - currentPosition.y);
        }

        return currentPosition;
    }

    private static Vector2 ResolveTopIconPosition(
        Vector2 currentPosition,
        bool isCurrentlyOnBottom
    )
    {
        return isCurrentlyOnBottom
            ? new Vector2(currentPosition.x, -currentPosition.y)
            : currentPosition;
    }

    private void ApplyIconPosition(TalkUIPosition position)
    {
        bool isBottom = position == TalkUIPosition.Bottom;
        if (_skipIcon != null)
        {
            _skipIcon.anchoredPosition = new Vector2(
                _topSkipIconPosition.x,
                isBottom ? -_topSkipIconPosition.y : _topSkipIconPosition.y
            );
        }
        if (_globalSkipIcon != null)
        {
            _globalSkipIcon.anchoredPosition = new Vector2(
                _topGlobalSkipIconPosition.x,
                isBottom ? -_topGlobalSkipIconPosition.y : _topGlobalSkipIconPosition.y
            );
        }
    }
}

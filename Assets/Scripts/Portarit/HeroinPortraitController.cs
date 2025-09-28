using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Fungus;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class HeroinPortraitController : MonoBehaviour
{
    public static HeroinPortraitController instance;

    [Header("UI References")]
    public Image bodyImage;
    public Image faceImage;
    public Image expressionImage;

    [Header("Sprite Database")]
    [Tooltip("ここにHeroinの胴体・顔・表情のスプライトをすべてドラッグ＆ドロップしてください")]
    public List<Sprite> portraitSprites = new List<Sprite>();

    private Dictionary<string, Sprite> _portraitDictionary;

    [Tooltip("アニメーションの動き方")]
    [SerializeField]
    private Ease animationEase = Ease.OutQuad;

    //立ち絵が表示される際のアニメーション時間（秒）

    private float animationDuration = 1f;

    // 実行中のアニメーションを管理するためのDOTweenのSequence
    private Sequence _activeTweenAnimation;

    // 立ち絵の親オブジェクトのRectTransform
    private RectTransform _portraitContainerRect;

    // 立ち絵全体の透明度を管理するCanvasGroup
    private CanvasGroup _portraitCanvasGroup;

    // アニメーション後の最終的な画面上の位置
    private Vector2 _onScreenPosition;

    //現在の会話のBlockTypeを保持する変数
    private BlockType currentBlockType = BlockType.Default;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _portraitDictionary = new Dictionary<string, Sprite>();
        foreach (var sprite in portraitSprites)
        {
            if (sprite == null)
                continue;
            if (!_portraitDictionary.ContainsKey(sprite.name))
            {
                _portraitDictionary.Add(sprite.name, sprite);
            }
            else
            {
                Debug.LogWarning($"スプライト名が重複しています: {sprite.name}");
            }
        }

        // DOTween用にコンポーネントを取得
        _portraitContainerRect = GetComponent<RectTransform>();
        _portraitCanvasGroup = GetComponent<CanvasGroup>();
        _onScreenPosition = _portraitContainerRect.anchoredPosition;

        // 初期状態では立ち絵を非表示にする
        HidePortrait();
    }

    //OnEnableだと、TalkStartコマンドのOnEnterより後に呼ばれてしまい、イベントを受け取れない可能性がある
    private void Start()
    {
        FungusCustomSignals.OnRequestDynamicPortrait += HandleShowRequest;
        FungusCustomSignals.OnRequestHideDynamicPortrait += HidePortrait;
        FungusCustomSignals.OnTalkBlockStart += HandleBlockStart;
    }

    private void OnDestroy()
    {
        FungusCustomSignals.OnRequestDynamicPortrait -= HandleShowRequest;
        FungusCustomSignals.OnRequestHideDynamicPortrait -= HidePortrait;
        FungusCustomSignals.OnTalkBlockStart -= HandleBlockStart;
    }

    /// <summary>
    /// Sayコマンドからの表示リクエスト（イベント）を処理するメソッド
    /// </summary>
    private void HandleShowRequest(string portraitString)
    {
        // PlayerBodyManagerのインスタンスがない場合は処理しない
        if (PlayerBodyManager.instance == null)
        {
            Debug.LogError("PlayerBodyManagerのインスタンスが見つかりません！");
            return;
        }

        //Storyブロックでない場合は、立ち絵表示リクエストを無視する
        if (!(currentBlockType == BlockType.Story))
        {
            return;
        }

        // 文字列の解析とスプライト名の組み立てロジックをここに移動
        string[] parts = portraitString.Split('_');
        if (parts.Length >= 3)
        {
            string characterName = parts[0];
            string expressionString = parts.LastOrDefault();
            string bodyStateString = PlayerBodyManager
                .instance.GetCurrentBodyStateEnum()
                .ToString()
                .Replace("BodyState_", "");

            // 先頭だけ小文字に
            if (!string.IsNullOrEmpty(bodyStateString))
            {
                bodyStateString = char.ToLower(bodyStateString[0]) + bodyStateString.Substring(1);
            }

            string bodySpriteName = $"{characterName}_{bodyStateString}_body";
            string faceSpriteName = $"{characterName}_{bodyStateString}_face";
            string expressionSpriteName = $"{characterName}_expression_{expressionString}";

            // 組み立てた名前で自身の表示メソッドを呼び出す
            ShowPortrait(bodySpriteName, faceSpriteName, expressionSpriteName);
        }
        else
        {
            Debug.LogWarning($"portraitStringのフォーマットが正しくありません: {portraitString}");
        }
    }

    /// <summary>
    /// 指定された名前のスプライトで立ち絵を表示します。
    /// 元々非表示だった場合は、アニメーションを再生します。
    /// </summary>
    public void ShowPortrait(
        string bodySpriteName,
        string faceSpriteName,
        string expressionSpriteName
    )
    {
        if (_activeTweenAnimation != null && _activeTweenAnimation.IsActive())
        {
            // Kill()の代わりにComplete()を呼び出すことで、
            // アニメーションを瞬時に完了させ、位置や透明度を最終状態にします。
            _activeTweenAnimation.Complete(); 
        }

        // CanvasGroupの透明度で、元々非表示だったかを判定
        bool wasHidden = _portraitCanvasGroup.alpha == 0;

        // まずスプライトを設定し、Imageコンポーネントを有効化
        if (_portraitDictionary.TryGetValue(bodySpriteName, out Sprite bodySprite))
        {
            bodyImage.sprite = bodySprite;
            bodyImage.enabled = true;
        }
        else
        {
            Debug.LogError($"胴体スプライトが見つかりません: {bodySpriteName}");
            bodyImage.enabled = false;
        }

        if (_portraitDictionary.TryGetValue(faceSpriteName, out Sprite faceSprite))
        {
            faceImage.sprite = faceSprite;
            faceImage.enabled = true;
        }
        else
        {
            Debug.LogError($"顔スプライトが見つかりません: {faceSpriteName}");
            faceImage.enabled = false;
        }

        if (_portraitDictionary.TryGetValue(expressionSpriteName, out Sprite expressionSprite))
        {
            expressionImage.sprite = expressionSprite;
            expressionImage.enabled = true;
        }
        else
        {
            Debug.LogError($"表情スプライトが見つかりません: {expressionSpriteName}");
            expressionImage.enabled = false;
        }

        // もし元々非表示だったら、DOTweenでアニメーションを開始する
        if (wasHidden && bodyImage.enabled)
        {
            // 1. アニメーションの初期状態を設定
            Vector2 offScreenPosition = new Vector2(
                _onScreenPosition.x - _portraitContainerRect.rect.width,
                _onScreenPosition.y
            );
            _portraitContainerRect.anchoredPosition = offScreenPosition;
            _portraitCanvasGroup.alpha = 0f;

            // 2. DOTweenのシーケンスを作成
            _activeTweenAnimation = DOTween.Sequence();
            _activeTweenAnimation
                // anchoredPositionを_onScreenPositionへアニメーションさせる
                .Append(
                    _portraitContainerRect
                        .DOAnchorPos(_onScreenPosition, animationDuration)
                        .SetEase(animationEase)
                )
                // alphaを1へアニメーションさせる（Appendと同時に実行）
                .Join(_portraitCanvasGroup.DOFade(1f, animationDuration))
                //Time.timeScaleを無視してアニメーションを更新する
                .SetUpdate(true)
                // アニメーション完了時に管理変数をクリア
                .OnComplete(() => _activeTweenAnimation = null);
        }
    }

    /// <summary>
    /// 立ち絵を非表示にします。
    /// </summary>
    public void HidePortrait()
    {
        // 実行中のアニメーションがあれば停止
        _activeTweenAnimation?.Kill();
        _activeTweenAnimation = null;

        // CanvasGroupの透明度を0にして瞬時に隠す
        _portraitCanvasGroup.alpha = 0;

        // Imageを無効にして描画負荷を削減
        bodyImage.enabled = false;
        faceImage.enabled = false;
        expressionImage.enabled = false;
    }

    private void HandleBlockStart(BlockType blockType)
    {
        //現在のBlockTypeを更新
        currentBlockType = blockType;

        // もしStoryでないなら、現在表示されている可能性のある立ち絵を即座に非表示にする
        if (currentBlockType != BlockType.Story)
        {
            HidePortrait();
        }
    }
}

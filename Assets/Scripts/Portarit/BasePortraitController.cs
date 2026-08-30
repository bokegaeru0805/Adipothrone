using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Fungus;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 立ち絵のX座標（配置）の定義
/// UnityのInspectorでのシリアライズ崩れを防ぐため、具体的な数値は持たせません。
/// </summary>
public enum PortraitPositionX
{
    FarLeft = 100,
    MiddleLeft = 200,
    NearLeft = 300,
    NearRight = 400,
    MiddleRight = 500,
    FarRight = 600,
}

/// <summary>
/// 立ち絵の描画順（Sort Order）の定義
/// UnityのInspectorでのシリアライズ崩れを防ぐため、具体的な数値は持たせません。
/// </summary>
public enum PortraitSortOrder
{
    InFrontOfHeroine = 100, // ヒロインより前
    BehindHeroine =
        200 // ヒロインより後ろ
    ,
}

/// <summary>
/// 立ち絵（胴体、顔、表情エフェクト）の表示・非表示・アニメーションを管理する基底コントローラー。
/// HeroinやNPCなどの派生クラスはこれを継承して固有の処理を実装します。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public abstract class BasePortraitController : MonoBehaviour
{
    #region UI References

    [Header("UI References")]
    [Tooltip("キャラクターの胴体を表示するImage")]
    public Image bodyImage;

    [Tooltip("キャラクターの顔（目や口など）を表示するImage")]
    public Image faceImage;

    [Tooltip("キャラクターの表情エフェクトを表示するImage")]
    public Image expressionImage;

    // 立ち絵の親オブジェクトのRectTransform（スライド移動・オフセット・反転に使用）
    protected RectTransform _portraitContainerRect;

    // 立ち絵全体の透明度を管理するCanvasGroup（フェードアニメーションに使用）
    protected CanvasGroup _portraitCanvasGroup;

    #endregion

    #region Settings & State Variables

    [Header("Character Settings")]
    [Tooltip(
        "このコントローラーが担当するキャラクター（FungusのCharacterオブジェクトをアサインします）"
    )]
    public Character character;

    [Header("Transform Settings")]
    [Tooltip(
        "初期状態で左を向いているかどうか。trueの場合、デフォルトで反転(scale.x = -1)します。"
    )]
    public bool isFacingLeftByDefault = false;

    [Header("Sprite Database")]
    [Tooltip(
        "ここに胴体・顔・表情のスプライトをすべてドラッグ＆ドロップしてください。\n※ファイル名（スプライト名）を元に辞書検索して表示を切り替えます。"
    )]
    public List<Sprite> portraitSprites = new List<Sprite>();

    // スプライト名をキーにして高速に検索するための辞書
    protected Dictionary<string, Sprite> _portraitDictionary;

    // 全ての起動中コントローラーを管理するリスト。カスタムコマンドからの検索に使用します。
    public static List<BasePortraitController> ActiveControllers =
        new List<BasePortraitController>();

    [Header("Animation Settings")]
    [Tooltip(
        "アニメーションの動き方（イージング）。デフォルトはOutQuad（最初は速く、最後にゆっくり止まる）"
    )]
    [SerializeField]
    protected Ease animationEase = Ease.OutQuad;

    // 立ち絵が画面外から表示される際のスライドイン・フェードインの時間（秒）
    protected float slideInDuration = 1f;

    // 体形等のベースが変化した際に、一度フェードアウトして再度フェードインするアニメーションの時間（秒）
    protected float bodyChangeFadeDuration = 0.15f;

    // 実行中のアニメーションを管理するためのDOTweenのSequence。
    protected Sequence _activeTweenAnimation;

    // アニメーション完了後の本来の画面上の位置（Awake時に初期位置を記憶します）
    protected Vector2 _baseOnScreenPosition;

    // 一時的な配置変更（オフセット）
    protected Vector2 _temporaryOffset = Vector2.zero;

    // 現在表示中の胴体スプライト名を記憶する変数。変化アニメーションを再生すべきかの判定に使用します。
    protected string _currentBodySpriteName = "";

    // CanvasのSort Order制御用
    protected Canvas _portraitCanvas;
    protected int _defaultSortOrder;

    // 初期状態を完全に復元するためのバックアップ変数
    protected Vector2 _initialPosition;
    protected Vector3 _initialScale;
    protected float _initialAlpha;

    [Header("Focus Settings (明暗制御)")]
    [Tooltip("他のキャラが話している時の暗さ (1=通常, 0.5=半分の暗さ)")]
    [SerializeField]
    protected float unfocusedDarkness = 0.5f;

    [Tooltip("明暗が切り替わる際のアニメーション時間（秒）")]
    [SerializeField]
    protected float focusFadeDuration = 0.2f;

    #endregion

    #region Unity Lifecycle Methods

    protected virtual void Awake()
    {
        if (character == null)
        {
            Debug.LogError("BasePortraitControllerのcharacterフィールドが設定されていません。");
        }

        // Listで受け取ったスプライト群を、名前検索しやすいようにDictionaryに変換
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

        // Canvasコンポーネントを取得し、初期の描画順を記憶する
        _portraitCanvas = GetComponent<Canvas>();
        if (_portraitCanvas != null)
        {
            _defaultSortOrder = _portraitCanvas.sortingOrder;
        }

        // 自身を静的リストに追加
        ActiveControllers.Add(this);

        // DOTween用およびUI操作用にコンポーネントを取得
        _portraitContainerRect = GetComponent<RectTransform>();
        _portraitCanvasGroup = GetComponent<CanvasGroup>();

        // インスペクター上で設定されている初期状態を記憶
        _initialPosition = _portraitContainerRect.anchoredPosition;
        _initialScale = _portraitContainerRect.localScale;
        _initialAlpha = _portraitCanvasGroup.alpha;

        // インスペクター上で設定されている初期位置（表示時の本来の位置）を記憶
        _baseOnScreenPosition = _portraitContainerRect.anchoredPosition;

        // 初期状態では立ち絵を完全に非表示にしておく
        HidePortrait();

        // デフォルトの向きを適用する
        ApplyDefaultDirection();

        FungusCustomSignals.OnTalkBlockStart += HandleBlockStart;
    }

    protected virtual void Start()
    {
        // OnEnableだと、TalkStartコマンドのOnEnterより後に呼ばれてしまい、
        // イベントを正しく受け取れない可能性があるため、Startでイベント購読を行う。
        FungusCustomSignals.OnRequestDynamicPortrait += HandleShowRequest;
        FungusCustomSignals.OnRequestHideDynamicPortrait += HidePortrait;
    }

    protected virtual void OnDestroy()
    {
        // 自身を静的リストから削除
        ActiveControllers.Remove(this);

        FungusCustomSignals.OnTalkBlockStart -= HandleBlockStart;
        FungusCustomSignals.OnRequestDynamicPortrait -= HandleShowRequest;
        FungusCustomSignals.OnRequestHideDynamicPortrait -= HidePortrait;

        // Tweenの後始末
        _activeTweenAnimation?.Kill();
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// TalkStartコマンドから「会話ブロックが始まった」通知を受け取るハンドラ。
    /// 前の会話で表示していた立ち絵の状態をリセットします。
    /// </summary>
    protected virtual void HandleBlockStart(BlockType blockType)
    {
        ResetToInitialState(); // ブロック開始時に状態を初期値にリセット
        HidePortrait();
    }

    /// <summary>
    /// Sayコマンドから「誰かが話し始めた」通知を受け取るハンドラ。
    /// 話者が自分なら通常の明るさに、自分以外なら少し暗くしてフォーカスを外します。
    /// </summary>
    protected virtual void HandleCharacterSpeak(Character speakingCharacter)
    {
        // characterがアサインされていない場合のエラーを防ぐため名前を安全に取得
        string myName = (character != null) ? character.name : "";

        // 話者が自分（オブジェクトの参照が一致、または名前が一致）なら通常の明るさに
        bool isMyTurn =
            (speakingCharacter == null)
            || (speakingCharacter == character)
            || (speakingCharacter.name == myName);

        Color targetColor = isMyTurn
            ? Color.white
            : new Color(unfocusedDarkness, unfocusedDarkness, unfocusedDarkness, 1f);

        SetPortraitColorTween(targetColor, focusFadeDuration);
    }

    /// <summary>
    /// 立ち絵の色（明暗）をTweenで変更する仮想メソッド。派生クラスで追加のImageを制御可能。
    /// </summary>
    public virtual void SetPortraitColorTween(Color targetColor, float duration)
    {
        bodyImage.DOColor(targetColor, duration).SetUpdate(true);
        faceImage.DOColor(targetColor, duration).SetUpdate(true);
        expressionImage.DOColor(targetColor, duration).SetUpdate(true);
    }

    #endregion

    #region Abstract / Virtual Methods

    /// <summary>
    /// Fungusからの動的立ち絵表示リクエストを処理します。
    /// 子クラスでオーバーライドし、固有の表示ロジック（前髪の追加、体形の判定など）を実装してください。
    /// </summary>
    /// <param name="portraitString">Fungus側で指定されたポートレート指定文字列</param>
    public virtual void HandleShowRequest(string portraitString)
    {
        // 派生クラスでオーバーライドして使用
    }

    #endregion

    #region Transform Controls (一時的な配置・向き変更)

    /// <summary>
    /// 初期状態の向きを適用します。
    /// </summary>
    protected void ApplyDefaultDirection()
    {
        SetDirection(isFacingLeftByDefault);
    }

    /// <summary>
    /// 一時的に立ち絵の左右の向きを変更します。
    /// </summary>
    /// <param name="isLeft">trueの場合、左向きに反転します</param>
    public void SetDirection(bool isLeft)
    {
        Vector3 scale = _portraitContainerRect.localScale;
        scale.x = isLeft ? -1f : 1f;
        _portraitContainerRect.localScale = scale;
    }

    /// <summary>
    /// 一時的にRectTransformのX座標を指定したEnumの値に変更します。
    /// </summary>
    public void SetPositionX(PortraitPositionX positionX)
    {
        // Enumの値から実際のX座標（絶対座標）を決定する
        float targetX = 0f;
        switch (positionX)
        {
            case PortraitPositionX.FarLeft:
                targetX = -700f;
                break;
            case PortraitPositionX.MiddleLeft:
                targetX = -550f;
                break;
            case PortraitPositionX.NearLeft:
                targetX = -400f;
                break;
            case PortraitPositionX.NearRight:
                targetX = 400f;
                break;
            case PortraitPositionX.MiddleRight:
                targetX = 550f;
                break;
            case PortraitPositionX.FarRight:
                targetX = 700f;
                break;
        }

        // 本来の位置(_baseOnScreenPosition.x)から、指定された絶対座標(targetX)への差分をオフセットとして設定
        _temporaryOffset.x = targetX - _baseOnScreenPosition.x;
        UpdateScreenPosition();
    }

    /// <summary>
    /// 一時的に描画順(Sort Order)を変更します。
    /// </summary>
    public void SetSortOrder(PortraitSortOrder sortOrder)
    {
        if (_portraitCanvas != null)
        {
            // Enumの値から実際のSort Orderを決定する
            int targetOrder = _defaultSortOrder;
            switch (sortOrder)
            {
                case PortraitSortOrder.InFrontOfHeroine:
                    targetOrder = -10;
                    break; // ヒロインより前
                case PortraitSortOrder.BehindHeroine:
                    targetOrder = -12;
                    break; // ヒロインより後ろ
            }

            _portraitCanvas.sortingOrder = targetOrder;
        }
        else
        {
            Debug.LogWarning(
                "Canvasコンポーネントがアタッチされていないため、描画順を変更できません。",
                this
            );
        }
    }

    /// <summary>
    /// 現在の本来の位置とオフセットを考慮して実際のRectTransformの位置を更新します。
    /// </summary>
    protected void UpdateScreenPosition()
    {
        _portraitContainerRect.anchoredPosition = _baseOnScreenPosition + _temporaryOffset;
    }

    /// <summary>
    /// Awake時に保存した基本状態にリセットします。
    /// </summary>
    public virtual void ResetToInitialState()
    {
        // 実行中のアニメーションがあれば停止
        _activeTweenAnimation?.Kill();
        _activeTweenAnimation = null;

        // 座標とオフセットの復元
        _portraitContainerRect.anchoredPosition = _initialPosition;
        _temporaryOffset = Vector2.zero;

        // スケール（向き）の復元
        _portraitContainerRect.localScale = _initialScale;

        ApplyDefaultDirection(); // デフォルトの向き（初期値）に戻す

        // 透明度の復元
        _portraitCanvasGroup.alpha = _initialAlpha;

        // 描画順の復元
        if (_portraitCanvas != null)
        {
            _portraitCanvas.sortingOrder = _defaultSortOrder;
        }

        // 色（明暗）の復元
        SetPortraitColorTween(Color.white, 0f);

        // 各Imageの有効状態を初期化（HidePortraitのロジックに準拠）
        bodyImage.enabled = false;
        faceImage.enabled = false;
        expressionImage.enabled = false;
    }

    #endregion

    #region Core Display Logic

    /// <summary>
    /// 指定された名前のスプライトで立ち絵を画面に表示します。
    /// </summary>
    public virtual void ShowPortrait(
        string bodySpriteName,
        string faceSpriteName,
        string expressionSpriteName
    )
    {
        if (_activeTweenAnimation != null && _activeTweenAnimation.IsActive())
        {
            _activeTweenAnimation.Complete();
        }

        bool wasHidden = _portraitCanvasGroup.alpha == 0;
        bool isBodyChange = !wasHidden && _currentBodySpriteName != bodySpriteName;

        if (isBodyChange)
        {
            _activeTweenAnimation = DOTween.Sequence();
            _activeTweenAnimation
                .Append(_portraitCanvasGroup.DOFade(0, bodyChangeFadeDuration))
                .AppendCallback(() =>
                {
                    SetAllSprites(bodySpriteName, faceSpriteName, expressionSpriteName);
                })
                .Append(_portraitCanvasGroup.DOFade(1, bodyChangeFadeDuration))
                .SetUpdate(true)
                .OnComplete(() => _activeTweenAnimation = null);
        }
        else if (wasHidden)
        {
            SetAllSprites(bodySpriteName, faceSpriteName, expressionSpriteName);

            if (!bodyImage.enabled)
                return;

            Vector2 targetPosition = _baseOnScreenPosition + _temporaryOffset;
            // X座標が0以下なら左から、0より大きいなら右側から出現させる
            float startOffsetX =
                targetPosition.x <= 0f
                    ? -_portraitContainerRect.rect.width
                    : _portraitContainerRect.rect.width;

            Vector2 offScreenPosition = new Vector2(
                targetPosition.x + startOffsetX,
                targetPosition.y
            );

            _portraitContainerRect.anchoredPosition = offScreenPosition;
            _portraitCanvasGroup.alpha = 0f;

            _activeTweenAnimation = DOTween.Sequence();
            _activeTweenAnimation
                .Append(
                    _portraitContainerRect
                        .DOAnchorPos(targetPosition, slideInDuration)
                        .SetEase(animationEase)
                )
                .Join(_portraitCanvasGroup.DOFade(1f, slideInDuration))
                .SetUpdate(true)
                .OnComplete(() => _activeTweenAnimation = null);
        }
        else
        {
            SetAllSprites(bodySpriteName, faceSpriteName, expressionSpriteName);
        }
    }

    /// <summary>
    /// 辞書からスプライトを検索し、各Imageコンポーネントに割り当てる仮想メソッド。
    /// </summary>
    protected virtual void SetAllSprites(
        string bodySpriteName,
        string faceSpriteName,
        string expressionSpriteName
    )
    {
        _currentBodySpriteName = bodySpriteName;

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
            expressionImage.enabled = false;
        }
    }

    /// <summary>
    /// 立ち絵を即座に非表示にし、内部状態をリセットします。
    /// </summary>
    public virtual void HidePortrait()
    {
        _activeTweenAnimation?.Kill();
        _activeTweenAnimation = null;

        _portraitCanvasGroup.alpha = 0;

        bodyImage.enabled = false;
        faceImage.enabled = false;
        expressionImage.enabled = false;
    }

    #endregion
}

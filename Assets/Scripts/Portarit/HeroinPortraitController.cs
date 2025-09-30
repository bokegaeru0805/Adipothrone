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

    [Tooltip("Immobile状態の時に表示する補助的なImage")]
    public Image immobileAuxImage;

    [Header("Sprite Database")]
    [Tooltip("ここにHeroinの胴体・顔・表情のスプライトをすべてドラッグ＆ドロップしてください")]
    public List<Sprite> portraitSprites = new List<Sprite>();

    private Dictionary<string, Sprite> _portraitDictionary;

    [Tooltip("アニメーションの動き方")]
    [SerializeField]
    private Ease animationEase = Ease.OutQuad;

    //立ち絵が表示される際のアニメーション時間（秒）
    private float slideInDuration = 1f;

    //体形が変化する際のフェードアニメーション時間（秒）
    private float bodyChangeFadeDuration = 0.15f;

    // 実行中のアニメーションを管理するためのDOTweenのSequence
    private Sequence _activeTweenAnimation;

    // 立ち絵の親オブジェクトのRectTransform
    private RectTransform _portraitContainerRect;

    // 立ち絵全体の透明度を管理するCanvasGroup
    private CanvasGroup _portraitCanvasGroup;

    // アニメーション後の最終的な画面上の位置
    private Vector2 _onScreenPosition;

    // 現在の会話のBlockTypeを保持する変数
    private BlockType currentBlockType = BlockType.Default;

    // 現在表示中の胴体スプライト名を記憶する変数
    private string _currentBodySpriteName = "";

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

        // immobile用のImageも初期状態では非表示にする
        if (immobileAuxImage != null)
        {
            immobileAuxImage.enabled = false;
        }
        else
        {
            Debug.LogWarning("immobileAuxImageが設定されていません。");
        }

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

            // --- immobile状態かどうかの判定 ---
            bool isImmobile = (bodyStateString == "immobile");

            string bodySpriteName = $"{characterName}_{bodyStateString}_body";
            string faceSpriteName = $"{characterName}_{bodyStateString}_face";
            string expressionSpriteName = $"{characterName}_expression_{expressionString}";

            // 組み立てた名前で自身の表示メソッドを呼び出す
            ShowPortrait(bodySpriteName, faceSpriteName, expressionSpriteName, isImmobile);
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
        string expressionSpriteName,
        bool isImmobile
    )
    {
        if (_activeTweenAnimation != null && _activeTweenAnimation.IsActive())
        {
            // Kill()の代わりにComplete()を呼び出すことで、
            // アニメーションを瞬時に完了させ、位置や透明度を最終状態にします。
            _activeTweenAnimation.Complete();
        }

        // 状況を判定
        // CanvasGroupの透明度で、元々非表示だったかを判定
        bool wasHidden = _portraitCanvasGroup.alpha == 0;
        bool isBodyChange = !wasHidden && _currentBodySpriteName != bodySpriteName;

        // 【ケース1】表示中に体形が変化した場合
        // 条件：立ち絵が表示中(wasHiddenがfalse)であり、かつ新しく指定された胴体スプライト名(_currentBodySpriteName)が、
        //      以前表示されていたものと異なる場合。
        if (isBodyChange)
        {
            // --- 処理の流れ ---
            // アニメーションを順番に実行するため、DOTweenのSequenceを作成します。
            // 1. フェードアウト → 2. スプライト入れ替え → 3. フェードイン
            _activeTweenAnimation = DOTween.Sequence();
            _activeTweenAnimation
                // 1. フェードアウト：現在の立ち絵を、指定した時間(bodyChangeFadeDuration)をかけて完全に見えなくします。
                .Append(_portraitCanvasGroup.DOFade(0, bodyChangeFadeDuration))
                // 2. スプライト入れ替え：フェードアウトが完了した瞬間にこの処理を呼び出します。
                //    立ち絵が見えなくなっている間に、スプライトを新しいものに瞬時に差し替えます。
                .AppendCallback(() =>
                {
                    SetAllSprites(bodySpriteName, faceSpriteName, expressionSpriteName, isImmobile);
                })
                // 3. フェードイン：新しいスプライトに切り替わった状態で、指定時間をかけて再び表示させます。
                .Append(_portraitCanvasGroup.DOFade(1, bodyChangeFadeDuration))
                // Time.timeScaleが0（ポーズ中）でもアニメーションが動くように設定します。
                .SetUpdate(true)
                // アニメーションが全て完了したら、管理用の変数をリセットします。
                .OnComplete(() => _activeTweenAnimation = null);
        }
        // 【ケース2】初めて表示される場合
        // 条件：立ち絵が完全に非表示(wasHiddenがtrue)の状態から表示される場合。
        else if (wasHidden)
        {
            // --- 処理の流れ ---
            // 1. 事前準備 → 2. アニメーション開始

            // 1. 事前準備：
            //    まず、これから表示するスプライトをImageコンポーネントに設定します。
            SetAllSprites(bodySpriteName, faceSpriteName, expressionSpriteName, isImmobile);

            //    もしスプライトの設定に失敗してbodyImageが無効なままなら、アニメーションは実行せず処理を終了します。
            if (!bodyImage.enabled)
                return;

            //    アニメーションの開始地点（画面外の左側）を計算します。
            Vector2 offScreenPosition = new Vector2(
                _onScreenPosition.x - _portraitContainerRect.rect.width,
                _onScreenPosition.y
            );

            //    アニメーションが始まる前に、立ち絵を開始地点へ移動させ、完全に見えないようにしておきます。
            _portraitContainerRect.anchoredPosition = offScreenPosition;
            _portraitCanvasGroup.alpha = 0f;

            // 2. アニメーション開始：
            //    スライド移動とフェードインを「同時」に実行するため、Sequenceを作成します。
            _activeTweenAnimation = DOTween.Sequence();
            _activeTweenAnimation
                // 立ち絵の位置を、画面外から本来の位置(_onScreenPosition)へ指定時間(slideInDuration)かけて移動させます。
                .Append(
                    _portraitContainerRect
                        .DOAnchorPos(_onScreenPosition, slideInDuration)
                        .SetEase(animationEase)
                )
                // .Join()を使うことで、前のAppend()のアニメーションと「同時」に、透明度を0から1へ変化させます。
                .Join(_portraitCanvasGroup.DOFade(1f, slideInDuration))
                // Time.timeScaleが0でもアニメーションが動くようにします。
                .SetUpdate(true)
                // アニメーション完了後に管理変数をリセットします。
                .OnComplete(() => _activeTweenAnimation = null);
        }
        // 【ケース3】表示中に表情だけが変わった場合
        // 条件：上記以外のすべての場合。つまり、立ち絵は表示中で、体形も変わらない（表情や補助画像だけが変わる）場合。
        else
        {
            // この場合は特別なアニメーションは不要なため、
            // 新しいスプライトを瞬時に設定するだけで処理を完了します。
            SetAllSprites(bodySpriteName, faceSpriteName, expressionSpriteName, isImmobile);
        }
    }

    /// <summary>
    ///  全てのスプライトを設定するヘルパーメソッド
    /// </summary>
    private void SetAllSprites(
        string bodySpriteName,
        string faceSpriteName,
        string expressionSpriteName,
        bool isImmobile
    )
    {
        _currentBodySpriteName = bodySpriteName;

        // 胴体の設定
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

        // 顔の設定
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

        // 表情エフェクトの設定
        if (_portraitDictionary.TryGetValue(expressionSpriteName, out Sprite expressionSprite))
        {
            expressionImage.sprite = expressionSprite;
            expressionImage.enabled = true;
        }
        else
        {
            expressionImage.enabled = false;
        }

        // Immobile状態の補助Imageの設定
        if (immobileAuxImage != null)
        {
            immobileAuxImage.enabled = isImmobile;
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

        if (immobileAuxImage != null)
        {
            immobileAuxImage.enabled = false;
        }
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

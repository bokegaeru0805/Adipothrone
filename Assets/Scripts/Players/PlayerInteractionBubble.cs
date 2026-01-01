using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// プレイヤーが特定のタグを持つオブジェクトに触れた際に、頭上の吹き出しスプライトを管理するスクリプト。
/// プレイヤーのルートオブジェクト（Rigidbody2Dを持つオブジェクト）にアタッチして使用します。
/// </summary>
public class PlayerInteractionBubble : MonoBehaviour
{
    [Header("表示する吹き出し")]
    [Tooltip("吹き出しを表示するためのSpriteRenderer")]
    [SerializeField]
    private SpriteRenderer bubbleSpriteRenderer;

    [Header("タグごとのスプライト設定")]
    [Tooltip("インタラクト可能なオブジェクト用の吹き出し")]
    [SerializeField]
    private Sprite interactableBubbleSprite;

    [Tooltip("エリア遷移用の吹き出し")]
    [SerializeField]
    private Sprite areaTransitionBubbleSprite;

    private Transform bubbleTransform; // 吹き出しのTransformをキャッシュ

    // 実行中にタグ名（string）から対応するスプライト（Sprite）を高速に引くための辞書
    private Dictionary<string, Sprite> bubbleDictionary;

    // 接触中の「Collider2D」を直接保持するリスト
    private List<Collider2D> activeColliders = new List<Collider2D>();
    private bool isTalking = false; // 会話状態を保存するローカル変数
    private Collider2D monitoredCollider = null; // タグの変更を監視している対象のコライダー
    private string monitoredTag = null; // 監視対象のコライダーの前回チェック時のタグ

    private float floatingHeight = 0.3f; //上下に浮遊する移動幅
    private float floatingDuration = 1.5f; //浮遊アニメーションの片道にかかる時間（秒）
    private Vector3 initialPosition; // 浮遊アニメーションの基準となる初期座標(ローカル座標)

    private void Awake()
    {
        if (bubbleSpriteRenderer == null)
        {
            Debug.LogError("吹き出し用のSpriteRendererが設定されていません。", this);
            this.enabled = false;
            return;
        }
        else
        {
            bubbleTransform = bubbleSpriteRenderer.transform;
        }

        // GameConstantsで定義されたタグと、Inspectorで設定されたスプライトを紐付けて辞書を作成
        bubbleDictionary = new Dictionary<string, Sprite>();
        if (interactableBubbleSprite != null)
        {
            bubbleDictionary[GameConstants.INTERACTABLE_OBJECT_TAG_NAME] = interactableBubbleSprite;
        }
        if (areaTransitionBubbleSprite != null)
        {
            bubbleDictionary[GameConstants.AREA_TRANSITION_TAG_NAME] = areaTransitionBubbleSprite;
        }

        // 初期位置を保存
        initialPosition = bubbleTransform.localPosition;
        // ゲーム開始時は吹き出しを非表示にする
        bubbleSpriteRenderer.enabled = false;
    }

    /// <summary>
    /// オブジェクトが有効になった際に、イベントを購読します。
    /// </summary>
    private void OnEnable()
    {
        // GameManagerの会話状態変化イベントを購読
        GameManager.OnTalkingStateChanged += HandleTalkingStateChanged;
        // オブジェクトが有効になった時、リストをクリアして安全な状態から始める
        activeColliders.Clear();
        UpdateBubbleState();
    }

    /// <summary>
    /// オブジェクトが無効になった際に、イベントの購読を解除します。
    /// </summary>
    private void OnDisable()
    {
        // メモリリークを防ぐため、必ず購読を解除
        GameManager.OnTalkingStateChanged -= HandleTalkingStateChanged;
    }

    /// <summary>
    /// 毎フレーム、監視対象のタグが変更されていないかチェックする
    /// </summary>
    private void Update()
    {
        // 監視対象のコライダーが存在するかチェック
        if (monitoredCollider != null)
        {
            // 監視対象のコライダーの現在のタグを取得
            string currentTag = monitoredCollider.tag;

            // 記憶しておいたタグと現在のタグが異なる場合、タグが変更されたと判断
            if (currentTag != monitoredTag)
            {
                // 吹き出しの状態を再評価する
                UpdateBubbleState();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 接触したオブジェクトのタグが吹き出し表示対象か確認
        if (bubbleDictionary.ContainsKey(other.tag))
        {
            // 接触中の「コライダー」をリストに追加
            if (!activeColliders.Contains(other))
            {
                activeColliders.Add(other);
            }
            UpdateBubbleState();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // 離れた「コライダー」がリストにあれば削除（タグに関係なく確実に削除できる）
        if (activeColliders.Remove(other))
        {
            UpdateBubbleState();
        }
    }

    /// <summary>
    /// 現在の接触状況に応じて、吹き出しの表示を更新する。
    /// </summary>
    private void UpdateBubbleState()
    {
        // もし会話中なら、他の条件に関わらず吹き出しを非表示にする
        if (isTalking)
        {
            SetBubbleDisplayState(false);
            // 監視対象をクリア
            monitoredCollider = null;
            monitoredTag = null;
            return;
        }

        // --- 以下は会話中でない場合の処理 ---

        // リストからnullや無効になったコライダーを掃除する
        activeColliders.RemoveAll(c => c == null || !c.gameObject.activeInHierarchy);

        // 接触している対象がなければ非表示にする
        if (activeColliders.Count == 0)
        {
            SetBubbleDisplayState(false);
            // 監視対象をクリア
            monitoredCollider = null;
            monitoredTag = null;
            return;
        }

        // 最後に接触した有効なコライダーを取得
        Collider2D latestCollider = activeColliders.Last();
        string latestTag = latestCollider.tag;

        // 新しい監視対象として、最後に接触したコライダーとその現在のタグを記録
        monitoredCollider = latestCollider;
        monitoredTag = latestTag;

        // 最後に接触したコライダーの「現在の」タグが、表示対象のタグであるか再確認
        if (bubbleDictionary.TryGetValue(latestTag, out Sprite bubbleSprite))
        {
            // 対応するスプライトをセットして表示
            bubbleSpriteRenderer.sprite = bubbleSprite;
            SetBubbleDisplayState(true);
        }
        else
        {
            // 最新のコライダーのタグが表示対象外（例："Untagged"）に変わっていた場合、非表示にする
            SetBubbleDisplayState(false);
        }
    }

    // <summary>
    /// 吹き出しの表示・非表示を切り替え、表示する場合は浮遊アニメーションを開始します。
    /// </summary>
    /// <param name="shouldShow">trueの場合表示しアニメーション開始、falseの場合非表示にしアニメーション停止。</param>
    private void SetBubbleDisplayState(bool shouldShow)
    {
        // 既存のTweenがあれば停止してから新しいTweenを開始する（安全のため）
        // このオブジェクトに紐づくDOTweenの動作をすべて停止
        bubbleTransform.DOKill();

        if (shouldShow)
        {
            // DOMoveYを使って、Y軸方向にアニメーションさせる
            bubbleTransform
                .DOLocalMoveY(initialPosition.y + floatingHeight, floatingDuration)
                .SetEase(Ease.InOutSine) // 動きの緩急をサインカーブのように滑らかにする
                .SetLoops(-1, LoopType.Yoyo) // 無限に（-1）、行って戻ってくる（Yoyo）ループを設定
                .SetUpdate(UpdateType.Normal); // Time.timeScaleの影響を受けるように設定（デフォルト）
        }
        else
        {
            // 座標をアニメーション開始前の初期位置に戻す
            bubbleTransform.localPosition = initialPosition;
        }

        // 吹き出しの表示状態を更新
        bubbleSpriteRenderer.enabled = shouldShow;
    }

    /// <summary>
    /// GameManagerの会話状態が変化したときに呼び出される処理
    /// </summary>
    private void HandleTalkingStateChanged(bool talkState)
    {
        isTalking = talkState;

        // 会話状態が変わったので、吹き出しの表示を再評価する
        UpdateBubbleState();
    }
}

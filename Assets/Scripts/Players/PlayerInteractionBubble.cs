using System.Collections.Generic;
using System.Linq;
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

    // 実行中にタグ名（string）から対応するスプライト（Sprite）を高速に引くための辞書
    private Dictionary<string, Sprite> bubbleDictionary;

    // 接触中の「Collider2D」を直接保持するリスト
    private List<Collider2D> activeColliders = new List<Collider2D>();

    private void Awake()
    {
        if (bubbleSpriteRenderer == null)
        {
            Debug.LogError("吹き出し用のSpriteRendererが設定されていません。", this);
            this.enabled = false;
            return;
        }

        // GameConstantsで定義されたタグと、Inspectorで設定されたスプライトを紐付けて辞書を作成
        bubbleDictionary = new Dictionary<string, Sprite>();
        if (interactableBubbleSprite != null)
        {
            bubbleDictionary[GameConstants.InteractableObjectTagName] = interactableBubbleSprite;
        }
        if (areaTransitionBubbleSprite != null)
        {
            bubbleDictionary[GameConstants.AreaTransitionTagName] = areaTransitionBubbleSprite;
        }

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
        if (GameManager.IsTalking)
        {
            bubbleSpriteRenderer.enabled = false;
            return;
        }

        // --- 以下は会話中でない場合の処理 ---

        // リストからnullや無効になったコライダーを掃除する
        activeColliders.RemoveAll(c => c == null || !c.gameObject.activeInHierarchy);

        // 接触している対象がなければ非表示にする
        if (activeColliders.Count == 0)
        {
            bubbleSpriteRenderer.enabled = false;
            return;
        }

        // 最後に接触した有効なコライダーを取得
        Collider2D latestCollider = activeColliders.Last();
        string latestTag = latestCollider.tag;

        // 最後に接触したコライダーの「現在の」タグが、表示対象のタグであるか再確認
        if (bubbleDictionary.TryGetValue(latestTag, out Sprite bubbleSprite))
        {
            // 対応するスプライトをセットして表示
            bubbleSpriteRenderer.sprite = bubbleSprite;
            bubbleSpriteRenderer.enabled = true;
        }
        else
        {
            // 最新のコライダーのタグが表示対象外（例："Untagged"）に変わっていた場合、非表示にする
            bubbleSpriteRenderer.enabled = false;
        }
    }

    /// <summary>
    /// GameManagerの会話状態が変化したときに呼び出される処理
    /// </summary>
    private void HandleTalkingStateChanged(bool isTalking)
    {
        // 会話状態が変わったので、吹き出しの表示を再評価する
        UpdateBubbleState();
    }
}

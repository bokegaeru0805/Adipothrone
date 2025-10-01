using System.Collections;
using System.Collections.Generic;
using Fungus;
using UnityEngine;

/// <summary>
/// 【最適化版】NPCとの会話をトリガーする汎用コンポーネント。
/// Inspectorから設定された条件リストに基づき、実行するFungusブロックを動的に決定します。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class NPCDialogueTrigger : MonoBehaviour
{
    [Header("実行するFungusのFlowchart")]
    [SerializeField]
    private Flowchart targetFlowchart;

    [Header("会話の分岐設定")]
    [Tooltip("どの条件にも一致しない場合に実行されるデフォルトの会話ブロック名。")]
    [SerializeField]
    private string defaultBlockName;

    [Tooltip("会話の条件リスト。上から順に評価され、最初に一致したものが実行されます。")]
    [SerializeField]
    private List<DialogueCondition> dialogueConditions = new List<DialogueCondition>();

    private ShopInteractionTrigger shopInteractionTrigger = null;
    private bool isShopTrigger = false;
    private bool isTalking = false; // 会話状態を保存するローカル変数

    private void Awake()
    {
        // 必須コンポーネントのnullチェック
        if (targetFlowchart == null)
        {
            Debug.LogError("ターゲットのFlowchartが設定されていません。", this);
        }

        shopInteractionTrigger = this.GetComponent<ShopInteractionTrigger>();
        isShopTrigger = shopInteractionTrigger != null;
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // ゲームが動作中、他の会話が実行中でなく、プレイヤーがインタラクトした場合に会話を試みる
        if (
            Time.timeScale > 0
            && !isTalking
            && InputManager.instance.GetInteract()
            && collision.gameObject.CompareTag(GameConstants.PlayerTagName)
        )
        {
            // TryExecuteDialogue()は、無効化された後でも呼び出される
            TryExecuteDialogue();
        }
    }

    /// <summary>
    /// 設定された会話条件を評価し、適切なFungusブロックを実行する。
    /// </summary>
    private void TryExecuteDialogue()
    {
        if (targetFlowchart == null)
            return;

        // 条件リストを上から順に評価
        foreach (var condition in dialogueConditions)
        {
            if (condition.AreAllFlagsMet())
            {
                if (
                    isShopTrigger
                    && (
                        condition.blockNameToExecute == "Shop"
                        || condition.blockNameToExecute == "shop"
                    )
                )
                {
                    // ShopInteractionTriggerが設定されている場合、ShopTriggerを実行
                    if (shopInteractionTrigger != null)
                    {
                        shopInteractionTrigger.ShopTrigger();
                    }
                }
                else
                {
                    // 条件に一致した場合、ブロックを実行し、追加イベントを呼び出す
                    FungusHelper.ExecuteBlock(targetFlowchart, condition.blockNameToExecute);
                }

                condition.onDialogueTriggered?.Invoke(); // 追加イベントの呼び出し
                return; // 一致したものが見つかったので処理終了
            }
        }

        // どの条件にも一致しなかった場合、デフォルトのブロックを実行
        if (!string.IsNullOrEmpty(defaultBlockName))
        {
            FungusHelper.ExecuteBlock(targetFlowchart, defaultBlockName);
        }
    }

    private void OnEnable()
    {
        StartCoroutine(DelayedInitialization());
    }

    /// <summary>
    /// 全てのAwake/Startが完了するのを待ってから、初期化処理を実行するコルーチン
    /// </summary>
    private IEnumerator DelayedInitialization()
    {
        // 最初のフレームの描画が終わるまで待つ
        // これにより、全てのシングルトンが確実に初期化されている状態になる
        yield return new WaitForEndOfFrame();

        // イベントを購読する
        GameManager.OnTalkingStateChanged += HandleTalkingStateChanged;
    }

    private void OnDisable()
    {
        // オブジェクトが非アクティブになったら、購読を解除（メモリリーク防止）
        GameManager.OnTalkingStateChanged -= HandleTalkingStateChanged;
    }

    /// <summary>
    /// GameManagerから会話状態の変更通知を受け取る
    /// </summary>
    private void HandleTalkingStateChanged(bool talkState)
    {
        isTalking = talkState;
    }
}

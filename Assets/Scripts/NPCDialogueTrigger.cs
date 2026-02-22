using System;
using System.Collections;
using System.Collections.Generic;
using Fungus;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// NPCとの会話をトリガーする汎用コンポーネント。
/// Inspectorから設定された条件リストに基づき、実行するFungusブロックを動的に決定します。
/// また、フラグ状態に応じて吹き出しアイコンの表示/非表示を制御します。
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

    [InfoBox("時系列が後の条件を上に配置してください。")]
    [Tooltip("会話の条件リスト。上から順に評価され、最初に一致したものが実行されます。")]
    [SerializeField]
    private List<DialogueCondition> dialogueConditions = new List<DialogueCondition>();

    [Header("吹き出し設定")]
    [Tooltip("頭上に表示する吹き出しのゲームオブジェクト")]
    [SerializeField]
    private GameObject speechBubbleObject;

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

        // 初期状態では吹き出しを一旦非表示にしておく（DelayedInitializationで正しい状態になる）
        if (speechBubbleObject != null)
        {
            speechBubbleObject.SetActive(false);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // コンポーネントが無効(false)なら、ここで処理を終了する
        if (!this.enabled)
            return;

        // ゲームが動作中、他の会話が実行中でなく、プレイヤーがインタラクトした場合に会話を試みる
        if (
            Time.timeScale > 0
            && !isTalking
            && InputManager.instance.GetInteract()
            && collision.gameObject.CompareTag(GameConstants.PLAYER_TAG_NAME)
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
        FlagManager.OnBoolFlagChanged += HandleFlagChanged;

        // 初期化時に一度吹き出しの状態を更新
        UpdateBubbleState();
    }

    private void OnDisable()
    {
        // オブジェクトが非アクティブになったら、購読を解除（メモリリーク防止）
        GameManager.OnTalkingStateChanged -= HandleTalkingStateChanged;
        FlagManager.OnBoolFlagChanged -= HandleFlagChanged;
    }

    /// <summary>
    /// GameManagerから会話状態の変更通知を受け取る
    /// </summary>
    private void HandleTalkingStateChanged(bool talkState)
    {
        isTalking = talkState;
        // 会話状態が変わったら、即座に吹き出しの表示状態も更新する
        UpdateBubbleState();
    }

    /// <summary>
    /// フラグ変更時に呼ばれるコールバック
    /// </summary>
    private void HandleFlagChanged(Enum flagName, bool newValue)
    {
        UpdateBubbleState();
    }

    /// <summary>
    /// 現在のフラグ状態に基づいて、吹き出しの表示/非表示を更新する。
    /// </summary>
    private void UpdateBubbleState()
    {
        if (speechBubbleObject == null)
            return;

        // 会話中は強制的に吹き出しを非表示にする
        if (isTalking)
        {
            if (speechBubbleObject.activeSelf)
            {
                speechBubbleObject.SetActive(false);
            }
            return;
        }

        bool shouldShow = false; // 初期値は非表示

        // 会話実行時と同じロジックで、現在有効な条件を探す
        foreach (var condition in dialogueConditions)
        {
            if (condition.AreAllFlagsMet())
            {
                shouldShow = condition.showBubble;
                break; // 最初に一致した条件のshowBubble設定を採用
            }
        }

        // 状態が異なる場合のみSetActiveを呼ぶ（負荷軽減）
        if (speechBubbleObject.activeSelf != shouldShow)
        {
            speechBubbleObject.SetActive(shouldShow);
        }
    }
}

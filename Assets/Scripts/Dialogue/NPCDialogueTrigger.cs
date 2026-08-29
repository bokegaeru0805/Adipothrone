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
    #region Inspector Settings

    [Header("実行するFungusのFlowchart")]
    [Tooltip(
        "trueの場合、シーン内の「GlobalFlowchart」という名前のオブジェクトを自動的に取得して使用します。"
    )]
    [SerializeField]
    private bool useGlobalFlowchart = false;

    [Tooltip("実行するFlowchart（useGlobalFlowchartがtrueの場合は自動で上書きされます）")]
    [SerializeField]
    [HideIf(nameof(useGlobalFlowchart))]
    private Flowchart targetFlowchart;

    [Header("会話の分岐設定")]
    [Tooltip("どの条件にも一致しない場合に実行されるデフォルトの会話ブロック名。")]
    [SerializeField]
    private string defaultBlockName;

    [InfoBox("時系列が後の条件（進行度が高いもの）を下に配置してください。")]
    [Tooltip("会話の条件リスト。下から順（逆順）に評価され、最初に一致したものが実行されます。")]
    [SerializeField]
    private List<DialogueCondition> dialogueConditions = new List<DialogueCondition>();

    [Header("吹き出し設定")]
    [Tooltip("頭上に表示する吹き出しのゲームオブジェクト")]
    [SerializeField]
    private GameObject speechBubbleObject;

    #endregion

    #region Private Fields

    private const string LocalFlowchartObjectName = "LocalFlowchart";

    // 外部コンポーネント参照
    private ShopInteractionTrigger shopInteractionTrigger = null;

    // 状態フラグ
    private bool isShopTrigger = false;
    private bool isTalking = false; // 現在会話中かどうか
    private bool isDialogueEnabled = true; // 会話機能自体が有効かどうか
    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// コンポーネント追加時に、同じシーン内のLocalFlowchartを自動設定します。
    /// </summary>
    private void Reset()
    {
        if (useGlobalFlowchart || targetFlowchart != null)
            return;

        Flowchart localFlowchart = FindLocalFlowchartInScene();

        if (localFlowchart == null)
        {
            Debug.LogWarning(
                $"同じシーン内に「{LocalFlowchartObjectName}」という名前のFlowchartが見つからないため、targetFlowchartを自動設定できませんでした。",
                this
            );
            return;
        }

        targetFlowchart = localFlowchart;
    }

    private Flowchart FindLocalFlowchartInScene()
    {
        Flowchart localFlowchart = null;
        Flowchart[] flowcharts = FindObjectsByType<Flowchart>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (Flowchart flowchart in flowcharts)
        {
            if (
                flowchart.gameObject.scene != gameObject.scene
                || !string.Equals(
                    flowchart.gameObject.name,
                    LocalFlowchartObjectName,
                    StringComparison.Ordinal
                )
            )
            {
                continue;
            }

            if (localFlowchart != null)
                return null;

            localFlowchart = flowchart;
        }

        return localFlowchart;
    }

    /// <summary>
    /// シーン上のPrefabインスタンスへ配置・変更された際に、LocalFlowchartを自動設定します。
    /// </summary>
    private void OnValidate()
    {
        // Prefabアセット編集中や再生中は、シーン上の参照を自動保存しない
        if (
            Application.isPlaying
            || useGlobalFlowchart
            || targetFlowchart != null
            || !gameObject.scene.IsValid()
        )
        {
            return;
        }

        targetFlowchart = FindLocalFlowchartInScene();
    }

    private void Awake()
    {
        // Prefabから生成されたインスタンスなど、シーン参照を保持できない場合に実行時解決する
        if (targetFlowchart == null && !useGlobalFlowchart)
        {
            targetFlowchart = FindLocalFlowchartInScene();
        }

        // 必須コンポーネントのチェック
        if (targetFlowchart == null && !useGlobalFlowchart)
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

    private void Start()
    {
        if (useGlobalFlowchart)
        {
            targetFlowchart = GlobalFlowchartController.instance.globalFlowchart;
            if (targetFlowchart == null)
            {
                Debug.LogError("GlobalFlowchartControllerのFlowchartが設定されていません。", this);
            }
        }
    }

    private void OnEnable()
    {
        StartCoroutine(DelayedInitialization());
    }

    private void OnDisable()
    {
        // オブジェクトが非アクティブになったら、購読を解除（メモリリーク防止）
        GameManager.OnTalkingStateChanged -= HandleTalkingStateChanged;
        FlagManager.OnBoolFlagChanged -= HandleFlagChanged;
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // このコンポーネントが無効化されている、会話機能が無効化されている、またはターゲットのFlowchartが設定されていない場合は、何もしない
        if (!this.enabled || !isDialogueEnabled || targetFlowchart == null)
            return;

        // ゲームが動作中、他の会話が実行中でなく、プレイヤーがインタラクトした場合に会話を試みる
        if (
            Time.timeScale > 0
            && !isTalking
            && InputManager.instance.GetInteract()
            && collision.gameObject.CompareTag(GameConstants.PLAYER_TAG_NAME)
        )
        {
            TryExecuteDialogue();
        }
    }

    #endregion

    #region Initialization

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

    #endregion

    #region Public API

    /// <summary>
    /// 外部から会話機能の有効/無効を切り替えます。
    /// 無効化するとプレイヤーのインタラクト吹き出しも出なくなります。
    /// </summary>
    /// <param name="isEnabled">有効にする場合はtrue、無効にする場合はfalse</param>
    public void SetDialogueEnabled(bool isEnabled)
    {
        isDialogueEnabled = isEnabled;

        if (isEnabled)
        {
            // 有効化：タグをInteractableに変更して、PlayerInteractionBubble を表示させる
            gameObject.tag = GameConstants.INTERACTABLE_OBJECT_TAG_NAME;
            UpdateBubbleState();
        }
        else
        {
            // 無効化：タグをUntaggedに変更し、PlayerInteractionBubble を非表示にさせる
            gameObject.tag = GameConstants.UNTAGGED_TAG_NAME;

            // NPC自身の頭上の吹き出し（クエストアイコン等）も強制的に消す
            if (speechBubbleObject != null && speechBubbleObject.activeSelf)
            {
                speechBubbleObject.SetActive(false);
            }
        }
    }

    #endregion

    #region Core Logic

    /// <summary>
    /// 設定された会話条件を評価し、適切なFungusブロックを実行する。
    /// </summary>
    private void TryExecuteDialogue()
    {
        if (targetFlowchart == null)
            return;

        // 条件リストを下から順（新しい/進行度が高い条件）に評価
        for (int i = dialogueConditions.Count - 1; i >= 0; i--)
        {
            var condition = dialogueConditions[i];

            if (condition.AreAllFlagsMet())
            {
                // ショップトリガーとしての処理かどうかを判定
                bool isShopBlock =
                    isShopTrigger
                    && string.Equals(
                        condition.blockNameToExecute,
                        "Shop",
                        StringComparison.OrdinalIgnoreCase
                    );

                if (isShopBlock)
                {
                    // ShopInteractionTriggerが設定されている場合、ShopTriggerを実行
                    if (shopInteractionTrigger != null)
                    {
                        shopInteractionTrigger.ShopTrigger();
                    }
                }
                else
                {
                    // 条件に一致した場合、ブロックを実行
                    FungusHelper.ExecuteBlock(targetFlowchart, condition.blockNameToExecute);
                }

                // 追加イベントの呼び出し
                condition.onDialogueTriggered?.Invoke();
                return; // 一致したものが見つかったので処理終了
            }
        }

        // どの条件にも一致しなかった場合、デフォルトのブロックを実行
        if (!string.IsNullOrEmpty(defaultBlockName))
        {
            // ショップトリガーとしての処理かどうかを判定
            bool isShopBlock =
                isShopTrigger
                && string.Equals(defaultBlockName, "Shop", StringComparison.OrdinalIgnoreCase);

            if (isShopBlock)
            {
                // ShopInteractionTriggerが設定されている場合、ShopTriggerを実行
                if (shopInteractionTrigger != null)
                {
                    shopInteractionTrigger.ShopTrigger();
                }
            }
            else
            {
                FungusHelper.ExecuteBlock(targetFlowchart, defaultBlockName);
            }
        }
    }

    #endregion

    #region Visual & State Updates

    /// <summary>
    /// 現在のフラグ状態に基づいて、吹き出しの表示/非表示を更新する。
    /// </summary>
    private void UpdateBubbleState()
    {
        if (speechBubbleObject == null)
            return;

        // 会話中、もしくは会話機能が無効な場合は強制的に吹き出しを非表示にする
        if (isTalking || !isDialogueEnabled)
        {
            if (speechBubbleObject.activeSelf)
            {
                speechBubbleObject.SetActive(false);
            }
            return;
        }

        bool shouldShow = false; // 初期値は非表示

        // 会話実行時と同じロジックで、現在有効な条件を下から順に探す
        for (int i = dialogueConditions.Count - 1; i >= 0; i--)
        {
            var condition = dialogueConditions[i];

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

    #endregion

    #region Event Handlers

    /// <summary>
    /// GameManagerから会話状態の変更通知を受け取るコールバック
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

    #endregion
}

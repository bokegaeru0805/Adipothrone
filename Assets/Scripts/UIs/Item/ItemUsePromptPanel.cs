using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// アイテム使用時の確認ポップアップ（「使用する」「登録する」「やめる」など）を総括して管理するクラス。
/// 各ボタンの表示・非表示の切り替え、非表示に伴うUIナビゲーション（十字キー操作）の動的な繋ぎ直し、
/// および実際にアイテムを使用する処理の呼び出しまで、このクラスが一手に担います。
/// </summary>
public class ItemUsePromptPanel : MonoBehaviour
{
    #region 列挙型 (Enum) の定義

    /// <summary>
    /// パネルを表示する際のボタンの組み合わせパターンを定義します。
    /// </summary>
    public enum PromptMode
    {
        /// <summary>「使用する」「やめる」の2つのボタンのみを表示するモード（例：ステータス強化アイテム）</summary>
        Standard,

        /// <summary>「使用する」「登録する」「やめる」の3つのボタンを表示するモード（例：回復アイテム）</summary>
        WithRegister,
    }

    #endregion

    #region UIコンポーネントの参照

    [Header("UI ボタンの参照")]
    [Tooltip("アイテムを実際に消費・使用するためのボタン")]
    [SerializeField]
    private Button useButton;

    [Tooltip("クイックアイテムとしてスロットに登録するためのボタン")]
    [SerializeField]
    private Button registerButton;

    [Tooltip("操作をキャンセルしてポップアップを閉じるためのボタン")]
    [SerializeField]
    private Button cancelButton;

    #endregion

    #region その他の参照と内部状態

    [Header("その他の参照")]
    [Tooltip("「登録する」を選んだ際に開く、スロット選択用の別パネル")]
    [SerializeField]
    private QuickItemRegisterPanel itemRegisterPromptPanel;

    // --- 内部状態変数 ---

    /// <summary>現在操作の対象となっているアイテムのID</summary>
    private Enum currentItemID;

    /// <summary>
    /// 現在画面に表示されている有効なボタンのリスト。
    /// 非表示になったボタンを飛ばしてナビゲーションを構築するために使用します。
    /// </summary>
    private List<Button> activeButtons = new List<Button>();

    /// <summary>実際のアイテム消費やステータス反映を担うマネージャー</summary>
    private PlayerManager playerManager;

    #endregion

    #region Unity ライフサイクルメソッド

    private void Awake()
    {
        if (useButton != null)
            useButton.onClick.AddListener(OnUseClicked);

        if (registerButton != null)
            registerButton.onClick.AddListener(OnRegisterClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);
    }

    private void Start()
    {
        playerManager = PlayerManager.instance;
        if (playerManager == null)
        {
            Debug.LogWarning("PlayerManagerが存在しません。アイテム使用の処理が実行できません。");
        }
    }

    private void OnEnable()
    {
        EventSystem.current.SetSelectedGameObject(useButton != null ? useButton.gameObject : null);
        itemRegisterPromptPanel?.gameObject.SetActive(false); //登録パネルは最初は非表示にしておく
    }

    #endregion

    #region 初期化とセットアップ処理

    /// <summary>
    /// 呼び出し元のパネル（HealItemPanelActiveなど）から、ポップアップを開く直前に呼ばれるセットアップ関数。
    /// アイテムIDの保持と、指定されたモードに応じたボタンの表示切り替えを行います。
    /// </summary>
    /// <param name="itemID">対象となるアイテムのID</param>
    /// <param name="mode">表示するボタンの組み合わせパターン</param>
    public void SetupPrompt(Enum itemID, PromptMode mode)
    {
        currentItemID = itemID;

        // 「使用」と「キャンセル」は基本機能なので常に表示
        if (useButton != null)
            useButton.gameObject.SetActive(true);
        if (cancelButton != null)
            cancelButton.gameObject.SetActive(true);

        // 「登録」ボタンは、WithRegister モードの時だけ表示する
        if (registerButton != null)
        {
            registerButton.gameObject.SetActive(mode == PromptMode.WithRegister);
        }

        // 表示状態が確定したボタンだけで、コントローラー操作用のナビゲーションを作り直す
        RebuildNavigation();
    }

    #endregion

    #region UIナビゲーションの動的構築

    /// <summary>
    /// 現在表示されているボタンだけで、上下のExplicit Navigation（明示的なフォーカス移動）を動的に繋ぎ直します。
    /// </summary>
    /// <remarks>
    /// これを行わないと、「登録する」ボタンを非表示にした際、「使用する」から「やめる」へ
    /// 十字キーで移動できなくなり、操作不能（フォーカス迷子）に陥る原因になります。
    /// </remarks>
    private void RebuildNavigation()
    {
        activeButtons.Clear();

        // 1. 現在アクティブ（表示中）なボタンだけを、上から順にリストへ追加する
        if (useButton != null && useButton.gameObject.activeSelf)
            activeButtons.Add(useButton);

        if (registerButton != null && registerButton.gameObject.activeSelf)
            activeButtons.Add(registerButton);

        if (cancelButton != null && cancelButton.gameObject.activeSelf)
            activeButtons.Add(cancelButton);

        int count = activeButtons.Count;
        if (count == 0)
            return;

        // 2. リスト内のボタンをループさせ、自分の一つ上・一つ下のボタンをリンクさせる
        for (int i = 0; i < count; i++)
        {
            Navigation nav = new Navigation();
            nav.mode = Navigation.Mode.Explicit;

            // 上方向：1つ前のボタン（自分が先頭なら、一番後ろのボタンへループさせる）
            nav.selectOnUp = activeButtons[(i - 1 + count) % count];

            // 下方向：1つ後のボタン（自分が末尾なら、一番前のボタンへループさせる）
            nav.selectOnDown = activeButtons[(i + 1) % count];

            activeButtons[i].navigation = nav;
        }
    }

    #endregion

    #region IPanelActive インターフェース実装

    /// <summary>
    /// UIManager 等からパネルが開かれた際に呼ばれる、初期フォーカス設定処理。
    /// </summary>
    public void SelectFirstButton()
    {
        // 先ほどの RebuildNavigation で作成した「有効なボタンリスト」の先頭を選択する
        if (activeButtons.Count > 0)
        {
            StartCoroutine(SelectButtonAfterDelay(activeButtons[0].gameObject));
        }
        else
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    /// <summary>
    /// EventSystem の競合（前画面のクリック判定の残りなど）を避けるため、
    /// 1フレーム待機してからボタンを確実にフォーカスさせるコルーチン。
    /// </summary>
    private IEnumerator SelectButtonAfterDelay(GameObject targetButton)
    {
        yield return new WaitForEndOfFrame();
        yield return null;

        if (targetButton != null && targetButton.activeInHierarchy)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(targetButton);
        }
    }

    #endregion

    #region ボタンクリック時のイベントハンドラ

    /// <summary>
    /// 「使用する」ボタンがクリックされた時の処理
    /// </summary>
    private void OnUseClicked()
    {
        if (currentItemID == null || playerManager == null)
            return;

        // アイテムIDから種類（TypeID）を抽出し、対応する処理に振り分ける
        int typeID = EnumIDUtility.ExtractTypeID(EnumIDUtility.ToID(currentItemID));

        if (typeID == (int)TypeID.HealItem)
        {
            playerManager.UseHealItem(currentItemID);
        }
        else if (typeID == (int)TypeID.StatusEnhanceItem)
        {
            playerManager.UseStatusEnhanceItem(currentItemID);
        }
        else
        {
            Debug.LogWarning(
                $"対応していないアイテムタイプ(TypeID: {typeID})が使用されようとしました"
            );
        }

        // 処理が終わったら自身（ポップアップ）を閉じる
        ClosePanel();
    }

    /// <summary>
    /// 「登録する」ボタンがクリックされた時の処理
    /// </summary>
    private void OnRegisterClicked()
    {
        this.gameObject.SetActive(false);

        if (itemRegisterPromptPanel != null)
        {
            // 登録先パネルのスクリプトに、対象となるアイテムIDを引き継ぐ
            itemRegisterPromptPanel.itemID = currentItemID;
            // 登録先パネルを開く(UIManagerのスタックには積まず、独立したポップアップとして扱う)
            UIManager.instance.OpenPopup(itemRegisterPromptPanel.gameObject);
        }
        else
        {
            Debug.LogWarning(
                "クイックアイテム登録パネル(itemRegisterPromptPanel)が設定されていません。"
            );
        }
    }

    /// <summary>
    /// 「やめる」ボタンがクリックされた時の処理
    /// </summary>
    private void OnCancelClicked()
    {
        ClosePanel();
    }

    #endregion

    #region 内部処理（パネル閉鎖）

    /// <summary>
    /// このポップアップ自身を閉じて、フォーカスを元のアイテムリストへ戻す処理。
    /// </summary>
    private void ClosePanel()
    {
        UIManager.instance.ClosePopup();
    }

    #endregion
}

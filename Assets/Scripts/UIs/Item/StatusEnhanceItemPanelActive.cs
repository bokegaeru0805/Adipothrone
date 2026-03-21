using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StatusEnhanceItemPanelActive
    : MonoBehaviour,
        IPanelActive,
        IPageNavigable,
        IItemPromptHandler
{
    [Tooltip("アイテム使用確認パネルの位置調整用オフセット")]
    [SerializeField]
    private Vector2 offset = Vector2.zero;

    [Header("アイテム詳細情報のパネル")]
    [SerializeField]
    private ItemDetailPanel itemDetailPanel = null; //アイテム効果パネルのオブジェクト

    [Header("選択ボタンコンポーネント")]
    [SerializeField]
    private List<Button> buttonList; //アイテム用選択ボタンのリスト

    [SerializeField]
    private List<Button> leftSideButtonList; //左側のアイテム用選択ボタンのリスト

    [SerializeField]
    private List<Button> rightSideButtonList; //右側のアイテム用選択ボタンのリスト

    [Header("アイテム使用確認パネル")]
    [Tooltip("アイテム使用確認パネルのオブジェクト")]
    [SerializeField]
    private GameObject ItemUsePromptPanel = null;

    [Tooltip("アイテム使用確認パネルのYesボタンのオブジェクト")]
    [SerializeField]
    private GameObject ItemUsePromptYes = null;

    // ※ステータス強化アイテムは「登録」しないため、RegisterPromptの変数は削除しています

    public List<Button> LeftSideButtons => leftSideButtonList;
    public List<Button> RightSideButtons => rightSideButtonList;
    public int Page
    {
        get => page;
        set => page = value;
    }

    private int rowCount = 0; //UIの行数(自動設定)
    private int page = 0; //現在のページ番号
    private Enum selectedButtonItemID = null;
    private Enum preselectedButtonItemID = null;

    // 最後に選択したアイテムのIDと「ボタンの位置」を記憶する変数 (NewTabSubPanelTemplate仕様)
    private int? lastSelectedItemID = null;
    private int lastSelectedIndex = -1; // -1は未選択を表す

    // プレイヤーが所持しているアイテム情報のリスト
    private List<ItemEntry> itemList = new List<ItemEntry>();

    private void Awake()
    {
        if (itemDetailPanel == null)
        {
            Debug.LogWarning("アイテム効果パネルが設定されていません");
            return;
        }

        if (
            buttonList == null
            || buttonList.Count == 0
            || rightSideButtonList == null
            || rightSideButtonList.Count == 0
            || leftSideButtonList == null
            || leftSideButtonList.Count == 0
        )
        {
            Debug.LogWarning("アイテム選択ボタンが設定されていません");
            return;
        }

        if (ItemUsePromptPanel == null || ItemUsePromptYes == null)
        {
            Debug.LogWarning("アイテム使用確認パネルのUIコンポーネントが設定されていません");
            return;
        }

        // 初期化処理
        ItemUsePromptPanel.SetActive(false);
        itemDetailPanel.gameObject.SetActive(false);
        rowCount = rightSideButtonList.Count; //UIの行数を設定
    }

    private void OnEnable()
    {
        // 共通のボタン群に「現在アクティブなのは自分だ」と教える
        if (buttonList != null)
        {
            foreach (var btn in buttonList)
            {
                var itemBtn = btn.GetComponent<ItemSelectButton>();
                if (itemBtn != null)
                {
                    itemBtn.RegisterActivePanel(this);
                }
            }
        }

        // パネルが有効化されたときに最初のボタンを選択する
        SelectFirstButton();
    }

    private void Update()
    {
        // 選択されているアイテムボタンのアイテムIDを取得し、効果説明パネルの文章を変更する
        GetSelectedButtonItemID();
    }

    /// <summary>
    /// ページ番号に応じてアイテムをボタンに割り当てる
    /// </summary>
    public bool TryAssignItemsToPage(int pageNumber, int previousRow, bool moveRight)
    {
        return UIUtility.AssignItemsToButtons(
            buttonList,
            rowCount,
            itemList,
            pageNumber,
            previousRow,
            moveRight
        );
    }

    /// <summary>
    /// パネルが開かれた際に、最初に選択状態にするボタンを決定します。
    /// NewTabSubPanelTemplateに準拠したフォーカス復元処理です。
    /// </summary>
    public void SelectFirstButton()
    {
        // 手順1：最新の所持アイテムリストを読み込む
        LoadItemData();
        if (itemList.Count == 0)
        {
            foreach (var button in buttonList)
            {
                button.gameObject.SetActive(false);
            }
            itemDetailPanel.gameObject.SetActive(false);
            EventSystem.current.SetSelectedGameObject(null);
            return;
        }

        int targetItemIndex = -1;

        // 手順2：最後に選択していたアイテムが、現在の所持リストにまだ存在するか探す
        if (lastSelectedItemID.HasValue)
        {
            targetItemIndex = itemList.FindIndex(entry => entry.itemID == lastSelectedItemID.Value);
        }

        // 手順3：アイテムが存在しなかった場合（消費された等）、近いものを選択
        if (targetItemIndex == -1)
        {
            if (lastSelectedIndex != -1 && itemList.Count > 0)
            {
                int absoluteIndex = (this.page * buttonList.Count) + lastSelectedIndex;
                targetItemIndex = Mathf.Clamp(absoluteIndex, 0, itemList.Count - 1);
            }
            else if (itemList.Count > 0)
            {
                targetItemIndex = 0;
            }
        }

        // 手順4：最終的なターゲットアイテムの位置から、表示すべきページとボタンを計算
        int targetPage = 0;
        int targetButtonIndexOnPage = -1;

        if (targetItemIndex != -1)
        {
            targetPage = targetItemIndex / buttonList.Count;
            targetButtonIndexOnPage = targetItemIndex % buttonList.Count;
        }

        // 手順5：計算したページを表示する
        this.page = targetPage;
        UpdateDisplayedButtons();

        // 手順6：計算したボタンを「1フレーム遅延させて」選択状態にする
        if (targetButtonIndexOnPage != -1 && targetButtonIndexOnPage < buttonList.Count)
        {
            StartCoroutine(SelectButtonAfterDelay(buttonList[targetButtonIndexOnPage].gameObject));
        }
        else if (itemList.Count > 0)
        {
            var firstButton = buttonList.FirstOrDefault(b => b.gameObject.activeInHierarchy);
            if (firstButton != null)
                StartCoroutine(SelectButtonAfterDelay(firstButton.gameObject));
        }
        else
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    /// <summary>
    /// EventSystemのクリック終了判定との競合を避けるため、1フレーム遅延してボタンを選択状態にします。
    /// </summary>
    private IEnumerator SelectButtonAfterDelay(GameObject targetButton)
    {
        yield return new WaitForEndOfFrame();
        yield return null;

        if (targetButton != null && targetButton.activeInHierarchy)
        {
            // 一旦フォーカスをクリアして内部状態をリセットする
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(targetButton);
        }
    }

    /// <summary>
    /// 最新の所持アイテムデータを読み込み、リストを更新します。
    /// </summary>
    private void LoadItemData()
    {
        itemList.Clear();
        if (GameManager.instance.savedata?.ItemInventoryData?.ownedItems != null)
        {
            // ステータス強化アイテムのみを取得
            itemList = GameManager.instance.savedata.ItemInventoryData.GetAllItemByType(
                InventoryItemData.ItemType.StatusEnhanceItem
            );
            // 所持数0のものは除外
            itemList = itemList.Where(entry => entry.count > 0).ToList();
        }
    }

    /// <summary>
    /// 現在のページ番号とアイテムリストに基づいて、ボタンの表示/非表示を更新します。
    /// </summary>
    private void UpdateDisplayedButtons()
    {
        if (itemList.Count == 0 && itemDetailPanel.gameObject.activeSelf)
        {
            itemDetailPanel.gameObject.SetActive(false);
        }
        else if (itemList.Count > 0 && !itemDetailPanel.gameObject.activeSelf)
        {
            itemDetailPanel.gameObject.SetActive(true);
        }

        TryAssignItemsToPage(this.page, 0, false);
    }

    /// <summary>
    /// アイテム使用の確認パネルを表示します。
    /// </summary>
    public void SetPromptPanel(Enum itemID, Button selectedButton)
    {
        if (this.gameObject.activeSelf == false || selectedButton == null)
            return;

        // ポップアップを開く直前に、現在の選択状態を確実に記憶する
        lastSelectedItemID = EnumIDUtility.ToID(itemID);
        lastSelectedIndex = buttonList.IndexOf(selectedButton);

        if (UIManager.instance != null && ItemUsePromptPanel != null)
        {
            Vector3 buttonWorldPosition = selectedButton.transform.position;
            Vector2 finalOffset = offset;

            // 右側のボタンならオフセットを反転
            if (rightSideButtonList.Contains(selectedButton))
            {
                finalOffset.x *= -1;
            }

            RectTransform promptRect = ItemUsePromptPanel.GetComponent<RectTransform>();
            promptRect.position = buttonWorldPosition;
            promptRect.anchoredPosition += finalOffset;

            UIManager.instance.OpenPanel(ItemUsePromptPanel, -1);
        }
        else
        {
            Debug.LogWarning("UIManagerもしくはアイテム使用確認パネルが存在しません");
        }

        // 使用ボタンにIDを渡す
        var script = ItemUsePromptYes.GetComponent<ItemUsePromptButton>();
        if (script != null)
        {
            script.itemID = itemID;
        }
        else
        {
            Debug.LogWarning("ItemUsePromptButtonスクリプトが入手できませんでした");
        }
    }

    /// <summary>
    /// 選択されているアイテムボタンのアイテムIDを取得し、効果説明パネルの文章を変更する
    /// </summary>
    private void GetSelectedButtonItemID()
    {
        if (itemList.Count == 0 && itemDetailPanel.gameObject.activeSelf)
        {
            itemDetailPanel.gameObject.SetActive(false);
            return;
        }

        GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
        if (selectedObj == null)
            return;

        // 選択中のボタンからIDを取得
        var selectedBtn = buttonList.FirstOrDefault(b => b.gameObject == selectedObj);
        if (selectedBtn != null)
        {
            var info = selectedBtn.GetComponent<IItemAssignable>();
            if (info != null)
            {
                selectedButtonItemID = info.AssignedItemID;
            }
            else
            {
                selectedButtonItemID = null;
                Debug.LogWarning("ItemSelectButton スクリプトが見つかりませんでした");
            }
        }
        else
        {
            return; // リスト外のボタンを選択中の場合は処理をスキップ
        }

        // 選択アイテムが変わった場合のみ詳細パネルを更新
        if (preselectedButtonItemID != selectedButtonItemID && selectedButtonItemID != null)
        {
            if (!itemDetailPanel.gameObject.activeSelf)
            {
                itemDetailPanel.gameObject.SetActive(true);
            }
            itemDetailPanel.DisplayItemDetails(selectedButtonItemID);
        }

        preselectedButtonItemID = selectedButtonItemID;
        if (selectedButtonItemID != null)
        {
            lastSelectedItemID = EnumIDUtility.ToID(selectedButtonItemID);
        }
    }

    /// <summary>
    /// 【将来拡張用】所持しているステータス強化アイテムを一括で使用する
    /// 別のボタンコンポーネント等から呼び出されることを想定
    /// </summary>
    public void OnClickUseAllItems()
    {
        Debug.Log("ステータス強化アイテムの一括使用処理が呼ばれました");

        // TODO: 所持しているステータス強化アイテムを全て消費し、ステータスに反映する処理を実装
        // PlayerManager.instance.UseAllStatusEnhanceItems(itemList); のようなイメージ

        // 使用後、リストとUIをリフレッシュしてフォーカスを当て直す
        SelectFirstButton();
    }

    private void OnDisable()
    {
        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

        if (currentSelected != null)
        {
            lastSelectedIndex = buttonList.FindIndex(b => b.gameObject == currentSelected);
            if (lastSelectedIndex != -1)
            {
                var itemInfo = buttonList[lastSelectedIndex].GetComponent<IItemAssignable>();
                if (itemInfo != null && itemInfo.AssignedItemID != null)
                {
                    lastSelectedItemID = EnumIDUtility.ToID(itemInfo.AssignedItemID);
                }
            }
        }
        else
        {
            lastSelectedItemID = null;
            lastSelectedIndex = -1;
        }

        itemDetailPanel.gameObject.SetActive(false);
    }
}

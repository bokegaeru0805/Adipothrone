using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemPanelActive : MonoBehaviour, IPanelActive, IPageNavigable
{
    [SerializeField]
    private Vector2 offset = Vector2.zero;

    [Header("アイテム詳細情報のパネルのGameObject")]
    [SerializeField]
    private GameObject ItemDetailPanel = null; //アイテム効果パネルのオブジェクト

    [Header("選択ボタンコンポーネント")]
    [SerializeField]
    private List<Button> buttonList; //アイテム用選択ボタンのリスト

    [SerializeField]
    private List<Button> leftSideButtonList; //左側のアイテム用選択ボタンのリスト

    [SerializeField]
    private List<Button> rightSideButtonList; //右側のアイテム用選択ボタンのリスト

    [Header("アイテム使用確認パネル")]
    [SerializeField]
    private GameObject ItemUsePromptPanel = null; //アイテム使用確認パネルのオブジェクト

    [SerializeField]
    private GameObject ItemUsePromptYes = null; //アイテム使用確認パネルのYesボタン

    [SerializeField]
    private GameObject ItemRegisterPrompt = null;

    public List<Button> LeftSideButtons => leftSideButtonList;
    public List<Button> RightSideButtons => rightSideButtonList;
    public int Page
    {
        get => page;
        set => page = value;
    }
    private int rowCount = 0; //UIの行数（例: 5行4列なら rowCount = 5）(自動設定)
    private int page = 0; //現在のページ番号
    private Enum selectedButtonItemID = null;
    private Enum preselectedButtonItemID = null;

    // 最後に選択したアイテムのIDと「ボタンの位置」を記憶する変数を追加
    private int? lastSelectedItemID = null;
    private int lastSelectedIndex = -1; // -1は未選択を表す

    // プレイヤーが所持しているアイテム情報のリスト。
    // 各要素は ItemEntry として、アイテムのID（itemID）とその所持数（count）を保持する。
    private List<ItemEntry> itemList = new List<ItemEntry>();

    private void Awake()
    {
        if (ItemDetailPanel == null)
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

        if (ItemUsePromptPanel == null || ItemUsePromptYes == null || ItemRegisterPrompt == null)
        {
            Debug.LogWarning(
                "Menuアイテムのアイテム使用確認パネルのUIコンポーネントが設定されていません"
            );
            return;
        }

        //アイテム選択ボタンの初期化
        ItemUsePromptPanel.SetActive(false);
        //アイテムの効果表示パネルを非表示化
        ItemDetailPanel.SetActive(false);
        rowCount = rightSideButtonList.Count; //UIの行数を設定
    }

    private void Update()
    {
        // 選択されているアイテムボタンのアイテムIDを取得し、効果説明パネルの文章を変更する
        GetSelectedButtonItemID();
    }

    //ページ番号に応じてアイテムをボタンに割り当てる
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
    /// 最後に選択していたアイテムと位置を復元し、なければ近いものを選択します。
    /// </summary>
    public void SelectFirstButton()
    {
        // 手順1：最新の所持アイテムリストを読み込む
        LoadItemData();
        if (itemList.Count == 0)
        {
            // 全てのボタンを非表示にする
            foreach (var button in buttonList)
            {
                button.gameObject.SetActive(false);
            }

            // アイテム詳細パネルも非表示にする
            ItemDetailPanel.SetActive(false);

            // 何も選択しない状態にする（カーソルを消す）
            EventSystem.current.SetSelectedGameObject(null);

            // これ以降の処理は不要なのでメソッドを抜ける
            return;
        }
        
        int targetItemIndex = -1;

        // 手順2：最後に選択していたアイテムが、現在の所持リストにまだ存在するか探す
        if (lastSelectedItemID.HasValue)
        {
            targetItemIndex = itemList.FindIndex(entry => entry.itemID == lastSelectedItemID.Value);
        }

        // 手順3：アイテムが存在しなかった場合（消費された等）、フォールバック処理を行う
        if (targetItemIndex == -1)
        {
            // 最後に選択していた「ボタンの位置（インデックス）」をヒントに、
            // 現在のリストでその位置に最も近いアイテムをターゲットにする
            if (lastSelectedIndex != -1 && itemList.Count > 0)
            {
                // リストの範囲内に収まるようにインデックスを調整
                targetItemIndex = Mathf.Min(lastSelectedIndex, itemList.Count - 1);
            }
            // それでもターゲットが決まらなければ、リストの先頭アイテムにする
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

        // 手順6：計算したボタンを選択状態にする
        if (targetButtonIndexOnPage != -1)
        {
            EventSystem.current.SetSelectedGameObject(
                buttonList[targetButtonIndexOnPage].gameObject
            );
        }
        // フォールバックの最終手段として、表示されている最初のボタンを選択
        else if (itemList.Count > 0)
        {
            var firstButton = buttonList.FirstOrDefault(b => b.gameObject.activeInHierarchy);
            if (firstButton != null)
                EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
        }
        else
        {
            // アイテムが一つもない場合は、何も選択しない
            EventSystem.current.SetSelectedGameObject(null);
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
            itemList = GameManager.instance.savedata.ItemInventoryData.GetAllItemByType(
                InventoryItemData.ItemType.HealItem
            );
            itemList = itemList.Where(entry => entry.count > 0).ToList();
        }
    }

    /// <summary>
    /// 現在のページ番号とアイテムリストに基づいて、ボタンの表示/非表示を更新します。
    /// </summary>
    private void UpdateDisplayedButtons()
    {
        if (itemList.Count == 0 && ItemDetailPanel.activeSelf)
        {
            ItemDetailPanel.SetActive(false);
        }
        else if (itemList.Count > 0 && !ItemDetailPanel.activeSelf)
        {
            ItemDetailPanel.SetActive(true);
        }

        TryAssignItemsToPage(this.page, 0, false);
    }

    /// <summary>
    /// アイテム使用の確認パネルを表示します。
    /// </summary>
    /// <param name="itemID">使用するアイテムのID</param>
    /// <param name="selectedButton">クリックされたボタン</param>
    public void SetPromptPanel(Enum itemID, Button selectedButton)
    {
        // 引数で渡されたボタンがnullなら処理を中断
        if (selectedButton == null)
            return;

        if (UIManager.instance != null && ItemUsePromptPanel != null)
        {
            // まず、クリックされたボタンのRectTransformと座標を取得
            RectTransform buttonRect = selectedButton.GetComponent<RectTransform>();
            Vector2 selectButtonPosition = buttonRect.anchoredPosition;

            // offsetをコピーして、変更があっても元の値に影響しないようにする
            Vector2 finalOffset = offset;

            // もしクリックされたボタンが「右側のボタンリスト」に含まれていたら
            if (rightSideButtonList.Contains(selectedButton))
            {
                // offsetのx座標の正負を反転させる
                finalOffset.x *= -1;
            }

            // 最終的なoffsetを使ってパネルの位置を決定
            RectTransform promptRect = ItemUsePromptPanel.GetComponent<RectTransform>();
            promptRect.anchoredPosition = selectButtonPosition + finalOffset;

            UIManager.instance.OpenPanel(ItemUsePromptPanel, -1);
        }
        else
        {
            Debug.LogWarning("UIManagerもしくはアイテム使用確認パネルが存在しません");
        }

        var script = ItemUsePromptYes.GetComponent<ItemUsePromptButton>();
        if (script != null)
        {
            script.itemID = itemID;
        }
        else
        {
            Debug.LogWarning("ItemUsePromptButtonスクリプトが入手できませんでした");
        }

        var script2 = ItemRegisterPrompt.GetComponent<ItemUsePromptButton>();
        if (script2 != null)
        {
            script2.itemID = itemID;
        }
        else
        {
            Debug.LogWarning("ItemRegisterPromptスクリプトが入手できませんでした");
        }
    }

    //選択されているアイテムボタンのアイテムIDを取得し、効果説明パネルの文章を変更する
    private void GetSelectedButtonItemID()
    {
        //所持しているアイテムが0個で、かつエフェクト表示パネルが表示されているとき
        if (itemList.Count == 0 && ItemDetailPanel.activeSelf)
        {
            ItemDetailPanel.SetActive(false);
            return;
        }

        //現在選択されているボタンのゲームオブジェクトを取得
        GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
        //選択されているボタンがないなら飛ばす
        if (selectedObj == null)
            return;

        //現在選択しているパネルのアイテムのIDを取得する
        for (int i = 0; i < buttonList.Count; i++)
        {
            if (buttonList[i].gameObject == selectedObj)
            {
                IItemAssignable info = buttonList[i].GetComponent<IItemAssignable>();
                if (info != null)
                {
                    //選択されているアイテムのIDを取得する
                    selectedButtonItemID = info.AssignedItemID;
                }
                else
                {
                    selectedButtonItemID = null; //選択されているアイテムのIDを初期化する
                    preselectedButtonItemID = selectedButtonItemID; //前フレームのアイテムIDを設定する
                    Debug.LogWarning("ItemSelectButton スクリプトが見つかりませんでした");
                }
            }
        }

        //効果説明パネルの文章を変更する
        if (preselectedButtonItemID != selectedButtonItemID)
        {
            if (!ItemDetailPanel.activeSelf)
            {
                //アイテム効果パネルを表示する
                ItemDetailPanel.SetActive(true);
            }

            var script = ItemDetailPanel.GetComponent<ItemDetailPanel>();
            if (script != null)
            {
                script.DisplayItemDetails(selectedButtonItemID); //アイテムの詳細を表示する
            }
            else
            {
                Debug.LogWarning("アイテム効果パネルに適切なスクリプトが設定されていません");
            }
        }

        preselectedButtonItemID = selectedButtonItemID; //前フレームのアイテムIDを設定する
        lastSelectedItemID = EnumIDUtility.ToID(selectedButtonItemID); //最後に選択したアイテムのIDを保存
    }

    // private void OnEnable()
    // {
    //     SelectFirstButton();
    // }

    private void OnDisable()
    {
        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

        // 現在何かボタンが選択されているかチェック
        if (currentSelected != null)
        {
            // 選択されているボタンが、このパネルのボタンリストの何番目かを探す
            lastSelectedIndex = buttonList.FindIndex(b => b.gameObject == currentSelected);

            if (lastSelectedIndex != -1)
            {
                // 見つかった場合、そのボタンのアイテムIDを取得して保存
                var itemInfo = buttonList[lastSelectedIndex].GetComponent<IItemAssignable>();
                if (itemInfo != null && itemInfo.AssignedItemID != null)
                {
                    lastSelectedItemID = EnumIDUtility.ToID(itemInfo.AssignedItemID);
                }
                else
                {
                    lastSelectedItemID = null;
                }
            }
            else
            {
                // 選択されているものがアイテムボタンではなかった場合、IDは保存しない
                lastSelectedItemID = null;
            }
        }
        else
        {
            // 何も選択されていなかった場合、両方の情報をリセット
            lastSelectedItemID = null;
            lastSelectedIndex = -1;
        }

        ItemDetailPanel.SetActive(false);
    }
}

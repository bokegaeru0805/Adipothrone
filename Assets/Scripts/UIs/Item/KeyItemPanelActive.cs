using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class KeyItemPanelActive : MonoBehaviour, IPanelActive, IPageNavigable
{
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

    public List<Button> LeftSideButtons => leftSideButtonList;
    public List<Button> RightSideButtons => rightSideButtonList;
    
    public int Page
    {
        get => page;
        set => page = value;
    }

    private int rowCount = 0; // UIの行数
    private int page = 0; // 現在のページ番号
    
    private Enum selectedButtonItemID = null;
    private Enum preselectedButtonItemID = null;

    // 最後に選択したアイテムのIDと「ボタンの位置」を記憶する変数
    private int? lastSelectedItemID = null;
    private int lastSelectedIndex = -1; // -1は未選択を表す

    // プレイヤーが所持しているアイテム情報のリスト
    private List<ItemEntry> itemList = new List<ItemEntry>();

    private void Awake()
    {
        if (ItemDetailPanel == null)
        {
            Debug.LogWarning("アイテム効果パネルが設定されていません");
            return;
        }

        if (buttonList == null || buttonList.Count == 0 ||
            rightSideButtonList == null || rightSideButtonList.Count == 0 ||
            leftSideButtonList == null || leftSideButtonList.Count == 0)
        {
            Debug.LogWarning("アイテム選択ボタンが設定されていません");
            return;
        }

        // アイテムの効果表示パネルを非表示化
        ItemDetailPanel.SetActive(false);
        rowCount = rightSideButtonList.Count; // UIの行数を設定
    }

    private void OnEnable()
    {
        // パネルが有効化されたときにデータをロードし、適切なボタンを表示・選択する
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
        // UIUtilityを使ってボタンへのアイテム割り当てと表示/非表示の切り替えを行う
        // これにより、アイテム数に応じた適切な数のボタンが表示される
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
    /// アイテムリストを更新し、ページ整合性を保ちます。
    /// </summary>
    public void SelectFirstButton()
    {
        // 1. 最新の所持アイテムリストを読み込む
        LoadItemData();

        if (itemList.Count == 0)
        {
            // アイテムがない場合は全て非表示にして終了
            foreach (var button in buttonList) button.gameObject.SetActive(false);
            ItemDetailPanel.SetActive(false);
            EventSystem.current.SetSelectedGameObject(null);
            return;
        }

        int targetItemIndex = -1;

        // 2. 最後に選択していたアイテムが、現在の所持リストにまだ存在するか探す
        if (lastSelectedItemID.HasValue)
        {
            targetItemIndex = itemList.FindIndex(entry => entry.itemID == lastSelectedItemID.Value);
        }

        // 3. 同じアイテムが見つからない場合（消費・売却済など）、元の「位置」に近いアイテムを探す
        if (targetItemIndex == -1)
        {
            if (lastSelectedIndex != -1)
            {
                // 「前回のページ × 1ページあたりの数 + 前回のボタン位置」でグローバルなインデックスを計算
                // これにより、勝手にページ0に戻るのを防ぐ
                int estimatedIndex = (this.page * buttonList.Count) + lastSelectedIndex;
                
                // 範囲内に収める（アイテムが減ってページが消滅した場合などに対応）
                targetItemIndex = Mathf.Clamp(estimatedIndex, 0, itemList.Count - 1);
            }
            else
            {
                // 位置情報もなければ先頭を選択
                targetItemIndex = 0;
            }
        }

        // 4. ターゲットとなるアイテムの位置から、表示すべき「ページ」と「ボタン位置」を逆算
        int targetPage = 0;
        int targetButtonIndexOnPage = -1;

        if (targetItemIndex != -1)
        {
            targetPage = targetItemIndex / buttonList.Count;
            targetButtonIndexOnPage = targetItemIndex % buttonList.Count;
        }

        // 5. 計算したページを適用して表示更新（ここでボタンのActive切り替えが行われる）
        this.page = targetPage;
        UpdateDisplayedButtons();

        // 6. 計算したボタンを選択状態にする
        if (targetButtonIndexOnPage != -1 && targetButtonIndexOnPage < buttonList.Count)
        {
            GameObject targetObj = buttonList[targetButtonIndexOnPage].gameObject;
            if (targetObj.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(targetObj);
            }
            else
            {
                // 万が一非表示の場合は、表示されている最初のボタンを選択
                var firstActive = buttonList.FirstOrDefault(b => b.gameObject.activeInHierarchy);
                if (firstActive != null) EventSystem.current.SetSelectedGameObject(firstActive.gameObject);
            }
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
                InventoryItemData.ItemType.KeyItem
            );
            // 所持数0のものは除外（必要に応じて調整）
            itemList = itemList.Where(entry => entry.count > 0).ToList();
        }
    }

    /// <summary>
    /// 現在のページ番号とアイテムリストに基づいて、ボタンの表示/非表示を更新します。
    /// </summary>
    private void UpdateDisplayedButtons()
    {
        // アイテム詳細パネルの表示切り替え
        if (itemList.Count == 0)
        {
            if (ItemDetailPanel.activeSelf) ItemDetailPanel.SetActive(false);
        }
        else
        {
            // アイテムがあるなら、選択変更時に表示されるのでここでは制御しないか、
            // 必要なタイミングで表示する。初期状態では非表示にしておくほうが自然な場合が多い。
            // 既存のロジックを尊重しつつ、選択変更イベントに任せる。
        }

        // ボタンへの割り当てを実行（UIUtility側でSetActiveが制御される）
        TryAssignItemsToPage(this.page, 0, false);
    }

    // 選択されているアイテムボタンのアイテムIDを取得し、効果説明パネルの文章を変更する
    private void GetSelectedButtonItemID()
    {
        if (itemList.Count == 0)
        {
            if (ItemDetailPanel.activeSelf) ItemDetailPanel.SetActive(false);
            return;
        }

        GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
        if (selectedObj == null) return;

        // 現在選択しているボタンがリスト内のものか判定
        var selectedBtn = buttonList.FirstOrDefault(b => b.gameObject == selectedObj);
        
        if (selectedBtn != null)
        {
            IItemAssignable info = selectedBtn.GetComponent<IItemAssignable>();
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
            // アイテムボタン以外（サイドボタン等）を選択中の場合は更新しない
            return; 
        }

        // 効果説明パネルの更新
        if (preselectedButtonItemID != selectedButtonItemID && selectedButtonItemID != null)
        {
            if (!ItemDetailPanel.activeSelf) ItemDetailPanel.SetActive(true);

            var script = ItemDetailPanel.GetComponent<ItemDetailPanel>();
            if (script != null)
            {
                script.DisplayItemDetails(selectedButtonItemID);
            }
        }

        preselectedButtonItemID = selectedButtonItemID;
        
        // 有効なアイテムを選択している場合のみ履歴を保存
        if (selectedButtonItemID != null)
        {
            lastSelectedItemID = EnumIDUtility.ToID(selectedButtonItemID);
        }
    }

    private void OnDisable()
    {
        // 閉じる瞬間に選択されていたボタンの位置を保存
        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

        if (currentSelected != null)
        {
            lastSelectedIndex = buttonList.FindIndex(b => b.gameObject == currentSelected);
            
            // ボタンが特定できた場合のみID保存（GetSelectedButtonItemIDで保存しているので念のため）
            if (lastSelectedIndex != -1)
            {
                // IDの保存はUpdate内で行っているのでここではIndexの保存が主目的
            }
            else
            {
                // アイテムボタン以外を選択して閉じた場合、位置記憶はリセットするか、前回のままにするか
                // ここではリセットしないでおく（前回有効だった位置を維持）
            }
        }
        else
        {
            lastSelectedIndex = -1;
        }

        ItemDetailPanel.SetActive(false);
    }
}
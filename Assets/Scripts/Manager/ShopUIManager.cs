using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fungus;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopUIManager : MonoBehaviour
{
    public static ShopUIManager instance { get; private set; } // ShopUIManagerのインスタンス
    private PlayerManager playerManager;
    public static bool isPurchasing = false; // 購入中かどうかのフラグ

    [Header("ロジック関連の参照")]
    [SerializeField]
    private ShopDataBase shopDataBase; // 店のデータベース

    [Header("UI参照のルート")]
    [SerializeField]
    private ShopUIRefs uiRefs; // UI参照のルートオブジェクト

    [Header("店の会話ハンドラー")]
    [SerializeField]
    private MonoBehaviour conversationHandlerObject;
    private IShopConversation conversationHandler;

    [HideInInspector]
    private ShopName currentShopID = ShopName.None; // 現在の店のID
    public ShopStatus shopStatus { get; private set; } = ShopStatus.None; // 現在の店のステータス（購入モードか売却モードか）
    private SellItemType sellItemType = SellItemType.None; // 現在の売却アイテムの種類
    private Enum selectedButtonItemID = null; // 選択されているアイテムのID
    private Enum preselectedButtonItemID = null; // 前フレームのアイテムID
    private GameObject lastSelected; //最後に選ばれていたボタンを保存する変数
    private GameObject lastSelectedPrompt; //最後に選ばれていた購入確認パネルのボタンを保存する変数
    private GameObject previousSelectedForPageUpdate; // ページナビゲーションの更新用に前フレームの選択を保存する変数
    private string shopStartBlockName = "StartShopDialogue"; // 店の開始ブロック名
    private string shopEndBlockName = "EndShopDialogue"; // 店の終了ブロック名
    private int currentTabIndex = 0; // 選択されているアイテムの種類のインデックス
    private List<ItemEntry> bladeWeaponList = new List<ItemEntry>(); // ブレード武器のリスト
    private List<ItemEntry> shootWeaponList = new List<ItemEntry>(); // シュート武器のリスト
    private List<ItemEntry> healItemList = new List<ItemEntry>(); // 回復アイテムのリスト

    // タブの選択状態を保存するためのデータ構造
    private struct TabState
    {
        public int PageIndex;
        public int SelectedButtonIndex;
    }

    // 各アイテムタイプごとの状態を保存する辞書
    private Dictionary<SellItemType, TabState> sellTabStates =
        new Dictionary<SellItemType, TabState>();

    [System.Serializable]
    public class SellItemEntry
    {
        public GameObject itemTab; // アイテムタブのGameObject

        public SellItemType sellItemType; // アイテムの種類

        /// <summary>
        /// このタブが選択されたかどうかに応じて、UIの表示を更新します。
        /// </summary>
        /// <param name="isSelected">このタブが選択された場合はtrue。</param>

        public void SetTabSelected(bool isSelected)
        {
            if (itemTab != null)
            {
                if (isSelected)
                {
                    // 選択中のタブの画像を変更
                    itemTab.GetComponent<Image>().sprite = instance.uiRefs.SelectedTabImage;

                    // アイテムリストの更新
                    List<ItemEntry> items;

                    // 表示すべきアイテムリストを取得
                    switch (sellItemType)
                    {
                        case SellItemType.BladeWeapon:
                            items = instance.bladeWeaponList;
                            break;
                        case SellItemType.ShootWeapon:
                            items = instance.shootWeaponList;
                            break;
                        case SellItemType.HealItem:
                            items = instance.healItemList;
                            break;
                        default:
                            Debug.LogError("...");
                            return;
                    }

                    instance.sellItemType = sellItemType; // 現在の売却アイテムの種類を設定

                    // アイテムリストが空かどうかで処理を分岐
                    if (items == null || items.Count == 0)
                    {
                        // --- リストが空の場合 ---
                        // 1. 全てのショップボタンを非表示にする
                        foreach (var button in instance.uiRefs.ShopButtons)
                        {
                            button.gameObject.SetActive(false);
                        }

                        // 2. アイテム詳細パネルを非表示にし、選択をクリアする
                        instance.uiRefs.ItemDetailPanel.SetActive(false);
                        instance.uiRefs.WeaponDetailPanel.SetActive(false);
                        instance.uiRefs.SelectedItemAmountText.text = "0"; // 所持数テキストをリセット
                        instance.selectedButtonItemID = null; // 選択中IDをリセット
                        EventSystem.current.SetSelectedGameObject(null); // UIの選択状態も解除
                    }
                    else
                    {
                        // --- リストにアイテムが存在する場合 ---
                        int targetPage = 0;
                        int targetIndex = 0;

                        //保存された状態データがあれば、それを読み込む
                        if (
                            instance.sellTabStates.TryGetValue(
                                sellItemType,
                                out TabState savedState
                            )
                        )
                        {
                            targetPage = savedState.PageIndex;
                            targetIndex = savedState.SelectedButtonIndex;
                        }

                        // --- 例外処理 ---
                        // 1. 保存されたページが現在のアイテム数に対して無効になっていないか検証
                        int maxPage = (items.Count - 1) / instance.uiRefs.ShopButtons.Count;
                        if (targetPage > maxPage)
                        {
                            targetPage = maxPage; // 存在しないページなら最終ページに補正
                            targetIndex = 0; // ページが変わったのでインデックスは先頭にリセット
                        }

                        // ターゲットページの内容でUIを更新
                        UIUtility.AssignItemsVerticalNavigation(
                            instance.uiRefs.ShopButtons,
                            items,
                            targetPage,
                            true
                        );

                        // --- 例外処理 ---
                        // 2. ページに表示されたボタンの数に対して、保存されたインデックスが無効でないか検証
                        int activeButtons = 0;
                        for (int i = 0; i < instance.uiRefs.ShopButtons.Count; i++)
                        {
                            if (instance.uiRefs.ShopButtons[i].gameObject.activeSelf)
                                activeButtons++;
                        }
                        if (targetIndex >= activeButtons)
                        {
                            targetIndex = activeButtons - 1; // 存在しないインデックスなら最後のボタンに補正
                        }

                        // 最終的に決定したボタンを選択状態にする
                        if (targetIndex >= 0)
                        {
                            // 新しいオブジェクトを選択する前に、現在の選択を一度リセットする
                            // これにより、ボタンにつけられたスクリプトのOnSelectが確実に発火するようになる
                            EventSystem.current.SetSelectedGameObject(null);

                            EventSystem.current.SetSelectedGameObject(
                                instance.uiRefs.ShopButtons[targetIndex].gameObject
                            );

                            instance.UpdateSelectedItemDetails(); // 選択されているアイテムの詳細を更新
                        }
                    }
                }
                else
                {
                    itemTab.GetComponent<Image>().sprite = instance.uiRefs.UnselectedTabImage; // 選択されていないタブの画像を変更
                }
            }
        }
    }

    public enum SellItemType
    {
        None = 0,
        ShootWeapon = 10,
        BladeWeapon = 20,
        HealItem = 30,
    }

    public enum ShopStatus
    {
        None = 0,
        Buy = 10, // 購入モード
        Sell = 20, // 売却モード
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            isPurchasing = false;

            if (!ValidateReferences())
                return;

            //iRefs経由でアクセス
            lastSelectedPrompt = uiRefs.PurchaseYesButton.gameObject;
            uiRefs.ShopUIPanel.SetActive(false);
            uiRefs.PurchasePromptPanel.SetActive(false);
            uiRefs.ItemDetailPanel.SetActive(false);
            uiRefs.WeaponDetailPanel.SetActive(false);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    /// <summary>
    /// ShopUIManager に必要な参照がすべて設定されているか検証する。
    /// 1つでも未設定の参照がある場合はエラーを出力し、false を返す。
    /// </summary>
    private bool ValidateReferences()
    {
        bool result = true;

        /// <summary>
        /// 指定したオブジェクトが null かをチェックし、null ならエラーログを出して result を false にする。
        /// </summary>
        void Check(object obj, string name)
        {
            if (obj == null)
            {
                Debug.LogError($"ShopUIManagerに{name}がセットされていません。", this);
                result = false;
            }
        }

        //チェック対象をuiRefsに集約
        Check(shopDataBase, nameof(shopDataBase));
        Check(conversationHandlerObject, nameof(conversationHandlerObject));
        Check(uiRefs, nameof(uiRefs));
        if (uiRefs != null)
        {
            if (uiRefs != null)
                Check(uiRefs.SellItemTab, "uiRefs.SellItemTab");
        }

        return result;
    }

    private void Start()
    {
        playerManager = PlayerManager.instance; // PlayerManagerの参照を取得
        if (playerManager == null)
        {
            Debug.LogError("PlayerManagerが見つかりません。ShopUIManagerの初期化に失敗しました。");
            return;
        }

        // 店の会話ハンドラーを初期化
        conversationHandler = conversationHandlerObject as IShopConversation;
        if (conversationHandler == null)
        {
            Debug.LogError("IShopConversationの実装が不正です。");
        }
    }

    private void Update()
    {
        if (uiRefs.ShopUIPanel == null)
            return;
        //下記のactiveselfのチェック時にnullである必要がある

        if (!uiRefs.ShopUIPanel.activeSelf)
            return;

        if (isPurchasing)
        {
            //購入中のとき、YesかNoのボタンが選択されていない場合、最後に選択されていたボタンを選択状態にする
            GameObject selectedObject = EventSystem.current.currentSelectedGameObject;

            // IsPromptButtonはローカル関数。購入確認パネルのボタンかどうかを判定
            if (!IsPromptButton(selectedObject))
            {
                if (lastSelectedPrompt != null)
                    EventSystem.current.SetSelectedGameObject(lastSelectedPrompt);
            }
            else
            {
                lastSelectedPrompt = selectedObject; // Yes/Noボタンのときのみ更新
            }

            bool IsPromptButton(GameObject obj)
            {
                return obj == uiRefs.PurchaseYesButton.gameObject
                    || obj == uiRefs.PurchaseNoButton.gameObject;
            }
        }

        if (shopStatus == ShopStatus.Sell && !isPurchasing)
        {
            // 売却モードのとき、タブの切り替えを行う
            if (InputManager.instance.GetTabRight())
            {
                ChangeTab(1);
            }
            else if (InputManager.instance.GetTabLeft())
            {
                ChangeTab(-1);
            }
        }

        if (InputManager.instance.UISelectNo() && !isPurchasing)
        {
            //購入中でない場合、UIを閉じる
            StartCoroutine(CloseShopCoroutine());
            return;
        }

        //選択されているボタンのアイテムIDを取得し、効果説明パネルの文章を変更する
        GetSelectedButtonItemID();
        //ページナビゲーションの更新
        UpdatePageNavigationBySelection();
    }

    /// <summary>
    /// 現在選択されている UI ボタンに応じて、売却用アイテムリストのページを上下に切り替える処理を行います。
    /// 下端ボタンが選択されていて下入力があれば次ページへ、
    /// 上端ボタンが選択されていて上入力があれば前ページへ移動します。
    /// 各アイテムタイプ（BladeWeapon, ShootWeapon, HealItem）に応じたリストとページ番号を操作します。
    /// </summary>
    private void UpdatePageNavigationBySelection()
    {
        // EventSystem が null の場合は処理しない
        if (EventSystem.current == null)
            return;

        // 現在選択されている UI 要素を取得
        GameObject selected = EventSystem.current?.currentSelectedGameObject;
        if (selected == null)
            return;

        if (selected != previousSelectedForPageUpdate)
        {
            previousSelectedForPageUpdate = selected;
            return; // ページナビゲーションの連続入力防止
        }

        // 現在選択されているオブジェクトが Button でなければ処理しない
        Button selectedButton = selected.GetComponent<Button>();
        if (selectedButton == null)
            return;

        // 下端のボタンが選択されている状態で、下入力が押されたとき
        if (uiRefs.BottomButton == selectedButton && InputManager.instance.UIMoveDown())
        {
            UpdatePage(1); // 次のページへ
        }
        // 上端のボタンが選択されている状態で、上入力が押されたとき
        else if (uiRefs.TopButton == selectedButton && InputManager.instance.UIMoveUp())
        {
            UpdatePage(-1); // 前のページへ
        }
    }

    /// <summary>
    /// ページを更新する共通処理
    /// </summary>
    /// <param name="direction">1なら次へ, -1なら前へ</param>
    private void UpdatePage(int direction)
    {
        // 購入モードの場合は何もしない（ページめくりは売却モードのみ）
        if (shopStatus == ShopStatus.Buy)
            return;

        // GetCurrentSellListAndPage()はDictionaryを参照するようになる
        // 現在のタブに応じたアイテムリストとページ番号を取得
        var (items, currentPage) = GetCurrentSellListAndPage();

        // 前のページに行こうとしているが、既に最初のページの場合は何もしない
        if (direction < 0 && currentPage <= 0)
            return;

        int newPage = currentPage + direction;

        if (
            UIUtility.AssignItemsVerticalNavigation(
                uiRefs.ShopButtons,
                items,
                newPage,
                direction > 0
            )
        )
        {
            // Dictionary内のページ番号を直接更新する
            sellTabStates.TryGetValue(sellItemType, out TabState currentState); // 既存のStateを取得（なければデフォルト）
            currentState.PageIndex = newPage; // ページ番号だけを更新
            currentState.SelectedButtonIndex = 0; // ページをめくったら選択は先頭に戻す
            sellTabStates[sellItemType] = currentState; // 更新したStateを辞書に保存

            // フォーカスも先頭のボタンに移動
            if (uiRefs.ShopButtons.Count > 0 && uiRefs.ShopButtons[0].gameObject.activeSelf)
            {
                EventSystem.current.SetSelectedGameObject(uiRefs.ShopButtons[0].gameObject);
                UpdateSelectedItemDetails(); // 選択されているアイテムの詳細を更新
            }
        }
    }

    /// <summary>
    /// 現在選択中の売却タブに応じたアイテムリストとページ番号を返す
    /// </summary>
    private (List<ItemEntry> items, int page) GetCurrentSellListAndPage()
    {
        //Dictionaryから現在のタブのページ番号を取得。なければ0を返す。
        int page = 0;
        if (sellTabStates.TryGetValue(sellItemType, out TabState state))
        {
            page = state.PageIndex;
        }

        switch (sellItemType)
        {
            case SellItemType.BladeWeapon:
                return (bladeWeaponList, page);
            case SellItemType.ShootWeapon:
                return (shootWeaponList, page);
            case SellItemType.HealItem:
                return (healItemList, page);
            default:
                Debug.LogError("売却アイテムの種類が設定されていません。");
                return (new List<ItemEntry>(), 0);
        }
    }

    /// <summary>
    /// 指定された方向（-1 or 1）にタブを切り替えます。リストの端に達するとループします。
    /// </summary>
    /// <param name="direction">移動方向（-1で左、1で右）</param>
    private void ChangeTab(int direction)
    {
        // タブを切り替える前に、現在の選択状態を保存する
        SaveCurrentTabState();

        int newIndex = currentTabIndex + direction;

        // 範囲外をループさせる（必要ならClampでも可）
        if (newIndex < 0)
        {
            newIndex = uiRefs.SellItemTab.Count - 1;
        }
        else if (newIndex >= uiRefs.SellItemTab.Count)
        {
            newIndex = 0;
        }

        currentTabIndex = newIndex;
        UpdateTabPanelVisibility();
    }

    /// <summary>
    /// 指定されたインデックスのタブを直接選択します。
    /// </summary>
    /// <param name="index">選択したいタブのインデックス</param>
    private void SetTab(int index)
    {
        // タブを切り替える前に、現在の選択状態を保存する
        SaveCurrentTabState();

        if (index < 0)
        {
            index = 0; // 最小値を0に設定
        }
        else if (index >= uiRefs.SellItemTab.Count)
        {
            index = uiRefs.SellItemTab.Count - 1; // 最大値を最終インデックスに設定
        }

        currentTabIndex = index;
        UpdateTabPanelVisibility();
    }

    /// <summary>
    /// 現在の`currentTabIndex`に基づいて、すべてのタブの表示状態（選択/非選択のスプライト）を更新します。
    /// </summary>
    private void UpdateTabPanelVisibility()
    {
        for (int i = 0; i < uiRefs.SellItemTab.Count; i++)
        {
            if (i == currentTabIndex)
            {
                uiRefs.SellItemTab[i].SetTabSelected(true); // 選択中のタブのパネルの画像を変更
            }
            else
            {
                uiRefs.SellItemTab[i].SetTabSelected(false); // 選択されていないタブの画像を変更
            }
        }
    }

    // 店のIDを設定して、店の開始会話を実行するメソッド
    /// <summary>
    public void SetShopID(ShopName shopID)
    {
        // 店のIDを設定
        currentShopID = shopID;

        // 店の開始会話を実行
        StartShopDialogue();
    }

    //購入時の店のUIを開くメソッド
    /// <summary>
    public void OpenBuyShop()
    {
        // 購入モードに設定
        shopStatus = ShopStatus.Buy;
        //選択されているボタンのIDをリセットする
        selectedButtonItemID = null;
        preselectedButtonItemID = null;
        // 購入中フラグをリセット
        isPurchasing = false;
        //購入確認パネルを非表示にする
        uiRefs.PurchasePromptPanel.SetActive(false);
        //全てのタブを非表示にする
        foreach (var entry in uiRefs.SellItemTab)
        {
            entry.itemTab.SetActive(false);
        }

        // 店のデータを取得
        ShopData shopData = shopDataBase.GetShopByID(currentShopID);
        if (shopData == null)
        {
            Debug.LogError($"ShopID {currentShopID} に対応するデータが見つかりません。");
            return;
        }

        // 店のUIを表示
        uiRefs.ShopUIPanel.SetActive(true);

        //一旦全てのボタンを非表示にする
        foreach (var button in uiRefs.ShopButtons)
        {
            button.gameObject.SetActive(false);
        }

        for (int i = 0; i < shopData.shopItems.Length; i++)
        {
            // ボタンを取得
            Button button = uiRefs.ShopButtons[i];

            var script = button.GetComponent<PurchaseSelectButton>();
            if (script == null)
            {
                Debug.LogError(
                    $"Button {button.name} に PurchaseSelectButton スクリプトがアサインされていません。"
                );
                continue;
            }

            //アイテムのIDを取得
            Enum itemID = shopData.shopItems[i].GetItemID();
            // アイテムのIDがnullの場合はスキップ
            if (itemID == null)
            {
                Debug.LogWarning($"ShopItem {i} のアイテムIDがnullです。");
                continue;
            }
            // 購入選択ボタンを初期化
            script.InitializePurchaseSelectButton(shopData.shopItems[i]);
            //ボタンを表示
            button.gameObject.SetActive(true);
        }

        // 最初のボタンを選択状態にする
        EventSystem.current.SetSelectedGameObject(uiRefs.ShopButtons[0].gameObject);
        //選択されているアイテムの詳細を更新するメソッドを登録
        GameManager.instance.OnAnyItemAddedToInventory += UpdateSelectedItemDetails;
        GameManager.instance.OnAnyItemRemovedFromInventory += HandleInventoryChanged;
        playerManager.OnChangePlayerMoney += SetCoinText; // 所持金の変更イベントを登録
        SetCoinText(); // 現在の所持金を表示
    }

    //販売時の店のUIを開くメソッド
    /// <summary>
    public void OpenSellShop()
    {
        // 売却モードに設定
        shopStatus = ShopStatus.Sell;
        //選択されているボタンのIDをリセットする
        selectedButtonItemID = null;
        preselectedButtonItemID = null;
        // 購入中フラグをリセット
        isPurchasing = false;
        //購入確認パネルを非表示にする
        uiRefs.PurchasePromptPanel.SetActive(false);
        //全てのタブを表示する
        foreach (var entry in uiRefs.SellItemTab)
        {
            entry.itemTab.SetActive(true);
        }

        // 店のUIを表示
        uiRefs.ShopUIPanel.SetActive(true);

        var savedata = GameManager.instance.savedata;
        if (savedata == null)
        {
            Debug.LogError("GameManagerのsavedataがnullです。");
            return;
        }

        //それぞれのアイテムリストを取得
        bladeWeaponList = savedata?.WeaponInventoryData?.GetAllItemByType(
            InventoryWeaponData.WeaponType.blade
        );
        shootWeaponList = savedata?.WeaponInventoryData?.GetAllItemByType(
            InventoryWeaponData.WeaponType.shoot
        );
        healItemList = savedata?.ItemInventoryData?.GetAllItemByType(
            InventoryItemData.ItemType.HealItem
        );

        // それぞれのアイテムリストを取得し、フィルタリングする
        // 各リストから所持数が0以下のアイテムを除外する
        // 売却不可のアイテムも除外する
        bladeWeaponList = savedata
            ?.WeaponInventoryData?.GetAllItemByType(InventoryWeaponData.WeaponType.blade)
            .Where(item =>
                item.count > 0
                && ItemDataManager.instance.IsItemSellable(EnumIDUtility.FromID(item.itemID))
            )
            .ToList();

        shootWeaponList = savedata
            ?.WeaponInventoryData?.GetAllItemByType(InventoryWeaponData.WeaponType.shoot)
            .Where(item =>
                item.count > 0
                && ItemDataManager.instance.IsItemSellable(EnumIDUtility.FromID(item.itemID))
            )
            .ToList();

        healItemList = savedata
            ?.ItemInventoryData?.GetAllItemByType(InventoryItemData.ItemType.HealItem)
            .Where(item =>
                item.count > 0
                && ItemDataManager.instance.IsItemSellable(EnumIDUtility.FromID(item.itemID))
            )
            .ToList();

        // 個別のページ変数のリセットの代わりに、辞書をクリアする
        sellTabStates.Clear();

        //最初はブレード武器のタブを選択状態にする
        SetTab(0);
        //選択されているアイテムの詳細を更新するメソッドを登録
        GameManager.instance.OnAnyItemAddedToInventory += UpdateSelectedItemDetails;
        GameManager.instance.OnAnyItemRemovedFromInventory += HandleInventoryChanged;
        // 所持金の変更イベントを登録
        playerManager.OnChangePlayerMoney += SetCoinText;
        // 現在の所持金を表示
        SetCoinText();
    }

    /// <summary>
    /// 店を閉じる処理を行います。
    /// </summary>
    private IEnumerator CloseShopCoroutine()
    {
        // 1フレーム待機して、時間の再開を全システムに反映させる
        yield return null;

        // 店のUIを非表示
        uiRefs.ShopUIPanel.SetActive(false);

        // 購入確認パネルを非表示
        uiRefs.PurchasePromptPanel.SetActive(false);

        // アイテム詳細パネルを非表示
        uiRefs.ItemDetailPanel.SetActive(false);

        // 武器詳細パネルを非表示
        uiRefs.WeaponDetailPanel.SetActive(false);

        isPurchasing = false; // 購入中フラグをリセット
        selectedButtonItemID = null; // 選択されているアイテムのIDをリセット
        preselectedButtonItemID = null; // 前フレームのアイテムIDをリセット
        lastSelected = null; // 最後に選択されていたボタンをリセット
        // イベントの登録を解除
        GameManager.instance.OnAnyItemAddedToInventory -= UpdateSelectedItemDetails;
        GameManager.instance.OnAnyItemRemovedFromInventory -= HandleInventoryChanged;
        playerManager.OnChangePlayerMoney -= SetCoinText;

        // 店の終わりの会話のBlockを取得
        Block block = GlobalFlowchartController.instance?.globalFlowchart?.FindBlock(
            shopEndBlockName
        );
        if (block == null)
        {
            Debug.LogWarning($"Block '{shopEndBlockName}' が見つかりません");
            yield break;
        }
        // 店の終わりの会話を実行
        GlobalFlowchartController.instance?.globalFlowchart?.ExecuteBlock(block);
    }

    //購入確認パネルを表示するメソッド
    /// <summary>
    public void SetPromptPanel(Enum itemID, int itemPrice, Vector2 selectButtonPosition)
    {
        //最後に選択されているアイテムボタンを取得(確認パネルが開く前に行う)
        lastSelected = EventSystem.current.currentSelectedGameObject;

        if (uiRefs.PurchasePromptPanel != null)
        {
            // 購入確認パネルの位置を設定
            // RectTransform rect = purchasePromptPanel.GetComponent<RectTransform>();
            // rect.anchoredPosition = selectButtonPosition + offset;
        }
        else
        {
            Debug.LogWarning("UIManagerもしくはアイテム購入確認パネルが存在しません");
            return;
        }

        //購入確認パネルのYesボタンにアイテムIDを設定
        var script = uiRefs.PurchaseYesButton.GetComponent<PurchasePromptButton>();
        if (script != null)
        {
            //購入確認パネルのYesボタンにアイテムIDを設定
            script.SetItemID(itemID);
            //購入確認パネルのYesボタンに購入価格を設定
            script.SetBuyPrice(itemPrice);
        }
        else
        {
            Debug.LogWarning("PurchasePromptButtonスクリプトが入手できませんでした");
            return;
        }

        //購入確認パネルを表示
        uiRefs.PurchasePromptPanel.SetActive(true);
        //購入確認パネルのYesボタンを選択状態にする
        EventSystem.current.SetSelectedGameObject(uiRefs.PurchaseYesButton.gameObject);
        // 購入中フラグを立てる
        isPurchasing = true;
    }

    /// <summary>
    /// 現在EventSystemで選択されているボタンからアイテムIDを取得し、必要であれば詳細パネルを更新します。
    /// </summary>
    public void GetSelectedButtonItemID()
    {
        //現在選択されているボタンのゲームオブジェクトを取得
        GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
        //選択されているボタンがないなら飛ばす
        if (selectedObj == null)
            return;

        //現在選択しているパネルのアイテムのIDを取得する
        for (int i = 0; i < uiRefs.ShopButtons.Count; i++)
        {
            if (uiRefs.ShopButtons[i].gameObject == selectedObj)
            {
                PurchaseSelectButton info = uiRefs
                    .ShopButtons[i]
                    .GetComponent<PurchaseSelectButton>();
                if (info != null)
                {
                    if (shopStatus == ShopStatus.Buy)
                    {
                        //購入モードのとき、選択されているアイテムのデータを取得
                        BaseItemData selectedButtonItemData = info.baseItemData;
                        //選択されているアイテムのIDを取得
                        if (selectedButtonItemData != null)
                        {
                            selectedButtonItemID = selectedButtonItemData.GetItemID();
                        }
                    }
                    else if (shopStatus == ShopStatus.Sell)
                    {
                        //売却モードのとき、選択されているアイテムのIDを取得
                        selectedButtonItemID = info.AssignedItemID;
                    }
                    else
                    {
                        Debug.LogWarning("ShopStatusが不正です");
                        return;
                    }
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
        if (!object.Equals(preselectedButtonItemID, selectedButtonItemID))
        {
            //選択されているアイテムの詳細を更新する
            if (selectedButtonItemID == null)
            {
                Debug.LogWarning("選択されているアイテムのIDがnullです");
                return;
            }
            UpdateSelectedItemDetails();
        }

        preselectedButtonItemID = selectedButtonItemID; //前フレームのアイテムIDを設定する
    }

    //選択されているアイテムの詳細を更新するメソッド
    private void UpdateSelectedItemDetails()
    {
        // パネルを非表示にする
        uiRefs.ItemDetailPanel.SetActive(false);
        uiRefs.WeaponDetailPanel.SetActive(false);

        //Enumから、タイプを判別する数に変更
        int typeNumber = EnumIDUtility.ExtractTypeID(EnumIDUtility.ToID(selectedButtonItemID));

        switch (typeNumber)
        {
            case (int)TypeID.Blade:
            case (int)TypeID.Shoot:
                uiRefs.WeaponDetailPanel.SetActive(true);
                WeaponDetailPanel weaponScript =
                    uiRefs.WeaponDetailPanel.GetComponent<WeaponDetailPanel>();

                if (weaponScript != null)
                {
                    // 選択されたアイテムの武器タイプを決定
                    var selectedWeaponType =
                        (typeNumber == (int)TypeID.Blade)
                            ? InventoryWeaponData.WeaponType.blade
                            : InventoryWeaponData.WeaponType.shoot;

                    // パネルの現在のタイプと異なれば、パネルのタイプを更新して再表示
                    if (weaponScript.weaponType != selectedWeaponType)
                    {
                        weaponScript.weaponType = selectedWeaponType;
                        weaponScript.RefreshEquippedWeaponDisplay();
                    }

                    // 選択中（カーソルが合っている）アイテムの詳細を表示
                    weaponScript.DisplayNextWeaponDetails(selectedButtonItemID);
                }
                else
                {
                    Debug.LogWarning("武器詳細パネルに適切なスクリプトが設定されていません");
                }
                break;
            case (int)TypeID.HealItem:
                uiRefs.ItemDetailPanel.SetActive(true); // アイテム詳細パネルを表示
                ItemDetailPanel itemScript = uiRefs.ItemDetailPanel.GetComponent<ItemDetailPanel>();
                if (itemScript != null)
                {
                    itemScript.DisplayItemDetails(selectedButtonItemID);
                }
                else
                {
                    Debug.LogWarning("アイテム詳細パネルに適切なスクリプトが設定されていません");
                }
                break;
            default:
                Debug.LogWarning($"選択されたアイテムのIDが不正です: {selectedButtonItemID}");
                return;
        }

        //選択されているアイテムの所持数を取得
        int itemAmount = GameManager.instance?.GetAllTypeIDToAmount(selectedButtonItemID) ?? 0;
        //選択されているアイテムの所持数を表示する
        uiRefs.SelectedItemAmountText.text = itemAmount.ToString();
    }

    /// <summary>
    /// GameManagerのアイテム増減イベントを受け取るための中継メソッド。
    /// 受け取ったIDは使わず、単にUI更新メソッドを呼び出す。
    /// </summary>
    private void HandleInventoryChanged(Enum itemID)
    {
        UpdateSelectedItemDetails();
    }

    //購入確認パネルを閉じるメソッド
    public void ClosePromptPanel()
    {
        // 購入中フラグをリセット
        isPurchasing = false;
        //購入確認パネルを非表示
        uiRefs.PurchasePromptPanel.SetActive(false);
        //最後に選択されていたボタンを再選択
        EventSystem.current.SetSelectedGameObject(lastSelected);
    }

    // 店の開始会話を実行するメソッド
    /// <summary>
    public void StartShopDialogue()
    {
        // 店のデータを取得
        ShopData shopData = shopDataBase.GetShopByID(currentShopID);
        if (shopData == null)
        {
            Debug.LogError($"ShopID {currentShopID} に対応するデータが見つかりません。");
            return;
        }

        //店の始めの会話を設定
        SetShopDialogue(shopStartBlockName, shopData.GetStartingDialogue());
        //店の終わりの会話を設定
        SetShopDialogue(shopEndBlockName, shopData.GetEndingDialogue());

        // 店の始めの会話のBlockを取得
        Block block = GlobalFlowchartController.instance?.globalFlowchart?.FindBlock(
            shopStartBlockName
        );
        if (block == null)
        {
            Debug.LogWarning($"Block '{shopStartBlockName}' が見つかりません");
            return;
        }

        // 店の始めの会話を実行
        GlobalFlowchartController.instance?.globalFlowchart?.ExecuteBlock(block);
    }

    /// <summary>
    /// FungusのBlock内にある最初のSayコマンドのテキストを指定された文字列で上書きします。
    /// </summary>
    /// <param name="blockName">対象のBlock名</param>
    /// <param name="newText">新しいテキスト</param>
    private void SetShopDialogue(string blockName, string newText)
    {
        // Blockを取得
        Block block = GlobalFlowchartController.instance?.globalFlowchart?.FindBlock(blockName);
        if (block == null)
        {
            Debug.LogWarning($"Block '{blockName}' が見つかりません");
            return;
        }

        // Block の中の最初の Say を見つけて変更
        foreach (var command in block.CommandList)
        {
            if (command is Say sayCommand)
            {
                sayCommand.SetStandardText(newText);
                break; // 最初のSayのみ変更
            }
        }
    }

    //店の会話を開始するメソッド
    /// <summary>
    public void StartShopConversation()
    {
        if (conversationHandler == null)
        {
            Debug.LogError("IShopConversationの実装が見つかりません。");
            return;
        }
        // 店の会話を開始
        conversationHandler?.StartShopConversation(currentShopID);
    }

    //現在の所持金を表示するメソッド
    /// <summary>
    private void SetCoinText()
    {
        // 現在の所持金を取得
        int currentMoney = playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerMoney);
        // 所持金をテキストに設定(金色で表示)
        uiRefs.CurrentMoneyText.text = $"<color=#C6A34C>{currentMoney}</color>";
    }

    /// <summary>
    /// 現在のタブの選択状態（ページとボタンのインデックス）を保存します。タブが切り替わる直前に呼び出されます。
    /// </summary>
    private void SaveCurrentTabState()
    {
        // 現在選択されているボタンのGameObjectを取得
        GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
        if (selectedObj == null)
            return;

        // 選択されているボタンがショップのボタンリストの何番目かを探す
        int buttonIndex = uiRefs.ShopButtons.FindIndex(button => button.gameObject == selectedObj);

        // ボタンが見つかった場合のみ状態を保存
        if (buttonIndex != -1)
        {
            // 現在のページ番号を取得
            var (_, currentPage) = GetCurrentSellListAndPage();

            // 新しい状態を作成
            TabState newState = new TabState
            {
                PageIndex = currentPage,
                SelectedButtonIndex = buttonIndex,
            };

            // 現在のタブの種類をキーとして、辞書に状態を保存（または上書き）
            sellTabStates[sellItemType] = newState;
        }
    }
}

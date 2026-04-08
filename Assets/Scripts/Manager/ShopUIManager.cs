using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fungus;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#region ショップUI管理クラス
/// <summary>
/// ショップのUI（購入・売却・タブ切り替え・会話など）を総合的に管理するシングルトンクラス。
/// </summary>
public class ShopUIManager : MonoBehaviour
{
    #region 定義・列挙型・内部クラス

    /// <summary>
    /// 売却アイテムの種類を定義する列挙型
    /// </summary>
    public enum SellItemType
    {
        None = 0,
        ShootWeapon = 10,
        BladeWeapon = 20,
        HealItem = 30,
        MaterialItem = 40,
    }

    /// <summary>
    /// 現在の店の状態（購入モードか売却モードか）を定義する列挙型
    /// </summary>
    public enum ShopStatus
    {
        None = 0,
        Buy = 10, // 購入モード
        Sell = 20, // 売却モード
    }

    /// <summary>
    /// タブの選択状態（ページとフォーカス位置）を保存するためのデータ構造
    /// </summary>
    private struct TabState
    {
        public int PageIndex;
        public int SelectedButtonIndex;
    }

    /// <summary>
    /// 売却タブごとのUI要素と更新ロジックを管理するクラス
    /// </summary>
    [System.Serializable]
    public class SellItemEntry
    {
        [Tooltip("アイテムタブのGameObject")]
        public GameObject itemTab;

        [Tooltip("このタブが扱うアイテムの種類")]
        public SellItemType sellItemType;

        /// <summary>
        /// このタブが選択されたかどうかに応じて、UIの表示を更新します。
        /// </summary>
        /// <param name="isSelected">このタブが選択された場合はtrue</param>
        public void SetTabSelected(bool isSelected)
        {
            if (itemTab != null)
            {
                if (isSelected)
                {
                    // 選択中のタブの画像を変更
                    itemTab.GetComponent<Image>().sprite = instance.uiRefs.SelectedTabImage;

                    // アイテムリストの取得
                    List<ItemEntry> items;

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
                        case SellItemType.MaterialItem:
                            items = instance.materialItemList;
                            break;
                        default:
                            Debug.LogError("未定義の売却アイテムタイプです。");
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

                        // 保存された状態データがあれば、それを読み込む
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

                        // --- 例外処理1: ページの補正 ---
                        // 保存されたページが現在のアイテム数に対して無効になっていないか検証
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

                        // --- 例外処理2: インデックスの補正 ---
                        // ページに表示されたボタンの数に対して、保存されたインデックスが無効でないか検証
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
                    // 選択されていないタブの画像を変更
                    itemTab.GetComponent<Image>().sprite = instance.uiRefs.UnselectedTabImage;
                }
            }
        }
    }

    #endregion

    #region 変数・プロパティ

    public static ShopUIManager instance { get; private set; } // ShopUIManagerのシングルトンインスタンス
    private PlayerManager playerManager;
    public static bool isPurchasing = false; // 現在、購入確認パネルを開いて購入処理中かどうかのフラグ

    [Header("ロジック関連の参照")]
    [SerializeField, Tooltip("全店舗のデータが格納されたデータベース")]
    private ShopDataBase shopDataBase;

    [Header("UI参照のルート")]
    [SerializeField, Tooltip("ショップUIの各パーツへの参照をまとめたオブジェクト")]
    private ShopUIRefs uiRefs;

    [Header("店の会話ハンドラー")]
    [SerializeField, Tooltip("IShopConversationを実装した会話制御用オブジェクト")]
    private MonoBehaviour conversationHandlerObject;
    private IShopConversation conversationHandler;

    [HideInInspector]
    private ShopName currentShopID = ShopName.None; // 現在開いている店のID
    public ShopStatus shopStatus { get; private set; } = ShopStatus.None; // 現在の店のステータス（購入/売却）
    private SellItemType sellItemType = SellItemType.None; // 現在選択中の売却アイテムの種類

    private Enum selectedButtonItemID = null; // 現在カーソルが合っているアイテムのID
    private Enum preselectedButtonItemID = null; // 1フレーム前にカーソルが合っていたアイテムのID

    private GameObject lastSelected; // 最後に選ばれていたリストのボタンを保存する変数
    private GameObject lastSelectedPrompt; // 最後に選ばれていた「購入確認パネル」のボタンを保存する変数
    private GameObject previousSelectedForPageUpdate; // ページナビゲーションの連続入力防止用に前フレームの選択を保存

    private string shopStartBlockName = "StartShopDialogue"; // Fungusの開始ブロック名
    private string shopEndBlockName = "EndShopDialogue"; // Fungusの終了ブロック名

    private int currentTabIndex = 0; // 現在選択されているタブのインデックス

    // 現在の所持アイテムリストのキャッシュ
    private List<ItemEntry> bladeWeaponList = new List<ItemEntry>();
    private List<ItemEntry> shootWeaponList = new List<ItemEntry>();
    private List<ItemEntry> healItemList = new List<ItemEntry>();
    private List<ItemEntry> materialItemList = new List<ItemEntry>();

    // 各アイテムタイプごとのタブ状態（ページ番号・カーソル位置）を保存する辞書
    private Dictionary<SellItemType, TabState> sellTabStates =
        new Dictionary<SellItemType, TabState>();

    #endregion

    #region 初期化・ライフサイクル

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            isPurchasing = false;

            if (!ValidateReferences())
                return;

            // UIの初期状態を設定
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

    private void Start()
    {
        playerManager = PlayerManager.instance;
        if (playerManager == null)
        {
            Debug.LogError("PlayerManagerが見つかりません。ShopUIManagerの初期化に失敗しました。");
            return;
        }

        // 店の会話ハンドラーをインターフェースとして取得
        conversationHandler = conversationHandlerObject as IShopConversation;
        if (conversationHandler == null)
        {
            Debug.LogError("IShopConversationの実装が不正です。");
        }
    }

    /// <summary>
    /// ShopUIManager に必要な参照がすべて設定されているか検証します。
    /// 1つでも未設定の参照がある場合はエラーを出力し、false を返します。
    /// </summary>
    private bool ValidateReferences()
    {
        bool result = true;

        void Check(object obj, string name)
        {
            if (obj == null)
            {
                Debug.LogError($"ShopUIManagerに{name}がセットされていません。", this);
                result = false;
            }
        }

        Check(shopDataBase, nameof(shopDataBase));
        Check(conversationHandlerObject, nameof(conversationHandlerObject));
        Check(uiRefs, nameof(uiRefs));

        if (uiRefs != null)
        {
            Check(uiRefs.SellItemTab, "uiRefs.SellItemTab");
        }

        return result;
    }

    #endregion

    #region 更新処理 (Update)

    private void Update()
    {
        if (uiRefs.ShopUIPanel == null || !uiRefs.ShopUIPanel.activeSelf)
            return;

        // --- 購入確認パネル操作時のフォーカス制御 ---
        if (isPurchasing)
        {
            // 購入中のとき、YesかNoのボタンが選択されていない場合、最後に選択されていたボタンを強制的に選択状態にする
            GameObject selectedObject = EventSystem.current.currentSelectedGameObject;

            if (!IsPromptButton(selectedObject))
            {
                if (lastSelectedPrompt != null)
                    EventSystem.current.SetSelectedGameObject(lastSelectedPrompt);
            }
            else
            {
                lastSelectedPrompt = selectedObject; // Yes/Noボタンのときのみ更新
            }

            // ローカル関数：オブジェクトが購入確認パネルのボタンかどうかを判定
            bool IsPromptButton(GameObject obj)
            {
                return obj == uiRefs.PurchaseYesButton.gameObject
                    || obj == uiRefs.PurchaseNoButton.gameObject;
            }
        }

        // --- 売却モード時のタブ切り替え入力検知 ---
        if (shopStatus == ShopStatus.Sell && !isPurchasing)
        {
            if (InputManager.instance.GetTabRight())
            {
                ChangeTab(1);
            }
            else if (InputManager.instance.GetTabLeft())
            {
                ChangeTab(-1);
            }
        }

        // --- キャンセル入力時のショップ終了処理 ---
        if (InputManager.instance.UISelectNo() && !isPurchasing)
        {
            StartCoroutine(CloseShopCoroutine());
            return;
        }

        // 選択されているボタンのアイテムIDを取得し、効果説明パネルの内容を更新する
        GetSelectedButtonItemID();

        // ページナビゲーションの入力検知と更新
        UpdatePageNavigationBySelection();
    }

    #endregion

    #region UI開閉・初期設定

    /// <summary>
    /// 店のIDを設定し、店の開始会話（Fungus）を実行します。
    /// </summary>
    /// <param name="shopID">開く店のID</param>
    public void SetShopID(ShopName shopID)
    {
        currentShopID = shopID;
        StartShopDialogue();
    }

    /// <summary>
    /// 購入モードとしてショップUIを開き、初期化を行います。
    /// </summary>
    public void OpenBuyShop()
    {
        shopStatus = ShopStatus.Buy;
        selectedButtonItemID = null;
        preselectedButtonItemID = null;
        isPurchasing = false;

        uiRefs.PurchasePromptPanel.SetActive(false);

        // 全ての売却用タブを非表示にする
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

        uiRefs.ShopUIPanel.SetActive(true);

        // 一旦全てのボタンを非表示にする
        foreach (var button in uiRefs.ShopButtons)
        {
            button.gameObject.SetActive(false);
        }

        // ショップデータに基づいてボタンをセットアップ
        for (int i = 0; i < shopData.shopItems.Count; i++)
        {
            Button button = uiRefs.ShopButtons[i];
            var script = button.GetComponent<PurchaseSelectButton>();

            if (script == null)
            {
                Debug.LogError(
                    $"Button {button.name} に PurchaseSelectButton スクリプトがアサインされていません。"
                );
                continue;
            }

            BaseItemData itemData = shopData.shopItems[i].item;

            if (itemData == null)
            {
                Debug.LogWarning($"ShopItem {i} のアイテムデータが設定されていません。");
                continue;
            }

            Enum itemID = itemData.GetItemID();

            // 購入選択ボタンを初期化して表示
            script.InitializePurchaseSelectButton(itemData);
            button.gameObject.SetActive(true);
        }

        // 最初のボタンを選択状態にする
        EventSystem.current.SetSelectedGameObject(uiRefs.ShopButtons[0].gameObject);

        // イベントの登録
        GameManager.instance.OnAnyItemAddedToInventory += UpdateSelectedItemDetails;
        GameManager.instance.OnAnyItemRemovedFromInventory += HandleInventoryChanged;
        playerManager.OnChangePlayerMoney += SetCoinText;

        SetCoinText(); // 現在の所持金を表示
    }

    /// <summary>
    /// 売却モードとしてショップUIを開き、プレイヤーの所持品を読み込んで初期化を行います。
    /// </summary>
    public void OpenSellShop()
    {
        shopStatus = ShopStatus.Sell;
        selectedButtonItemID = null;
        preselectedButtonItemID = null;
        isPurchasing = false;

        uiRefs.PurchasePromptPanel.SetActive(false);

        // 全ての売却用タブを表示する
        foreach (var entry in uiRefs.SellItemTab)
        {
            entry.itemTab.SetActive(true);
        }

        uiRefs.ShopUIPanel.SetActive(true);

        var savedata = GameManager.instance.savedata;
        if (savedata == null)
        {
            Debug.LogError("GameManagerのsavedataがnullです。");
            return;
        }

        // --- 所持アイテムのフィルタリング ---
        // 各リストから所持数が1以上、かつ「売却可能」なアイテムのみを抽出する
        bladeWeaponList = savedata
            .WeaponInventoryData?.GetAllItemByType(InventoryWeaponData.WeaponType.blade)
            .Where(item =>
                item.count > 0
                && ItemDataManager.instance.IsItemSellable(EnumIDUtility.FromID(item.itemID))
            )
            .ToList();

        shootWeaponList = savedata
            .WeaponInventoryData?.GetAllItemByType(InventoryWeaponData.WeaponType.shoot)
            .Where(item =>
                item.count > 0
                && ItemDataManager.instance.IsItemSellable(EnumIDUtility.FromID(item.itemID))
            )
            .ToList();

        healItemList = savedata
            .ItemInventoryData?.GetAllItemByType(InventoryItemData.ItemType.HealItem)
            .Where(item =>
                item.count > 0
                && ItemDataManager.instance.IsItemSellable(EnumIDUtility.FromID(item.itemID))
            )
            .ToList();

        materialItemList = savedata
            .ItemInventoryData?.GetAllItemByType(InventoryItemData.ItemType.MaterialItem)
            .Where(item =>
                item.count > 0
                && ItemDataManager.instance.IsItemSellable(EnumIDUtility.FromID(item.itemID))
            )
            .ToList();

        // 過去のタブ状態をクリア
        sellTabStates.Clear();

        // 最初は一番左（インデックス0）のタブを選択状態にする
        SetTab(0);

        // イベントの登録
        GameManager.instance.OnAnyItemAddedToInventory += UpdateSelectedItemDetails;
        GameManager.instance.OnAnyItemRemovedFromInventory += HandleInventoryChanged;
        playerManager.OnChangePlayerMoney += SetCoinText;

        SetCoinText(); // 現在の所持金を表示
    }

    /// <summary>
    /// 店のUIを閉じ、関連するイベントの解除や終了会話の実行を行います。
    /// </summary>
    private IEnumerator CloseShopCoroutine()
    {
        // 1フレーム待機して、時間の再開を全システムに安全に反映させる
        yield return null;

        uiRefs.ShopUIPanel.SetActive(false);
        uiRefs.PurchasePromptPanel.SetActive(false);
        uiRefs.ItemDetailPanel.SetActive(false);
        uiRefs.WeaponDetailPanel.SetActive(false);

        isPurchasing = false;
        selectedButtonItemID = null;
        preselectedButtonItemID = null;
        lastSelected = null;

        // イベントの登録を解除
        GameManager.instance.OnAnyItemAddedToInventory -= UpdateSelectedItemDetails;
        GameManager.instance.OnAnyItemRemovedFromInventory -= HandleInventoryChanged;
        playerManager.OnChangePlayerMoney -= SetCoinText;

        // 終了の会話ブロックを取得して実行
        Block block = GlobalFlowchartController.instance?.globalFlowchart?.FindBlock(
            shopEndBlockName
        );
        if (block == null)
        {
            Debug.LogWarning($"Block '{shopEndBlockName}' が見つかりません");
            yield break;
        }
        GlobalFlowchartController.instance?.globalFlowchart?.ExecuteBlock(block);
    }

    /// <summary>
    /// アイテム購入確認のポップアップパネルを表示します。
    /// </summary>
    /// <param name="itemID">対象のアイテムID</param>
    /// <param name="itemPrice">アイテムの価格</param>
    /// <param name="selectButtonPosition">パネルを表示する基準位置</param>
    public void SetPromptPanel(Enum itemID, int itemPrice, Vector2 selectButtonPosition)
    {
        // 確認パネルが開く直前のアイテムボタンを記憶しておく
        lastSelected = EventSystem.current.currentSelectedGameObject;

        if (uiRefs.PurchasePromptPanel == null)
        {
            Debug.LogWarning("UIManagerもしくはアイテム購入確認パネルが存在しません");
            return;
        }

        var script = uiRefs.PurchaseYesButton.GetComponent<PurchasePromptButton>();
        if (script != null)
        {
            script.SetItemID(itemID);
            script.SetBuyPrice(itemPrice);
        }
        else
        {
            Debug.LogWarning("PurchasePromptButtonスクリプトが入手できませんでした");
            return;
        }

        uiRefs.PurchasePromptPanel.SetActive(true);
        EventSystem.current.SetSelectedGameObject(uiRefs.PurchaseYesButton.gameObject);
        isPurchasing = true; // 購入中フラグをオン
    }

    /// <summary>
    /// 購入確認パネルを閉じ、元のアイテムリストにフォーカスを戻します。
    /// </summary>
    public void ClosePromptPanel()
    {
        isPurchasing = false;
        uiRefs.PurchasePromptPanel.SetActive(false);
        EventSystem.current.SetSelectedGameObject(lastSelected);
    }

    #endregion

    #region タブ・ページ遷移処理

    /// <summary>
    /// 現在選択されている UI ボタンに応じて、売却用アイテムリストのページを上下に切り替えます。
    /// </summary>
    private void UpdatePageNavigationBySelection()
    {
        if (EventSystem.current == null)
            return;

        GameObject selected = EventSystem.current?.currentSelectedGameObject;
        if (selected == null)
            return;

        // ページナビゲーションの連続入力を防ぐためのチェック
        if (selected != previousSelectedForPageUpdate)
        {
            previousSelectedForPageUpdate = selected;
            return;
        }

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
    /// 指定された方向へリストのページを更新します。
    /// </summary>
    /// <param name="direction">1なら次ページへ, -1なら前ページへ</param>
    private void UpdatePage(int direction)
    {
        // 購入モードの場合はページめくりを行わない
        if (shopStatus == ShopStatus.Buy)
            return;

        // 現在のタブに応じたアイテムリストとページ番号を取得
        var (items, currentPage) = GetCurrentSellListAndPage();

        // 既に最初のページにいる状態で前へ行こうとした場合は無視
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
            // 状態辞書のページ番号を更新し、選択位置をリセット
            sellTabStates.TryGetValue(sellItemType, out TabState currentState);
            currentState.PageIndex = newPage;
            currentState.SelectedButtonIndex = 0;
            sellTabStates[sellItemType] = currentState;

            // フォーカスを先頭のボタンに移動
            if (uiRefs.ShopButtons.Count > 0 && uiRefs.ShopButtons[0].gameObject.activeSelf)
            {
                EventSystem.current.SetSelectedGameObject(uiRefs.ShopButtons[0].gameObject);
                UpdateSelectedItemDetails();
            }
        }
    }

    /// <summary>
    /// 現在選択中の売却タブに応じたアイテムリストとページ番号を返します。
    /// </summary>
    private (List<ItemEntry> items, int page) GetCurrentSellListAndPage()
    {
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
            case SellItemType.MaterialItem:
                return (materialItemList, page);
            default:
                Debug.LogError("売却アイテムの種類が設定されていません。");
                return (new List<ItemEntry>(), 0);
        }
    }

    /// <summary>
    /// 指定された方向（-1で左、1で右）にタブを切り替えます。リストの端に達するとループします。
    /// </summary>
    private void ChangeTab(int direction)
    {
        SaveCurrentTabState(); // 切り替え前に状態を保存

        int newIndex = currentTabIndex + direction;

        // 範囲外に出た場合はループさせる
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
    private void SetTab(int index)
    {
        SaveCurrentTabState();

        if (index < 0)
            index = 0;
        else if (index >= uiRefs.SellItemTab.Count)
            index = uiRefs.SellItemTab.Count - 1;

        currentTabIndex = index;
        UpdateTabPanelVisibility();
    }

    /// <summary>
    /// 現在の `currentTabIndex` に基づいて、すべてのタブの見た目（選択/非選択）とリストを更新します。
    /// </summary>
    private void UpdateTabPanelVisibility()
    {
        for (int i = 0; i < uiRefs.SellItemTab.Count; i++)
        {
            bool isSelected = (i == currentTabIndex);
            uiRefs.SellItemTab[i].SetTabSelected(isSelected);
        }
    }

    /// <summary>
    /// 現在のタブの選択状態（ページ番号とボタン位置）を辞書に保存します。タブが切り替わる直前に呼ばれます。
    /// </summary>
    private void SaveCurrentTabState()
    {
        GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
        if (selectedObj == null)
            return;

        int buttonIndex = uiRefs.ShopButtons.FindIndex(button => button.gameObject == selectedObj);

        if (buttonIndex != -1)
        {
            var (_, currentPage) = GetCurrentSellListAndPage();

            TabState newState = new TabState
            {
                PageIndex = currentPage,
                SelectedButtonIndex = buttonIndex,
            };

            sellTabStates[sellItemType] = newState;
        }
    }

    #endregion

    #region アイテム詳細・UI更新

    /// <summary>
    /// 現在EventSystemで選択されているボタンからアイテムIDを取得し、必要に応じて詳細パネルを更新します。
    /// </summary>
    public void GetSelectedButtonItemID()
    {
        GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
        if (selectedObj == null)
            return;

        // 選択中のボタンから該当するアイテムIDを取得する
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
                        BaseItemData selectedButtonItemData = info.baseItemData;
                        if (selectedButtonItemData != null)
                        {
                            selectedButtonItemID = selectedButtonItemData.GetItemID();
                        }
                    }
                    else if (shopStatus == ShopStatus.Sell)
                    {
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
                    selectedButtonItemID = null;
                    preselectedButtonItemID = selectedButtonItemID;
                    Debug.LogWarning("PurchaseSelectButton スクリプトが見つかりませんでした");
                }
            }
        }

        // カーソルが別のアイテムに移動した時のみ詳細パネルを更新する
        if (!object.Equals(preselectedButtonItemID, selectedButtonItemID))
        {
            if (selectedButtonItemID == null)
            {
                Debug.LogWarning("選択されているアイテムのIDがnullです");
                return;
            }
            UpdateSelectedItemDetails();
        }

        preselectedButtonItemID = selectedButtonItemID;
    }

    /// <summary>
    /// 選択されているアイテムの種類を判別し、適切な詳細パネル（武器用・アイテム用）に情報を表示します。
    /// </summary>
    private void UpdateSelectedItemDetails()
    {
        uiRefs.ItemDetailPanel.SetActive(false);
        uiRefs.WeaponDetailPanel.SetActive(false);

        // Enumからアイテムの種類を表す数値を抽出
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
                    var selectedWeaponType =
                        (typeNumber == (int)TypeID.Blade)
                            ? InventoryWeaponData.WeaponType.blade
                            : InventoryWeaponData.WeaponType.shoot;

                    // 武器タイプが変わっていれば再設定・リフレッシュ
                    if (weaponScript.weaponType != selectedWeaponType)
                    {
                        weaponScript.weaponType = selectedWeaponType;
                        weaponScript.RefreshEquippedWeaponDisplay();
                    }

                    weaponScript.DisplayNextWeaponDetails(selectedButtonItemID);
                }
                else
                {
                    Debug.LogWarning("武器詳細パネルに適切なスクリプトが設定されていません");
                }
                break;

            case (int)TypeID.HealItem:
                uiRefs.ItemDetailPanel.SetActive(true);
                ItemDetailPanel itemScript = uiRefs.ItemDetailPanel.GetComponent<ItemDetailPanel>();
                if (itemScript != null)
                {
                    itemScript.DisplayItemDetails(selectedButtonItemID);
                }
                break;

            case (int)TypeID.MaterialItem:
                uiRefs.ItemDetailPanel.SetActive(true);
                ItemDetailPanel materialScript =
                    uiRefs.ItemDetailPanel.GetComponent<ItemDetailPanel>();
                if (materialScript != null)
                {
                    materialScript.DisplayItemDetails(selectedButtonItemID);
                }
                break;

            default:
                Debug.LogWarning($"選択されたアイテムのIDが不正です: {selectedButtonItemID}");
                return;
        }

        // プレイヤーが現在所持している数を取得して表示
        int itemAmount = GameManager.instance?.GetAllTypeIDToAmount(selectedButtonItemID) ?? 0;
        uiRefs.SelectedItemAmountText.text = itemAmount.ToString();
    }

    /// <summary>
    /// GameManagerのインベントリ変動イベントを受け取ってUIを更新する中継メソッド。
    /// </summary>
    /// <param name="itemID">変動があったアイテムID</param>
    private void HandleInventoryChanged(Enum itemID)
    {
        UpdateSelectedItemDetails();
    }

    /// <summary>
    /// 現在の所持金を取得し、UIのテキストに反映します。
    /// </summary>
    private void SetCoinText()
    {
        int currentMoney = playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerMoney);
        uiRefs.CurrentMoneyText.text = $"<color=#C6A34C>{currentMoney}</color>";
    }

    #endregion

    #region ダイアログ・会話処理

    /// <summary>
    /// Fungusを使用して、店の開始時のダイアログ（会話）を実行します。
    /// </summary>
    public void StartShopDialogue()
    {
        ShopData shopData = shopDataBase.GetShopByID(currentShopID);
        if (shopData == null)
        {
            Debug.LogError($"ShopID {currentShopID} に対応するデータが見つかりません。");
            return;
        }

        // データベースから取得したセリフでFungusのテキストを上書き
        SetShopDialogue(shopStartBlockName, shopData.GetStartingDialogue());
        SetShopDialogue(shopEndBlockName, shopData.GetEndingDialogue());

        Block block = GlobalFlowchartController.instance?.globalFlowchart?.FindBlock(
            shopStartBlockName
        );
        if (block == null)
        {
            Debug.LogWarning($"Block '{shopStartBlockName}' が見つかりません");
            return;
        }

        GlobalFlowchartController.instance?.globalFlowchart?.ExecuteBlock(block);
    }

    /// <summary>
    /// IShopConversationを利用して、店舗固有の複雑な会話フローを開始します。
    /// </summary>
    public void StartShopConversation()
    {
        if (conversationHandler == null)
        {
            Debug.LogError("IShopConversationの実装が見つかりません。");
            return;
        }
        conversationHandler?.StartShopConversation(currentShopID);
    }

    #endregion

    #region ヘルパーメソッド

    /// <summary>
    /// FungusのBlock内にある最初のSayコマンドのテキストを、指定された文字列で上書きします。
    /// </summary>
    /// <param name="blockName">対象となるFungusのBlock名</param>
    /// <param name="newText">上書きする新しいセリフ</param>
    private void SetShopDialogue(string blockName, string newText)
    {
        Block block = GlobalFlowchartController.instance?.globalFlowchart?.FindBlock(blockName);
        if (block == null)
        {
            Debug.LogWarning($"Block '{blockName}' が見つかりません");
            return;
        }

        // Block内のコマンドリストから最初のSayコマンドを探し、テキストを差し替える
        foreach (var command in block.CommandList)
        {
            if (command is Say sayCommand)
            {
                sayCommand.SetStandardText(newText);
                break;
            }
        }
    }

    #endregion
}
#endregion

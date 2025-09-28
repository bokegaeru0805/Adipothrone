using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PurchasePromptButton : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    //マネージャーの参照を保持する変数
    private PlayerManager playerManager;
    private ShopUIManager shopUIManager;
    private GameManager gameManager;
    private InputManager inputManager;
    private Enum itemID; // 購入するアイテムのID
    private int buyPrice; // 購入価格

    [SerializeField]
    private GameObject purchasePromptPanel;

    [SerializeField]
    private PromptType promptType;

    [Header("Yesボタンの文章(Yesボタンのみが必要)")]
    [SerializeField]
    private TextMeshProUGUI yesButtonText = null; // 購入確認のYesボタンのテキスト

    [Header("売却・購入合計金額のUI本体(Yesボタンのみが必要)")]
    [SerializeField]
    private GameObject totalPriceUI = null;

    [SerializeField]
    private TextMeshProUGUI totalPriceNumberText = null; // 合計金額のテキスト

    [Header("個数変更のUI(Yesボタンのみが必要)")]
    [SerializeField]
    private GameObject IncreaseQuantityImage;

    [SerializeField]
    private GameObject DecreaseQuantityImage;
    private ShopUIManager.ShopStatus shopStatus = ShopUIManager.ShopStatus.None; // 現在のショップの状態

    // --- 売却関連の変数 ---
    private int itemSellQuantity = 1; // 売却時の個数
    private int itemSellMaxQuantity = 0; // 売却可能な最大個数
    private int sellPricePerItem = 0; // 売却時の1個あたりの価格

    // --- 購入関連の変数 ---
    private int itemBuyQuantity = 1; // 購入時の個数

    [Header("個数変更UIのアニメーション設定")]
    [SerializeField]
    [Tooltip("選択時に左右のUIが揺れ動く幅（ピクセル）")]
    private float yoyoMoveDistance = 5f;
    private Button myButton; // ボタンコンポーネントをキャッシュする変数

    private enum PromptType
    {
        none = 0,
        Yes = 1,
        No = 2,
    }

    // --- アニメーション制御用の変数 ---
    private RectTransform increaseImageRect;
    private RectTransform decreaseImageRect;
    private Vector2 increaseImageOriginalPos;
    private Vector2 decreaseImageOriginalPos;
    private Tween increaseTween;
    private Tween decreaseTween;

    public void SetItemID(Enum num) => itemID = num;

    public void SetBuyPrice(int price) => buyPrice = price;

    private void Awake()
    {
        if (promptType == PromptType.none)
        {
            Debug.LogWarning("購入確認選択ボタンにPromptTypeが設定されていません。");
            return;
        }

        if (purchasePromptPanel == null)
        {
            Debug.LogWarning("購入確認パネルが設定されていません。");
            return;
        }

        if (promptType == PromptType.Yes)
        {
            if (yesButtonText == null)
            {
                Debug.LogError("購入Yesボタンのテキストが設定されていません。");
                return;
            }
            if (totalPriceUI == null || totalPriceNumberText == null)
            {
                Debug.LogError("合計金額のUIが設定されていません。");
                return;
            }
            if (IncreaseQuantityImage == null || DecreaseQuantityImage == null)
            {
                Debug.LogError("購入Yesボタンの個数変更UIが設定されていません。");
                return;
            }

            if (IncreaseQuantityImage != null)
            {
                increaseImageRect = IncreaseQuantityImage.GetComponent<RectTransform>();
                increaseImageOriginalPos = increaseImageRect.anchoredPosition;
            }
            else
            {
                Debug.LogWarning("個数変更(増加)UIが設定されていません。");
            }

            if (DecreaseQuantityImage != null)
            {
                decreaseImageRect = DecreaseQuantityImage.GetComponent<RectTransform>();
                decreaseImageOriginalPos = decreaseImageRect.anchoredPosition;
            }
            else
            {
                Debug.LogWarning("個数変更(減少)UIが設定されていません。");
            }
        }

        myButton = GetComponent<Button>();
        myButton.onClick.AddListener(OnPromptSelected); // ボタンのクリックイベントを登録
    }

    private void Start()
    {
        // Startで、他のマネージャー（他人）との連携を行う
        playerManager = PlayerManager.instance;
        shopUIManager = ShopUIManager.instance;
        gameManager = GameManager.instance;
        inputManager = InputManager.instance;

        if (
            playerManager == null
            || shopUIManager == null
            || gameManager == null
            || inputManager == null
        )
        {
            Debug.LogError(
                "必要なマネージャーが見つかりません。PurchasePromptButtonは機能しません。"
            );
            gameObject.SetActive(false);
            return;
        }

        // OnEnableにあった初期化処理を、マネージャー取得後のStartに移動
        InitializePanelState();
    }

    private void OnEnable()
    {
        // Start()が完了した後（＝マネージャーが取得済み）の再有効化時のみ、状態を更新する
        // playerManagerがnullということは、まだStart()が呼ばれていない初回起動時なので、何もしない
        if (playerManager == null)
            return;

        // 2回目以降の表示の際に、状態をリフレッシュする
        InitializePanelState();
        //パネルが再表示されたときにアニメーションをリセットする
        StopAndResetAnimation();
    }

    private void OnDisable()
    {
        StopAndResetAnimation();
    }

    // OnEnableとStartから呼ばれる共通の初期化メソッド
    /// <summary>
    /// 現在のショップの状態に合わせて、パネルの表示を初期化・更新します。
    /// </summary>
    private void InitializePanelState()
    {
        shopStatus = shopUIManager.shopStatus;

        if (promptType == PromptType.Yes)
        {
            IncreaseQuantityImage.SetActive(true); // 個数変更(増加)UIを表示
            DecreaseQuantityImage.SetActive(true); // 個数変更(減少)UIを表示
            totalPriceUI.SetActive(true); // 合計金額UIを表示

            if (shopStatus == ShopUIManager.ShopStatus.Sell)
            {
                //売却時の個数を初期化
                itemSellQuantity = 1;
                //購入時の価格を取得
                sellPricePerItem = gameManager.GetAllTypeIDtoSellPrice(itemID);
                //売却するアイテムの所持数を取得
                itemSellMaxQuantity = gameManager.GetAllTypeIDToAmount(itemID);
                // //売却時のYesボタンのテキストを設定
                // yesButtonText.text = $"<color=#C6A34C>売却 {itemSellQuantity}個</color>";
                //売却時のYesボタンのテキストと合計金額を更新
                UpdateSellUI();
            }
            else if (shopStatus == ShopUIManager.ShopStatus.Buy)
            {
                //購入時の個数を初期化
                itemBuyQuantity = 1;
                //購入時のYesボタンのテキストと合計金額を更新
                UpdateBuyUI();
            }
        }
    }

    private void Update()
    {
        if (promptType == PromptType.Yes)
        {
            if (shopStatus == ShopUIManager.ShopStatus.Sell)
            {
                //売却時の個数変更処理
                if (inputManager.UIMoveRight() && itemSellQuantity < itemSellMaxQuantity)
                {
                    // 売却する個数を増やす
                    itemSellQuantity++;
                    // 最大個数を超えたら1に戻る（ループ処理）
                    if (itemSellQuantity > itemSellMaxQuantity)
                    {
                        itemSellQuantity = 1;
                    }
                    UpdateSellUI();
                }
                else if (inputManager.UIMoveLeft() && itemSellQuantity > 1)
                {
                    // 売却する個数を減らす
                    itemSellQuantity--;
                    // 1未満になったら最大値に戻る（ループ処理）
                    if (itemSellQuantity < 1)
                    {
                        itemSellQuantity = itemSellMaxQuantity;
                    }
                    UpdateSellUI();
                }
            }
            else if (shopStatus == ShopUIManager.ShopStatus.Buy)
            {
                // --- 購入時の個数変更処理 ---
                int playerMoney = playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerMoney);
                // プレイヤーが購入可能な最大個数を計算
                int maxPurchasableQuantity =
                    (buyPrice > 0) ? playerMoney / buyPrice : GameConstants.BuyMaxQuantity;
                // 購入可能限界個数と購入可能な個数のうち、小さい方を上限とする
                int effectiveMaxQuantity = Mathf.Min(
                    GameConstants.BuyMaxQuantity,
                    maxPurchasableQuantity
                );

                if (inputManager.UIMoveRight())
                {
                    itemBuyQuantity++;
                    // 上限を超えたら1に戻る（ループ処理）
                    if (itemBuyQuantity > effectiveMaxQuantity)
                    {
                        itemBuyQuantity = 1;
                    }
                    UpdateBuyUI();
                }
                else if (inputManager.UIMoveLeft())
                {
                    itemBuyQuantity--;
                    // 1未満になったら上限値に戻る（ループ処理）
                    if (itemBuyQuantity < 1)
                    {
                        // 在庫が0の場合は1に戻す
                        itemBuyQuantity = (effectiveMaxQuantity > 0) ? effectiveMaxQuantity : 1;
                    }
                    UpdateBuyUI();
                }
            }
        }
        else if (promptType == PromptType.No)
        {
            // もしこのパネルがアクティブで、ボタンが操作可能な状態なら
            if (purchasePromptPanel.activeInHierarchy && myButton.interactable)
            {
                if (inputManager.UISelectNo())
                {
                    // Noボタンが押された場合の処理
                    HandleNo();
                    return;
                }
            }
        }
    }

    //購入UIを更新するメソッド
    private void UpdateBuyUI()
    {
        // Yesボタンのテキストを更新
        yesButtonText.text = $"<color=#C6A34C>購入 {itemBuyQuantity}個</color>";
        // 購入時の合計金額を更新
        totalPriceNumberText.text = $"{buyPrice * itemBuyQuantity}";
    }

    //売却UIを更新するメソッド
    private void UpdateSellUI()
    {
        // Yesボタンのテキストを更新
        yesButtonText.text = $"<color=#C6A34C>売却 {itemSellQuantity}個</color>";
        // 売却時の合計金額を更新
        totalPriceNumberText.text = $"{sellPricePerItem * itemSellQuantity}";
    }

    private void OnPromptSelected()
    {
        if (promptType == PromptType.Yes)
        {
            HandleYes();
        }
        else if (promptType == PromptType.No)
        {
            HandleNo();
        }
    }

    private void HandleYes()
    {
        if (shopStatus == ShopUIManager.ShopStatus.Buy)
        {
            //指定した個数分アイテムを追加し、合計金額を引く
            int totalPrice = buyPrice * itemBuyQuantity;
            //購入確定前にも所持金を最終チェック
            if (playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerMoney) < totalPrice)
            {
                SEManager.instance?.PlayUISE(SE_UI.Beep1); // エラー音を鳴らす
                return; // 処理を中断
            }

            //購入したアイテムをインベントリに追加
            GameManager.instance.AddAllTypeIDToInventory(itemID, itemBuyQuantity);
            //データの金額の更新
            PlayerManager.instance.ChangeMoney(-totalPrice);
            // 購入完了のSEを再生
            SEManager.instance?.PlaySystemEventSE(SE_SystemEvent.CashRegister);
        }
        else if (shopStatus == ShopUIManager.ShopStatus.Sell)
        {
            //売却時のアイテムをインベントリから削除
            GameManager.instance.RemoveAllTypeIDFromInventory(itemID, itemSellQuantity);
            //データの金額の更新
            PlayerManager.instance.ChangeMoney(+sellPricePerItem * itemSellQuantity);
            // 売却完了のSEを再生
            SEManager.instance?.PlaySystemEventSE(SE_SystemEvent.CashRegister);
        }
        else
        {
            Debug.LogWarning("ShopUIManagerの状態が不正です。");
            return;
        }
        ClosePanel();
    }

    private void HandleNo()
    {
        ClosePanel();
    }

    private void ClosePanel()
    {
        if (shopUIManager != null)
        {
            shopUIManager.ClosePromptPanel();
        }
        else
            Debug.LogWarning("ShopUIManagerが存在しません");
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (promptType == PromptType.Yes)
        {
            // 既存のアニメーションを停止
            StopAndResetAnimation();

            // 増加UIを右にゆらゆら動かす
            increaseTween = increaseImageRect
                .DOAnchorPosX(increaseImageOriginalPos.x + yoyoMoveDistance, 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);

            // 減少UIを左にゆらゆら動かす
            decreaseTween = decreaseImageRect
                .DOAnchorPosX(decreaseImageOriginalPos.x - yoyoMoveDistance, 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (promptType == PromptType.Yes)
        {
            StopAndResetAnimation();
        }
    }

    private void StopAndResetAnimation()
    {
        // 実行中のアニメーションを完全に停止
        increaseTween?.Kill();
        decreaseTween?.Kill();

        // UIを元の位置に戻す
        if (increaseImageRect != null)
        {
            increaseImageRect.anchoredPosition = increaseImageOriginalPos;
        }
        if (decreaseImageRect != null)
        {
            decreaseImageRect.anchoredPosition = decreaseImageOriginalPos;
        }
    }
}

using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PurchaseSelectButton : MonoBehaviour, IItemAssignable, ISelectHandler, IDeselectHandler
{
    private PlayerManager playerManager;
    private ShopUIManager shopUIManager;
    private ItemDataManager itemDataManager;
    private GameManager gameManager;

    [Header("アニメーション対象")]
    [SerializeField]
    private RectTransform backgroundToAnimate;

    [SerializeField]
    private RectTransform itemIconRectTransform;

    [Header("購入選択ボタン")]
    [SerializeField]
    private Image itemIconImage; // アイテムのアイコン画像

    [SerializeField]
    private TextMeshProUGUI itemNameText; // アイテムの名前テキスト

    [SerializeField]
    private TextMeshProUGUI itemPriceText; // アイテムの価格テキスト

    [HideInInspector]
    public BaseItemData baseItemData; // 選択されたアイテムのデータ

    [HideInInspector]
    public Enum AssignedItemID => assignedItemID; //選択されているアイテムのID
    private Enum assignedItemID; // 実際のEnum型

    public void AssignItem(Enum itemID)
    {
        assignedItemID = itemID;
        InitializeSellSelectButton(); //アイテムの売却時の情報を更新
    }

    private Image backgroundImage; // 背景画像のキャッシュ
    private Color originalBackgroundColor; // 背景の元の色
    private float baseSize = 0; // ボタンのアイテム画像のベースサイズ（初期化時に設定）
    private int itemPrice = 0; // 購入・売却価格
    private Vector2 selectedIconOffset = new Vector2(-15f, 0f); // 選択されたアイテムアイコンのオフセット位置
    private Vector2 itemIconDefaultPosition; // アイテムアイコンのデフォルト位置
    private Tween selectionTween;
    private Tween iconPositionTween;

    private void Awake()
    {
        // ボタンのクリックイベントを登録
        GetComponent<Button>()
            .onClick.AddListener(SelectItem);

        if (backgroundToAnimate == null)
        {
            Debug.LogError("背景のRectTransformが正しく設定されていません。");
        }
        else
        {
            backgroundImage = backgroundToAnimate.GetComponent<Image>();
            if (backgroundImage != null)
            {
                originalBackgroundColor = backgroundImage.color;
            }
        }

        if (itemIconRectTransform == null)
        {
            Debug.LogError("アイテムアイコンのRectTransformが正しく設定されていません。");
        }
        else
        {
            itemIconDefaultPosition = itemIconRectTransform.anchoredPosition; // アイテムアイコンのデフォルト位置を保存
        }

        if (itemIconImage == null || itemNameText == null || itemPriceText == null)
        {
            Debug.LogWarning("PurchaseSelectButtonのUIコンポーネントが正しく設定されていません。");
            return;
        }

        // アイテム画像のベースサイズを取得
        RectTransform rectTransform = itemIconImage.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            baseSize = rectTransform.sizeDelta.x; // 横幅をベースサイズとして使用
        }
        else
        {
            Debug.LogWarning("アイテム画像のRectTransformが取得できませんでした。");
        }
    }

    private void Start()
    {
        if (playerManager == null)
        {
            playerManager = PlayerManager.instance;
            if (playerManager == null)
            {
                Debug.LogError("PlayerManagerが見つかりません。");
                return;
            }
        }

        if (shopUIManager == null)
        {
            shopUIManager = ShopUIManager.instance;
            if (shopUIManager == null)
            {
                Debug.LogError("ShopUIManagerが見つかりません。");
                return;
            }
        }

        if (itemDataManager == null)
        {
            itemDataManager = ItemDataManager.instance;
            if (itemDataManager == null)
            {
                Debug.LogError("ItemDataManagerが見つかりません。");
                return;
            }
        }

        if (gameManager == null)
        {
            gameManager = GameManager.instance;
            if (gameManager == null)
            {
                Debug.LogError("GameManagerが見つかりません。");
                return;
            }
        }
    }

    private void OnEnable()
    {
        // GameManagerのイベントを購読
        if (GameManager.instance != null)
        {
            GameManager.instance.OnAnyItemRemovedFromInventory += HandleItemAmountChanged;
        }

        if (backgroundToAnimate != null)
        {
            backgroundImage.color = originalBackgroundColor; // 背景色を元に戻す
        }
    }

    //購入選択ボタンを初期化するメソッド
    public void InitializePurchaseSelectButton(BaseItemData baseItemData)
    {
        if (baseItemData == null)
        {
            Debug.LogWarning("BaseItemDataがnullです。");
            return;
        }

        if (BaseItemManager.instance == null)
        {
            Debug.LogError("BaseItemManagerが初期化されていません。");
            return;
        }

        // アイテムのアイコン画像を設定
        UIUtility.SetSpriteFitToSquare(itemIconImage, baseItemData.itemSprite, baseSize);

        // アイテムの名前を設定
        itemNameText.text = baseItemData.itemName;

        // アイテムの価格を取得
        itemPrice = baseItemData.buyPrice;
        //アイテムの価格を表示
        itemPriceText.text = itemPrice.ToString();

        // アイテムデータを保存
        this.baseItemData = baseItemData;

        UpdateVisualsBasedOnStock(); // 在庫数に応じて見た目を更新
    }

    //売却選択ボタンを初期化するメソッド
    private void InitializeSellSelectButton()
    {
        // itemDataManagerが未初期化の場合に備えて、ここで取得を試みる
        if (itemDataManager == null)
        {
            itemDataManager = ItemDataManager.instance;
            if (itemDataManager == null)
            {
                Debug.LogError("ItemDataManagerのインスタンスが見つかりません。");
                return; // 処理を中断
            }
        }

        if (assignedItemID == null)
            {
                Debug.LogWarning("AssignedItemIDがnullです。");
                return;
            }

        baseItemData = null; //購入時に必要なアイテムデータを初期化
        //アイテムIDからアイテムアイコンを取得
        Sprite itemSprite = itemDataManager.GetItemSpriteByID(assignedItemID);
        //アイテムIDから名前を取得
        string itemName = itemDataManager.GetItemNameByID(assignedItemID);
        //アイテムIDから売却価格を取得
        itemPrice = itemDataManager.GetItemSellPriceByID(assignedItemID);

        // アイテムのアイコン画像を設定
        UIUtility.SetSpriteFitToSquare(itemIconImage, itemSprite, baseSize);
        // アイテムの名前を設定
        itemNameText.text = itemName;
        //アイテムの価格を表示
        itemPriceText.text = itemPrice.ToString();
    }

    private void SelectItem()
    {
        //ShopUIManagerの状態を確認
        ShopUIManager.ShopStatus shopStatus = shopUIManager.shopStatus;

        if (shopStatus == ShopUIManager.ShopStatus.Buy) //ショップが購入状態のとき
        {
            //プレイヤーの所持金を取得
            int PlayerMoney = playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerMoney);

            if (PlayerMoney < itemPrice)
            {
                //所持金が足りない場合は、選べないようにする
                SEManager.instance?.PlayUISE(SE_UI.Beep1);
                return;
            }
        }
        else if (shopStatus == ShopUIManager.ShopStatus.Sell) //ショップが売却状態のとき
        {
            int itemAmount = gameManager.GetAllTypeIDToAmount(assignedItemID);
            if (itemAmount <= 0)
            {
                //売却するアイテムがない場合は、選べないようにする
                SEManager.instance?.PlayUISE(SE_UI.Beep1);
                return;
            }
        }
        else
        {
            Debug.LogWarning("ShopUIManagerの状態が不正です。");
            return;
        }

        // 購入確認パネルを表示
        if (shopUIManager != null)
        {
            //自分のUI上での座標を取得
            RectTransform rect = this.gameObject.GetComponent<RectTransform>();
            Vector2 mypos = rect.anchoredPosition;

            BaseItemManager baseItemManager = BaseItemManager.instance;
            if (baseItemManager == null)
            {
                Debug.LogError("BaseItemManagerが見つかりません。");
                return;
            }

            Enum itemID = null; // アイテムのIDを初期化
            if (shopStatus == ShopUIManager.ShopStatus.Buy)
            {
                // アイテムのIDを取得
                itemID = baseItemManager.GetItemIDFromData(baseItemData);
                if (itemID == null)
                {
                    Debug.LogWarning("アイテムのIDが取得できませんでした。");
                    return;
                }
            }
            else if (shopStatus == ShopUIManager.ShopStatus.Sell)
            {
                // 売却時は選択されているアイテムIDを使用
                itemID = assignedItemID;
            }
            else
            {
                Debug.LogWarning("ShopUIManagerの状態が不正です。");
                return;
            }

            // 購入確認パネルを表示
            // 購入確認パネルの位置を設定
            shopUIManager.SetPromptPanel(itemID, itemPrice, mypos);
        }
        else
        {
            Debug.LogWarning("ShopUIManagerが存在しません");
        }
    }

    // オブジェクトが非アクティブになった時にアニメーションを確実に停止させる
    private void OnDisable()
    {
        // 念のため、Tweenをキルしてスケールを元に戻す
        selectionTween?.Kill();
        iconPositionTween?.Kill();
        if (backgroundToAnimate != null)
        {
            backgroundToAnimate.localScale = Vector3.one;
        }
        if (itemIconRectTransform != null && itemIconDefaultPosition != null)
        {
            itemIconRectTransform.anchoredPosition = itemIconDefaultPosition; // アイテムアイコンの位置をデフォルトに戻す
        }

        // GameManagerのイベント購読を解除（メモリリーク防止）
        if (GameManager.instance != null)
        {
            GameManager.instance.OnAnyItemRemovedFromInventory -= HandleItemAmountChanged;
        }
    }

    // このボタンが選択された時に呼び出されるメソッド
    public void OnSelect(BaseEventData eventData)
    {
        // 既存のアニメーションがあれば停止
        selectionTween?.Kill();
        iconPositionTween?.Kill();
        // 拡大・縮小を繰り返すアニメーションを開始
        if (backgroundToAnimate != null)
        {
            selectionTween = backgroundToAnimate
                .DOScale(1.04f, 0.8f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }
        // アイテムアイコンの位置を選択された位置に移動
        if (itemIconRectTransform != null)
        {
            iconPositionTween = itemIconRectTransform
                .DOAnchorPos(itemIconDefaultPosition + selectedIconOffset, 0.8f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }
    }

    // このボタンの選択が外れた時に呼び出されるメソッド
    public void OnDeselect(BaseEventData eventData)
    {
        // 実行中のアニメーションを停止
        selectionTween?.Kill();
        iconPositionTween?.Kill();
        // スケールを元のサイズに滑らかに戻す
        if (backgroundToAnimate != null)
        {
            selectionTween = backgroundToAnimate.DOScale(1f, 0.1f).SetUpdate(true);
        }
        // アイテムアイコンの位置をデフォルト位置に戻す
        if (itemIconRectTransform != null && itemIconDefaultPosition != null)
        {
            itemIconRectTransform.anchoredPosition = itemIconDefaultPosition;
        }
    }

    /// <summary>
    /// アイテム数が変更されたときにGameManagerから呼ばれるイベントハンドラ
    /// </summary>
    private void HandleItemAmountChanged(Enum changedItemID)
    {
        // 変更されたアイテムが、このボタンが担当するアイテムと同じかチェック
        if (object.Equals(assignedItemID, changedItemID))
        {
            // 同じなら、見た目を更新
            UpdateVisualsBasedOnStock();
        }
    }

    /// <summary>
    /// アイテムの在庫数に基づいて背景色を更新する
    /// </summary>
    private void UpdateVisualsBasedOnStock()
    {
        if (assignedItemID == null || backgroundImage == null)
            return;

        // 最新のアイテム数を取得
        int amount = gameManager.GetAllTypeIDToAmount(assignedItemID);

        if (amount <= 0)
        {
            // アイテム数が0以下なら背景を黒くする
            float h,
                s,
                v;
            Color.RGBToHSV(originalBackgroundColor, out h, out s, out v);
            backgroundImage.color = Color.HSVToRGB(h, s, 0.2f); // V(明度)を0.2にする
        }
        else
        {
            // アイテム数が1以上なら元の色に戻す
            backgroundImage.color = originalBackgroundColor;
        }
    }
}

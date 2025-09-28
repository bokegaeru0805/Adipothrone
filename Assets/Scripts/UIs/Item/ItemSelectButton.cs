using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemSelectButton : MonoBehaviour, IItemAssignable
{
    [HideInInspector]
    public bool isEquippedWeaponButton = false;

    [HideInInspector]
    public Enum AssignedItemID => assignedItemID; //選択されているアイテムのID
    private Enum assignedItemID; // 実際のEnum型

    public void AssignItem(Enum itemID)
    {
        assignedItemID = itemID;
        UpdateItemIcon(); // アイテムのアイコンを更新
    }

    [Header("アイテム選択ボタンのUIコンポーネント")]
    [SerializeField]
    private Image IconImage; //アイテム選択ボタンのImageコンポーネント

    [SerializeField]
    private TextMeshProUGUI ItemAmount_text; //アイテム選択ボタンの所持数を表示するTextMeshProUGUIコンポーネント

    [Header("アイテム選択ボタンの親パネル")]
    [SerializeField]
    private GameObject ItemPanel; //アイテム選択ボタンのパネル
    private int itemAmount = 0; //アイテムの現在の個数
    private int preItemAmount = 0; //前フレームでのアイテムの個数
    private float baseSize = 0; // ボタンのアイテム画像のベースサイズ（初期化時に設定）
    private ItemType itemType;

    [Header("アイテムデータベース")]
    [SerializeField]
    private HealItemDatabase healItemDatabase;

    private enum ItemType
    {
        HealItem = 8,
    }

    private void Awake()
    {
        if (IconImage == null && ItemAmount_text == null)
        {
            Debug.LogError("アイテム選択ボタンのコンポーネントが設定されていません。");
            return;
        }

        if (ItemPanel == null)
        {
            Debug.LogError("アイテム選択ボタンのパネルが設定されていません。");
            return;
        }

        if (healItemDatabase == null)
        {
            Debug.LogError("HealItemDatabaseが設定されていません。");
            return;
        }

        // アイテム画像のベースサイズを取得
        RectTransform rectTransform = IconImage.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            baseSize = rectTransform.sizeDelta.x; // 横幅をベースサイズとして使用
        }
        else
        {
            Debug.LogWarning("アイテム画像のRectTransformが取得できませんでした。");
        }

        // ボタンのクリックイベントを登録
        GetComponent<Button>()
            .onClick.AddListener(SelectItem);
    }

    private void Update()
    {
        itemAmount = GameManager.instance.savedata.ItemInventoryData.GetItemAmount(assignedItemID); //アイテムの所持数を取得
        if (itemAmount != preItemAmount)
        {
            string _text = $"<color=#FFD700>{itemAmount}</color>"; //所持数の字体を取得
            ItemAmount_text.text = _text; //所持数の表記を変更
        }
        preItemAmount = itemAmount; //アイテムの所持数を合わせる

        if (itemAmount <= 0 && IconImage != null)
        {
            Color originalColor = IconImage.color;
            Color.RGBToHSV(originalColor, out float h, out float s, out float v); // RGB → HSV に変換
            float clampedV = Mathf.Clamp01(20 / 255f); // V を新しい値に設定(安全のため [0,1] に制限)
            Color newColor = Color.HSVToRGB(h, s, clampedV); //HSV → RGB に変換
            newColor.a = originalColor.a; // alpha値は元のまま保つ
            IconImage.color = newColor;
        }
        else if (itemAmount > 0 && IconImage != null)
        {
            Color originalColor = IconImage.color;
            Color.RGBToHSV(originalColor, out float h, out float s, out float v); // RGB → HSV に変換
            float clampedV = Mathf.Clamp01(255 / 255f); // V を新しい値に設定(安全のため [0,1] に制限)
            Color newColor = Color.HSVToRGB(h, s, clampedV); //HSV → RGB に変換
            newColor.a = originalColor.a; // alpha値は元のまま保つ
            IconImage.color = newColor;
        }
    }

    private void OnEnable()
    {
        UpdateItemIcon(); // アイテムのアイコンを更新
    }

    /// <summary>
    /// アイテムのアイコンを更新します。
    /// </summary>
    private void UpdateItemIcon()
    {
        Sprite itemSprite = null;

        if (assignedItemID == null)
        {
            return;
        }

        // アイテムタイプを識別
        switch (EnumIDUtility.ExtractTypeID(EnumIDUtility.ToID(assignedItemID)))
        {
            case (int)TypeID.HealItem:
                itemType = ItemType.HealItem;
                break;
        }

        // アイテムタイプに応じた処理
        switch (itemType)
        {
            case ItemType.HealItem:
                HealItemData item = healItemDatabase.GetItemByID(assignedItemID);
                if (item == null)
                {
                    Debug.LogWarning("該当するIDのアイテムが見つかりませんでした。");
                    return;
                }
                itemSprite = item.itemSprite;
                break;
        }

        // スプライト設定
        if (IconImage != null)
        {
            UIUtility.SetSpriteFitToSquare(IconImage, itemSprite, baseSize);
        }
        else
        {
            Debug.LogWarning("アイテム選択ボタンがImageコンポーネントを持っていません");
        }
    }

    private void OnDisable()
    {
        assignedItemID = null; //itemIDを初期化
    }

    private void SelectItem()
    {
        PanelActive panelActive = ItemPanel.GetComponent<PanelActive>();
        if (panelActive != null)
        {
            panelActive.SetLastSelectedButton(this.gameObject);
        }

        if (itemAmount <= 0)
        {
            //アイテムの所持数が0以下の時は、選べないようにする
            SEManager.instance?.PlayUISE(SE_UI.Beep1);
            return;
        }

        if (ItemPanel != null)
        {
            var script = ItemPanel.GetComponent<ItemPanelActive>();
            if (script != null)
            {
                script.SetPromptPanel(assignedItemID, this.GetComponent<Button>());
            }
            else
            {
                Debug.LogWarning("ItemPanelActiveコンポーネントが付いていません");
            }
        }
        else
        {
            Debug.LogWarning("ItemPanelが存在しません");
        }
    }
}

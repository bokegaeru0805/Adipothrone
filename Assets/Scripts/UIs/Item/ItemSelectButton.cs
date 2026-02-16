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
        // 強制的に更新がかかるように前回値をリセット
        // これがないと、所持数が0個のときに「0 != 0」で更新がスキップされ、デフォルトの値のままになる
        preItemAmount = -1;

        assignedItemID = itemID;
        UpdateItemIcon(); // アイテムのアイコンを更新
        UpdateItemCount(); // アイテムの所持数を更新
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

    private enum ItemType
    {
        HealItem = 8,
        KeyItem = 12,
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

        if(ItemAmount_text == null)
        {
            Debug.LogWarning("アイテム選択ボタンの所持数表示テキストが設定されていません。所持数は表示されません。");
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

    private void OnEnable()
    {
        // アイテム数変更イベントに登録
        if (GameManager.instance != null)
        {
            GameManager.instance.savedata.ItemInventoryData.OnItemCountChanged += UpdateItemCount;
        }
    }

    private void OnDisable()
    {
        assignedItemID = null; //itemIDを初期化

        // オブジェクト破棄時にイベント解除
        if (GameManager.instance != null)
        {
            GameManager.instance.savedata.ItemInventoryData.OnItemCountChanged -= UpdateItemCount;
        }
    }

    /// <summary>
    /// イベントから呼ばれる、または初期化時に呼ぶ更新処理
    /// </summary>
    private void UpdateItemCount()
    {
        if (assignedItemID == null)
            return;

        // 所持数を取得
        itemAmount = GameManager.instance.savedata.ItemInventoryData.GetItemAmount(assignedItemID);

        // UI更新（所持数が変わった場合のみテキスト更新）
        if (itemAmount != preItemAmount)
        {
            string _text = $"<color=#FFD700>{itemAmount}</color>";
            if (ItemAmount_text != null)
            {
                ItemAmount_text.text = _text;
            }
        }
        preItemAmount = itemAmount;

        // 色の更新ロジック
        UpdateIconColor();
    }

    /// <summary>
    /// アイテムの所持数に応じてアイコンの色を更新します。
    /// </summary>
    private void UpdateIconColor()
    {
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

    /// <summary>
    /// アイテムのアイコンを更新します。
    /// </summary>
    private void UpdateItemIcon()
    {
        Sprite itemSprite = null;

        if (assignedItemID == null)
            return;
        if (GameManager.instance == null)
            return;

        // GameManagerからデータベースへのショートカット参照
        var healDB = GameManager.instance.healItemDatabase;
        var keyDB = GameManager.instance.keyItemDatabase;

        // アイテムタイプを識別
        ItemType itemType = default;
        switch (EnumIDUtility.ExtractTypeID(EnumIDUtility.ToID(assignedItemID)))
        {
            case (int)TypeID.HealItem:
                itemType = ItemType.HealItem;
                break;
            case (int)TypeID.KeyItem:
                itemType = ItemType.KeyItem;
                break;
            default:
                Debug.LogError($"このID{assignedItemID}はアイテムタイプを識別できません");
                return;
        }

        // アイテムタイプに応じた処理
        switch (itemType)
        {
            case ItemType.HealItem:
                HealItemData item = healDB.GetItemByID(assignedItemID);
                if (item == null)
                {
                    Debug.LogError("該当するIDのアイテムが見つかりませんでした。");
                    return;
                }
                itemSprite = item.itemSprite;
                break;
            case ItemType.KeyItem:
                KeyItemData keyItem = keyDB.GetItemByID(assignedItemID);
                if (keyItem == null)
                {
                    Debug.LogError("該当するIDのキーアイテムが見つかりませんでした。");
                    return;
                }
                itemSprite = keyItem.itemSprite;
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
            var script = ItemPanel.GetComponent<HealItemPanelActive>();
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

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

    private int itemAmount = 0; //アイテムの現在の個数
    private int preItemAmount = 0; //前フレームでのアイテムの個数
    private float baseSize = 0; // ボタンのアイテム画像のベースサイズ（初期化時に設定）

    private MonoBehaviour currentActivePanel; // 現在アクティブな親パネルを記憶する

    /// <summary>
    /// パネル側から「現在アクティブなのは自分だ」と登録を受け付けるメソッド
    /// </summary>
    public void RegisterActivePanel(MonoBehaviour panel)
    {
        currentActivePanel = panel;
    }

    private void Awake()
    {
        if (IconImage == null && ItemAmount_text == null)
        {
            Debug.LogError("アイテム選択ボタンのコンポーネントが設定されていません。");
            return;
        }

        if (ItemAmount_text == null)
        {
            Debug.LogWarning(
                "アイテム選択ボタンの所持数表示テキストが設定されていません。所持数は表示されません。"
            );
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
            GameManager.instance.OnInventoryUpdated += UpdateItemCount;
        }
    }

    private void OnDisable()
    {
        assignedItemID = null; //itemIDを初期化

        // オブジェクト破棄時にイベント解除
        if (GameManager.instance != null)
        {
            GameManager.instance.OnInventoryUpdated -= UpdateItemCount;
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
    /// ItemDataManagerを利用してアイテム種別に関わらず取得します。
    /// </summary>
    private void UpdateItemIcon()
    {
        if (assignedItemID == null || GameManager.instance == null)
            return;

        // データベースからアイテムデータを一括取得
        var itemData = ItemDataManager.instance.GetBaseItemDataByID(assignedItemID);
        if (itemData == null)
        {
            Debug.LogError($"該当するID({assignedItemID})のアイテムデータが見つかりませんでした。");
            return;
        }

        // スプライト設定
        if (IconImage != null)
        {
            UIUtility.SetSpriteFitToSquare(IconImage, itemData.itemSprite, baseSize);
        }
        else
        {
            Debug.LogWarning("アイテム選択ボタンがImageコンポーネントを持っていません");
        }
    }

    private void SelectItem()
    {
        // 1. パネルの最後の選択状態を保存する
        // IPanelActive(インターフェース)ではなく、PanelActive(具象クラス)かどうかを判定する
        if (currentActivePanel is PanelActive panelActive)
        {
            panelActive.SetLastSelectedButton(this.gameObject);
        }
        // ※ HealItemPanelActive などの新しいタブパネルは自身の OnDisable 等で
        // 自動的に記憶するため、ここでは何もしなくてOKです。

        if (itemAmount <= 0)
        {
            //アイテムの所持数が0以下の時は、選べないようにする
            SEManager.instance?.PlayUISE(SE_UI.Beep1);
            return;
        }

        // 2. アイテム使用プロンプトを表示する
        if (currentActivePanel is IItemPromptHandler promptHandler)
        {
            // パネルがプロンプト対応なら開く
            promptHandler.SetPromptPanel(assignedItemID, this.GetComponent<Button>());

        }
        else
        {
            // KeyItemPanel など、プロンプトを持たないパネルの場合は何もしない
        }
    }
}

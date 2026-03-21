using System;
using System.Collections.Generic;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// アイテムの詳細情報（回復量、バフ効果、ステータス強化量など）をUIに表示するパネルの制御クラス
/// </summary>
public class ItemDetailPanel : MonoBehaviour
{
    #region 各種パネル・ベースUIの参照

    [BoxGroup("各アイテムタイプの専用パネル")]
    [SerializeField, Required("回復アイテム用パネルをアタッチしてください")]
    [Tooltip("回復アイテム選択時に表示される専用パネル")]
    private GameObject healItemPanel = null;

    [BoxGroup("各アイテムタイプの専用パネル")]
    [SerializeField, Required("ステータス強化アイテム用パネルをアタッチしてください")]
    [Tooltip("ステータス強化アイテム選択時に表示される専用パネル")]
    private GameObject statusEnhancePanel = null;

    [BoxGroup("各アイテムタイプの専用パネル")]
    [SerializeField, Required("キーアイテム用パネルをアタッチしてください")]
    [Tooltip("キーアイテム選択時に表示される専用パネル")]
    private GameObject keyItemPanel = null;

    [BoxGroup("共通UI設定")]
    [SerializeField, Required("アイテムのアイコンを表示するImageをアタッチしてください")]
    [Tooltip("アイテムのアイコンを表示するImageコンポーネント")]
    private Image ItemDetailPanel_image = null;

    [BoxGroup("共通UI設定")]
    [SerializeField, Required("アイテムの名前を表示するTextをアタッチしてください")]
    [Tooltip("アイテムの名前を表示するTextMeshProコンポーネント")]
    private TextMeshProUGUI ItemDetailPanel_txt = null;

    #endregion

    #region 回復アイテム用UIの参照

    [Foldout("回復アイテム (HealItem) 用UI")]
    [SerializeField]
    [Tooltip("HP回復量のプレビューを表示するバーの親オブジェクト")]
    private GameObject playerHPBar;

    [Foldout("回復アイテム (HealItem) 用UI")]
    [SerializeField]
    [Tooltip("HP回復量のプレビューを表示するImage（Fill用）")]
    private Image playerHPHealthBarImage;

    [Foldout("回復アイテム (HealItem) 用UI")]
    [SerializeField]
    [Tooltip("HP回復量の数値を表示するテキスト")]
    private TextMeshProUGUI playerHPText;

    [Foldout("回復アイテム (HealItem) 用UI")]
    [SerializeField]
    [Tooltip("WP回復量のプレビューを表示するバーの親オブジェクト")]
    private GameObject playerWPBar;

    [Foldout("回復アイテム (HealItem) 用UI")]
    [SerializeField]
    [Tooltip("WP回復量のプレビューを表示するImage（Fill用）")]
    private Image playerWPHealthBarImage;

    [Foldout("回復アイテム (HealItem) 用UI")]
    [SerializeField]
    [Tooltip("WP回復量の数値を表示するテキスト")]
    private TextMeshProUGUI playerWPText;

    [Foldout("回復アイテム (HealItem) 用UI")]
    [SerializeField]
    [Tooltip("回復アイテムに付随するバフ効果のアイコンやバーのリスト")]
    private List<BuffEffectUI> buffEffectUIList;

    [System.Serializable]
    public class BuffEffectUI
    {
        [Tooltip("バフのアイコンオブジェクト")]
        public GameObject icon;

        [Tooltip("バフ持続時間のバーの親オブジェクト")]
        public GameObject barObject;

        [Tooltip("バフ持続時間のバーのFill用Image")]
        public Image barFillImage;
    }

    [Foldout("回復アイテム (HealItem) 用UI")]
    [SerializeField]
    [Tooltip("1つ目のバフ効果の名前を表示するテキスト")]
    private TextMeshProUGUI buff1NameText;

    [Foldout("回復アイテム (HealItem) 用UI")]
    [SerializeField]
    [Tooltip("2つ目のバフ効果の名前を表示するテキスト")]
    private TextMeshProUGUI buff2NameText;

    [Foldout("回復アイテム (HealItem) 用UI")]
    [SerializeField]
    [Tooltip("1つ目のバフ効果の数値を表示するテキスト")]
    private TextMeshProUGUI buff1ValueText;

    [Foldout("回復アイテム (HealItem) 用UI")]
    [SerializeField]
    [Tooltip("2つ目のバフ効果の数値を表示するテキスト")]
    private TextMeshProUGUI buff2ValueText;

    #endregion

    #region ステータス強化アイテム用UIの参照

    [System.Serializable]
    public class StatusIconMapping
    {
        [Tooltip("対象のステータスタイプ")]
        public EnhanceTargetStatus status;

        [Tooltip("ステータスに対応するアイコンスプライト")]
        public Sprite icon;
    }

    [Foldout("ステータス強化アイテム (StatusEnhance) 用UI")]
    [SerializeField]
    [Tooltip("ステータスとアイコンの紐付け設定リスト")]
    private List<StatusIconMapping> statusIconMappings;

    [Foldout("ステータス強化アイテム (StatusEnhance) 用UI")]
    [SerializeField]
    [Tooltip("強化効果1のステータスアイコンを表示するImage")]
    private Image enhance1Icon;

    [Foldout("ステータス強化アイテム (StatusEnhance) 用UI")]
    [SerializeField]
    [Tooltip("強化効果1のステータス名を表示するテキスト")]
    private TextMeshProUGUI enhance1NameText;

    [Foldout("ステータス強化アイテム (StatusEnhance) 用UI")]
    [SerializeField]
    [Tooltip("強化効果1の上昇値を表示するテキスト")]
    private TextMeshProUGUI enhance1ValueText;

    [Foldout("ステータス強化アイテム (StatusEnhance) 用UI")]
    [SerializeField]
    [Tooltip("強化効果2のステータスアイコンを表示するImage")]
    private Image enhance2Icon;

    [Foldout("ステータス強化アイテム (StatusEnhance) 用UI")]
    [SerializeField]
    [Tooltip("強化効果2のステータス名を表示するテキスト")]
    private TextMeshProUGUI enhance2NameText;

    [Foldout("ステータス強化アイテム (StatusEnhance) 用UI")]
    [SerializeField]
    [Tooltip("強化効果2の上昇値を表示するテキスト")]
    private TextMeshProUGUI enhance2ValueText;

    [Foldout("ステータス強化アイテム (StatusEnhance) 用UI")]
    [SerializeField]
    [Tooltip("強化効果3のステータスアイコンを表示するImage")]
    private Image enhance3Icon;

    [Foldout("ステータス強化アイテム (StatusEnhance) 用UI")]
    [SerializeField]
    [Tooltip("強化効果3のステータス名を表示するテキスト")]
    private TextMeshProUGUI enhance3NameText;

    [Foldout("ステータス強化アイテム (StatusEnhance) 用UI")]
    [SerializeField]
    [Tooltip("強化効果3の上昇値を表示するテキスト")]
    private TextMeshProUGUI enhance3ValueText;

    [Foldout("ステータス強化アイテム (StatusEnhance) 用UI")]
    [SerializeField]
    [Tooltip("強化効果4のステータスアイコンを表示するImage")]
    private Image enhance4Icon;

    [Foldout("ステータス強化アイテム (StatusEnhance) 用UI")]
    [SerializeField]
    [Tooltip("強化効果4のステータス名を表示するテキスト")]
    private TextMeshProUGUI enhance4NameText;

    [Foldout("ステータス強化アイテム (StatusEnhance) 用UI")]
    [SerializeField]
    [Tooltip("強化効果4の上昇値を表示するテキスト")]
    private TextMeshProUGUI enhance4ValueText;
    #endregion
    #region 内部状態変数

    private Dictionary<GameObject, (GameObject, Image)> buffUIs;
    private float baseSize = 0; // ボタンのアイテム画像のベースサイズ（初期化時に設定）
    #endregion

    #region Unity ライフサイクル

    private void Awake()
    {
        // [Required]属性によりインスペクター上で未アタッチエラーが視覚化されるため、
        // 冗長な Debug.LogWarning による手動のnullチェックは削減しています。

        // バフUIのリストを高速アクセス用の Dictionary に変換
        buffUIs = new Dictionary<GameObject, (GameObject, Image)>();
        if (buffEffectUIList != null)
        {
            foreach (var ui in buffEffectUIList)
            {
                if (ui.icon != null)
                {
                    buffUIs[ui.icon] = (ui.barObject, ui.barFillImage);
                }
            }
        }

        // アイテム画像のベースサイズ（横幅）を取得し保持する
        if (ItemDetailPanel_image != null)
        {
            RectTransform rectTransform = ItemDetailPanel_image.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                baseSize = rectTransform.sizeDelta.x;
            }
            else
            {
                Debug.LogWarning("アイテム画像にRectTransformが見つかりませんでした。");
            }
        }
    }

    #endregion

    #region 公開メソッド

    /// <summary>
    /// アイテムの詳細情報をUIに反映し、対応するパネルを表示します。
    /// </summary>
    /// <param name="itemID">表示したいアイテムのID (Enum)</param>
    public void DisplayItemDetails(Enum itemID)
    {
        var itemData = ItemDataManager.instance.GetBaseItemDataByID(itemID);
        if (itemData == null)
            return;

        UpdateCommonUI(itemData);
        HideAllSubPanels();

        // アイテムのタイプに応じて専用の処理とパネルの有効化を行う
        if (itemData is HealItemData healItem)
        {
            UpdateHealItemUI(healItem, EnumIDUtility.ToID(itemID));
        }
        else if (itemData is StatusEnhanceItemData statusEnhanceItem)
        {
            UpdateStatusEnhanceItemUI(statusEnhanceItem);
        }
        else if (itemData is KeyItemData keyItem)
        {
            UpdateKeyItemUI(keyItem);
        }
    }

    #endregion

    #region 内部処理メソッド（UI更新）

    /// <summary>
    /// 全アイテムタイプに共通するUI（アイコン画像、アイテム名）を更新します。
    /// </summary>
    private void UpdateCommonUI(BaseItemData itemData)
    {
        if (ItemDetailPanel_image != null && itemData.itemSprite != null)
        {
            UIUtility.SetSpriteFitToSquare(ItemDetailPanel_image, itemData.itemSprite, baseSize);
        }

        if (ItemDetailPanel_txt != null)
        {
            ItemDetailPanel_txt.text = $"<color=#FFD700>{itemData.itemName}</color>";
        }
    }

    /// <summary>
    /// 表示を切り替える前に、一旦すべてのサブパネルを非表示にします。
    /// </summary>
    private void HideAllSubPanels()
    {
        if (healItemPanel != null)
            healItemPanel.SetActive(false);
        if (keyItemPanel != null)
            keyItemPanel.SetActive(false);
        if (statusEnhancePanel != null)
            statusEnhancePanel.SetActive(false);
    }

    /// <summary>
    /// 回復アイテム用のUI情報を更新し、パネルを表示します。
    /// </summary>
    private void UpdateHealItemUI(HealItemData healItem, int itemIDNumber)
    {
        if (healItemPanel != null)
            healItemPanel.SetActive(true);

        // 回復量の表示
        if (playerHPText != null)
            playerHPText.text = healItem.hpHealAmount.ToString();
        if (playerWPText != null)
            playerWPText.text = healItem.wpHealAmount.ToString();

        // バフ効果の表示（最大2つまで）
        if (healItem.buffEffects != null && healItem.buffEffects.Count > 0)
        {
            // 1つ目のバフ
            string effect1Name = StatusEffectUtility.GetDisplayName(
                healItem.buffEffects[0].effectType,
                healItem.buffEffects[0].effectrank
            );
            if (buff1NameText != null)
                buff1NameText.text = $"<color=#C6A34C>{effect1Name}</color>";
            if (buff1ValueText != null)
                buff1ValueText.text = healItem.buffEffects[0].multiplier.ToString();

            // 2つ目のバフ
            if (healItem.buffEffects.Count > 1)
            {
                string effect2Name = StatusEffectUtility.GetDisplayName(
                    healItem.buffEffects[1].effectType,
                    healItem.buffEffects[1].effectrank
                );
                if (buff2NameText != null)
                    buff2NameText.text = $"<color=#C6A34C>{effect2Name}</color>";
                if (buff2ValueText != null)
                    buff2ValueText.text = healItem.buffEffects[1].multiplier.ToString();
            }
            else
            {
                ClearBuff2Text();
            }
        }
        else
        {
            ClearBuff1Text();
            ClearBuff2Text();
        }

        // UIの非表示化やプレビュー機能の呼び出し
        if (HealItemPreviewUIManager.instance != null)
        {
            HealItemPreviewUIManager.instance.DisplaySelectedItemEffects(
                itemIDNumber,
                playerHPBar,
                playerHPHealthBarImage,
                playerWPBar,
                playerWPHealthBarImage,
                buffUIs
            );
        }
        else
        {
            Debug.LogError("HealItemPreviewUIManagerが見つかりませんでした。");
        }
    }

    /// <summary>
    /// ステータス強化アイテム用のUI情報を更新し、パネルを表示します。
    /// </summary>
    private void UpdateStatusEnhanceItemUI(StatusEnhanceItemData statusEnhanceItem)
    {
        if (statusEnhancePanel != null)
            statusEnhancePanel.SetActive(true);

        // 以前の表示をクリア
        ClearEnhanceTexts();

        // 強化効果の表示
        var effects = statusEnhanceItem.enhanceEffects;
        if (effects != null)
        {
            if (effects.Count > 0 && enhance1NameText != null && enhance1ValueText != null)
            {
                string jpName = GetStatusNameInJapanese(effects[0].targetStatus);
                enhance1NameText.text = $"<color=#C6A34C>{jpName}</color>";
                enhance1ValueText.text = $"ステータスレベル + {effects[0].amount}";

                if (enhance1Icon != null)
                {
                    enhance1Icon.sprite = GetStatusIcon(effects[0].targetStatus);
                    enhance1Icon.gameObject.SetActive(enhance1Icon.sprite != null);
                }
            }

            if (effects.Count > 1 && enhance2NameText != null && enhance2ValueText != null)
            {
                string jpName = GetStatusNameInJapanese(effects[1].targetStatus);
                enhance2NameText.text = $"<color=#C6A34C>{jpName}</color>";
                enhance2ValueText.text = $"ステータスレベル + {effects[1].amount}";

                if (enhance2Icon != null)
                {
                    enhance2Icon.sprite = GetStatusIcon(effects[1].targetStatus);
                    enhance2Icon.gameObject.SetActive(enhance2Icon.sprite != null);
                }
            }

            if (effects.Count > 2 && enhance3NameText != null && enhance3ValueText != null)
            {
                string jpName = GetStatusNameInJapanese(effects[2].targetStatus);
                enhance3NameText.text = $"<color=#C6A34C>{jpName}</color>";
                enhance3ValueText.text = $"ステータスレベル + {effects[2].amount}";
                if (enhance3Icon != null)
                {
                    enhance3Icon.sprite = GetStatusIcon(effects[2].targetStatus);
                    enhance3Icon.gameObject.SetActive(enhance3Icon.sprite != null);
                }
            }

            if (effects.Count > 3 && enhance4NameText != null && enhance4ValueText != null)
            {
                string jpName = GetStatusNameInJapanese(effects[3].targetStatus);
                enhance4NameText.text = $"<color=#C6A34C>{jpName}</color>";
                enhance4ValueText.text = $"ステータスレベル + {effects[3].amount}";
                if (enhance4Icon != null)
                {
                    enhance4Icon.sprite = GetStatusIcon(effects[3].targetStatus);
                    enhance4Icon.gameObject.SetActive(enhance4Icon.sprite != null);
                }
            }
        }
    }

    /// <summary>
    /// キーアイテム用のUI情報を更新し、パネルを表示します。
    /// </summary>
    private void UpdateKeyItemUI(KeyItemData keyItem)
    {
        if (keyItemPanel != null)
            keyItemPanel.SetActive(true);

        // キーアイテム固有の表示処理が必要な場合はここに追記します
    }

    #endregion

    #region テキストクリア用ヘルパーメソッド

    private void ClearBuff1Text()
    {
        if (buff1NameText != null)
            buff1NameText.text = string.Empty;
        if (buff1ValueText != null)
            buff1ValueText.text = string.Empty;
    }

    private void ClearBuff2Text()
    {
        if (buff2NameText != null)
            buff2NameText.text = string.Empty;
        if (buff2ValueText != null)
            buff2ValueText.text = string.Empty;
    }

    private void ClearEnhanceTexts()
    {
        if (enhance1NameText != null)
            enhance1NameText.text = string.Empty;
        if (enhance1ValueText != null)
            enhance1ValueText.text = string.Empty;
        if (enhance1Icon != null)
            enhance1Icon.gameObject.SetActive(false);
        if (enhance2NameText != null)
            enhance2NameText.text = string.Empty;
        if (enhance2ValueText != null)
            enhance2ValueText.text = string.Empty;
        if (enhance2Icon != null)
            enhance2Icon.gameObject.SetActive(false);
        if (enhance3NameText != null)
            enhance3NameText.text = string.Empty;
        if (enhance3ValueText != null)
            enhance3ValueText.text = string.Empty;
        if (enhance3Icon != null)
            enhance3Icon.gameObject.SetActive(false);
        if (enhance4NameText != null)
            enhance4NameText.text = string.Empty;
        if (enhance4ValueText != null)
            enhance4ValueText.text = string.Empty;
        if (enhance4Icon != null)
            enhance4Icon.gameObject.SetActive(false);
    }

    #endregion

    #region 日本語化・アイコン取得用ヘルパーメソッド

    /// <summary>
    /// ステータスの種類を日本語名に変換します
    /// </summary>
    private string GetStatusNameInJapanese(EnhanceTargetStatus status)
    {
        switch (status)
        {
            // case EnhanceTargetStatus.HP: return "最大HP"; // HPが追加された場合用
            case EnhanceTargetStatus.Attack:
                return "基礎攻撃力";
            case EnhanceTargetStatus.Defense:
                return "防御力";
            case EnhanceTargetStatus.Speed:
                return "素早さ補正";
            case EnhanceTargetStatus.Luck:
                return "幸運補正";
            default:
                return status.ToString();
        }
    }

    /// <summary>
    /// ステータスに対応するアイコン（Sprite）を取得します
    /// </summary>
    private Sprite GetStatusIcon(EnhanceTargetStatus status)
    {
        if (statusIconMappings == null || statusIconMappings.Count == 0)
            return null;

        foreach (var mapping in statusIconMappings)
        {
            if (mapping.status == status)
            {
                return mapping.icon;
            }
        }
        return null;
    }

    #endregion
}

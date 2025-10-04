using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemDetailPanel : MonoBehaviour
{
    [Header("アイテム効果パネル")]
    [SerializeField]
    private Image ItemDetailPanel_image = null; //アイテム効果パネルのImage

    [SerializeField]
    private TextMeshProUGUI ItemDetailPanel_txt = null; //アイテム効果パネルの名前のText

    [Header("HPの回復量のUI")]
    [SerializeField]
    private GameObject playerHPBar;

    [SerializeField]
    private Image playerHPHealthBarImage;

    [SerializeField]
    private TextMeshProUGUI playerHPText;

    [Header("WPの回復量のUI")]
    [SerializeField]
    private GameObject playerWPBar;

    [SerializeField]
    private Image playerWPHealthBarImage;

    [SerializeField]
    private TextMeshProUGUI playerWPText;

    [Header("バフのアイコンとバーのUI")]
    [SerializeField]
    private List<BuffEffectUI> buffEffectUIList;

    [System.Serializable]
    public class BuffEffectUI
    {
        public GameObject icon;
        public GameObject barObject;
        public Image barFillImage;
    }

    private Dictionary<GameObject, (GameObject, Image)> buffUIs;

    [Header("バフの説明文のUI")]
    [SerializeField]
    private TextMeshProUGUI buff1NameText;

    [SerializeField]
    private TextMeshProUGUI buff2NameText;

    [Header("バフの効果値のUI")]
    [SerializeField]
    private TextMeshProUGUI buff1ValueText;

    [SerializeField]
    private TextMeshProUGUI buff2ValueText;
    private float baseSize = 0; // ボタンのアイテム画像のベースサイズ（初期化時に設定）

    private void Awake()
    {
        if (ItemDetailPanel_image == null)
        {
            Debug.LogWarning("アイテム詳細パネルのImageコンポーネントが設定されていません");
            return;
        }

        if (playerHPBar == null || playerHPHealthBarImage == null || playerHPText == null)
        {
            Debug.LogWarning("MenuアイテムのHPのUIコンポーネントが設定されていません");
            return;
        }

        if (playerWPBar == null || playerWPHealthBarImage == null || playerWPText == null)
        {
            Debug.LogWarning("MenuアイテムのWPのUIコンポーネントが設定されていません");
            return;
        }

        if (buffEffectUIList == null)
        {
            Debug.LogError("Menuアイテムのバフに関するUIが設定されていません");
            return;
        }

        if (buff1NameText == null || buff2NameText == null)
        {
            Debug.LogError("Menuアイテムのバフの名前のテキストコンポーネントが設定されていません");
            return;
        }

        if (buff1ValueText == null || buff2ValueText == null)
        {
            Debug.LogError(
                "Menuアイテムのバフの効果の値のテキストコンポーネントが設定されていません"
            );
            return;
        }

        // リストを Dictionary に変換
        buffUIs = new();
        foreach (var ui in buffEffectUIList)
        {
            buffUIs[ui.icon] = (ui.barObject, ui.barFillImage);
        }

        // アイテム画像のベースサイズを取得
        RectTransform rectTransform = ItemDetailPanel_image.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            baseSize = rectTransform.sizeDelta.x; // 横幅をベースサイズとして使用
        }
        else
        {
            Debug.LogWarning("アイテム画像のRectTransformが取得できませんでした。");
        }
    }

    /// <summary>
    /// アイテムの詳細を表示するメソッド
    /// </summary>
    public void DisplayItemDetails(Enum itemID)
    {
        var itemData = ItemDataManager.instance.GetBaseItemDataByID(itemID);

        if (itemData is HealItemData healItem)
        {
            //アイテムの画像を設定
            if (ItemDetailPanel_image != null)
            {
                // アイテムのアイコン画像を設定
                UIUtility.SetSpriteFitToSquare(
                    ItemDetailPanel_image,
                    healItem.itemSprite,
                    baseSize
                );
            }
            //アイテムの名前を設定(金色)
            ItemDetailPanel_txt.text = $"<color=#FFD700>{healItem.itemName}</color>";
            //アイテムのHP回復量の文章を表示
            playerHPText.text = healItem.hpHealAmount.ToString();
            //アイテムのWP回復量の文章を表示
            playerWPText.text = healItem.wpHealAmount.ToString();
            //アイテムのバフ効果の文章を表示
            if (healItem.buffEffects.Count > 0)
            {
                //バフの名前の表示を設定
                String effect1Name = StatusEffectUtility.GetDisplayName(
                    healItem.buffEffects[0].effectType,
                    healItem.buffEffects[0].effectrank
                );
                buff1NameText.text = $"<color=#C6A34C>{effect1Name}</color>";
                //バフの効果値の表示を設定
                buff1ValueText.text = healItem.buffEffects[0].multiplier.ToString();

                if (healItem.buffEffects.Count > 1)
                {
                    String effect2Name = StatusEffectUtility.GetDisplayName(
                        healItem.buffEffects[1].effectType,
                        healItem.buffEffects[1].effectrank
                    );
                    buff2NameText.text = $"<color=#C6A34C>{effect2Name}</color>";
                    //バフの効果値の表示を設定
                    buff2ValueText.text = healItem.buffEffects[1].multiplier.ToString();
                }
                else
                {
                    buff2NameText.text = null;
                    buff2ValueText.text = null;
                }
            }
            else
            {
                buff1NameText.text = null;
                buff1ValueText.text = null;
                buff2NameText.text = null;
                buff2ValueText.text = null;
            }

            //アイテムの数字のIDを保存する
            int itemIDNumber = EnumIDUtility.ToID(itemID);

            //UIの非表示化の機能も備えている
            if (HealItemPreviewUIManager.instance != null)
            {
                //アイテムの効果を表示する
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
                Debug.LogError("PlayerEffectUIManagerが見つかりませんでした");
            }
        }
    }
}

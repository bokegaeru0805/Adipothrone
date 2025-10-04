using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponSelectButton : MonoBehaviour, IItemAssignable
{
    [Header("武器のランクのTextコンポーネント")]
    [SerializeField]
    private TextMeshProUGUI weaponRankText; //武器のランクを表示するTextコンポーネント

    [HideInInspector]
    public bool isEquippedWeaponButton = false;

    [HideInInspector]
    public Enum AssignedItemID => assignedItemID; //選択されているアイテムのID
    private Enum assignedItemID; // 実際のEnum型
    private WeaponManager.WeaponType weaponType; // 武器の種類
    private float weaponUIImageScale = 0.45f; //武器の画像のScale

    public void AssignItem(Enum itemID)
    {
        assignedItemID = itemID;
        UpdateWeaponIcon(); // アイテムのアイコンを更新
    }

    private void Awake()
    {
        if (weaponRankText == null)
        {
            Debug.LogError("武器のランクのTextコンポーネントが設定されていません");
            return;
        }
        GetComponent<Button>().onClick.AddListener(SelectWeapon);
    }

    private void OnDisable()
    {
        assignedItemID = null; //weaponIDを初期化する
    }

    private void SelectWeapon()
    {
        if (WeaponManager.instance != null)
        {
            SEManager.instance?.PlayUISE(SE_UI.WeaponDecision1); //SEを鳴らす
            WeaponManager.instance.ReplaceEquippedWeapon(assignedItemID); //装備中の武器を変更する
            isEquippedWeaponButton = true; //選択ボタンの装備中のフラグをtrueにする
        }
        else
        {
            Debug.LogWarning("WeaponManagerが存在しません");
        }
    }

    public void UpdateWeaponIcon()
    {
        //武器の画像を表示するImageコンポーネントを取得
        Image myImage = this.transform.GetChild(0).GetComponent<Image>();

        if (myImage == null)
        {
            Debug.LogWarning("武器選択ボタンがImageコンポーネントを持っていません");
            return;
        }

        if (assignedItemID is ShootName)
        {
            weaponType = WeaponManager.WeaponType.shoot;
        }
        else if (assignedItemID is BladeName)
        {
            weaponType = WeaponManager.WeaponType.blade;
        }
        else
        {
            Debug.LogWarning("武器の種類が設定されていません");
            return;
        }

        //武器の種類によって画像の角度を変更
        if (weaponType == WeaponManager.WeaponType.blade)
        {
            myImage.rectTransform.rotation = Quaternion.Euler(0, 0, 90f); //剣の武器の場合は画像の角度を90度だけ変更
        }
        else
        {
            myImage.rectTransform.rotation = Quaternion.Euler(0, 0, 0f); //それ以外は角度を元に戻す
        }

        //武器の画像スプライトを取得
        Sprite weaponSprite = ItemDataManager.instance.GetItemSpriteByID(assignedItemID);

        if (myImage != null)
        {
            myImage.sprite = weaponSprite; //下記のmyImage.sprite.rect.widthのため先に行う
            if (weaponType == WeaponManager.WeaponType.blade)
            {
                myImage.rectTransform.rotation = Quaternion.Euler(0, 0, 90f); //剣の武器の場合は画像の角度を90度だけ変更
                myImage.SetNativeSize(); //スプライトの元サイズに合わせる
                myImage.rectTransform.localScale = new Vector3(
                    weaponUIImageScale,
                    weaponUIImageScale,
                    1f
                ); // Scale を 0.7 にする（サイズを縮小）
                float originalWidth = myImage.sprite.rect.width; // スプライトの width を取得
                float bottomY = originalWidth * 6.25f * weaponUIImageScale * 0.5f + 44.7f; // 底の位置（Y座標）を調整

                myImage.rectTransform.pivot = new Vector2(0.5f, 0f); // Pivot を下に設定（0にしないと bottomY がズレる）
                myImage.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                myImage.rectTransform.anchorMax = new Vector2(0.5f, 0f); // Anchor を下中央に固定（親の下端基準で配置）
                myImage.rectTransform.anchoredPosition = new Vector2(19.7f, bottomY); // 底から bottomY に配置
            }
        }
        else
        {
            Debug.LogWarning("武器選択ボタンがImageコンポーネントを持っていません");
        }

        //武器のランクを取得
        ItemRank itemRank = ItemDataManager.instance.GetItemRankByID(assignedItemID);
        //武器のランクを表示
        if (itemRank != ItemRank.None)
        {
            // ランクの文字列を取得
            String weaponRankString = itemRank.ToString();
            // ランクの文字列を色付け
            weaponRankString = string.Format(GameConstants.UIColorTagGold, weaponRankString);
            // ランクの文字列をTextMeshProに設定
            weaponRankText.text = weaponRankString;
        }
    }
}

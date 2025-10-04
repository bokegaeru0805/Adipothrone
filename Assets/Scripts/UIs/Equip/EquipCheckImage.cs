using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class EquipCheckImage : MonoBehaviour
{
    private WeaponManager weaponManager;

    [SerializeField, Header("武器の画像")]
    private Image bladeImage;

    [SerializeField]
    private Image shootImage;
    private float weaponUIImageScale = 0.7f; //武器の画像のScale

    private enum WeaponType
    {
        blade = 1,
        shoot = 2,
    }

    private void Awake()
    {
        if (bladeImage == null && shootImage == null)
        {
            Debug.LogWarning("武器の画像が設定されていません");
        }
    }

    private void OnEnable()
    {
        if (weaponManager == null)
        {
            weaponManager = WeaponManager.instance;
            if (weaponManager == null)
            {
                Debug.LogError(
                    "WeaponManagerが見つかりません。EquipCheckImageの機能に影響します。"
                );
                return;
            }
        }

        //武器の画像スプライト変更関数を登録
        weaponManager.OnWeaponReplaced += ApplyEquippedWeaponSprites;

        //武器の画像スプライトを初期化する
        ApplyEquippedWeaponSprites(null);
    }

    private void OnDisable()
    {
        if (weaponManager != null)
        {
            weaponManager.OnWeaponReplaced -= ApplyEquippedWeaponSprites;
        }
    }

    private void ApplyEquippedWeaponSprites(Enum _weaponID)
    {
        //武器の種類ごとに画像を変える関数を呼び出す
        if (bladeImage != null)
        {
            ChangeWeaponSprite(bladeImage, WeaponType.blade);
        }

        if (shootImage != null)
        {
            ChangeWeaponSprite(shootImage, WeaponType.shoot);
        }
    }

    //メニュー画面の武器の画像を変更する
    private void ChangeWeaponSprite(Image image, WeaponType weaponType)
    {
        var WeaponEquipmentData = GameManager.instance.savedata.WeaponEquipmentData;
        if (WeaponEquipmentData == null)
        {
            Debug.LogWarning("WeaponEquipmentDataが設定されていません");
            return;
        }
        Enum weaponID = null;

        //装備武器からタイプごとのIDを取得
        if (weaponType == WeaponType.blade)
        {
            var list = WeaponEquipmentData.GetAllWeaponsByType(
                InventoryWeaponData.WeaponType.blade
            );
            if (list != null && list.Count > 0)
            {
                weaponID = (BladeName)list[0].WeaponID;
            }
            else
            {
                if (image.gameObject.activeSelf)
                {
                    image.gameObject.SetActive(false); //画像がアクティブなら非表示にする
                    return; //何も表示しない
                }
            }
        }
        else if (weaponType == WeaponType.shoot)
        {
            var list = WeaponEquipmentData.GetAllWeaponsByType(
                InventoryWeaponData.WeaponType.shoot
            );
            if (list != null && list.Count > 0)
            {
                weaponID = (ShootName)list[0].WeaponID;
            }
            else
            {
                if (image.gameObject.activeSelf)
                {
                    image.gameObject.SetActive(false); //画像がアクティブなら非表示にする
                    return; //何も表示しない
                }
            }
        }

        //武器の画像スプライトを設定
        if (weaponID != null)
        {
            //IDからスプライトを取得
            Sprite weaponSprite = ItemDataManager.instance.GetItemSpriteByID(weaponID);
            //武器の画像スプライトを取得
            if (weaponSprite == null)
            {
                Debug.LogWarning($"{weaponID}のスプライトが見つかりません");
                return;
            }

            image.sprite = weaponSprite; //SetNativeSize()のために先に行う
            if (weaponType == WeaponType.blade)
            {
                image.SetNativeSize(); //スプライトの元サイズに合わせる
                image.rectTransform.rotation = Quaternion.Euler(0, 0, 90f); //剣の武器の場合は画像の角度を90度だけ変更
                image.rectTransform.localScale = new Vector3(
                    weaponUIImageScale,
                    weaponUIImageScale,
                    1f
                ); // Scale を 0.7 にする（サイズを縮小）
            }

            if (image.gameObject.activeSelf == false)
            {
                image.gameObject.SetActive(true); //画像が非アクティブならアクティブにする
            }
        }
    }
}

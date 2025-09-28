using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponDetailPanel : MonoBehaviour
{
    private WeaponManager weaponManager; // WeaponManagerのインスタンスを保持

    [HideInInspector]
    public InventoryWeaponData.WeaponType weaponType; //武器の種類

    [Header("武器の名前のテキスト")]
    [SerializeField]
    private TextMeshProUGUI WeaponNameText = null; //武器の名前のText

    [Header("武器のImage")]
    [SerializeField]
    private Image WeaponImage = null; //武器のImage

    [Header("武器のレンジ/速度のテキスト")]
    [SerializeField]
    private TextMeshProUGUI RangeOrSpeedNameText = null; //武器のレンジ/速度のText

    [Header("武器の取り回し/貫通数のテキスト")]
    [SerializeField]
    private TextMeshProUGUI HandlingOrPenetrationNameText = null; //武器の取り回し/貫通数のText

    [Header("現在の攻撃力のUI")]
    [SerializeField]
    private TextMeshProUGUI AttackPowerCurrentText;

    [Header("変更後の攻撃力のUI")]
    [SerializeField]
    private TextMeshProUGUI AttackPowerNextText;

    [Header("現在の武器のWP消費量のUI")]
    [SerializeField]
    private TextMeshProUGUI WPCostCurrentText;

    [Header("変更後の武器のWP消費量のUI")]
    [SerializeField]
    private TextMeshProUGUI WPCostNextText;

    [Header("現在武器のレンジ/速度のUI")]
    [SerializeField]
    private GameObject RangeOrSpeedCurrentBar;

    [SerializeField]
    private Image RangeOrSpeedCurrentBarImage;

    [Header("変更後の武器のレンジのUI")]
    [SerializeField]
    private GameObject RangeOrSpeedNextBar;

    [SerializeField]
    private Image RangeOrSpeedNextBarImage;

    [Header("現在武器の取り回し/貫通数のUI")]
    [SerializeField]
    private GameObject HandlingCurrentBar;

    [SerializeField]
    private Image HandlingCurrentBarImage;

    [Header("変更後の武器の取り回しのUI")]
    [SerializeField]
    private GameObject HandlingNextBar;

    [SerializeField]
    private Image HandlingNextBarImage;

    [Header("現在武器の貫通数のUI")]
    [SerializeField]
    private TextMeshProUGUI PenetrationCurrentText;

    [Header("変更後の武器の貫通数のUI")]
    [SerializeField]
    private TextMeshProUGUI PenetrationNextText;

    private float baseSize = 0; // ボタンのアイテム画像のベースサイズ（初期化時に設定）

    private void Awake()
    {
        if (WeaponNameText == null)
        {
            Debug.LogWarning("武器の名前のTextMeshProUGUIが設定されていません");
            return;
        }

        if (RangeOrSpeedNameText == null)
        {
            Debug.LogWarning("武器のレンジ/速度のTextMeshProUGUIが設定されていません");
            return;
        }

        if (HandlingOrPenetrationNameText == null)
        {
            Debug.LogWarning("武器の取り回し/貫通数のTextMeshProUGUIが設定されていません");
            return;
        }

        if (AttackPowerCurrentText == null)
        {
            Debug.LogWarning("現在の攻撃力のTextMeshProUGUIが設定されていません");
            return;
        }

        if (AttackPowerNextText == null)
        {
            Debug.LogWarning("変更後の攻撃力のTextMeshProUGUIが設定されていません");
            return;
        }

        if (WPCostCurrentText == null)
        {
            Debug.LogWarning("現在の武器のWP消費量のTextMeshProUGUIが設定されていません");
            return;
        }

        if (WPCostNextText == null)
        {
            Debug.LogWarning("変更後の武器のWP消費量のTextMeshProUGUIが設定されていません");
            return;
        }

        if (RangeOrSpeedCurrentBar == null || RangeOrSpeedCurrentBarImage == null)
        {
            Debug.LogWarning("現在武器のレンジ/速度のUIが設定されていません");
            return;
        }

        if (RangeOrSpeedNextBar == null || RangeOrSpeedNextBarImage == null)
        {
            Debug.LogWarning("変更後の武器のレンジ/速度のUIが設定されていません");
            return;
        }

        if (HandlingCurrentBar == null || HandlingCurrentBarImage == null)
        {
            Debug.LogWarning("現在武器の取り回し/貫通数のUIが設定されていません");
            return;
        }

        if (HandlingNextBar == null || HandlingNextBarImage == null)
        {
            Debug.LogWarning("変更後の武器の取り回し/貫通数のUIが設定されていません");
            return;
        }

        if (PenetrationCurrentText == null)
        {
            Debug.LogWarning("現在武器の貫通数のTextMeshProUGUIが設定されていません");
            return;
        }

        if (PenetrationNextText == null)
        {
            Debug.LogWarning("変更後の武器の貫通数のTextMeshProUGUIが設定されていません");
            return;
        }

        // アイテム画像のベースサイズを取得
        RectTransform rectTransform = WeaponImage.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            baseSize = rectTransform.sizeDelta.x; // 横幅をベースサイズとして使用
        }
        else
        {
            Debug.LogWarning("アイテム画像のRectTransformが取得できませんでした。");
        }
    }

    private void OnEnable()
    {
        if (weaponManager == null)
        {
            weaponManager = WeaponManager.instance;
            if (weaponManager == null)
            {
                Debug.LogWarning("WeaponManagerが見つかりません。このUIは機能しません。");
                return; // WeaponManagerがなければ、ここで処理を中断
            }
        }
        weaponManager.OnWeaponReplaced += DisplayEquippedWeaponDetails; //武器が変更された時に呼ばれるイベントを登録
        StartCoroutine(OnEnableCoroutine()); //次のフレームまで待機
    }

    //WeaponPanelActive.csとの連携のためにCoroutineを使用
    private IEnumerator OnEnableCoroutine()
    {
        yield return new WaitForEndOfFrame(); //次のフレームまで待機

        RefreshEquippedWeaponDisplay(); //現在装備中の武器の詳細を表示
    }

    /// <summary>
    /// 現在のweaponTypeに基づいて、装備中の武器情報をUIに表示します。
    /// </summary>
    public void RefreshEquippedWeaponDisplay()
    {
        if (weaponType == InventoryWeaponData.WeaponType.shoot)
        {
            var shootSaveData =
                GameManager.instance.savedata.WeaponEquipmentData.GetFirstWeaponByType(
                    InventoryWeaponData.WeaponType.shoot
                ); //現在装備中のShoot武器のデータを取得
            var shootWeaponID = shootSaveData?.EnumWeaponID;
            if (shootWeaponID != null)
            {
                DisplayEquippedWeaponDetails(shootWeaponID); //現在装備中のShoot武器の詳細情報を初期化
            }
            else
            {
                DisplayEquippedWeaponDetails(null); //現在装備中のShoot武器がない場合は、詳細パネルを空にする
                Debug.LogWarning("現在装備中のShoot武器が見つかりませんでした");
            }
        }
        else if (weaponType == InventoryWeaponData.WeaponType.blade)
        {
            var bladeSaveData =
                GameManager.instance.savedata.WeaponEquipmentData.GetFirstWeaponByType(
                    InventoryWeaponData.WeaponType.blade
                ); //現在装備中のBlade武器のデータを取得
            var bladeWeaponID = bladeSaveData?.EnumWeaponID;
            if (bladeWeaponID != null)
            {
                DisplayEquippedWeaponDetails(bladeWeaponID); //現在装備中のBlade武器の詳細情報を初期化
            }
            else
            {
                DisplayEquippedWeaponDetails(null); //現在装備中のBlade武器がない場合は、詳細パネルを空にする
                Debug.LogWarning("現在装備中のBlade武器が見つかりませんでした");
            }
        }
    }

    private void OnDisable()
    {
        // 初期化が完了していない場合は何もしない
        if (!GameManager.isFirstGameSceneOpen)
            return;

        if (weaponManager == null)
        {
            Debug.LogWarning("WeaponManagerが設定されていません");
            return;
        }
        weaponManager.OnWeaponReplaced -= DisplayEquippedWeaponDetails; //武器が変更された時に呼ばれるイベントを解除
    }

    /// <summary>
    /// 選択中の武器の詳細パネルを設定する
    /// </summary>
    /// <param name="weaponID"></param>
    public void DisplayNextWeaponDetails(Enum weaponID)
    {
        if (weaponManager == null)
        {
            Debug.LogWarning("WeaponManagerが設定されていません");
            return;
        }

        if (weaponID == null)
        {
            // 選択中の武器がない場合は、詳細パネルを空にする
            weaponManager.DisplaySelectedWeaponDetails(
                null,
                AttackPowerNextText,
                WPCostNextText,
                RangeOrSpeedNextBar,
                RangeOrSpeedNextBarImage,
                HandlingNextBar,
                HandlingNextBarImage,
                PenetrationNextText
            );
            return;
        }

        //選択中の武器の名前を取得
        String weaponName = null;
        var weaponData = weaponManager.GetWeaponByID(weaponID);

        if (weaponData != null)
        {
            weaponName = weaponData.itemName;
        }
        else
        {
            Debug.LogWarning("武器のデータが見つかりませんでした");
            return;
        }

        WeaponNameText.text = weaponName; //武器の名前を設定
        if (WeaponImage != null)
        {
            // 武器のアイコン画像を設定
            UIUtility.SetSpriteFitToSquare(WeaponImage, weaponData.itemSprite, baseSize);
        }

        //現在選択中の武器の詳細情報を表示
        weaponManager.DisplaySelectedWeaponDetails(
            weaponID,
            AttackPowerNextText,
            WPCostNextText,
            RangeOrSpeedNextBar,
            RangeOrSpeedNextBarImage,
            HandlingNextBar,
            HandlingNextBarImage,
            PenetrationNextText
        );
    }

    /// <summary>
    /// 現在装備中の武器の詳細情報を表示する
    /// </summary>
    /// <param name="weaponType"></param>
    public void DisplayEquippedWeaponDetails(Enum _weaponID)
    {
        if (
            GameManager.instance == null
            || GameManager.instance.savedata.WeaponEquipmentData == null
        )
        {
            Debug.LogWarning("GameManagerまたはWeaponEquipmentDataが設定されていません");
            return;
        }

        //武器の種類によって、UIの名前表示を変更
        if (_weaponID is ShootName)
        {
            RangeOrSpeedNameText.text = "<color=#C6A34C>速度</color>";
            HandlingOrPenetrationNameText.text = "<color=#C6A34C>貫通数</color>";
            weaponType = InventoryWeaponData.WeaponType.shoot; //武器の種類を設定
        }
        else if (_weaponID is BladeName)
        {
            RangeOrSpeedNameText.text = "<color=#C6A34C>レンジ</color>";
            HandlingOrPenetrationNameText.text = "<color=#C6A34C>重さ</color>";
            weaponType = InventoryWeaponData.WeaponType.blade; //武器の種類を設定
        }
        else
        {
            Debug.LogWarning("武器の種類が設定されていません");
            WeaponManager.instance.DisplaySelectedWeaponDetails(
                null,
                AttackPowerCurrentText,
                WPCostCurrentText,
                RangeOrSpeedCurrentBar,
                RangeOrSpeedCurrentBarImage,
                HandlingCurrentBar,
                HandlingCurrentBarImage,
                PenetrationCurrentText
            );
            return;
        }

        //現在装備中の武器の詳細を表示
        WeaponManager.instance.DisplaySelectedWeaponDetails(
            _weaponID,
            AttackPowerCurrentText,
            WPCostCurrentText,
            RangeOrSpeedCurrentBar,
            RangeOrSpeedCurrentBarImage,
            HandlingCurrentBar,
            HandlingCurrentBarImage,
            PenetrationCurrentText
        );
    }
}

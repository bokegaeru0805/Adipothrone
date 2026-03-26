using System;
using System.Collections;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#region 武器詳細パネルクラス
/// <summary>
/// 武器の詳細情報（名前、画像、各種ステータスなど）をUIに表示・更新するクラス。
/// チラつき防止のための透明化処理や、非同期的なUIレイアウト待ち処理を含みます。
/// </summary>
public class WeaponDetailPanel : MonoBehaviour
{
    #region フィールド・プロパティ
    private WeaponManager weaponManager; // WeaponManagerのインスタンスをキャッシュ

    [HideInInspector]
    public InventoryWeaponData.WeaponType weaponType; // 武器の種類

    [Header("武器の名前のテキスト")]
    [SerializeField, Required("武器の名前用Textを設定してください")]
    private TextMeshProUGUI WeaponNameText = null; //武器の名前のText

    [Header("武器のImage")]
    [SerializeField, Required("武器のアイコン用Imageを設定してください")]
    private Image WeaponImage = null; //武器のImage

    [Header("武器のレンジ/速度のテキスト")]
    [SerializeField, Required("レンジ/速度の項目名Textを設定してください")]
    private TextMeshProUGUI RangeOrSpeedNameText = null; //武器のレンジ/速度のText

    [Header("武器の取り回し/貫通数のテキスト")]
    [SerializeField, Required("取り回し/貫通数の項目名Textを設定してください")]
    private TextMeshProUGUI HandlingOrPenetrationNameText = null; //武器の取り回し/貫通数のText

    [Header("現在の攻撃力のUI")]
    [SerializeField, Required("現在の攻撃力Textを設定してください")]
    private TextMeshProUGUI AttackPowerCurrentText;

    [Header("変更後の攻撃力のUI")]
    [SerializeField, Required("変更後の攻撃力Textを設定してください")]
    private TextMeshProUGUI AttackPowerNextText;

    [Header("現在の武器のWP消費量のUI")]
    [SerializeField, Required("現在のWP消費量Textを設定してください")]
    private TextMeshProUGUI WPCostCurrentText;

    [Header("変更後の武器のWP消費量のUI")]
    [SerializeField, Required("変更後のWP消費量Textを設定してください")]
    private TextMeshProUGUI WPCostNextText;

    [Header("現在武器のレンジ/速度のUI")]
    [SerializeField, Required("現在のレンジ/速度のバー(GameObject)を設定してください")]
    private GameObject RangeOrSpeedCurrentBar;

    [SerializeField, Required("現在のレンジ/速度のバー(Image)を設定してください")]
    private Image RangeOrSpeedCurrentBarImage;

    [Header("変更後の武器のレンジのUI")]
    [SerializeField, Required("変更後のレンジ/速度のバー(GameObject)を設定してください")]
    private GameObject RangeOrSpeedNextBar;

    [SerializeField, Required("変更後のレンジ/速度のバー(Image)を設定してください")]
    private Image RangeOrSpeedNextBarImage;

    [Header("現在武器の取り回し/貫通数のUI")]
    [SerializeField, Required("現在の取り回し/貫通数のバー(GameObject)を設定してください")]
    private GameObject HandlingCurrentBar;

    [SerializeField, Required("現在の取り回し/貫通数のバー(Image)を設定してください")]
    private Image HandlingCurrentBarImage;

    [Header("変更後の武器の取り回しのUI")]
    [SerializeField, Required("変更後の取り回し/貫通数のバー(GameObject)を設定してください")]
    private GameObject HandlingNextBar;

    [SerializeField, Required("変更後の取り回し/貫通数のバー(Image)を設定してください")]
    private Image HandlingNextBarImage;

    [Header("現在武器の貫通数のUI")]
    [SerializeField, Required("現在の貫通数Textを設定してください")]
    private TextMeshProUGUI PenetrationCurrentText;

    [Header("変更後の武器の貫通数のUI")]
    [SerializeField, Required("変更後の貫通数Textを設定してください")]
    private TextMeshProUGUI PenetrationNextText;

    private float baseSize = 0; // ボタンのアイテム画像のベースサイズ（初期化時に設定）
    private CanvasGroup canvasGroup; // チラつき防止用のCanvasGroup
    #endregion

    #region 初期化・ライフサイクル
    /// <summary>
    /// オブジェクト生成時の初期化処理。
    /// ベースサイズの計算と、表示チラつきを防ぐための透明化を行います。
    /// </summary>
    private void Awake()
    {
        // ※NaughtyAttributesの[Required]により、インスペクターでのアタッチ漏れ警告は自動化されたため、
        // 冗長なnullチェック（Debug.LogWarning）をすべて削除し、コードを簡潔化しています。

        // アイテム画像のベースサイズを取得
        if (WeaponImage != null)
        {
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

        // チラつき防止のためCanvasGroupを取得（なければ追加）
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f; // 初期状態でパネルを完全に透明にする
        ClearInitialUI(); // プレハブ上のダミーテキスト等をクリア
    }

    /// <summary>
    /// オブジェクトがアクティブになった際の処理。
    /// イベント登録と、UIレイアウト計算待ちのコルーチンを開始します。
    /// </summary>
    private void OnEnable()
    {
        // WeaponManagerを安全に取得し、イベントを登録
        if (EnsureWeaponManager())
        {
            weaponManager.OnWeaponReplaced += DisplayEquippedWeaponDetails; //武器が変更された時に呼ばれるイベントを登録
        }

        // レイアウト計算と他スクリプトの準備を待つため、コルーチンを実行
        StartCoroutine(OnEnableCoroutine());
    }

    /// <summary>
    /// オブジェクトが非アクティブになる際の処理。
    /// イベント解除と、次回表示時のための事前透明化を行います。
    /// </summary>
    private void OnDisable()
    {
        // 初期化が完了していない場合（シーンロード直後など）は何もしない
        if (!GameManager.isFirstGameSceneOpen)
            return;

        if (weaponManager != null)
        {
            weaponManager.OnWeaponReplaced -= DisplayEquippedWeaponDetails; //武器が変更された時に呼ばれるイベントを解除
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f; // パネルを完全に透明にしてから非表示にする（次回のチラつき防止）
        }
    }
    #endregion

    #region UI更新ロジック
    /// <summary>
    /// UIのレイアウト計算が終わるまで1フレーム待機し、安全に描画を行うコルーチン。
    /// WeaponPanelActive.csとの連携やバーの長さ計算を正確に行うために必要です。
    /// </summary>
    private IEnumerator OnEnableCoroutine()
    {
        yield return new WaitForEndOfFrame(); // 次のフレーム（UIのサイズ計算等が終わる）まで待機

        RefreshEquippedWeaponDisplay(); // 現在装備中の武器の詳細を安全に表示

        // バーの計算やテキスト代入など、全てが整った瞬間にパッと表示（透明化解除）
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }

    /// <summary>
    /// 初期状態のチラつきを防ぐため、UIのテキストや画像を空（リセット状態）にします。
    /// </summary>
    private void ClearInitialUI()
    {
        if (WeaponNameText != null)
            WeaponNameText.text = "";
        if (AttackPowerCurrentText != null)
            AttackPowerCurrentText.text = "";
        if (AttackPowerNextText != null)
            AttackPowerNextText.text = "";
        if (WPCostCurrentText != null)
            WPCostCurrentText.text = "";
        if (WPCostNextText != null)
            WPCostNextText.text = "";
        if (PenetrationCurrentText != null)
            PenetrationCurrentText.text = "";
        if (PenetrationNextText != null)
            PenetrationNextText.text = "";
        if (WeaponImage != null)
            WeaponImage.sprite = null;
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

    /// <summary>
    /// リストから選択中の「変更後（プレビュー用）」の武器詳細パネルを設定・表示します。
    /// </summary>
    /// <param name="weaponID">選択された武器のID</param>
    public void DisplayNextWeaponDetails(Enum weaponID)
    {
        if (!EnsureWeaponManager())
            return; // 取得失敗時は処理を中断

        if (weaponID == null)
        {
            // 武器がない場合は名前と画像もクリアする
            if (WeaponNameText != null)
                WeaponNameText.text = "";
            if (WeaponImage != null)
                WeaponImage.sprite = null;

            // 選択中の武器がない場合は、詳細パネルの各数値を空（リセット）にする
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

        //選択中の武器のデータを取得
        var weaponData = weaponManager.GetWeaponByID(weaponID);
        if (weaponData == null)
        {
            Debug.LogWarning("武器のデータが見つかりませんでした");
            return;
        }

        // 武器の名前とアイコン画像を設定
        if (WeaponNameText != null)
            WeaponNameText.text = weaponData.itemName;
        if (WeaponImage != null)
        {
            UIUtility.SetSpriteFitToSquare(WeaponImage, weaponData.itemSprite, baseSize);
        }

        // 現在選択中の武器の詳細数値を表示
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
    /// 「現在装備中」の武器の詳細情報を表示し、項目名を武器タイプに合わせて切り替えます。
    /// </summary>
    /// <param name="_weaponID">装備中の武器のID</param>
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

        if (!EnsureWeaponManager())
            return; // 取得失敗時は処理を中断

        // nullの場合は警告を出さずにUIをクリアして終了する
        if (_weaponID == null)
        {
            weaponManager.DisplaySelectedWeaponDetails(
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

        // 武器の種類によって、UIの項目名表示を変更する
        if (_weaponID is ShootName)
        {
            if (RangeOrSpeedNameText != null)
                RangeOrSpeedNameText.text = "<color=#C6A34C>速度</color>";
            if (HandlingOrPenetrationNameText != null)
                HandlingOrPenetrationNameText.text = "<color=#C6A34C>貫通数</color>";
            weaponType = InventoryWeaponData.WeaponType.shoot; //武器の種類を設定
        }
        else if (_weaponID is BladeName)
        {
            if (RangeOrSpeedNameText != null)
                RangeOrSpeedNameText.text = "<color=#C6A34C>レンジ</color>";
            if (HandlingOrPenetrationNameText != null)
                HandlingOrPenetrationNameText.text = "<color=#C6A34C>重さ</color>";
            weaponType = InventoryWeaponData.WeaponType.blade; //武器の種類を設定
        }
        else
        {
            Debug.LogWarning("武器の種類が設定されていません");
            weaponManager.DisplaySelectedWeaponDetails(
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

        //現在装備中の武器の詳細数値を表示
        weaponManager.DisplaySelectedWeaponDetails(
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
    #endregion

    #region ヘルパーメソッド
    /// <summary>
    /// WeaponManagerのインスタンスが未取得の場合、安全に取得を試みるヘルパー関数です。
    /// UIの初期化順序によるエラー（NullReferenceException）を防ぎます。
    /// </summary>
    /// <returns>取得に成功、または既に保持していればtrue</returns>
    private bool EnsureWeaponManager()
    {
        if (weaponManager == null)
        {
            weaponManager = WeaponManager.instance;
        }
        return weaponManager != null;
    }
    #endregion
}
#endregion

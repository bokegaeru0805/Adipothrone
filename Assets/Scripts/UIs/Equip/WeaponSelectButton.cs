using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 武器選択画面などのリストに並ぶ、個別の武器選択ボタンを制御するクラス。
/// 選択時の装備変更処理や、武器種（剣・銃）ごとのアイコンレイアウト調整を行います。
/// </summary>
public class WeaponSelectButton : MonoBehaviour, IItemAssignable
{
    #region UI参照設定
    [Header("UI設定")]
    [Tooltip("武器のランクを表示するTextコンポーネント")]
    [SerializeField]
    private TextMeshProUGUI weaponRankText;

    [Tooltip("武器アイコンを表示するImageコンポーネント")]
    [SerializeField]
    private Image weaponIconImage;
    #endregion

    #region 内部変数・プロパティ
    /// <summary>
    /// このボタンが現在装備中の武器を表しているかどうかのフラグ
    /// </summary>
    [HideInInspector]
    public bool isEquippedWeaponButton = false;

    /// <summary>
    /// 現在このボタンに割り当てられているアイテムのID（外部公開用）
    /// </summary>
    public Enum AssignedItemID => assignedItemID;

    private Enum assignedItemID; // 実際のアイテムID
    private InventoryWeaponData.WeaponType weaponType; // 武器の種類（剣、銃など）
    private float weaponUIImageScale = 0.45f; // 武器画像の共通縮小スケール
    #endregion

    #region 初期化・イベント処理
    private void Awake()
    {
        if (weaponRankText == null)
        {
            Debug.LogError("武器のランクのTextコンポーネントが設定されていません");
            return;
        }

        // ボタンクリック時に武器選択メソッドを呼び出すようリスナーを登録
        GetComponent<Button>()
            .onClick.AddListener(SelectWeapon);
    }

    private void OnDisable()
    {
        // オブジェクトが非アクティブになる際、割り当て情報をリセットする
        assignedItemID = null;
    }

    /// <summary>
    /// ボタンがクリックされた（決定された）際に呼ばれ、装備中の武器を変更します。
    /// </summary>
    private void SelectWeapon()
    {
        if (WeaponManager.instance != null)
        {
            SEManager.instance?.PlayUISE(SE_UI.WeaponDecision1); // 決定音を再生
            WeaponManager.instance.ReplaceEquippedWeapon(assignedItemID); // 装備を変更
            isEquippedWeaponButton = true; // 装備中フラグを立てる
        }
        else
        {
            Debug.LogWarning("WeaponManagerが存在しません");
        }
    }
    #endregion

    #region IItemAssignable 実装
    /// <summary>
    /// リスト生成時などに外部から呼ばれ、このボタンにアイテムを割り当てます。
    /// </summary>
    /// <param name="itemID">割り当てるアイテムのEnum ID</param>
    public void AssignItem(Enum itemID)
    {
        assignedItemID = itemID;
        UpdateWeaponIcon(); // 割り当てと同時に見た目を更新する
    }
    #endregion

    #region UI更新処理
    /// <summary>
    /// 割り当てられたアイテムIDに基づいて、武器のアイコン画像、向き、配置、およびランクテキストを更新します。
    /// </summary>
    public void UpdateWeaponIcon()
    {
        // 1. 武器アイコン用のImageコンポーネントを取得
        Image myImage = weaponIconImage;
        if (myImage == null && transform.childCount > 0)
        {
            // 移行前の既存ボタンでも動作を維持するための互換処理。
            myImage = transform.GetChild(0).GetComponent<Image>();
        }

        if (myImage == null)
        {
            Debug.LogWarning("武器選択ボタンの武器アイコンImageが設定されていません");
            return;
        }

        // 2. 武器の種類の判定
        if (assignedItemID is ShootName)
        {
            weaponType = InventoryWeaponData.WeaponType.shoot;
        }
        else if (assignedItemID is BladeName)
        {
            weaponType = InventoryWeaponData.WeaponType.blade;
        }
        else
        {
            Debug.LogWarning("武器の種類が設定されていないか、不明なアイテムIDです");
            return;
        }

        // 3. アイコン画像の取得と適用
        Sprite weaponSprite = ItemDataManager.instance.GetItemSpriteByID(assignedItemID);
        myImage.sprite = weaponSprite;

        // 4. スケールとレイアウトの調整
        // 全ての武器で共通して本来のピクセルサイズに戻し、同じ比率で縮小する。
        // これにより、剣と銃でドットの大きさ（スケール感）が統一され美しく表示されます。
        myImage.SetNativeSize();
        myImage.rectTransform.localScale = new Vector3(weaponUIImageScale, weaponUIImageScale, 1f);

        // 武器の種類に応じた配置と角度の適用
        if (weaponType == InventoryWeaponData.WeaponType.blade)
        {
            // --- 剣 (Blade) の特殊レイアウト（縦向け・下端合わせ） ---
            myImage.rectTransform.rotation = Quaternion.Euler(0, 0, 90f);

            // スプライトの幅とスケールから底面の位置（Y座標）を計算
            float originalWidth = myImage.sprite.rect.width;
            float bottomY = originalWidth * 6.25f * weaponUIImageScale * 0.5f + 44.7f;

            myImage.rectTransform.pivot = new Vector2(0.5f, 0f); // Pivotを下に設定
            myImage.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            myImage.rectTransform.anchorMax = new Vector2(0.5f, 0f); // Anchorを下中央に固定
            myImage.rectTransform.anchoredPosition = new Vector2(19.7f, bottomY);
        }
        else
        {
            // --- 銃 (Shoot) などの通常レイアウト（横向け・中央合わせ） ---
            myImage.rectTransform.rotation = Quaternion.Euler(0, 0, 0f);

            myImage.rectTransform.pivot = new Vector2(0.5f, 0.5f); // Pivotを中央に設定
            myImage.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            myImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); // Anchorを中央に固定
            myImage.rectTransform.anchoredPosition = Vector2.zero; // ボタンのど真ん中に配置
        }

        // 5. 武器のランク表示の更新
        ItemRank itemRank = ItemDataManager.instance.GetItemRankByID(assignedItemID);
        if (itemRank != ItemRank.None)
        {
            // ランクの文字列を取得し、専用のカラータグで装飾して適用
            string weaponRankString = itemRank.ToString();
            weaponRankString = string.Format(GameConstants.UI_COLOR_TAG_GOLD, weaponRankString);
            weaponRankText.text = weaponRankString;
        }
        else
        {
            // ランクが設定されていない場合はテキストを消す
            weaponRankText.text = "";
        }
    }
    #endregion
}

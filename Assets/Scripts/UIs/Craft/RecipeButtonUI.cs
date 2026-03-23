using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 合成リストに並ぶ1つ1つのレシピボタンUIを制御するクラス。
/// フォーカスされた際に詳細ビューを更新し、決定キーで合成ポップアップを開きます。
/// </summary>
public class RecipeButtonUI : MonoBehaviour, ISelectHandler, ISubmitHandler
{
    #region UI参照設定
    [Header("UI参照")]
    [Tooltip("合成後のアイテムのアイコンを表示する画像")]
    [SerializeField]
    private Image itemIcon;

    [Tooltip("合成後のアイテム名を表示するテキスト")]
    [SerializeField]
    private TextMeshProUGUI itemNameText;

    [Tooltip("合成後のアイテムの現在所持数を表示するテキスト")]
    [SerializeField]
    private TextMeshProUGUI ownedCountText;

    [Tooltip("合成可能であることを示すアイコン（任意）")]
    [SerializeField]
    private Image craftableIcon;

    [Tooltip("「New」と表示するためのImageコンポーネント")]
    [SerializeField]
    private Image newIcon;
    #endregion

    #region 見た目の設定
    [Header("カラー設定")]
    [Tooltip("合成可能な場合のテキストカラー")]
    [SerializeField]
    private Color craftableColor = Color.white;

    [Tooltip("素材不足や上限到達により合成不可能な場合のテキストカラー")]
    [SerializeField]
    private Color uncraftableColor = Color.gray;
    #endregion

    #region 内部変数
    private RecipeItemData recipeData; // このボタンに紐づくレシピデータ
    private CraftMenuManager menuManager; // 全体を統括するマネージャーの参照
    private int maxCraftableCount; // 現在の所持素材や制限から計算された「最大合成可能数」
    private int maxCraftLimit = 99; // 1回で合成できる最大数の上限
    #endregion

    #region 初期化・セットアップ
    /// <summary>
    /// ボタン生成時に呼ばれ、レシピデータとマネージャーの参照をセットアップします。
    /// 同時に合成可能数を計算し、UIの見た目を更新します。
    /// </summary>
    /// <param name="data">紐づけるレシピのデータ</param>
    /// <param name="manager">CraftMenuManagerの参照</param>
    public void Setup(RecipeItemData data, CraftMenuManager manager)
    {
        recipeData = data;
        menuManager = manager;

        // --- 必要な計算 ---
        maxCraftableCount = CalculateMaxCraftableCount();
        bool isCraftable = maxCraftableCount > 0;

        // --- UI反映 ---
        itemNameText.text = recipeData.craftedItem.itemName;
        int currentOwned = GameManager.instance.GetAllTypeIDToAmount(recipeData.craftedItem);
        ownedCountText.text = $"{currentOwned}";

        if (itemIcon != null)
        {
            itemIcon.sprite = recipeData.craftedItem.itemSprite;
        }

        // 合成可能かどうかで色やアイコンの表示状態を切り替える
        itemNameText.color = isCraftable ? craftableColor : uncraftableColor;
        if (craftableIcon != null)
            craftableIcon.gameObject.SetActive(isCraftable);

        // Newアイコンの表示更新
        if (newIcon != null)
        {
            // セーブデータからこのレシピのエントリーを取得し、新規かどうか（isNew）を判定
            int recipeIDNumber = EnumIDUtility.ToID(recipeData.GetItemID());
            var entry = GameManager.instance.savedata.RecipeData.knownRecipes.Find(r =>
                r.recipeID == recipeIDNumber
            );

            // エントリーが存在し、かつisNewがtrueなら表示する
            newIcon.enabled = (entry != null && entry.isNew);
        }
    }
    #endregion

    #region 計算処理
    /// <summary>
    /// 現在の素材所持数とレシピの合成回数制限を比較し、
    /// 最大でいくつ合成できるかを計算して返します。
    /// </summary>
    /// <returns>最大合成可能数</returns>
    private int CalculateMaxCraftableCount()
    {
        int maxByMaterials = int.MaxValue;

        // 1. 各素材の所持数から、作れる限界数を計算
        foreach (var mat in recipeData.materials)
        {
            int owned = GameManager.instance.GetAllTypeIDToAmount(mat.item);
            int possible = owned / mat.requiredAmount;

            // 一番少ない素材の限界数に合わせる
            if (possible < maxByMaterials)
            {
                maxByMaterials = possible;
            }
        }

        // 2. レシピ自体の合成回数制限を計算（無制限でない場合のみ）
        if (!recipeData.IsUnlimitedCrafting())
        {
            int craftCount = GameManager.instance.savedata.RecipeData.GetCraftCount(
                recipeData.GetItemID()
            );
            int remaining = recipeData.maxCraftCount - craftCount;

            // 素材で作れる数と、レシピの残り回数のうち、小さい方を採用する
            if (remaining < maxByMaterials)
            {
                maxByMaterials = remaining;
            }
        }

        // 3. さらに、1回で合成できる最大数の上限も考慮する
        if (maxByMaterials > maxCraftLimit)
        {
            maxByMaterials = maxCraftLimit;
        }

        return maxByMaterials;
    }
    #endregion

    #region イベントハンドラ (ISelectHandler / ISubmitHandler)
    /// <summary>
    /// 十字キーやマウス操作でこのボタンが選択（フォーカス）された時に呼ばれます。
    /// 右側の詳細ビューの表示を更新させます。
    /// </summary>
    public void OnSelect(BaseEventData eventData)
    {
        menuManager.UpdateDetailView(recipeData, maxCraftableCount);

        // もしこのレシピが新規（Newアイコンが表示されている状態）なら、フラグをオフにする
        if (newIcon != null && newIcon.enabled)
        {
            newIcon.enabled = false;
            // RecipeSaveData側のNEWフラグもオフにして保存状態を更新する
            GameManager.instance.savedata.RecipeData.MarkAsSeen(recipeData.GetItemID());
        }
    }

    /// <summary>
    /// このボタンが選択状態で決定キー（UIConfirmなど）が押された時に呼ばれます。
    /// 合成可能であれば個数選択のポップアップを開きます。
    /// </summary>
    public void OnSubmit(BaseEventData eventData)
    {
        if (maxCraftableCount > 0)
        {
            menuManager.OpenConfirmPopup(recipeData, maxCraftableCount);
        }
        else
        {
            // TODO: 合成不可のときのエラーSEなどを鳴らす場合はここに実装
            Debug.Log("素材が足りないか、合成回数の上限に達しているため合成できません。");
        }
    }
    #endregion
}

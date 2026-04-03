using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 選択されたレシピの詳細情報（完成品のステータスや必要素材のリスト）を
/// 右側の固定エリアに表示・更新するクラス。
/// </summary>
public class CraftDetailView : MonoBehaviour
{
    #region UI参照設定 (合成品情報)
    [Header("合成品情報")]
    [Tooltip("完成品アイテムのアイコン")]
    [SerializeField]
    private Image itemIcon;

    [Tooltip("完成品アイテムの名称")]
    [SerializeField]
    private TextMeshProUGUI itemNameText;

    [Tooltip("完成品アイテムの種類（回復アイテム、強化アイテム等）")]
    [SerializeField]
    private TextMeshProUGUI itemTypeText;

    [Tooltip("完成品アイテムの説明文")]
    [SerializeField]
    private TextMeshProUGUI descriptionText;

    [Tooltip("残り合成可能回数を表示するテキスト")]
    [SerializeField]
    private TextMeshProUGUI remainingCraftCountText;
    #endregion

    #region UI参照設定 (素材リスト関連)
    [Header("素材リスト関連")]
    [Tooltip("素材UIを並べる親オブジェクト(VerticalLayoutGroup等をアタッチしておく)")]
    [SerializeField]
    private Transform materialsContainer;

    [Tooltip("素材1つ分を表示するためのUIプレハブ")]
    [SerializeField]
    private GameObject materialUIPrefab;
    #endregion

    #region 内部変数
    private float iconBaseSize = 0f; // 完成品アイコンの基準サイズ
    #endregion

    private void Awake()
    {
        // 起動時にインスペクターで設定されている完成品アイコンの横幅を基準サイズとして記憶する
        if (itemIcon != null)
        {
            RectTransform rect = itemIcon.GetComponent<RectTransform>();
            if (rect != null)
            {
                iconBaseSize = rect.sizeDelta.x;
            }
        }
    }

    #region 更新処理
    /// <summary>
    /// 左側のリストで新しいレシピが選択された際に呼ばれ、
    /// 右側の詳細情報のUI表示をすべて更新します。
    /// </summary>
    /// <param name="recipeData">選択されたレシピのデータ</param>
    /// <param name="maxCraftableCount">最大合成可能数（今回は表示に使用していませんが拡張用に保持）</param>
    public void UpdateView(RecipeItemData recipeData, int maxCraftableCount)
    {
        if (recipeData == null)
            return;

        // --- 1. 完成品の基本情報の更新 ---
        UIUtility.SetSpriteFitToSquare(itemIcon, recipeData.craftedItem.itemSprite, iconBaseSize);
        itemNameText.text = recipeData.craftedItem.itemName;
        itemTypeText.text = GameManager.instance.GetItemTypePrefix(
            recipeData.craftedItem.GetItemID()
        );
        descriptionText.text = recipeData.craftedItem.description;

        // --- 追加：残り合成可能回数の計算と表示 ---
        if (remainingCraftCountText != null)
        {
            if (recipeData.IsUnlimitedCrafting())
            {
                // 無制限の場合
                remainingCraftCountText.text = "合成可能回数: 無制限";
            }
            else
            {
                // 制限がある場合、最大回数からこれまでの合成回数を引く
                int craftedCount = GameManager.instance.savedata.RecipeData.GetCraftCount(
                    recipeData.GetItemID()
                );
                int remaining = recipeData.maxCraftCount - craftedCount;

                // 念のため0未満にならないように制限
                remaining = Mathf.Max(0, remaining);

                // remainingCraftCountText.text =
                //     $"残り合成可能回数: {remaining} / {recipeData.maxCraftCount}";

                remainingCraftCountText.text = $"残り合成可能回数: {remaining} ";
            }
        }

        // --- 2. 既存の素材UIの破棄 ---
        // 別のレシピが選ばれるたびに、前に表示していた素材リストのオブジェクトを削除する
        foreach (Transform child in materialsContainer)
        {
            Destroy(child.gameObject);
        }

        // --- 3. 新しい素材UIの生成と設定 ---
        foreach (var mat in recipeData.materials)
        {
            // プレハブをコンテナの子として生成
            GameObject obj = Instantiate(materialUIPrefab, materialsContainer);
            int owned = GameManager.instance.GetAllTypeIDToAmount(mat.item);

            // プレハブ内のUIコンポーネントを取得 (※階層名 "Icon", "NameText", "CountText" に依存)
            var icon = obj.transform.Find("Icon").GetComponent<Image>();
            var nameText = obj.transform.Find("NameText").GetComponent<TextMeshProUGUI>();
            var countText = obj.transform.Find("CountText").GetComponent<TextMeshProUGUI>();

            // 情報をセット
            icon.sprite = mat.item.itemSprite;
            nameText.text = mat.item.itemName;
            countText.text = $"{owned} / {mat.requiredAmount}";

            // 所持数が足りていない場合は文字を赤色にして警告する
            countText.color = owned >= mat.requiredAmount ? Color.white : Color.red;
        }
    }
    #endregion
}

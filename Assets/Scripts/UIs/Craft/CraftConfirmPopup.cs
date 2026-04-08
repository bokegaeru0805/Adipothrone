using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 合成を実行する直前の確認ポップアップを制御するクラス。
/// 左右キーで合成個数を増減させ、決定キーで素材の消費とアイテムの獲得を実行します。
/// </summary>
public class CraftConfirmPopup : MonoBehaviour, IPanelActive
{
    #region UI参照設定
    [Header("UI参照")]
    [Tooltip("合成するアイテムの名称と確認メッセージを表示するテキスト")]
    [SerializeField]
    private TextMeshProUGUI targetNameText;

    [Tooltip("現在選択している合成個数と最大数を表示するテキスト")]
    [SerializeField]
    private TextMeshProUGUI craftAmountText;

    [Tooltip("左向き矢印のRectTransform")]
    [SerializeField]
    private RectTransform leftArrowRect;

    [Tooltip("右向き矢印のRectTransform")]
    [SerializeField]
    private RectTransform rightArrowRect;
    #endregion

    #region アニメーション設定
    [Header("矢印アニメーション設定(DOTween)")]
    [Tooltip("矢印が動く幅（ピクセル）")]
    [SerializeField]
    private float arrowMoveAmplitude = 5f;

    [Tooltip("片道のアニメーションにかかる時間（秒）")]
    [SerializeField]
    private float arrowMoveDuration = 0.5f;
    #endregion

    #region 内部変数
    private RecipeItemData currentRecipe; // 現在合成しようとしているレシピ
    private int maxAmount; // 計算済みの最大合成可能数
    private int currentAmount = 1; // 現在選択している個数（初期値は1）

    private CraftMenuManager menuManager; // リストの更新などを依頼するマネージャー
    private InputManager inputManager; // 入力検知用のマネージャー
    #endregion

    #region ライフサイクル
    private void Start()
    {
        inputManager = InputManager.instance;
        if (inputManager == null)
        {
            Debug.LogError("InputManagerが見つかりません。ポップアップの操作ができません。");
        }

        // DOTweenによるアニメーション設定
        // 左矢印：現在位置からX座標をマイナス方向に移動させて、ヨーヨー(往復)ループ
        if (leftArrowRect != null)
        {
            leftArrowRect
                .DOAnchorPosX(
                    leftArrowRect.anchoredPosition.x - arrowMoveAmplitude,
                    arrowMoveDuration
                )
                .SetLoops(-1, LoopType.Yoyo) // -1で無限ループ、Yoyoで往復
                .SetEase(Ease.InOutSine) // 滑らかな加減速
                .SetUpdate(true); // Time.timeScale=0（ポーズ中）でも動かす
        }

        // 右矢印：現在位置からX座標をプラス方向に移動させて、ヨーヨー(往復)ループ
        if (rightArrowRect != null)
        {
            rightArrowRect
                .DOAnchorPosX(
                    rightArrowRect.anchoredPosition.x + arrowMoveAmplitude,
                    arrowMoveDuration
                )
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }
    }

    private void Update()
    {
        if (inputManager == null)
            return;

        // --- 個数の増減操作 (左右キー) ---
        if (inputManager.UIMoveRight())
        {
            // 最大値を超えないように増やす
            currentAmount = Mathf.Min(currentAmount + 1, maxAmount);
            UpdateAmountText();
        }
        else if (inputManager.UIMoveLeft())
        {
            // 1を下回らないように減らす
            currentAmount = Mathf.Max(currentAmount - 1, 1);
            UpdateAmountText();
        }

        // --- 決定・キャンセル操作 ---
        if (inputManager.UISelectYes())
        {
            // Yes（決定）キーで合成を実行
            ExecuteCraft();
        }
        else if (inputManager.UISelectNo() || inputManager.UIClose())
        {
            // No（キャンセル）キーでポップアップを閉じる
            UIManager.instance.ClosePopup();
        }
    }

    private void OnDestroy()
    {
        // オブジェクトが破棄される時にDOTweenのアニメーションを安全に停止・破棄する
        if (leftArrowRect != null)
            DOTween.Kill(leftArrowRect);
        if (rightArrowRect != null)
            DOTween.Kill(rightArrowRect);
    }
    #endregion

    #region セットアップとUI更新
    /// <summary>
    /// ポップアップを開く際に呼ばれ、必要な初期データをセットします。
    /// </summary>
    /// <param name="recipe">対象のレシピデータ</param>
    /// <param name="max">計算済みの最大合成可能数</param>
    /// <param name="manager">CraftMenuManagerの参照</param>
    public void Setup(RecipeItemData recipe, int max, CraftMenuManager manager)
    {
        currentRecipe = recipe;
        maxAmount = max;
        currentAmount = 1; // 開き直すたびに1にリセットする
        menuManager = manager;

        targetNameText.text = $"作成しますか？";
        UpdateAmountText();
    }

    /// <summary>
    /// 画面上の個数表示テキストを更新します。
    /// </summary>
    private void UpdateAmountText()
    {
        craftAmountText.text = $"{currentAmount}\n(最大: {maxAmount})";
    }
    #endregion

    #region 合成実行処理
    /// <summary>
    /// 実際に素材を消費し、完成品を獲得し、セーブデータへ記録する一連の合成処理を実行します。
    /// </summary>
    private void ExecuteCraft()
    {
        // 1. 各素材を指定された個数分消費する
        foreach (var mat in currentRecipe.materials)
        {
            GameManager.instance.RemoveAllTypeIDFromInventory(
                mat.item,
                mat.requiredAmount * currentAmount
            );
        }

        // 2. 完成品をインベントリに追加する
        GameManager.instance.AddAllTypeIDToInventory(currentRecipe.craftedItem, currentAmount);

        // 3. 合成した回数をセーブデータに記録する（回数制限のあるレシピの管理用）
        GameManager.instance.savedata.RecipeData.AddCraftCount(
            currentRecipe.GetItemID(),
            currentAmount
        );

        // 4. 成功SEを再生する
        SEManager.instance.Play(SE_UI.Success1);
        Debug.Log($"{currentRecipe.craftedItem.itemName} を {currentAmount} 個合成しました！");

        // 5. ポップアップを閉じ、消費した状態を反映するためにリストを再生成する
        UIManager.instance.ClosePopup();
        menuManager.ReloadCurrentTab();
    }
    #endregion

    #region IPanelActive 実装
    /// <summary>
    /// IPanelActiveの実装。UIManagerからポップアップが開かれた際に呼ばれます。
    /// 今回はUpdateで直接キー入力を監視しているため、処理は空にしています。
    /// </summary>
    public void SelectFirstButton()
    {
        // UIの標準のボタンコンポーネントを配置する場合は、ここにフォーカス処理を記述します。
    }
    #endregion
}

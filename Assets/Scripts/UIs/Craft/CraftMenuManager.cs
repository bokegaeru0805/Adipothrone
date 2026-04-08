using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 合成メニュー全体を統括するマネージャークラス。
/// データベースからレシピを読み込み、解放状態や回数制限をチェックしてリストを動的に生成します。
/// </summary>
public class CraftMenuManager : MonoBehaviour, IPanelActive
{
    #region UI参照設定
    [Header("UI連携の参照")]
    [Tooltip("右側の詳細情報を表示するコンポーネント")]
    [SerializeField]
    private CraftDetailView detailView;

    [Tooltip("合成個数を決定するポップアップ画面")]
    [SerializeField]
    private CraftConfirmPopup confirmPopup;

    [Tooltip("タブ切り替えを管理するコントローラー")]
    [SerializeField]
    private TabPanelController tabPanelController;
    #endregion

    #region タブコンテナとプレハブ設定
    [Header("リスト生成先のコンテナ(ScrollRectのContent等)")]
    [Tooltip("レシピリストを並べる単一の親オブジェクト")]
    [SerializeField]
    private Transform recipeListContainer;

    [Header("生成するプレハブ")]
    [Tooltip("リストに並べる1つ分のボタンUIのプレハブ")]
    [SerializeField]
    private GameObject recipeButtonPrefab;
    #endregion

    #region 内部変数
    // 動的に生成したボタンを追跡し、再生成時に削除するためのリスト
    private List<GameObject> instantiatedButtons = new List<GameObject>();
    private int currentTabIndex = 0; // 現在選択されているタブのインデックスを保持
    #endregion

    #region ライフサイクル

    private void Awake()
    {
        HideDetailView(); // 最初は詳細ビューを非表示にしておく
    }

    /// <summary>
    /// このパネルがアクティブ（表示状態）になった瞬間に呼ばれます。
    /// タブコントローラーのイベントを購読し、直後に発行される更新通知を待ち受けます。
    /// </summary>
    private void OnEnable()
    {
        if (tabPanelController != null)
        {
            tabPanelController.OnTabChanged += ReloadList;
        }

        ReloadList(0); // 最初のタブ（全アイテム）を表示するために、タブインデックス0でリストを生成
    }

    /// <summary>
    /// このパネルが非アクティブ（非表示状態）になった瞬間に呼ばれます。
    /// 裏で無駄な更新処理が走らないよう、イベントの購読を解除します。
    /// </summary>
    private void OnDisable()
    {
        if (tabPanelController != null)
        {
            tabPanelController.OnTabChanged -= ReloadList;
        }
    }
    #endregion

    #region リストの生成・更新処理
    /// <summary>
    /// タブが切り替わった際、または再描画が必要な際に呼ばれ、
    /// 指定されたタブインデックスに基づいてリストを再生成します。
    /// </summary>
    /// <param name="tabIndex">0:全アイテム, 1:回復アイテム, 2:強化アイテム</param>
    public void ReloadList(int tabIndex)
    {
        currentTabIndex = tabIndex; // 現在のタブを記憶

        // 1. 既存のボタンをすべて削除してリストをクリア
        foreach (var btn in instantiatedButtons)
        {
            Destroy(btn);
        }
        instantiatedButtons.Clear();

        // 2. レシピデータベースの取得確認
        var database = GameManager.instance.recipeItemDatabase;
        if (database == null)
        {
            Debug.LogError("GameManagerにRecipeItemDatabaseが設定されていません。");
            return;
        }

        bool isFirstButtonSet = false;

        // 3. データベースの並び順に従ってレシピを1つずつチェックし、ボタンを生成
        foreach (var recipe in database.recipeItems)
        {
            // チェック1: セーブデータ上で解放済み（isUnlocked == true）かどうか
            if (!GameManager.instance.savedata.RecipeData.IsRecipeUnlocked(recipe.GetItemID()))
                continue;

            // チェック2: 合成回数制限に達しているかどうかの判定
            if (!recipe.IsUnlimitedCrafting())
            {
                int craftCount = GameManager.instance.savedata.RecipeData.GetCraftCount(
                    recipe.GetItemID()
                );
                // 最大回数に達している（または超えている）場合はリストから除外して非表示にする
                if (craftCount >= recipe.maxCraftCount)
                    continue;
            }

            // 完成品のEnumIDから、回復や強化などのTypeID（種類）を抽出する
            int typeID = EnumIDUtility.ExtractTypeID(
                EnumIDUtility.ToID(recipe.craftedItem.GetItemID())
            );

            // 現在のタブインデックスに合わせてフィルタリングする
            // 0 = 全アイテムタブ（無条件で追加）
            // 1 = 回復アイテムタブ
            // 2 = 強化アイテムタブ
            // 3 = 弾武器アイテムタブ
            // 4 = 剣武器アイテムタブ
            bool shouldAdd = false;
            if (currentTabIndex == 0)
            {
                shouldAdd = true;
            }
            else if (currentTabIndex == 1 && typeID == (int)TypeID.HealItem)
            {
                shouldAdd = true;
            }
            else if (currentTabIndex == 2 && typeID == (int)TypeID.StatusEnhanceItem)
            {
                shouldAdd = true;
            }
            else if (currentTabIndex == 3 && typeID == (int)TypeID.Shoot)
            {
                shouldAdd = true;
            }
            else if (currentTabIndex == 4 && typeID == (int)TypeID.Blade)
            {
                shouldAdd = true;
            }

            // 条件に合致した場合のみ、単一のコンテナにボタンを追加
            if (shouldAdd)
            {
                CreateRecipeButton(recipe, recipeListContainer, ref isFirstButtonSet);
            }
        }

        // リストが空の場合のダミーボタン生成
        if (instantiatedButtons.Count == 0)
        {
            CreateDummyButton(recipeListContainer, ref isFirstButtonSet);
        }

        // 生成された全ボタンのナビゲーション（上下ループ）を設定
        SetupNavigation();
    }

    /// <summary>
    /// 生成されたボタンの上下ナビゲーションを Explicit で明示的に設定し、ループさせます。
    /// </summary>
    private void SetupNavigation()
    {
        int count = instantiatedButtons.Count;

        // ボタンが0個の場合は設定できないため処理を抜ける
        if (count == 0)
            return;

        // ボタンが1個しかない場合、すべての方向への移動を無効化する
        if (count == 1)
        {
            Selectable singleSelectable = instantiatedButtons[0].GetComponent<Selectable>();
            if (singleSelectable != null)
            {
                Navigation nav = new Navigation();
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = null;
                nav.selectOnDown = null;
                nav.selectOnLeft = null;
                nav.selectOnRight = null;
                singleSelectable.navigation = nav;
            }
            return;
        }

        // 2個以上の場合は、既存の上下ループ設定を行う
        for (int i = 0; i < count; i++)
        {
            // 生成したオブジェクトから Selectable コンポーネント（Buttonなど）を取得
            Selectable currentSelectable = instantiatedButtons[i].GetComponent<Selectable>();
            if (currentSelectable == null)
                continue;

            // 新しい Navigation 設定を作成
            Navigation nav = new Navigation();
            nav.mode = Navigation.Mode.Explicit;

            // 上方向：1つ前のボタン（自分が先頭なら、一番後ろのボタンへループさせる）
            int prevIndex = (i - 1 + count) % count;
            nav.selectOnUp = instantiatedButtons[prevIndex].GetComponent<Selectable>();

            // 下方向：1つ後のボタン（自分が末尾なら、一番前のボタンへループさせる）
            int nextIndex = (i + 1) % count;
            nav.selectOnDown = instantiatedButtons[nextIndex].GetComponent<Selectable>();

            // 左右の移動はタブ切り替えなどに使う可能性があるため、一旦ここでは設定しません
            // （必要であれば nav.selectOnLeft = ... などで追加可能です）

            // 構築したナビゲーションを適用
            currentSelectable.navigation = nav;
        }
    }

    /// <summary>
    /// 確認ポップアップなどで合成が完了した後、現在のタブのままリストを更新するためのメソッド
    /// </summary>
    public void ReloadCurrentTab()
    {
        ReloadList(currentTabIndex);
    }

    /// <summary>
    /// 指定されたコンテナの子としてレシピボタンを生成し、初期化します。
    /// </summary>
    /// <param name="recipe">対象のレシピデータ</param>
    /// <param name="container">生成先となる親のTransform</param>
    /// <param name="isFirstButtonSet">最初のボタンが設定されたかどうかの参照フラグ（フォーカス制御用）</param>
    private void CreateRecipeButton(
        RecipeItemData recipe,
        Transform container,
        ref bool isFirstButtonSet
    )
    {
        // 第3引数に false を渡すことで、親(container)のUIレイアウトに正しく従わせる
        GameObject obj = Instantiate(recipeButtonPrefab, container, false);

        // 念のためスケールを1に強制リセット（UI生成時のバグ防止）
        obj.transform.localScale = Vector3.one;

        instantiatedButtons.Add(obj);

        var buttonUI = obj.GetComponent<RecipeButtonUI>();
        if (buttonUI != null)
        {
            buttonUI.Setup(recipe, this);
        }
        else
        {
            Debug.LogError("プレハブに RecipeButtonUI がアタッチされていません！");
        }

        // --- 追加：最初のボタンが生成されたら、それにフォーカスを当てる ---
        if (!isFirstButtonSet)
        {
            // EventSystemを使って、このオブジェクトを選択状態にする
            // ※これにより RecipeButtonUI の OnSelect が自動発火し、右側の詳細も更新されます
            EventSystem.current.SetSelectedGameObject(obj);
            isFirstButtonSet = true;
        }
    }

    /// <summary>
    /// レシピが1つもない場合、既存のプレハブを流用してダミーボタンを生成します。
    /// </summary>
    private void CreateDummyButton(Transform container, ref bool isFirstButtonSet)
    {
        // プレハブをそのまま生成
        GameObject obj = Instantiate(recipeButtonPrefab, container, false);
        obj.transform.localScale = Vector3.one;
        instantiatedButtons.Add(obj);

        var buttonUI = obj.GetComponent<RecipeButtonUI>();
        if (buttonUI != null)
        {
            // ダミー専用のセットアップを呼び出す
            buttonUI.SetupAsDummy();
        }

        // ダミーボタンにフォーカスを当てる（EventSystemが迷子になるのを防ぐ）
        if (!isFirstButtonSet)
        {
            EventSystem.current.SetSelectedGameObject(obj);
            isFirstButtonSet = true;
        }

        // ダミーボタンが選択されたときのために、右側の詳細ビューを非表示にする
        if (detailView != null && detailView.gameObject.activeSelf)
        {
            detailView.gameObject.SetActive(false);
        }
    }

    #endregion

    #region 子UIとの連携処理
    /// <summary>
    /// RecipeButtonUIからフォーカスされた際に呼ばれ、右側の詳細ビューを更新します。
    /// </summary>
    /// <param name="recipe">フォーカスされたレシピ</param>
    /// <param name="maxCraftable">最大合成可能数</param>
    public void UpdateDetailView(RecipeItemData recipe, int maxCraftable)
    {
        // パネルが非表示になっていれば表示する
        if (detailView != null && !detailView.gameObject.activeSelf)
        {
            detailView.gameObject.SetActive(true);
        }

        detailView.UpdateView(recipe, maxCraftable);
    }

    /// <summary>
    /// ダミーボタンがフォーカスされた際などに呼ばれ、右側の詳細ビューを非表示にします。
    /// </summary>
    public void HideDetailView()
    {
        if (detailView != null && detailView.gameObject.activeSelf)
        {
            detailView.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// RecipeButtonUIから決定された際に呼ばれ、個数選択のポップアップ画面を開きます。
    /// </summary>
    /// <param name="recipe">選択されたレシピ</param>
    /// <param name="maxCraftable">最大合成可能数</param>
    public void OpenConfirmPopup(RecipeItemData recipe, int maxCraftable)
    {
        if (confirmPopup == null)
        {
            Debug.LogError(
                "CraftMenuManager: confirmPopup（個数選択ポップアップ）がインスペクターで設定されていません！ヒエラルキーからアタッチしてください。",
                this
            );
            return;
        }

        // 既にポップアップが開いている場合は初期化（個数のリセット等）をスキップし、イベントの二重発火を防ぐ
        if (confirmPopup.gameObject.activeSelf)
        {
            return;
        }

        // ポップアップに「何を」「最大いくつ」合成するかをセット
        confirmPopup.Setup(recipe, maxCraftable, this);

        // UIManagerの機能を使ってポップアップとして画面の最前面に表示
        UIManager.instance.OpenPopup(confirmPopup.gameObject);
    }
    #endregion

    #region IPanelActive 実装
    /// <summary>
    /// IPanelActiveの実装。UIManagerからこのパネルが開かれた際に呼ばれます。
    /// 初期のフォーカス制御をタブコントローラーに委譲します。
    /// </summary>
    public void SelectFirstButton()
    {
        // リストにボタンが1つ以上生成されている場合
        if (instantiatedButtons != null && instantiatedButtons.Count > 0)
        {
            EventSystem.current.SetSelectedGameObject(instantiatedButtons[0]);
        }
        else if (tabPanelController != null)
        {
            // リストが空の場合は、タブボタンなどにフォーカスを逃がす
            tabPanelController.SelectFirstButton();
        }
    }
    #endregion
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#region 基底クラス
/// <summary>
/// 各種アイテムパネルの共通処理を管理する基底クラス
/// </summary>
public abstract class ItemPanelActiveBase : MonoBehaviour, IPanelActive, IPageNavigable
{
    #region 抽象プロパティ
    /// <summary>
    /// 各派生クラスで取得するアイテムタイプを指定する
    /// </summary>
    protected abstract InventoryItemData.ItemType TargetItemType { get; }
    #endregion

    #region シリアライズフィールド
    [Header("アイテム詳細情報のパネル")]
    [SerializeField]
    protected ItemDetailPanel itemDetailPanel = null; //アイテム効果パネルのオブジェクト

    [Header("選択ボタンコンポーネント")]
    [SerializeField]
    protected List<Button> buttonList; //アイテム用選択ボタンのリスト

    [SerializeField]
    protected List<Button> leftSideButtonList; //左側のアイテム用選択ボタンのリスト

    [SerializeField]
    protected List<Button> rightSideButtonList; //右側のアイテム用選択ボタンのリスト
    #endregion

    #region プロパティ
    public List<Button> LeftSideButtons => leftSideButtonList;
    public List<Button> RightSideButtons => rightSideButtonList;

    public int Page
    {
        get => page;
        set => page = value;
    }
    #endregion

    #region 内部変数
    protected int rowCount = 0; // UIの行数
    protected int page = 0; // 現在のページ番号

    protected Enum selectedButtonItemID = null;
    protected Enum preselectedButtonItemID = null;

    // 最後に選択したアイテムのIDと「ボタンの位置」を記憶する変数
    protected int? lastSelectedItemID = null;
    protected int lastSelectedIndex = -1; // -1は未選択を表す

    // 遅延更新用のコルーチンを保持する変数
    protected Coroutine detailUpdateCoroutine = null;

    // プレイヤーが所持しているアイテム情報のリスト
    protected List<ItemEntry> itemList = new List<ItemEntry>();
    #endregion

    #region Unityライフサイクル
    protected virtual void Awake()
    {
        if (itemDetailPanel == null)
        {
            Debug.LogWarning("アイテム効果パネルが設定されていません");
            return;
        }

        if (
            buttonList == null
            || buttonList.Count == 0
            || rightSideButtonList == null
            || rightSideButtonList.Count == 0
            || leftSideButtonList == null
            || leftSideButtonList.Count == 0
        )
        {
            Debug.LogWarning("アイテム選択ボタンが設定されていません");
            return;
        }

        // アイテムの効果表示パネルを非表示化
        itemDetailPanel.gameObject.SetActive(false);
        rowCount = rightSideButtonList.Count; // UIの行数を設定
    }

    protected virtual void OnEnable()
    {
        // 共通のボタン群に「現在アクティブなのは自分だ」と教える
        if (buttonList != null)
        {
            foreach (var btn in buttonList)
            {
                var itemBtn = btn.GetComponent<ItemSelectButton>();
                if (itemBtn != null)
                {
                    itemBtn.RegisterActivePanel(this);
                }
            }
        }

        // パネルが有効化されたときに最初のボタンを選択する
        SelectFirstButton();
    }

    protected virtual void Update()
    {
        // 選択されているアイテムボタンのアイテムIDを取得し、効果説明パネルの文章を変更する
        GetSelectedButtonItemID();
    }

    protected virtual void OnDisable()
    {
        // 閉じる瞬間に選択されていたボタンの位置を保存
        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

        if (currentSelected != null)
        {
            lastSelectedIndex = buttonList.FindIndex(b => b.gameObject == currentSelected);

            // ボタンが特定できた場合のみID保存
            if (lastSelectedIndex != -1)
            {
                var itemInfo = buttonList[lastSelectedIndex].GetComponent<IItemAssignable>();
                if (itemInfo != null && itemInfo.AssignedItemID != null)
                {
                    lastSelectedItemID = EnumIDUtility.ToID(itemInfo.AssignedItemID);
                }
            }
        }
        else
        {
            lastSelectedIndex = -1;
            lastSelectedItemID = null;
        }

        itemDetailPanel.gameObject.SetActive(false);
    }
    #endregion

    #region 共通メソッド
    /// <summary>
    /// ページ番号に応じてアイテムをボタンに割り当てる
    /// </summary>
    public bool TryAssignItemsToPage(int pageNumber, int previousRow, bool moveRight)
    {
        // UIUtilityを使ってボタンへのアイテム割り当てと表示/非表示の切り替えを行う
        // これにより、アイテム数に応じた適切な数のボタンが表示される
        return UIUtility.AssignItemsToButtons(
            buttonList,
            rowCount,
            itemList,
            pageNumber,
            previousRow,
            moveRight
        );
    }

    /// <summary>
    /// 最新の所持アイテムデータを読み込み、リストを更新します。
    /// </summary>
    protected virtual void LoadItemData()
    {
        itemList.Clear();
        if (GameManager.instance.savedata?.ItemInventoryData?.ownedItems != null)
        {
            // 抽象プロパティを利用してアイテムタイプごとのリストを取得
            itemList = GameManager.instance.savedata.ItemInventoryData.GetAllItemByType(
                TargetItemType
            );
            // 所持数0のものは除外（必要に応じて調整）
            itemList = itemList.Where(entry => entry.count > 0).ToList();
        }
    }

    /// <summary>
    /// パネルが開かれた際に、最初に選択状態にするボタンを決定します。
    /// アイテムリストを更新し、ページ整合性を保ちます。
    /// </summary>
    public virtual void SelectFirstButton()
    {
        // 1. 最新の所持アイテムリストを読み込む
        LoadItemData();

        if (itemList.Count == 0)
        {
            // アイテムがない場合は全て非表示にして終了
            foreach (var button in buttonList)
                button.gameObject.SetActive(false);
            itemDetailPanel.gameObject.SetActive(false);
            EventSystem.current.SetSelectedGameObject(null);
            OnSelectFirstButtonFinished();
            return;
        }

        int targetItemIndex = -1;

        // 2. 最後に選択していたアイテムが、現在の所持リストにまだ存在するか探す
        if (lastSelectedItemID.HasValue)
        {
            targetItemIndex = itemList.FindIndex(entry => entry.itemID == lastSelectedItemID.Value);
        }

        // 3. 同じアイテムが見つからない場合（消費・売却済など）、元の「位置」に近いアイテムを探す
        if (targetItemIndex == -1)
        {
            if (lastSelectedIndex != -1 && itemList.Count > 0)
            {
                // 「前回のページ × 1ページあたりの数 + 前回のボタン位置」でグローバルなインデックスを計算
                // これにより、勝手にページ0に戻るのを防ぐ
                int estimatedIndex = (this.page * buttonList.Count) + lastSelectedIndex;

                // 範囲内に収める（アイテムが減ってページが消滅した場合などに対応）
                targetItemIndex = Mathf.Clamp(estimatedIndex, 0, itemList.Count - 1);
            }
            else if (itemList.Count > 0)
            {
                // 位置情報もなければ先頭を選択
                targetItemIndex = 0;
            }
        }

        // 4. ターゲットとなるアイテムの位置から、表示すべき「ページ」と「ボタン位置」を逆算
        int targetPage = 0;
        int targetButtonIndexOnPage = -1;

        if (targetItemIndex != -1)
        {
            targetPage = targetItemIndex / buttonList.Count;
            targetButtonIndexOnPage = targetItemIndex % buttonList.Count;
        }

        // 5. 計算したページを適用して表示更新（ここでボタンのActive切り替えが行われる）
        this.page = targetPage;
        UpdateDisplayedButtons();

        // 6. 計算したボタンを「1フレーム遅延させて」選択状態にする
        if (targetButtonIndexOnPage != -1 && targetButtonIndexOnPage < buttonList.Count)
        {
            StartCoroutine(SelectButtonAfterDelay(buttonList[targetButtonIndexOnPage].gameObject));
        }
        else if (itemList.Count > 0)
        {
            // 万が一非表示の場合は、表示されている最初のボタンを選択
            var firstActive = buttonList.FirstOrDefault(b => b.gameObject.activeInHierarchy);
            if (firstActive != null)
                StartCoroutine(SelectButtonAfterDelay(firstActive.gameObject));
        }
        else
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // サブクラス独自の初期化後処理を呼び出す
        OnSelectFirstButtonFinished();
    }

    /// <summary>
    /// 初期選択が完了した後に呼ばれるフックメソッド。派生クラスで固有の処理（プロンプト非表示など）を記述可能。
    /// </summary>
    protected virtual void OnSelectFirstButtonFinished() { }

    /// <summary>
    /// EventSystemのクリック終了判定との競合を避けるため、1フレーム遅延してボタンを選択状態にします。
    /// </summary>
    protected IEnumerator SelectButtonAfterDelay(GameObject targetButton)
    {
        // EventSystemのクリック処理とUIManagerのUpdateが完全に終わるのを待つ
        yield return new WaitForEndOfFrame();
        yield return null;

        // ボタンが存在し、かつ画面に表示されている場合のみ選択する
        if (targetButton != null && targetButton.activeInHierarchy)
        {
            // 一旦フォーカスをクリアして内部状態をリセットする（EventSystemのスタック回避）
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(targetButton);
        }
    }

    /// <summary>
    /// 現在のページ番号とアイテムリストに基づいて、ボタンの表示/非表示を更新します。
    /// </summary>
    protected virtual void UpdateDisplayedButtons()
    {
        // アイテム詳細パネルの表示切り替え
        if (itemList.Count == 0 && itemDetailPanel.gameObject.activeSelf)
        {
            itemDetailPanel.gameObject.SetActive(false);
        }
        else if (itemList.Count > 0 && !itemDetailPanel.gameObject.activeSelf)
        {
            itemDetailPanel.gameObject.SetActive(true);
        }

        // ボタンへの割り当てを実行（UIUtility側でSetActiveが制御される）
        TryAssignItemsToPage(this.page, 0, false);
    }

    /// <summary>
    /// 選択されているアイテムボタンのアイテムIDを取得し、効果説明パネルの文章を変更する
    /// </summary>
    protected virtual void GetSelectedButtonItemID()
    {
        //所持しているアイテムが0個で、かつエフェクト表示パネルが表示されているとき
        if (itemList.Count == 0)
        {
            if (itemDetailPanel.gameObject.activeSelf)
                itemDetailPanel.gameObject.SetActive(false);
            return;
        }

        //現在選択されているボタンのゲームオブジェクトを取得
        GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
        //選択されているボタンがないなら飛ばす
        if (selectedObj == null)
            return;

        // 現在選択しているボタンがリスト内のものか判定
        var selectedBtn = buttonList.FirstOrDefault(b => b.gameObject == selectedObj);

        if (selectedBtn != null)
        {
            IItemAssignable info = selectedBtn.GetComponent<IItemAssignable>();
            if (info != null)
            {
                //選択されているアイテムのIDを取得する
                selectedButtonItemID = info.AssignedItemID;
            }
            else
            {
                selectedButtonItemID = null; //選択されているアイテムのIDを初期化する
                Debug.LogWarning("ItemSelectButton スクリプトが見つかりませんでした");
            }
        }
        else
        {
            // アイテムボタン以外（サイドボタン等）を選択中の場合は更新しない
            return;
        }

        // 効果説明パネルの文章を変更する（選択アイテムが変わった場合のみ詳細パネルを更新）
        if (preselectedButtonItemID != selectedButtonItemID)
        {
            // IDを更新
            preselectedButtonItemID = selectedButtonItemID;

            if (selectedButtonItemID != null)
            {
                lastSelectedItemID = EnumIDUtility.ToID(selectedButtonItemID); //最後に選択したアイテムのIDを保存

                // 既に走っている更新待ち（コルーチン）があればキャンセルする
                if (detailUpdateCoroutine != null)
                {
                    StopCoroutine(detailUpdateCoroutine);
                }

                // 新しく遅延更新をスタートする
                detailUpdateCoroutine = StartCoroutine(
                    UpdateDetailPanelWithDelay(selectedButtonItemID)
                );
            }
        }
        preselectedButtonItemID = selectedButtonItemID; //前フレームのアイテムIDを設定する
    }

    /// <summary>
    /// 一瞬だけ待機してから詳細パネルを更新するコルーチン（デバウンス処理）
    /// </summary>
    protected IEnumerator UpdateDetailPanelWithDelay(Enum targetItemID)
    {
        // カーソル移動中のブレを無視するための待機時間（0.05秒〜0.1秒程度がおすすめ）
        yield return new WaitForSecondsRealtime(0.05f);

        // 待機後もまだパネルが表示されるべき状態であれば更新
        if (!itemDetailPanel.gameObject.activeSelf)
        {
            itemDetailPanel.gameObject.SetActive(true);
        }

        // アイテムIDに基づいて、効果説明パネルの内容を更新する
        itemDetailPanel.DisplayItemDetails(targetItemID);
    }
    #endregion
}
#endregion

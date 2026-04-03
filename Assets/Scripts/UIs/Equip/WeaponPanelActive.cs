using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WeaponPanelActive : MonoBehaviour, IPanelActive, IPageNavigable
{
    [Header("選択ボタンコンポーネント")]
    [SerializeField]
    private List<Button> buttonList; //武器選択用のボタンのリスト

    [SerializeField]
    private List<Button> leftSideButtonList; //左側のアイテム用選択ボタンのリスト

    [SerializeField]
    private List<Button> rightSideButtonList; //右側のアイテム用選択ボタンのリスト

    [Header("武器詳細情報のパネル")]
    [SerializeField]
    private WeaponDetailPanel weaponDetailPanel;

    [Header("選択する武器の種類")]
    [SerializeField]
    private InventoryWeaponData.WeaponType weaponType;
    private Enum selectedButtonWeaponID = null;
    private Enum preselectedButtonWeaponID = null;
    private int page = 0; //現在のページ番号
    private int rowCount = 1; //UIの行数（例: 5行4列なら rowCount = 5）
    public List<Button> LeftSideButtons => leftSideButtonList;
    public List<Button> RightSideButtons => rightSideButtonList;
    public int Page
    {
        get => page;
        set => page = value;
    }

    // プレイヤーが所持している武器の情報のリスト。
    // 各要素は ItemEntry として、アイテムのID（itemID）とその所持数（count）を保持する。
    private List<ItemEntry> itemList = new List<ItemEntry>();

    private void Awake()
    {
        if (
            buttonList == null
            || buttonList.Count == 0
            || leftSideButtonList == null
            || leftSideButtonList.Count == 0
            || rightSideButtonList == null
            || rightSideButtonList.Count == 0
        )
        {
            Debug.LogError("武器選択ボタンが設定されていません");
            return;
        }

        if (weaponDetailPanel == null)
        {
            Debug.LogError("武器詳細パネルが設定されていません");
            return;
        }

        if (
            weaponType != InventoryWeaponData.WeaponType.shoot
            && weaponType != InventoryWeaponData.WeaponType.blade
        )
        //武器の種類が設定されていない場合
        {
            Debug.LogWarning("武器の種類が設定されていません");
            return;
        }

        switch (weaponType)
        {
            case InventoryWeaponData.WeaponType.shoot:
                weaponDetailPanel.weaponType = InventoryWeaponData.WeaponType.shoot;
                break;
            case InventoryWeaponData.WeaponType.blade:
                weaponDetailPanel.weaponType = InventoryWeaponData.WeaponType.blade;
                break;
        }
    }

    private void Update()
    {
        //選択されている武器ボタンの武器IDを取得し、詳細パネルの内容を変更する
        GetSelectedButtonWeaponID();
    }

    /// <summary>
    /// パネルが開かれた際にUIManagerから呼ばれる初期化・フォーカス処理
    /// 装備中の武器を探し、該当ページを開いてフォーカスを当てます。
    /// </summary>
    public void SelectFirstButton()
    {
        if (buttonList == null || buttonList.Count == 0)
            return;

        // --- 1. 装備中の武器を取得 ---
        var saveData = GameManager.instance.savedata;
        if (saveData == null)
        {
            FallbackSelectFirst();
            return;
        }

        // UI描画用のメンバ変数 itemList に最新の所持リストを代入し、データベースの並び順にソートしておく
        itemList = saveData
            .WeaponInventoryData.GetAllItemByType(weaponType)
            .OrderBy(item => GetWeaponSortIndex(item.itemID))
            .ToList();

        // 万が一、所持武器が0個の場合は既存の初期化（非表示処理）を呼んで安全に終了する
        if (itemList == null || itemList.Count == 0)
        {
            InitializeWeaponButtonUI();
            return;
        }

        // 現在のタブ（weaponType）に対応する装備中武器のリストを取得
        var equippedWeapons = saveData.WeaponEquipmentData.GetAllItemByType(weaponType);

        // 装備中の武器がない場合は先頭を選択
        if (equippedWeapons == null || equippedWeapons.Count == 0)
        {
            FallbackSelectFirst();
            return;
        }

        // ItemEntryのitemIDは int型 なので、EnumIDUtility.FromID() を使って Enum に変換する
        int equippedWeaponIntID = equippedWeapons[0].itemID;
        Enum equippedWeaponID = EnumIDUtility.FromID(equippedWeaponIntID);

        // --- 2. 所持リストから装備中武器が何番目（インデックス）にあるか検索 ---
        var ownedWeapons = saveData.WeaponInventoryData.GetAllItemByType(weaponType);
        int equippedIndex = -1;
        for (int i = 0; i < ownedWeapons.Count; i++)
        {
            // 比較する際は、確実で早い int型 同士（itemID）で一致確認を行う
            if (ownedWeapons[i].itemID == equippedWeaponIntID)
            {
                equippedIndex = i;
                break;
            }
        }

        // 万が一、装備しているはずの武器が所持リストに見つからなかった場合の安全措置
        if (equippedIndex == -1)
        {
            FallbackSelectFirst();
            return;
        }

        // --- 3. ページ番号を計算してUIを構築 ---
        // 1ページあたりのボタン数で割ることで、装備中武器が存在するページ番号を算出
        int targetPage = equippedIndex / buttonList.Count;
        Page = targetPage;

        // ページを描画（TryAssignItemsToPageの引数は既存のコードに合わせてください）
        TryAssignItemsToPage(Page, 0, true);

        // --- 4. 描画されたボタンの中から該当武器を探してフォーカス ---
        foreach (var button in buttonList)
        {
            var weaponButton = button.GetComponent<WeaponSelectButton>();
            if (
                weaponButton != null
                && weaponButton.AssignedItemID != null
                && weaponButton.AssignedItemID.Equals(equippedWeaponID)
            )
            {
                // 見つけたボタンをEventSystemで選択状態にする
                EventSystem.current.SetSelectedGameObject(button.gameObject);

                // 選択が変わったので詳細パネルを更新する処理を呼ぶ
                selectedButtonWeaponID = equippedWeaponID;
                if (weaponDetailPanel != null)
                {
                    weaponDetailPanel.gameObject.SetActive(true);
                    weaponDetailPanel.DisplayNextWeaponDetails(selectedButtonWeaponID);
                }
                return; // 無事にフォーカスできたので処理終了
            }
        }

        // ここまで来て見つからなかった場合の最終安全措置
        FallbackSelectFirst();
    }

    /// <summary>
    /// エラー時や未装備時に、強制的に0ページ目の先頭ボタンを選択する安全措置（フォールバック）
    /// </summary>
    private void FallbackSelectFirst()
    {
        Page = 0;
        TryAssignItemsToPage(0, 0, true); // 0ページ目を強制描画

        if (buttonList.Count > 0 && buttonList[0].gameObject.activeInHierarchy)
        {
            EventSystem.current.SetSelectedGameObject(buttonList[0].gameObject);

            // 詳細パネルの更新
            var firstWeapon = buttonList[0].GetComponent<WeaponSelectButton>();
            if (
                firstWeapon != null
                && firstWeapon.AssignedItemID != null
                && weaponDetailPanel != null
            )
            {
                weaponDetailPanel.gameObject.SetActive(true);
                weaponDetailPanel.DisplayNextWeaponDetails(firstWeapon.AssignedItemID);
            }
        }
    }

    //ページ番号に応じてアイテムをボタンに割り当てる
    public bool TryAssignItemsToPage(int pageNumber, int previousRow, bool moveRight)
    {
        return UIUtility.AssignItemsToButtons(
            buttonList,
            rowCount,
            itemList,
            pageNumber,
            previousRow,
            moveRight
        );
    }

    private void InitializeWeaponButtonUI()
    {
        page = 0; //ページ番号を初期化
        itemList = new List<ItemEntry>(); //所持している武器のリストを初期化

        if (WeaponManager.instance == null)
        {
            Debug.LogWarning("WeaponManagerが設定されていません");
            return;
        }

        if (GameManager.instance?.savedata?.WeaponInventoryData.ownedWeapons != null)
        {
            // 所持中の特定タイプの武器のIDと個数のリストを順番付きで取得し、ソートする
            var unsortedList = GameManager.instance.savedata.WeaponInventoryData.GetAllItemByType(
                weaponType
            );
            itemList = unsortedList.OrderBy(item => GetWeaponSortIndex(item.itemID)).ToList();
        }
        else
        {
            Debug.Log("WeaponInventoryDataが存在しません");
            return;
        }

        //所持している武器の数が0ならば、ボタンを非表示にする
        if (itemList == null || itemList.Count == 0)
        {
            for (int i = 0; i < buttonList.Count; i++)
            {
                // ボタンを非表示にする
                buttonList[i].gameObject.SetActive(false);
            }

            //次の装備武器の効果説明パネルを非表示にする
            if (weaponDetailPanel != null)
            {
                weaponDetailPanel.DisplayNextWeaponDetails(null);
            }

            return; //所持している武器がない場合は何もしない
        }

        //ページ番号に応じてアイテムをボタンに割り当てる
        TryAssignItemsToPage(0, 0, false);
    }

    //選択されている武器ボタンの武器IDを取得し、詳細パネルの内容を変更する
    public void GetSelectedButtonWeaponID()
    {
        //現在選択されているボタンのゲームオブジェクトを取得
        GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
        //選択されているボタンがないなら飛ばす
        if (selectedObj == null)
            return;

        //現在選択しているパネルの武器のIDを取得する
        for (int i = 0; i < buttonList.Count; i++)
        {
            if (buttonList[i].gameObject == selectedObj)
            {
                WeaponSelectButton info = buttonList[i].GetComponent<WeaponSelectButton>();
                if (info != null)
                {
                    selectedButtonWeaponID = info.AssignedItemID; //選択されている武器のIDを取得する
                }
                else
                {
                    selectedButtonWeaponID = null; //選択されている武器のIDを初期化する
                    preselectedButtonWeaponID = selectedButtonWeaponID; //前フレームの武器IDを設定する
                    Debug.LogWarning("WeaponSelectButton スクリプトが見つかりませんでした");
                }
            }
        }

        //効果説明パネルの文章を変更する
        if (preselectedButtonWeaponID != selectedButtonWeaponID)
        {
            if (weaponDetailPanel != null)
            {
                if (!weaponDetailPanel.gameObject.activeSelf)
                {
                    //武器効果パネルを表示する
                    weaponDetailPanel.gameObject.SetActive(true);
                }

                //選択中の武器の詳細を表示する
                weaponDetailPanel.DisplayNextWeaponDetails(selectedButtonWeaponID);
            }
        }

        preselectedButtonWeaponID = selectedButtonWeaponID; //前フレームの武器IDを設定する
    }

    /// <summary>
    /// アイテムID（int）を受け取り、ソート基準となるデータベース上のインデックスを返します。
    /// </summary>
    private int GetWeaponSortIndex(int itemIDInt)
    {
        Enum weaponID = EnumIDUtility.FromID(itemIDInt);

        // ItemDataManagerを経由して、インスペクターで設定されたデータベース上の並び順を取得する
        if (ItemDataManager.instance != null)
        {
            return ItemDataManager.instance.GetWeaponIndexByID(weaponID);
        }

        return int.MaxValue; // マネージャーが存在しない場合は末尾に配置
    }
}

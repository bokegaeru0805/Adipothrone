using System;
using System.Collections;
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

    [Header("武器詳細情報のパネルのGameObject")]
    [SerializeField]
    private GameObject weaponDetailPanel; //武器詳細パネルのオブジェクト

    [Header("選択する武器の種類")]
    [SerializeField]
    private WeaponManager.WeaponType weaponType;
    private Enum selectedButtonWeaponID = null;
    private Enum preselectedButtonWeaponID = null;
    private WeaponDetailPanel weaponDetailPanelScript;
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
            weaponType != WeaponManager.WeaponType.shoot
            && weaponType != WeaponManager.WeaponType.blade
        )
        //武器の種類が設定されていない場合
        {
            Debug.LogWarning("武器の種類が設定されていません");
            return;
        }

        //武器の詳細パネルの初期化
        weaponDetailPanelScript = weaponDetailPanel.GetComponent<WeaponDetailPanel>();
        if (weaponDetailPanelScript != null)
        {
            switch (weaponType)
            {
                case WeaponManager.WeaponType.shoot:
                    weaponDetailPanelScript.weaponType = InventoryWeaponData.WeaponType.shoot;
                    break;
                case WeaponManager.WeaponType.blade:
                    weaponDetailPanelScript.weaponType = InventoryWeaponData.WeaponType.blade;
                    break;
            }
        }
        else
        {
            Debug.LogWarning("武器詳細パネルに適切なスクリプトが設定されていません");
        }
    }

    private void Update()
    {
        //選択されている武器ボタンの武器IDを取得し、詳細パネルの内容を変更する
        GetSelectedButtonWeaponID();
    }

    public void SelectFirstButton()
    {
        InitializeWeaponButtonUI(); //武器ボタンの初期化
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
            // 所持中の特定タイプの武器のIDと個数のリストを順番付きで取得
            if (weaponType == WeaponManager.WeaponType.shoot)
            {
                itemList = GameManager.instance.savedata.WeaponInventoryData.GetAllItemByType(
                    InventoryWeaponData.WeaponType.shoot
                );
            }
            else if (weaponType == WeaponManager.WeaponType.blade)
            {
                itemList = GameManager.instance.savedata.WeaponInventoryData.GetAllItemByType(
                    InventoryWeaponData.WeaponType.blade
                );
            }
            else
            {
                Debug.LogWarning("武器の種類が正しく設定されていません");
                return;
            }
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
            if (weaponDetailPanelScript != null)
            {
                weaponDetailPanelScript.DisplayNextWeaponDetails(null);
            }
            else
            {
                Debug.LogWarning("アイテム効果パネルに適切なスクリプトが設定されていません");
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
            if (!weaponDetailPanel.activeSelf)
            {
                //武器効果パネルを表示する
                weaponDetailPanel.SetActive(true);
            }

            if (weaponDetailPanelScript != null)
            {
                //選択中の武器の詳細を表示する
                weaponDetailPanelScript.DisplayNextWeaponDetails(selectedButtonWeaponID);
            }
            else
            {
                Debug.LogWarning("アイテム効果パネルに適切なスクリプトが設定されていません");
            }
        }

        preselectedButtonWeaponID = selectedButtonWeaponID; //前フレームの武器IDを設定する
    }
}

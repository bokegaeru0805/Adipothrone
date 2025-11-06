using System;
using UnityEngine;

public class QuickItemRegisterPanel : MonoBehaviour
{
    [HideInInspector]
    public Enum itemID;

    [SerializeField]
    private QuickItemPanel quickItemPanel; //ゲーム画面のショートカットパネルのオブジェクト

    // private GameObject buttonYes;
    // private GameObject buttonNo;
    private GameObject lastSelectedObject; //最後に選ばれていたボタンを保存する変数

    private void Awake()
    {
        if (quickItemPanel == null)
        {
            Debug.LogError("QuickItemUIManager: QuickItemPanelが設定されていません");
        }
    }

    private void Update()
    {
        if (quickItemPanel == null)
            return; //クイックアイテムパネルが存在しない場合は何もしない

        if (InputManager.instance.UIMoveLeft())
            Move(-1);
        if (InputManager.instance.UIMoveRight())
            Move(1);
        if (InputManager.instance.UIMoveUp() || InputManager.instance.UIMoveDown())
            MoveVertical();
        if (InputManager.instance.UIConfirm())
            HandleYes();
        if (InputManager.instance.UISelectNo())
            HandleNo();
    }

    // <summary>
    /// 水平方向にカーソルを移動させる（左右）
    /// </summary>
    /// <param name="horizontal">
    /// -1なら左、+1なら右に移動
    /// </param>
    private void Move(int horizontal)
    {
        quickItemPanel.Move(horizontal);
    }

    /// <summary>
    /// 垂直方向にカーソルを移動させる（下に進む）
    /// 現在の行の下の行へ移動。最下行の場合は一番上にループする。
    /// </summary>
    private void MoveVertical()
    {
        quickItemPanel.MoveVertical();
    }

    private void HandleYes()
    {
        PlayerManager.instance?.AssignItemToQuickSlot(itemID, quickItemPanel.currentIndex); //アイテムをクイックスロットに登録
        ClosePanel();
    }

    private void HandleNo()
    {
        ClosePanel();
    }

    private void ClosePanel()
    {
         // オブジェクト名による分岐を、SaveLoadManager経由の呼び出しに変更
        if (SaveLoadManager.CurrentActiveManager != null)
        {
            // 登録されているManagerのCloseTopPanel()を呼び出す
            SaveLoadManager.CurrentActiveManager.CloseTopPanel();
        }
        else
        {
            // CurrentActiveManager が見つからなかった場合 (各ManagerのAwakeで登録し忘れている可能性)
            Debug.LogWarning(
                "SaveLoadPromptButton: SaveLoadManager.CurrentActiveManager が設定されていません。データ変更確認画面を閉じることができません。",
                this
            );
            this.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        UIManager.instance.SetQuickItemRegistering(true); //クイックアイテム登録画面が開いているフラグを立てる
    }

    private void OnDisable()
    {
        UIManager.instance.SetQuickItemRegistering(false); //クイックアイテム登録画面が開いているフラグを下げる
    }
}

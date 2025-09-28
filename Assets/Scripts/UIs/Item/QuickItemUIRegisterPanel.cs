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
        if (this.gameObject.name.Contains("_Menu"))
        {
            if (UIManager.instance != null)
            {
                UIManager.instance.CloseTopPanel();
            }
            else
            {
                Debug.LogWarning("UIManagerが存在しません");
            }
        }
        else if (this.gameObject.name.Contains("_Title"))
        {
            if (TitleUIManager.instance != null)
            {
                TitleUIManager.instance.CloseTopPanel();
            }
            else
            {
                Debug.LogWarning("TitleUIManagerが存在しません");
            }
        }
        else if (this.gameObject.name.Contains("_GameOver"))
        {
            if (GameOverUIManager.instance != null)
            {
                GameOverUIManager.instance.CloseTopPanel();
            }
            else
            {
                Debug.LogWarning("GameOverUIManagerが存在しません");
            }
        }
        else
        {
            this.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        quickItemPanel.currentIndex = 0; //カーソルの位置を初期化
        UIManager.instance.SetQuickItemRegistering(true); //クイックアイテム登録画面が開いているフラグを立てる
    }

    private void OnDisable()
    {
        UIManager.instance.SetQuickItemRegistering(false); //クイックアイテム登録画面が開いているフラグを下げる
        // quickItemPanel.currentIndex = 0; //カーソルの位置を初期化
    }
}

using System;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.UI;

public class ItemUsePromptButton : MonoBehaviour
{
    private PlayerManager playerManager; // プレイヤーマネージャーの参照

    [HideInInspector]
    public Enum itemID;
    private GameObject dataPromptWindow;

    [SerializeField]
    private PromptType promptType;

    [SerializeField,ShowIf(nameof(promptType), PromptType.Register)] //Registerボタンの場合のみ表示
    private GameObject ItemRegisterPromptPanel;

    private enum PromptType
    {
        None = 0,
        Yes = 1,
        Register = 2,
        No = 3,
    }

    public void SetItemID(Enum num) => itemID = num;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnPromptSelected);
        dataPromptWindow = this.transform.parent.gameObject;

        if (promptType == PromptType.None)
        {
            Debug.LogError(
                $"{this.gameObject.name}: PromptType が None に設定されています。適切な値に変更してください。",
                this
            );
        }
        else if (promptType == PromptType.Yes)
        {
            playerManager = PlayerManager.instance;
            if (playerManager == null)
            {
                Debug.LogWarning("PlayerManagerが存在しません。アイテム使用の確認ができません。");
                return;
            }
        }
    }

    private void OnPromptSelected()
    {
        if (promptType == PromptType.Yes)
        {
            HandleYes();
        }
        else if (promptType == PromptType.Register)
        {
            HandleRegister();
        }
        else if (promptType == PromptType.No)
        {
            HandleNo();
        }
    }

    private void HandleYes()
    {
        playerManager.UseHealItem(itemID);
        ClosePanel();
    }

    private void HandleNo()
    {
        ClosePanel();
    }

    private void HandleRegister()
    {
        ClosePanel();

        if (ItemRegisterPromptPanel != null)
        {
            QuickItemRegisterPanel script =
                ItemRegisterPromptPanel.GetComponent<QuickItemRegisterPanel>();
            if (script != null)
            {
                script.itemID = itemID;
            }
            UIManager.instance.OpenPanel(ItemRegisterPromptPanel, -1);
        }
        else
        {
            Debug.LogWarning("ItemRegisterPromptPanelが存在しません");
        }
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
            dataPromptWindow.SetActive(false);
        }
    }
}

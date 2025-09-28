using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemUsePromptButton : MonoBehaviour
{
    private PlayerManager playerManager; // プレイヤーマネージャーの参照

    [HideInInspector]
    public Enum itemID;
    private GameObject datePromptWindow;

    [SerializeField]
    private GameObject ItemRegisterPromptPanel;
    private PromptType promptType;

    private enum PromptType
    {
        Yes,
        Register,
        No,
    }

    public void SetItemID(Enum num) => itemID = num;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnPromptSelected);
        datePromptWindow = this.transform.parent.gameObject;

        if (this.gameObject.name.Contains("Yes"))
        {
            promptType = PromptType.Yes;
            playerManager = PlayerManager.instance;
            if (playerManager == null)
            {
                Debug.LogWarning("PlayerManagerが存在しません。アイテム使用の確認ができません。");
                return;
            }
        }
        else if (this.gameObject.name.Contains("Register"))
        {
            promptType = PromptType.Register;
        }
        else
        {
            promptType = PromptType.No;
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
            datePromptWindow.SetActive(false);
        }
    }
}

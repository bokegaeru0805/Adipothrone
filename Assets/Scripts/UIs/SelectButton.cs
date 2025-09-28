using UnityEngine;
using UnityEngine.UI;

public class SelectButton : MonoBehaviour
{
    [SerializeField, Tooltip("表示するパネル")]
    private GameObject EnablePanel;

    [SerializeField]
    private int panelStage;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(ActivePanel);
    }

    private void ActivePanel()
    {
        if (EnablePanel != null)
        {
            if (this.gameObject.name.Contains("_Menu") && UIManager.instance != null)
            {
                UIManager.instance.OpenPanel(EnablePanel, panelStage);
            }
            else if (this.gameObject.name.Contains("_Title") && TitleUIManager.instance != null)
            {
                TitleUIManager.instance.OpenPanel(EnablePanel, panelStage);
            }
            else if (
                this.gameObject.name.Contains("_GameOver")
                && GameOverUIManager.instance != null
            )
            {
                GameOverUIManager.instance.OpenPanel(EnablePanel, panelStage);
            }
            else
            {
                EnablePanel.SetActive(true);
            }

            // 最後に押されたボタンを親パネルに記録させる
            var parentPanelManager = transform.parent.GetComponentInParent<PanelActive>();
            if (parentPanelManager != null)
            {
                parentPanelManager.SetLastSelectedButton(this.gameObject);
            }
        }
        else
        {
            Debug.LogWarning($"{this.gameObject.name}は表示するパネルを持っていません");
        }
    }
}

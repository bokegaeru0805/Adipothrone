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
            // 現在アクティブなManager（UIManagerなど）を取得
            var activeManager = SaveLoadManager.CurrentActiveManager;

            if (activeManager != null)
            {
                // 取得したManagerのOpenPanel()を呼び出す
                activeManager.OpenPanel(EnablePanel, panelStage);
            }
            else
            {
                // Managerが見つからなかった場合のフォールバック処理
                Debug.LogWarning(
                    "SaveLoadFileButton: SaveLoadManager.CurrentActiveManager が設定されていません。"
                        + "OpenPanel() を実行できませんでした。",
                    this
                );
                EnablePanel.SetActive(true); // 従来の非表示パネルを表示するだけの動作
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

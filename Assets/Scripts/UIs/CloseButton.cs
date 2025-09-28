using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CloseButton : MonoBehaviour
{
    [SerializeField, Tooltip("非表示する親パネル")]
    private GameObject DisablePanel;

    private void Start()
    {
        Button button = this.GetComponent<Button>(); //自分のボタンのコンポーネントを取得
        button.onClick.AddListener(HidePanel);
    }

    private void HidePanel()
    {
        if (DisablePanel != null)
        {
            if (this.gameObject.name.Contains("_Menu") && UIManager.instance != null)
            {
                UIManager.instance.CloseTopPanel();
            }
            else if (this.gameObject.name.Contains("_Title") && TitleUIManager.instance != null)
            {
                TitleUIManager.instance.CloseTopPanel();
            }
            else if (
                this.gameObject.name.Contains("_GameOver")
                && GameOverUIManager.instance != null
            )
            {
                GameOverUIManager.instance.CloseTopPanel();
            }
            else
            {
                DisablePanel.SetActive(false);
            }
            // ❗重要：
            // パネルを非表示にした直後に、次に選択状態にしたいUIオブジェクトを明示的に指定する。
            // これは「PanelのOnDisable()などで自動化」するよりも、確実で柔軟な制御が可能。
            // CloseButtonから処理することで、どのボタンで閉じたかによって遷移先を変えることもできる。
        }
        else
        {
            Debug.LogWarning($"{this.gameObject.name}は非表示対象のパネルを持っていません");
        }
    }
}

using UnityEngine;
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
                DisablePanel.SetActive(false);
            }
        }
        else
        {
            Debug.LogWarning($"{this.gameObject.name}は非表示対象のパネルを持っていません");
        }
    }
}

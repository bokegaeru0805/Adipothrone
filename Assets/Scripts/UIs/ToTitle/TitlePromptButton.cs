using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitlePromptButton : MonoBehaviour
{
    public enum PromptType
    {
        None = 0,
        Yes = 10,
        No = 20,
    }

    [SerializeField]
    private PromptType promptType;
    private GameObject TitlePromptPanel;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnPromptSelected);

        if (promptType == PromptType.No)
        {
            TitlePromptPanel = this.transform.parent.gameObject;
            if (TitlePromptPanel == null)
            {
                Debug.LogWarning("TitlePromptのNoボタンはTitlePromptPanelを取得できませんでした");
            }
        }

        switch (promptType)
        {
            case PromptType.Yes:
                if (
                    !this.gameObject.name.Contains("Yes")
                    && !this.gameObject.name.Contains("_GameOver")
                )
                {
                    Debug.LogWarning(
                        $"{this.gameObject.name}はpromptTypeが間違っている可能性があります"
                    );
                }
                break;
            case PromptType.No:
                if (!this.gameObject.name.Contains("No"))
                {
                    Debug.LogWarning(
                        $"{this.gameObject.name}はpromptTypeが間違っている可能性があります"
                    );
                }
                break;
        }
    }

    private void Update()
    {
        switch (promptType)
        {
            case PromptType.Yes:
                if (
                    InputManager.instance.UISelectYes()
                    && this.gameObject.activeSelf
                    && !this.gameObject.name.Contains("_GameOver")
                )
                {
                    HandleYes();
                }
                break;
            case PromptType.No:
                if (InputManager.instance.UISelectNo() && this.gameObject.activeSelf)
                {
                    HandleNo();
                }
                break;
        }
    }

    private void OnPromptSelected()
    {
        if (promptType == PromptType.Yes)
        {
            HandleYes();
        }
        else
        {
            HandleNo();
        }
    }

    private void HandleYes()
    {
        BGMManager.instance?.GetComponent<BGMManager>().Stop(); //BGMを停止
        SEManager.instance?.GetComponent<SEManager>().StopAllSE(); //全てのSEを停止
        SaveLoadManager.instance.DisableSave(); //セーブできないようにする
        SceneManager.LoadScene(GameConstants.SCENE_NAME_TITLE); //Titleシーンに戻る
    }

    private void HandleNo()
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
            TitlePromptPanel.SetActive(false);
        }
    }
}

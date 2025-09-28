using UnityEngine;
using UnityEngine.UI;

public class SaveLoadFileButton : MonoBehaviour
{
    [HideInInspector]
    public int FileNumber;

    [Header("親パネルのPanelActiveスクリプト")]
    [SerializeField]
    private PanelActive parentPanelActive;

    [Header("データ変更確認ウィンドウ")]
    [SerializeField]
    private GameObject datePromptWindow;

    [Header("データ変更Yesボタン")]
    [SerializeField]
    private SaveLoadPromptButton yesPromptButton;
    private SaveLoadPromptTextDisplay promptHandler;

    private void Start()
    {
        this.GetComponent<Button>().onClick.AddListener(OnFileClicked);

        if (parentPanelActive == null)
        {
            Debug.LogError($"{this.name}は親パネルのPanelActiveが設定されていません。");
            return;
        }

        if (datePromptWindow == null)
        {
            Debug.LogError($"{this.name}はDataPromptWindowが設定されていません。");
            return;
        }
        else
        {
            promptHandler = datePromptWindow.GetComponent<SaveLoadPromptTextDisplay>();
            if (promptHandler == null)
            {
                Debug.LogError($"{this.name}はSaveLoadPromptTextDisplayが設定されていません。");
                return;
            }
        }

        if (yesPromptButton == null)
        {
            Debug.LogError($"{this.name}はYesPromptButtonが設定されていません。");
            return;
        }
    }

    private void OnFileClicked()
    {
        if (!SaveLoadManager.isDataPrompting && !SaveLoadManager.isOnSave)
        {
            if (
                SaveLoadManager.FilePlaytime[FileNumber] == 0
                && SaveLoadManager.instance.CurrentSaveLoadMode == SaveLoadManager.SaveLoadMode.Load
            )
            {
                SEManager.instance?.PlayUISE(SE_UI.Beep1);
            }
            else
            {
                //データ変更画面を表示するフラグをONにする
                SaveLoadManager.isDataPrompting = true;

                //データ変更画面が表示されているなら
                if (datePromptWindow != null)
                {
                    if (this.gameObject.name.Contains("_Menu") && UIManager.instance != null)
                    {
                        UIManager.instance.OpenPanel(datePromptWindow, -1);
                        //データ変更確認パネルを表示
                    }
                    else if (
                        this.gameObject.name.Contains("_Title")
                        && TitleUIManager.instance != null
                    )
                    {
                        TitleUIManager.instance.OpenPanel(datePromptWindow, -1);
                        //データ変更確認パネルを表示
                    }
                    else if (
                        this.gameObject.name.Contains("_GameOver")
                        && GameOverUIManager.instance != null
                    )
                    {
                        GameOverUIManager.instance.OpenPanel(datePromptWindow, -1);
                        //データ変更確認パネルを表示
                    }
                    else
                    {
                        datePromptWindow.SetActive(true);
                        //データ変更確認パネルを表示
                    }
                }

                promptHandler.SetPromptText(FileNumber); //データ変更確認パネルの文章を変更

                yesPromptButton.SetFileNumber(FileNumber); //データ変更確認パネルのYesボタンのファイルナンバーを変更
            }

            // 最後に押されたボタンを親パネルに記録させる
            parentPanelActive.SetLastSelectedButton(this.gameObject);
        }
    }
}

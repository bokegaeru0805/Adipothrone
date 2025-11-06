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
                SaveLoadManager.FileSlotInfos.ContainsKey(FileNumber) //セーブファイル情報が存在し、
                && SaveLoadManager.FileSlotInfos[FileNumber].playTime == 0f //かつプレイ時間が0なら
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
                    // 現在アクティブなManager（UIManagerなど）を取得
                    var activeManager = SaveLoadManager.CurrentActiveManager;

                    if (activeManager != null)
                    {
                        // 取得したManagerのOpenPanel()を呼び出す
                        activeManager.OpenPanel(datePromptWindow, -1);
                    }
                    else
                    {
                        // Managerが見つからなかった場合のフォールバック処理
                        Debug.LogWarning(
                            "SaveLoadFileButton: SaveLoadManager.CurrentActiveManager が設定されていません。"
                            + "OpenPanel() を実行できませんでした。",
                            this
                        );
                        datePromptWindow.SetActive(true); // 従来の非表示パネルを表示するだけの動作
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
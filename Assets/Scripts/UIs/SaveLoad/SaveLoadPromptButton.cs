using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SaveLoadPromptButton : MonoBehaviour
{
    public enum PromptType
    {
        Yes,
        No,
    }

    private PromptType promptType;
    private int fileNumber;
    private GameObject dataPromptWindow;

    [Header("Yesボタンのみが必要とする")]
    [SerializeField]
    private GameObject TopFile;

    [SerializeField]
    private GameObject MiddleFile;

    [SerializeField]
    private GameObject BottomFile;

    public void SetFileNumber(int num) => fileNumber = num;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnPromptSelected); //Buttonに関数を設定
        dataPromptWindow = this.transform.parent.gameObject; //データ変更確認画面オブジェクトを取得

        if (this.gameObject.name.Contains("Yes"))
        {
            promptType = PromptType.Yes;

            if (!TopFile.name.Contains("File_Top"))
            {
                Debug.LogWarning("間違えたFile_Topを取得した可能性があります");
            }

            if (!MiddleFile.name.Contains("File_Middle"))
            {
                Debug.LogWarning("間違えたFile_Middleを取得した可能性があります");
            }

            if (!BottomFile.name.Contains("File_Bottom"))
            {
                Debug.LogWarning("間違えたFile_Bottomを取得した可能性があります");
            }
        }
        else if (this.gameObject.name.Contains("No"))
        {
            promptType = PromptType.No;
        }
        else
        {
            Debug.LogWarning(
                $"{this.gameObject.name}はSaveLoadPromptButtonをつける必要がない可能性があります"
            );
        }
    }

    private void Update()
    {
        switch (promptType)
        {
            case PromptType.Yes:
                if (InputManager.instance.UISelectYes() && this.gameObject.activeSelf)
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
        if (SaveLoadManager.isOnSave || SaveLoadManager.instance == null)
            return;

        SaveLoadManager.instance.Settings.lastUsedSlotIndex = fileNumber; //最後に選択したファイル番号を保存する
        SaveLoadManager.instance.SaveSettings(); //設定を保存する

        var currentSaveLoadMode = SaveLoadManager.instance.CurrentSaveLoadMode;

        // セーブの場合の処理
        if (currentSaveLoadMode == SaveLoadManager.SaveLoadMode.Save)
        {
            SaveLoadManager.isOnSave = true; //セーブ中のフラグをONにする

            // プロンプトの文章を変更
            var textDisplay = dataPromptWindow.GetComponentInChildren<TextMeshProUGUI>();
            if (textDisplay != null)
                textDisplay.text = "Fileにセーブ中";

            // プレイ時間を記録
            SaveLoadManager.FilePlaytime[fileNumber] =
                SaveLoadManager.StartTime + Time.time - SaveLoadManager.timeSinceLoad;

            EventSystem.current.SetSelectedGameObject(null); //一時的にButtonを何も選ばせないようにする
            SaveLoadManager.instance.StartCoroutine(SaveLoadManager.instance.SaveLoad(fileNumber)); //セーブ処理を呼び出す

            // セーブ処理の非同期呼び出し
            StartCoroutine(WaitUntilSaveCompleted());
        }
        // ロードの場合の処理
        else if (currentSaveLoadMode == SaveLoadManager.SaveLoadMode.Load)
        {
            if (SaveLoadManager.instance != null)
            {
                EventSystem.current.SetSelectedGameObject(null); //一時的にButtonを何も選ばせないようにする
                SaveLoadManager.instance.StartCoroutine(
                    SaveLoadManager.instance.SaveLoad(fileNumber)
                ); //ロード処理を行う
            }
            else
            {
                Debug.LogWarning("SaveLoadManagerが存在しません");
            }
            SaveLoadManager.isDataPrompting = false; //データ変更画面が開いているかのフラグをOFFにする
        }
        else
        {
            Debug.LogError("SaveLoadManagerのセーブロード状態が不明です");
        }
    }

    private void HandleNo()
    {
        ClosePanel();
        SaveLoadManager.isDataPrompting = false; //データ変更画面が開いているかのフラグをOFFにする
    }

    private IEnumerator WaitUntilSaveCompleted()
    {
        // セーブが終わるまで待つ
        yield return new WaitUntil(() => !SaveLoadManager.isOnSave);

        var textDisplay = dataPromptWindow.GetComponentInChildren<TextMeshProUGUI>();
        if (textDisplay != null)
            textDisplay.text = "Fileにセーブ完了";

        SEManager.instance?.PlayUISE(SE_UI.Complete1); // 完了音を再生

        yield return new WaitForSecondsRealtime(0.5f); //セーブ完了をプレイヤーに確認させる時間

        // UI更新（ファイルの表示）
        GameObject fileText = null;

        // 3つのファイルボタンを配列に格納してループ処理
        var fileButtons = new[] { TopFile, MiddleFile, BottomFile };
        foreach (var fileButtonObject in fileButtons)
        {
            // 各ボタンからSaveLoadFileButtonコンポーネントを取得
            var saveLoadFileButton = fileButtonObject.GetComponent<SaveLoadFileButton>();
            if (saveLoadFileButton != null)
            {
                // 現在処理中のファイル番号と、ボタンが持つFileNumberが一致するか確認
                if (saveLoadFileButton.FileNumber == this.fileNumber)
                {
                    // 一致したら、そのボタンの子オブジェクト（テキスト）を取得してループを抜ける
                    fileText = fileButtonObject.transform.GetChild(0).gameObject;
                    break;
                }
            }
        }

        if (fileText != null)
        {
            //ファイルにプレイ時間を書き込む
            var tmp = fileText.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text =
                    "File"
                    + fileNumber
                    + "\nプレイ時間 "
                    + Mathf.FloorToInt(SaveLoadManager.FilePlaytime[fileNumber] / 3600)
                    + ":"
                    + Mathf.FloorToInt((SaveLoadManager.FilePlaytime[fileNumber] % 3600) / 60);
            }
        }
        else
        {
            Debug.LogWarning($"{fileNumber}のスロットのTextを取得できませんでした");
        }

        ClosePanel();
        SaveLoadManager.isDataPrompting = false; //データ変更画面が開いているかのフラグをOFFにする
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
            dataPromptWindow.SetActive(false);
        }
    }
}

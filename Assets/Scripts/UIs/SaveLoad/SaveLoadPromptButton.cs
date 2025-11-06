using System.Collections;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SaveLoadPromptButton : MonoBehaviour
{
    public enum PromptType
    {
        None = 0,
        Yes = 1,
        No = 2,
    }

    private int fileNumber;
    private GameObject dataPromptWindow;

    [SerializeField]
    private PromptType promptType;

    [SerializeField, ShowIf(nameof(promptType), PromptType.Yes)] //Yesボタンの場合のみ表示
    private SaveLoadPanelActive saveLoadPanelActive;

    public void SetFileNumber(int num) => fileNumber = num;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnPromptSelected); //Buttonに関数を設定
        dataPromptWindow = this.transform.parent.gameObject; //データ変更確認画面オブジェクトを取得

        if (promptType == PromptType.None)
        {
            Debug.LogError(
                $"{this.gameObject.name}: PromptType が None に設定されています。適切な値に変更してください。",
                this
            );
        }
        else if (promptType == PromptType.Yes)
        {
            // TopFileなどのチェックを削除し、saveLoadPanelActiveのチェックに変更
            if (saveLoadPanelActive == null)
            {
                Debug.LogError(
                    $"{this.gameObject.name}: Yesボタンには SaveLoadPanelActive の参照が必須です。",
                    this
                );
            }
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
            SaveLoadManager.FileSlotInfos[fileNumber].playTime =
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

        // 1. saveLoadPanelActive が参照設定されているか確認
        if (saveLoadPanelActive == null)
        {
            Debug.LogError("saveLoadPanelActive が設定されていないため、UIの更新ができません。");
        }
        else
        {
            // 2. 更新対象のGameObjectを特定する
            //    saveLoadPanelActive が持つ fileSlots リストから、
            //    現在セーブした fileNumber を持つボタン（GameObject）を探す
            GameObject targetSlotObject = saveLoadPanelActive.GetFileSlotUI(fileNumber)?.slotObject;

            if (targetSlotObject != null)
            {
                // 3. 特定したGameObjectとファイル番号を渡し、表示更新を依頼する
                saveLoadPanelActive.UpdateFileSlotDisplay(targetSlotObject, fileNumber);
            }
            else
            {
                Debug.LogWarning(
                    $"{fileNumber}を持つスロットが SaveLoadPanelActive に見つかりませんでした。"
                );
            }
        }

        ClosePanel();
        SaveLoadManager.isDataPrompting = false; //データ変更画面が開いているかのフラグをOFFにする
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

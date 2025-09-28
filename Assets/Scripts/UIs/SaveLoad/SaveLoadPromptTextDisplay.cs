using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class SaveLoadPromptTextDisplay : MonoBehaviour
{
    [Header("セーブデータ確認画面")]
    [
        SerializeField,
        Tooltip("セーブデータ確認画面のテキストを表示するTextMeshProUGUIコンポーネント")
    ]
    private TextMeshProUGUI promptText;

    [SerializeField]
    private GameObject dataPrompt_Yes;

    [SerializeField]
    private GameObject dataPrompt_No;
    private GameObject lastSelectedObject;

    private void Awake()
    {
        if (promptText == null || dataPrompt_Yes == null || dataPrompt_No == null)
        {
            Debug.LogWarning("セーブデータ確認画面のUIコンポーネントが設定されていません");
            return;
        }

        lastSelectedObject = dataPrompt_Yes;
    }

    public void SetPromptText(int fileNumber)
    {
        var currentMode = SaveLoadManager.instance.CurrentSaveLoadMode;
        
        if (currentMode == SaveLoadManager.SaveLoadMode.Save)
        {
            promptText.text = $"File{fileNumber}にセーブしますか？";
        }
        else if (currentMode == SaveLoadManager.SaveLoadMode.Load)
        {
            promptText.text = $"File{fileNumber}をロードしますか？";
        }
    }

    public void Update()
    {
        if (SaveLoadManager.isDataPrompting)
        {
            //データ管理確認画面が出ているとき
            GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
            if (!new[] { dataPrompt_Yes, dataPrompt_No }.Contains(selectedObject))
            {
                EventSystem.current.SetSelectedGameObject(lastSelectedObject);
            }
            lastSelectedObject = EventSystem.current.currentSelectedGameObject;
        }
    }
}

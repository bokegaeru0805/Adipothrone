using UnityEngine;
using UnityEngine.EventSystems;

public class PanelActive : MonoBehaviour, IPanelActive
{
    [SerializeField, Tooltip("最初の選択ボタン")]
    private GameObject firstSelected;

    [
        SerializeField,
        Tooltip("非アクティブ時に最後の選択状態をリセットし、初期のボタンに戻すかどうか")
    ]
    private bool resetOnDisable = false;

    private GameObject defaultFirstSelected; // 初期の選択ボタンを記憶するための変数

    private void Awake()
    {
        // インスペクターで設定された初期のボタンを記憶しておく
        defaultFirstSelected = firstSelected;
    }

    private void OnDisable()
    {
        // フラグがtrueの場合、非アクティブ時に初期のボタンに戻す
        if (resetOnDisable)
        {
            firstSelected = defaultFirstSelected;
        }
    }

    public void SelectFirstButton()
    {
        if (firstSelected != null)
        {
            EventSystem.current.SetSelectedGameObject(firstSelected);
        }
        else
        {
            Debug.LogWarning($"{this.gameObject.name}は最初の選択ボタンを持っていません");
        }
    }

    public void SetLastSelectedButton(GameObject button)
    {
        firstSelected = button;
    }
}

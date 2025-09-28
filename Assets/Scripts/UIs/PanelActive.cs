using UnityEngine;
using UnityEngine.EventSystems;

public class PanelActive : MonoBehaviour, IPanelActive
{
    [SerializeField, Tooltip("最初の選択ボタン")]
    private GameObject firstSelected;

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

using UnityEngine;

public class MenuCanvas : MonoBehaviour
{
    private void Start()
    {
        HideAllChildren();
        this.gameObject.SetActive(false);
    }

    /// <summary>
    /// 全ての子オブジェクトを非表示にする
    /// </summary>
    private void HideAllChildren()
    {
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(false);
        }
    }
}

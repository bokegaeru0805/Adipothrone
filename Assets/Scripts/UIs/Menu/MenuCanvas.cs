using UnityEngine;

public class MenuCanvas : MonoBehaviour
{
    private void Awake()
    {
        HideAllChildren();
    }

    /// <summary>
    /// エディタ編集時に表示状態のままになっている、全ての子オブジェクト（各種メニューUI）を非表示にします。
    /// ゲーム開始時に手動で非表示にする手間を省き、初期状態をクリアにするための関数です。
    /// </summary>
    private void HideAllChildren()
    {
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(false);
        }
    }
}

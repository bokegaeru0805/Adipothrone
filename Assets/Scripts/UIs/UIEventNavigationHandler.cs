using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// カスタム入力（旧InputSystemのInputManager）に基づいて、UI選択操作（上下左右＋決定）を手動で制御するコンポーネント
/// ※ StandaloneInputModuleのWASD等は無効化し、このスクリプトに置き換える
/// </summary>
public class UIEventNavigationHandler : MonoBehaviour
{
    private void Update()
    {
        // 現在選択中の UI オブジェクトを取得（nullの可能性あり）
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null)
            return;

        // UIオブジェクトに Selectable コンポーネントがあるか確認（Button, Toggle, 等）
        Selectable current = selected.GetComponent<Selectable>();
        if (current == null)
            return;

        // 上下左右のカスタム入力に応じて、対応するUI要素を探し、選択を切り替える
        if (InputManager.instance.UIMoveUp())
        {
            TryMoveTo(current.FindSelectableOnUp());
        }
        else if (InputManager.instance.UIMoveDown())
        {
            TryMoveTo(current.FindSelectableOnDown());
        }
        else if (InputManager.instance.UIMoveLeft())
        {
            TryMoveTo(current.FindSelectableOnLeft());
        }
        else if (InputManager.instance.UIMoveRight())
        {
            TryMoveTo(current.FindSelectableOnRight());
        }

        // 決定ボタンが押された場合、現在の選択オブジェクトに「Submit」イベントを送信
        if (InputManager.instance.UIConfirm())
        {
            // 選択中オブジェクトに "submit" イベントを送る（Buttonなどが反応）
            ExecuteEvents.Execute(
                current.gameObject,
                new BaseEventData(EventSystem.current),
                ExecuteEvents.submitHandler
            );
        }
    }

    /// <summary>
    /// 指定された Selectable に移動する。ただし非表示または非アクティブの場合は無視
    /// </summary>
    /// <param name="target">移動先の UI 要素</param>
    private void TryMoveTo(Selectable target)
    {
        if (target == null)
            return;

        GameObject targetGO = target.gameObject;

        // 非表示または非アクティブなオブジェクトには移動しない
        if (!targetGO.activeInHierarchy || !target.interactable)
            return;

        EventSystem.current.SetSelectedGameObject(targetGO);
    }
}
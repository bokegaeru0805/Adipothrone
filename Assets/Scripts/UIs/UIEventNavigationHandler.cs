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

        // UIオブジェクトに Selectable コンポーネントがあるか確認（Button, Toggle, Slider等）
        Selectable current = selected.GetComponent<Selectable>();
        if (current == null)
            return;

        // 方向キーの入力判定
        if (InputManager.instance.UIMoveUp())
        {
            HandleMoveOrNavigate(current, MoveDirection.Up, current.FindSelectableOnUp());
        }
        else if (InputManager.instance.UIMoveDown())
        {
            HandleMoveOrNavigate(current, MoveDirection.Down, current.FindSelectableOnDown());
        }
        else if (InputManager.instance.UIMoveLeft())
        {
            HandleMoveOrNavigate(current, MoveDirection.Left, current.FindSelectableOnLeft());
        }
        else if (InputManager.instance.UIMoveRight())
        {
            HandleMoveOrNavigate(current, MoveDirection.Right, current.FindSelectableOnRight());
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
    /// フォーカス移動、または現在選択中の要素（スライダー等）の操作を行う
    /// </summary>
    private void HandleMoveOrNavigate(
        Selectable current,
        MoveDirection direction,
        Selectable target
    )
    {
        // 1. もし現在選択中のUIがスライダーであり、かつ移動先に別のUIが存在しない（または移動先が自分自身）場合、
        // フォーカス移動ではなく、スライダーを動かすための「Moveイベント」を直接送信する。
        if (current is Slider && (target == null || target == current))
        {
            AxisEventData data = new AxisEventData(EventSystem.current);
            data.moveDir = direction;
            data.moveVector = GetMoveVector(direction);

            // SliderにMoveイベント（値を増減させる処理）を実行させる
            ExecuteEvents.Execute(current.gameObject, data, ExecuteEvents.moveHandler);
            return;
        }

        // 2. それ以外（通常時のフォーカス移動）
        TryMoveTo(target);
    }

    /// <summary>
    /// MoveDirectionからベクトルへの変換
    /// </summary>
    private Vector2 GetMoveVector(MoveDirection dir)
    {
        switch (dir)
        {
            case MoveDirection.Up:
                return Vector2.up;
            case MoveDirection.Down:
                return Vector2.down;
            case MoveDirection.Left:
                return Vector2.left;
            case MoveDirection.Right:
                return Vector2.right;
            default:
                return Vector2.zero;
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

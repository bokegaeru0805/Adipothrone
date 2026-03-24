using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// カスタム入力（旧InputSystemのInputManager）に基づいて、UI選択操作（上下左右＋決定）を手動で制御するコンポーネント
/// ※ StandaloneInputModuleのWASD等は無効化し、このスクリプトに置き換える
/// </summary>
public class UIEventNavigationHandler : MonoBehaviour
{
    #region 長押し（リピート）設定
    [Header("長押し入力設定")]
    [Tooltip("キーを押し続けてから連続入力が始まるまでの待機時間（秒）")]
    [SerializeField]
    private float repeatDelay = 0.4f;

    [Tooltip("連続入力中の1回あたりの入力間隔（秒）")]
    [SerializeField]
    private float repeatRate = 0.05f;

    private float nextActionTime = 0f;
    private MoveDirection? currentHoldDirection = null;
    #endregion

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
        MoveDirection? inputDir = null;

        // 1. 押された瞬間 (GetKeyDown) の判定
        if (InputManager.instance.UIMoveUp())
            inputDir = MoveDirection.Up;
        else if (InputManager.instance.UIMoveDown())
            inputDir = MoveDirection.Down;
        else if (InputManager.instance.UIMoveLeft())
            inputDir = MoveDirection.Left;
        else if (InputManager.instance.UIMoveRight())
            inputDir = MoveDirection.Right;

        if (inputDir != null)
        {
            // 押された瞬間に1回実行し、長押しの待機タイマーをセット
            ExecuteMove(current, inputDir.Value);
            currentHoldDirection = inputDir;
            nextActionTime = Time.unscaledTime + repeatDelay;
        }
        else
        {
            // 2. 押しっぱなし (GetKey) の判定
            bool isHolding = false;
            MoveDirection? holdDir = null;

            if (InputManager.instance.UIMoveUpHold())
            {
                isHolding = true;
                holdDir = MoveDirection.Up;
            }
            else if (InputManager.instance.UIMoveDownHold())
            {
                isHolding = true;
                holdDir = MoveDirection.Down;
            }
            else if (InputManager.instance.UIMoveLeftHold())
            {
                isHolding = true;
                holdDir = MoveDirection.Left;
            }
            else if (InputManager.instance.UIMoveRightHold())
            {
                isHolding = true;
                holdDir = MoveDirection.Right;
            }

            // 同じ方向キーが押し続けられている場合
            if (isHolding && holdDir == currentHoldDirection)
            {
                // 待機時間を超えたら、連続入力を発動
                if (Time.unscaledTime >= nextActionTime)
                {
                    ExecuteMove(current, holdDir.Value);
                    nextActionTime = Time.unscaledTime + repeatRate; // 次の連続入力までの間隔をセット
                }
            }
            else
            {
                // キーを離した、または別の方向キーに切り替わった場合はリセット
                currentHoldDirection = null;
            }

            //  決定・キャンセルの判定
            if (InputManager.instance.UIConfirm())
            {
                ExecuteEvents.Execute(
                    current.gameObject,
                    new BaseEventData(EventSystem.current),
                    ExecuteEvents.submitHandler
                );
            }
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

    /// <summary>
    /// 方向に基づいてターゲットを取得し、実際の移動処理（HandleMoveOrNavigate）を呼び出します。
    /// </summary>
    private void ExecuteMove(Selectable current, MoveDirection dir)
    {
        Selectable target = null;
        switch (dir)
        {
            case MoveDirection.Up:
                target = current.FindSelectableOnUp();
                break;
            case MoveDirection.Down:
                target = current.FindSelectableOnDown();
                break;
            case MoveDirection.Left:
                target = current.FindSelectableOnLeft();
                break;
            case MoveDirection.Right:
                target = current.FindSelectableOnRight();
                break;
        }
        HandleMoveOrNavigate(current, dir, target);
    }
}

using System;
using UnityEngine;
using UnityEngine.Serialization;

// 名前空間は元のままにしておくと、多くのコードを変更せずに済みます
namespace UnityEngine.EventSystems
{
    [AddComponentMenu("Event/Mouse Only Input Module")]
    public class MouseOnlyInputModule : PointerInputModule
    {
        private Vector2 m_LastMousePosition;
        private Vector2 m_MousePosition;
        private GameObject m_CurrentFocusedGameObject;
        private PointerEventData m_InputPointerEvent;
        private const float doubleClickTime = 0.3f;

        protected MouseOnlyInputModule() { }

        // ▼▼▼ 削除 ▼▼▼
        // Horizontal/Vertical Axis, Submit/Cancel Buttonなどの変数を全て削除

        [SerializeField]
        [HideInInspector]
        private bool m_ForceModuleActive;

        private bool ShouldIgnoreEventsOnNoFocus()
        {
#if UNITY_EDITOR
            return !UnityEditor.EditorApplication.isRemoteConnected;
#else
            return true;
#endif
        }

        public override void UpdateModule()
        {
            if (!eventSystem.isFocused && ShouldIgnoreEventsOnNoFocus())
            {
                if (
                    m_InputPointerEvent != null
                    && m_InputPointerEvent.pointerDrag != null
                    && m_InputPointerEvent.dragging
                )
                {
                    ReleaseMouse(
                        m_InputPointerEvent,
                        m_InputPointerEvent.pointerCurrentRaycast.gameObject
                    );
                }
                m_InputPointerEvent = null;
                return;
            }

            m_LastMousePosition = m_MousePosition;
            m_MousePosition = input.mousePosition;
        }

        private void ReleaseMouse(PointerEventData pointerEvent, GameObject currentOverGo)
        {
            ExecuteEvents.Execute(
                pointerEvent.pointerPress,
                pointerEvent,
                ExecuteEvents.pointerUpHandler
            );

            var pointerClickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(
                currentOverGo
            );

            if (pointerEvent.pointerClick == pointerClickHandler && pointerEvent.eligibleForClick)
            {
                ExecuteEvents.Execute(
                    pointerEvent.pointerClick,
                    pointerEvent,
                    ExecuteEvents.pointerClickHandler
                );
            }
            if (pointerEvent.pointerDrag != null && pointerEvent.dragging)
            {
                ExecuteEvents.ExecuteHierarchy(
                    currentOverGo,
                    pointerEvent,
                    ExecuteEvents.dropHandler
                );
            }

            pointerEvent.eligibleForClick = false;
            pointerEvent.pointerPress = null;
            pointerEvent.rawPointerPress = null;
            pointerEvent.pointerClick = null;

            if (pointerEvent.pointerDrag != null && pointerEvent.dragging)
                ExecuteEvents.Execute(
                    pointerEvent.pointerDrag,
                    pointerEvent,
                    ExecuteEvents.endDragHandler
                );

            pointerEvent.dragging = false;
            pointerEvent.pointerDrag = null;

            if (currentOverGo != pointerEvent.pointerEnter)
            {
                HandlePointerExitAndEnter(pointerEvent, null);
                HandlePointerExitAndEnter(pointerEvent, currentOverGo);
            }
            m_InputPointerEvent = pointerEvent;
        }

        public override bool ShouldActivateModule()
        {
            if (!base.ShouldActivateModule())
                return false;

            // ▼▼▼ 変更 ▼▼▼
            // マウスとタッチの操作があった場合のみアクティブにする
            var shouldActivate = m_ForceModuleActive;
            shouldActivate |= (m_MousePosition - m_LastMousePosition).sqrMagnitude > 0.0f;
            shouldActivate |= input.GetMouseButtonDown(0);

            if (input.touchCount > 0)
                shouldActivate = true;

            return shouldActivate;
        }

        public override void ActivateModule()
        {
            if (!eventSystem.isFocused && ShouldIgnoreEventsOnNoFocus())
                return;

            base.ActivateModule();
            m_MousePosition = input.mousePosition;
            m_LastMousePosition = input.mousePosition;

            // ▼▼▼ 削除 ▼▼▼
            // GameObjectの選択に関する処理を削除
            // var toSelect = eventSystem.currentSelectedGameObject; ... etc
        }

        public override void DeactivateModule()
        {
            base.DeactivateModule();
            ClearSelection();
        }

        public override void Process()
        {
            if (!eventSystem.isFocused && ShouldIgnoreEventsOnNoFocus())
                return;

            // ▼▼▼ 変更 ▼▼▼
            // マウスとタッチのイベント処理のみを残す
            if (!ProcessTouchEvents() && input.mousePresent)
                ProcessMouseEvent();

            // ▼▼▼ 削除 ▼▼▼
            // SendUpdateEventToSelectedObjectやナビゲーションイベントの処理を削除
        }

        // ▼▼▼ ProcessTouchEvents, ProcessTouchPress はそのまま残す ▼▼▼
        private bool ProcessTouchEvents()
        {
            for (int i = 0; i < input.touchCount; ++i)
            {
                Touch touch = input.GetTouch(i);

                if (touch.type == TouchType.Indirect)
                    continue;

                bool released;
                bool pressed;
                var pointer = GetTouchPointerEventData(touch, out pressed, out released);

                ProcessTouchPress(pointer, pressed, released);

                if (!released)
                {
                    ProcessMove(pointer);
                    ProcessDrag(pointer);
                }
                else
                    RemovePointerData(pointer);
            }
            return input.touchCount > 0;
        }

        protected void ProcessTouchPress(PointerEventData pointerEvent, bool pressed, bool released)
        {
            var currentOverGo = pointerEvent.pointerCurrentRaycast.gameObject;

            // PointerDown notification
            if (pressed)
            {
                pointerEvent.eligibleForClick = true;
                pointerEvent.delta = Vector2.zero;
                pointerEvent.dragging = false;
                pointerEvent.useDragThreshold = true;
                pointerEvent.pressPosition = pointerEvent.position;
                pointerEvent.pointerPressRaycast = pointerEvent.pointerCurrentRaycast;

                DeselectIfSelectionChanged(currentOverGo, pointerEvent);

                if (pointerEvent.pointerEnter != currentOverGo)
                {
                    // send a pointer enter to the touched element if it isn't the one to select...
                    HandlePointerExitAndEnter(pointerEvent, currentOverGo);
                    pointerEvent.pointerEnter = currentOverGo;
                }

                var resetDiffTime = Time.unscaledTime - pointerEvent.clickTime;
                if (resetDiffTime >= doubleClickTime)
                {
                    pointerEvent.clickCount = 0;
                }

                // search for the control that will receive the press
                // if we can't find a press handler set the press
                // handler to be what would receive a click.
                var newPressed = ExecuteEvents.ExecuteHierarchy(
                    currentOverGo,
                    pointerEvent,
                    ExecuteEvents.pointerDownHandler
                );

                var newClick = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);

                // didnt find a press handler... search for a click handler
                if (newPressed == null)
                    newPressed = newClick;

                // Debug.Log("Pressed: " + newPressed);

                float time = Time.unscaledTime;

                if (newPressed == pointerEvent.lastPress)
                {
                    var diffTime = time - pointerEvent.clickTime;
                    if (diffTime < doubleClickTime)
                        ++pointerEvent.clickCount;
                    else
                        pointerEvent.clickCount = 1;

                    pointerEvent.clickTime = time;
                }
                else
                {
                    pointerEvent.clickCount = 1;
                }

                pointerEvent.pointerPress = newPressed;
                pointerEvent.rawPointerPress = currentOverGo;
                pointerEvent.pointerClick = newClick;

                pointerEvent.clickTime = time;

                // Save the drag handler as well
                pointerEvent.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(
                    currentOverGo
                );

                if (pointerEvent.pointerDrag != null)
                    ExecuteEvents.Execute(
                        pointerEvent.pointerDrag,
                        pointerEvent,
                        ExecuteEvents.initializePotentialDrag
                    );
            }

            // PointerUp notification
            if (released)
            {
                // Debug.Log("Executing pressup on: " + pointer.pointerPress);
                ExecuteEvents.Execute(
                    pointerEvent.pointerPress,
                    pointerEvent,
                    ExecuteEvents.pointerUpHandler
                );

                // Debug.Log("KeyCode: " + pointer.eventData.keyCode);

                // see if we mouse up on the same element that we clicked on...
                var pointerClickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(
                    currentOverGo
                );

                // PointerClick and Drop events
                if (
                    pointerEvent.pointerClick == pointerClickHandler
                    && pointerEvent.eligibleForClick
                )
                {
                    ExecuteEvents.Execute(
                        pointerEvent.pointerClick,
                        pointerEvent,
                        ExecuteEvents.pointerClickHandler
                    );
                }

                if (pointerEvent.pointerDrag != null && pointerEvent.dragging)
                {
                    ExecuteEvents.ExecuteHierarchy(
                        currentOverGo,
                        pointerEvent,
                        ExecuteEvents.dropHandler
                    );
                }

                pointerEvent.eligibleForClick = false;
                pointerEvent.pointerPress = null;
                pointerEvent.rawPointerPress = null;
                pointerEvent.pointerClick = null;

                if (pointerEvent.pointerDrag != null && pointerEvent.dragging)
                    ExecuteEvents.Execute(
                        pointerEvent.pointerDrag,
                        pointerEvent,
                        ExecuteEvents.endDragHandler
                    );

                pointerEvent.dragging = false;
                pointerEvent.pointerDrag = null;

                // send exit events as we need to simulate this on touch up on touch device
                ExecuteEvents.ExecuteHierarchy(
                    pointerEvent.pointerEnter,
                    pointerEvent,
                    ExecuteEvents.pointerExitHandler
                );
                pointerEvent.pointerEnter = null;
            }

            m_InputPointerEvent = pointerEvent;
        }

        // ▼▼▼ ProcessMouseEvent, ProcessMousePress はそのまま残す ▼▼▼
        protected void ProcessMouseEvent()
        {
            ProcessMouseEvent(0);
        }

        protected void ProcessMouseEvent(int id)
        {
            var mouseData = GetMousePointerEventData(id);
            var leftButtonData = mouseData
                .GetButtonState(PointerEventData.InputButton.Left)
                .eventData;

            m_CurrentFocusedGameObject = leftButtonData.buttonData.pointerCurrentRaycast.gameObject;

            // Process the first mouse button fully
            ProcessMousePress(leftButtonData);
            ProcessMove(leftButtonData.buttonData);
            ProcessDrag(leftButtonData.buttonData);

            // Now process right / middle clicks
            ProcessMousePress(
                mouseData.GetButtonState(PointerEventData.InputButton.Right).eventData
            );
            ProcessDrag(
                mouseData.GetButtonState(PointerEventData.InputButton.Right).eventData.buttonData
            );
            ProcessMousePress(
                mouseData.GetButtonState(PointerEventData.InputButton.Middle).eventData
            );
            ProcessDrag(
                mouseData.GetButtonState(PointerEventData.InputButton.Middle).eventData.buttonData
            );

            if (!Mathf.Approximately(leftButtonData.buttonData.scrollDelta.sqrMagnitude, 0.0f))
            {
                var scrollHandler = ExecuteEvents.GetEventHandler<IScrollHandler>(
                    leftButtonData.buttonData.pointerCurrentRaycast.gameObject
                );
                ExecuteEvents.ExecuteHierarchy(
                    scrollHandler,
                    leftButtonData.buttonData,
                    ExecuteEvents.scrollHandler
                );
            }
        }

        protected void ProcessMousePress(MouseButtonEventData data)
        {
            var pointerEvent = data.buttonData;
            var currentOverGo = pointerEvent.pointerCurrentRaycast.gameObject;

            // PointerDown notification
            if (data.PressedThisFrame())
            {
                pointerEvent.eligibleForClick = true;
                pointerEvent.delta = Vector2.zero;
                pointerEvent.dragging = false;
                pointerEvent.useDragThreshold = true;
                pointerEvent.pressPosition = pointerEvent.position;
                pointerEvent.pointerPressRaycast = pointerEvent.pointerCurrentRaycast;

                DeselectIfSelectionChanged(currentOverGo, pointerEvent);

                var resetDiffTime = Time.unscaledTime - pointerEvent.clickTime;
                if (resetDiffTime >= doubleClickTime)
                {
                    pointerEvent.clickCount = 0;
                }

                // search for the control that will receive the press
                // if we can't find a press handler set the press
                // handler to be what would receive a click.
                var newPressed = ExecuteEvents.ExecuteHierarchy(
                    currentOverGo,
                    pointerEvent,
                    ExecuteEvents.pointerDownHandler
                );
                var newClick = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);

                // didnt find a press handler... search for a click handler
                if (newPressed == null)
                    newPressed = newClick;

                // Debug.Log("Pressed: " + newPressed);

                float time = Time.unscaledTime;

                if (newPressed == pointerEvent.lastPress)
                {
                    var diffTime = time - pointerEvent.clickTime;
                    if (diffTime < doubleClickTime)
                        ++pointerEvent.clickCount;
                    else
                        pointerEvent.clickCount = 1;

                    pointerEvent.clickTime = time;
                }
                else
                {
                    pointerEvent.clickCount = 1;
                }

                pointerEvent.pointerPress = newPressed;
                pointerEvent.rawPointerPress = currentOverGo;
                pointerEvent.pointerClick = newClick;

                pointerEvent.clickTime = time;

                // Save the drag handler as well
                pointerEvent.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(
                    currentOverGo
                );

                if (pointerEvent.pointerDrag != null)
                    ExecuteEvents.Execute(
                        pointerEvent.pointerDrag,
                        pointerEvent,
                        ExecuteEvents.initializePotentialDrag
                    );

                m_InputPointerEvent = pointerEvent;
            }

            // PointerUp notification
            if (data.ReleasedThisFrame())
            {
                ReleaseMouse(pointerEvent, currentOverGo);
            }
        }

        // ▼▼▼ 削除 ▼▼▼
        // SendSubmitEventToSelectedObject, GetRawMoveVector, SendMoveEventToSelectedObject などの
        // キーボード/コントローラー関連メソッドを全て削除

        protected GameObject GetCurrentFocusedGameObject()
        {
            return m_CurrentFocusedGameObject;
        }
    }
}

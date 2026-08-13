using UnityEditor;
using UnityEngine;

/// <summary>
/// Edit Mode中、Sceneビュー上のマウスカーソル位置をワールド座標としてConsoleへ出力します。
/// ショートカットキー（F8）で実行されます。
/// </summary>
[InitializeOnLoad]
public static class SceneViewCoordinateLogger
{
    private const KeyCode TRIGGER_KEY = KeyCode.F8;

    private static readonly Plane XY_PLANE = new Plane(Vector3.forward, Vector3.zero);

    static SceneViewCoordinateLogger()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    /// <summary>
    /// Sceneビュー上のショートカット入力を監視します。
    /// </summary>
    private static void OnSceneGUI(SceneView sceneView)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Event currentEvent = Event.current;
        if (
            currentEvent == null
            || currentEvent.type != EventType.KeyDown
            || currentEvent.keyCode != TRIGGER_KEY
        )
        {
            return;
        }

        Ray mouseRay = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
        if (!XY_PLANE.Raycast(mouseRay, out float distance))
        {
            Debug.LogWarning("[Scene座標] マウス位置からZ = 0平面上の座標を取得できませんでした。");
            currentEvent.Use();
            return;
        }

        Vector3 worldPosition = mouseRay.GetPoint(distance);
        Debug.Log($"X: {worldPosition.x:F2}, Y: {worldPosition.y:F2}");

        currentEvent.Use();
    }
}

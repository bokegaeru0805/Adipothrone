using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;

/// <summary>
/// Tile PaletteのActive Targetが特定のタイルマップ（GroundTileMap）の時に、
/// シーンビューの画面端に枠線とテキストを表示し、レイヤー間違いを防ぐエディタ拡張です。
/// </summary>
[InitializeOnLoad]
public class GroundTileMapFocusVisualizer
{
    // 監視する対象のタイルマップ名
    private const string TARGET_TILEMAP_NAME = "GroundTileMap";

    // 枠線の太さと色（目立つオレンジ色を半透明で設定）
    private const float BORDER_THICKNESS = 8f;
    private static readonly Color BORDER_COLOR = new Color(1f, 0.5f, 0f, 0.8f);

    /// <summary>
    /// エディタロード時にイベントを登録します。
    /// </summary>
    static GroundTileMapFocusVisualizer()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    /// <summary>
    /// シーンビュー描画時の処理
    /// </summary>
    private static void OnSceneGUI(SceneView sceneView)
    {
        // Tile Paletteウィンドウが開いているかどうかを確認
        if (!IsTilePaletteWindowOpen())
        {
            return;
        }

        // 現在Tile Paletteで選択されているペイント対象を取得
        GameObject paintTarget = GridPaintingState.scenePaintTarget;
        if (paintTarget == null)
            return;

        // ペイント対象の名前が指定したものと一致するか判定
        if (paintTarget.name == TARGET_TILEMAP_NAME)
        {
            DrawFocusBorder(sceneView);
        }
    }

    /// <summary>
    /// Tile Paletteウィンドウが現在開かれているかを判定します。
    /// </summary>
    private static bool IsTilePaletteWindowOpen()
    {
        // 存在するすべてのEditorWindowを取得
        EditorWindow[] allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
        foreach (EditorWindow window in allWindows)
        {
            // Tile Paletteのウィンドウクラス名と一致するか確認
            if (window != null && window.GetType().Name == "GridPaintPaletteWindow")
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// シーンビュー上に枠線とテキストを描画します。
    /// </summary>
    private static void DrawFocusBorder(SceneView sceneView)
    {
        // GUIの描画を開始
        Handles.BeginGUI();

        // シーンビューのウィンドウサイズを取得
        float width = sceneView.position.width;
        float height = sceneView.position.height;

        // 上下左右に枠線を描画
        EditorGUI.DrawRect(new Rect(0, 0, width, BORDER_THICKNESS), BORDER_COLOR);
        EditorGUI.DrawRect(
            new Rect(0, height - BORDER_THICKNESS, width, BORDER_THICKNESS),
            BORDER_COLOR
        );
        EditorGUI.DrawRect(new Rect(0, 0, BORDER_THICKNESS, height), BORDER_COLOR);
        EditorGUI.DrawRect(
            new Rect(width - BORDER_THICKNESS, 0, BORDER_THICKNESS, height),
            BORDER_COLOR
        );

        // 左上に分かりやすくテキストも表示
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = BORDER_COLOR },
        };

        // 文字の背景に少し影をつけて見やすくする
        GUI.Label(
            new Rect(BORDER_THICKNESS + 11, BORDER_THICKNESS + 11, 300, 30),
            $"【編集中】{TARGET_TILEMAP_NAME}",
            new GUIStyle(style) { normal = { textColor = Color.black } }
        );
        GUI.Label(
            new Rect(BORDER_THICKNESS + 10, BORDER_THICKNESS + 10, 300, 30),
            $"【編集中】{TARGET_TILEMAP_NAME}",
            style
        );

        // GUIの描画を終了
        Handles.EndGUI();
    }
}

using UnityEngine;

// Sceneビュー上でカメラ周辺にのみグリッドを描画するスクリプト
[ExecuteAlways]
[RequireComponent(typeof(Grid))]
public class CustomGridVisualizer : MonoBehaviour
{
#if UNITY_EDITOR
    [Header("グリッドの表示設定")]
    [SerializeField]
    private bool showGrid = true;

    [Header("グリッドの色設定")]
    [SerializeField]
    private Color normalGridColor = new Color(1f, 1f, 1f, 0.1f);

    [SerializeField]
    private Color fifthGridColor = new Color(1f, 1f, 1f, 0.3f);

    [SerializeField]
    private Color tenthGridColor = new Color(1f, 0.27f, 0.27f, 0.5f);

    [SerializeField]
    private Color hundredthGridColor = new Color(1f, 1f, 1f, 0.8f); // X座標100毎の色

    [SerializeField]
    private Color fiftiethXGridColor = new Color(1f, 1f, 1f, 0.8f); // X座標50毎の色

    [SerializeField]
    private Color fiftiethGridColor = new Color(1f, 1f, 1f, 0.8f); // Y座標50毎の色

    [Header("グリッドの太さ設定")]
    [SerializeField]
    private float hundredthThickness = 3f; // X座標100毎の太さ

    [SerializeField]
    private float fiftiethXThickness = 2f; // X座標50毎の太さ（通常より太く、100毎より細い）

    [SerializeField]
    private float fiftiethThickness = 3f; // Y座標50毎の太さ

    [SerializeField]
    private int drawDistance = 50; // カメラから何セル分描画するか

    private void OnDrawGizmos()
    {
        // Inspectorでチェックが外れている場合は描画しない
        if (!showGrid)
            return;

        Grid grid = GetComponent<Grid>();
        if (grid == null)
            return;

        // 現在のシーンビューのカメラ位置を取得
        Camera cam = Camera.current;
        if (cam == null)
            return;

        // カメラの位置をグリッドのセル座標に変換
        Vector3Int centerCell = grid.WorldToCell(cam.transform.position);

        // カメラ周辺の一定範囲にのみ直線を引く
        DrawGridLines(grid, centerCell, true); // 縦線の描画
        DrawGridLines(grid, centerCell, false); // 横線の描画
    }

    private void DrawGridLines(Grid grid, Vector3Int centerCell, bool isVertical)
    {
        for (int i = -drawDistance; i <= drawDistance; i++)
        {
            int currentPos = (isVertical ? centerCell.x : centerCell.y) + i;
            float thickness = 1f;

            // 10マスごと、5マスごと、通常マスで線の色を分ける
            if (isVertical && currentPos % 100 == 0)
            {
                UnityEditor.Handles.color = hundredthGridColor;
                thickness = hundredthThickness;
            }
            else if (isVertical && currentPos % 50 == 0)
            {
                UnityEditor.Handles.color = fiftiethXGridColor;
                thickness = fiftiethXThickness;
            }
            else if (!isVertical && currentPos % 50 == 0)
            {
                UnityEditor.Handles.color = fiftiethGridColor;
                thickness = fiftiethThickness;
            }
            else if (currentPos % 10 == 0)
            {
                UnityEditor.Handles.color = tenthGridColor;
            }
            else if (currentPos % 5 == 0)
            {
                UnityEditor.Handles.color = fifthGridColor;
            }
            else
            {
                UnityEditor.Handles.color = normalGridColor;
            }

            Vector3 startPos,
                endPos;
            if (isVertical)
            {
                startPos = grid.CellToWorld(
                    new Vector3Int(currentPos, centerCell.y - drawDistance, 0)
                );
                endPos = grid.CellToWorld(
                    new Vector3Int(currentPos, centerCell.y + drawDistance, 0)
                );
            }
            else
            {
                startPos = grid.CellToWorld(
                    new Vector3Int(centerCell.x - drawDistance, currentPos, 0)
                );
                endPos = grid.CellToWorld(
                    new Vector3Int(centerCell.x + drawDistance, currentPos, 0)
                );
            }

            UnityEditor.Handles.DrawLine(startPos, endPos, thickness);
        }
    }
#endif
}

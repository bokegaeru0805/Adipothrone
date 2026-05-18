using UnityEngine;
using System.Text.RegularExpressions;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// オブジェクト名から数値を自動抽出し、エディタのシーンビュー(Gizmos)上に表示するスクリプト。
/// レベルデザインや配置オブジェクトの整理・デバッグに活用します。
/// </summary>
[ExecuteAlways]
public class EditorGizmoLabel : MonoBehaviour
{
    // --- 拡張性: 表示モードの切り替え ---
    public enum DisplayMode
    {
        Always,         // 常に表示
        SelectedOnly,   // このオブジェクトが選択されている時だけ表示
        Disabled        // 非表示
    }

    [Header("表示設定")]
    [Tooltip("ラベルの表示条件")]
    public DisplayMode displayMode = DisplayMode.Always;
    
    [Tooltip("カメラからこれ以上離れるとラベルを非表示にする距離")]
    public float maxViewDistance = 60f;
    
    [Tooltip("ラベルの表示位置（オブジェクトの中心からのズレ）")]
    public Vector3 offset = new Vector3(0f, 1f, 0f);

    [Header("文字のスタイル")]
    public Color textColor = Color.yellow;
    public int fontSize = 20;

    // --- キャッシュ用変数（パフォーマンス対策） ---
    private string lastObjectName = "";
    private string cachedLabel = "";

#if UNITY_EDITOR
    /// <summary>
    /// 実際のラベル描画処理
    /// </summary>
    private void DrawLabel()
    {
        if (displayMode == DisplayMode.Disabled) return;

        // 1. 視認性対策: カメラ距離による描画カリング（遠すぎる場合は描画しない）
        Camera cam = SceneView.currentDrawingSceneView?.camera;
        if (cam != null)
        {
            float distance = Vector3.Distance(transform.position, cam.transform.position);
            if (distance > maxViewDistance)
            {
                return;
            }
        }

        // 2. パフォーマンス対策: オブジェクト名が変わった時だけ文字列解析（正規表現）を行う
        if (gameObject.name != lastObjectName)
        {
            lastObjectName = gameObject.name;
            cachedLabel = ExtractNumberFromName(lastObjectName);
        }

        // 抽出した文字列が空（数字が見つからなかった場合）は描画しない
        if (string.IsNullOrEmpty(cachedLabel)) return;

        // 3. 描画スタイルの設定と描画
        GUIStyle style = new GUIStyle();
        style.normal.textColor = textColor;
        style.fontSize = fontSize;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;

        // オブジェクトのワールド座標にオフセットを足した位置にラベルを表示
        Handles.Label(transform.position + offset, cachedLabel, style);
    }

    /// <summary>
    /// オブジェクト名から特定の規則で文字を抽出する安全なロジック
    /// </summary>
    private string ExtractNumberFromName(string objName)
    {
        // 正規表現パターン: '_' の直後にある 1文字以上の数字(\d+)のグループを抽出する
        // 例: "table_1" -> "1", "spawn_point_05" -> "05", "box" -> 見つからない
        Match match = Regex.Match(objName, @"_(\d+)");

        if (match.Success)
        {
            // Group[1] には括弧 () で囲んだ数字部分が入る
            return match.Groups[1].Value;
        }

        return ""; // ルールに合致しない場合は空文字を返す（エラー防止）
    }

    // --- Unity組み込みのGizmoイベント ---

    private void OnDrawGizmos()
    {
        // 常に表示モードの場合のみ呼ばれる
        if (displayMode == DisplayMode.Always)
        {
            DrawLabel();
        }
    }

    private void OnDrawGizmosSelected()
    {
        // 選択時のみ表示モードの場合のみ呼ばれる
        if (displayMode == DisplayMode.SelectedOnly)
        {
            DrawLabel();
        }
    }
#endif
}
using UnityEditor;
using UnityEngine;

/// <summary>
/// シーン内のオブジェクトに対する一括処理や、エディタ上での補助ツールをまとめたクラス。
/// Tools/MyGame/Scene メニューに機能を追加します。
/// </summary>
public static class MyGameSceneTools
{
    /// <summary>
    /// エディタ上部のメニューバー "Tools > MyGame > Scene > Center Pivot All AreaTransitions" から実行。
    /// シーン内の全てのAreaTransitionを探し、ピボット位置を中心へ一括調整します。
    /// </summary>
    [MenuItem("Tools/MyGame/Scene/Center Pivot All AreaTransitions")]
    private static void CenterAllAreaTransitionPivots()
    {
        AreaTransition[] targets = Object.FindObjectsOfType<AreaTransition>();

        if (targets.Length == 0)
        {
            Debug.Log("AreaTransitionが見つかりませんでした。");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Center All AreaTransition Pivots");
        int undoGroupIndex = Undo.GetCurrentGroup();

        int count = 0;

        foreach (var target in targets)
        {
            BoxCollider2D col = target.GetComponent<BoxCollider2D>();
            if (col == null)
                continue;

            if (col.offset.sqrMagnitude < 0.0001f)
                continue;

            Vector3 worldCenter = target.transform.TransformPoint(col.offset);

            Undo.RecordObject(target.transform, "Center Pivot Transform");
            Undo.RecordObject(col, "Center Pivot Collider");

            target.transform.position = worldCenter;
            col.offset = Vector2.zero;

            count++;
        }

        Undo.CollapseUndoOperations(undoGroupIndex);
        Debug.Log($"完了: {count} 個のAreaTransitionのピボットを調整しました。");
    }

    /// <summary>
    /// シーン内に存在する全てのCameraMoveAreaに対して、Light2Dの形状更新を一括実行します。
    /// </summary>
    [MenuItem("Tools/MyGame/Scene/Update All CameraMoveArea Light Shapes")]
    public static void UpdateAllCameraMoveAreaLightShapes()
    {
        CameraMoveArea[] allAreas = Object.FindObjectsOfType<CameraMoveArea>();
        int count = 0;

        foreach (var area in allAreas)
        {
            // private変数のareaColliderを直接触れないため、取得し直すか、UpdateLightShapeToCollider内で完結させます
            // 今回はCameraMoveArea側でpublicにしたメソッドを直接呼び出します

            // 変更対象のLight2Dを取得 (リフレクションまたはシリアライズオブジェクト経由、今回はシンプルにGetComponentInChildren等で代用可能ですが、元の挙動を尊重)
            var areaLightField = typeof(CameraMoveArea).GetField(
                "areaLight",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            if (areaLightField != null)
            {
                var light2D =
                    areaLightField.GetValue(area) as UnityEngine.Rendering.Universal.Light2D;
                if (light2D != null)
                {
                    Undo.RecordObject(light2D, "Update Light Shape");

                    // publicに変更したメソッドを実行
                    area.UpdateLightShapeToCollider();

                    EditorUtility.SetDirty(light2D);
                    count++;
                }
            }
        }
        Debug.Log(
            $"{count} 個のLight2Dの形状を更新しました。(CameraMoveArea)"
        );
    }

    /// <summary>
    /// CRI Atom Craftでビルドした最新データを反映させるための手動リロード機能です。
    /// メニューの「Tools > MyGame >  Reload CRIWARE Data」をクリックするか、
    /// ショートカットキー (Ctrl + Alt + R / Cmd + Option + R) で実行できます。
    /// </summary>
    [MenuItem("Tools/MyGame/Reload CRIWARE Data %&r")]
    public static void ForceDomainReload()
    {
        // スクリプトコンパイル時と同じ「ドメインリロード」を強制的に要求し、CRI内部のキャッシュを破棄します。
        EditorUtility.RequestScriptReload();
        Debug.Log(
            "[CRIWARE] 音声データ更新のための強制リセット（ドメインリロード）を実行しました。"
        );
    }
}

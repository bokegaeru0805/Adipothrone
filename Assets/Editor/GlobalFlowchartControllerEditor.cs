using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>GlobalFlowchartのPrefabインスタンスを元の状態へ戻す操作を提供する。</summary>
[CustomEditor(typeof(GlobalFlowchartController))]
public class GlobalFlowchartControllerEditor : Editor
{
    private const string GlobalFlowchartPrefabPath =
        "Assets/Prefabs/Managers/GlobalFlowchart.prefab";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prefab復元", EditorStyles.boldLabel);

        var controller = target as GlobalFlowchartController;
        bool canRevert = TryGetTargetInstance(controller, out GameObject instanceRoot);

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox(
                "Prefabの復元は、再生モードを終了してから実行してください。",
                MessageType.Info);
        }
        else if (!canRevert)
        {
            EditorGUILayout.HelpBox(
                "この操作は、シーン上に配置されたGlobalFlowchart.prefabのインスタンスでのみ実行できます。",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Flowchartのプロパティ、変数、ブロック、コマンドを含む、このPrefabインスタンスの全Overrideを復元します。",
                MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(!canRevert))
        {
            if (GUILayout.Button("GlobalFlowchartをPrefabの状態へ全体Revert"))
                RevertPrefabInstance(instanceRoot);
        }
    }

    private static bool TryGetTargetInstance(
        GlobalFlowchartController controller,
        out GameObject instanceRoot)
    {
        instanceRoot = null;

        if (controller == null
            || EditorApplication.isPlayingOrWillChangePlaymode
            || EditorUtility.IsPersistent(controller)
            || !PrefabUtility.IsPartOfPrefabInstance(controller))
        {
            return false;
        }

        instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(controller.gameObject);
        if (instanceRoot == null)
            return false;

        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot);
        return prefabPath == GlobalFlowchartPrefabPath;
    }

    private static void RevertPrefabInstance(GameObject instanceRoot)
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "GlobalFlowchartを全体Revert",
            "このPrefabインスタンスに加えられた全Overrideを破棄し、\n"
                + "GlobalFlowchart.prefabの状態へ戻します。\n\n"
                + "この操作を実行しますか？",
            "Revert",
            "キャンセル");

        if (!confirmed)
            return;

        PrefabUtility.RevertPrefabInstance(instanceRoot, InteractionMode.UserAction);
        EditorSceneManager.MarkSceneDirty(instanceRoot.scene);
        Selection.activeGameObject = instanceRoot;
    }
}

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Hierarchyで選択したGameObjectと、その全子オブジェクトの
/// コンポーネント型一覧をYAML風テキストとしてクリップボードへコピーします。
/// </summary>
public static class HierarchyComponentClipboardCopier
{
    private const string MenuPath = "GameObject/選択オブジェクト以下のコンポーネント一覧をコピー";

    /// <summary>
    /// Hierarchy上のGameObject右クリックメニュー、およびGameObjectメニューに追加します。
    /// </summary>
    [MenuItem(MenuPath, false, 49)]
    private static void CopyComponentHierarchy(MenuCommand menuCommand)
    {
        GameObject[] targetRoots = GetTargetRoots(menuCommand);

        if (targetRoots.Length == 0)
        {
            Debug.LogWarning("コンポーネント一覧のコピー対象が選択されていません。");
            return;
        }

        StringBuilder builder = new StringBuilder(4096);
        int objectCount = 0;
        int componentCount = 0;

        builder.AppendLine("unityHierarchyComponents:");
        builder.AppendLine("  formatVersion: 1");
        builder.AppendLine("  unityVersion: \"" + EscapeYaml(Application.unityVersion) + "\"");
        builder.AppendLine("  rootCount: " + targetRoots.Length);
        builder.AppendLine("  roots:");

        foreach (GameObject root in targetRoots)
        {
            AppendGameObjectRecursive(
                builder,
                root.transform,
                root.transform,
                2,
                ref objectCount,
                ref componentCount
            );
        }

        builder.AppendLine("  summary:");
        builder.AppendLine("    objectCount: " + objectCount);
        builder.AppendLine("    componentCount: " + componentCount);

        string output = builder.ToString().TrimEnd();
        EditorGUIUtility.systemCopyBuffer = output;

        Debug.Log(
            "コンポーネント一覧をクリップボードにコピーしました:\n"
                + "ルートオブジェクト数: "
                + targetRoots.Length
                + "\n"
                + "オブジェクト総数: "
                + objectCount
                + "\n"
                + "コンポーネント総数: "
                + componentCount
                + "\n\n"
                + output
        );
    }

    /// <summary>
    /// GameObjectが選択されているときだけメニューを有効にします。
    /// </summary>
    [MenuItem(MenuPath, true)]
    private static bool ValidateCopyComponentHierarchy()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }

    /// <summary>
    /// 右クリック対象または現在の複数選択から、重複しないルートだけを取得します。
    /// 親子を同時選択している場合、選択済みの親の配下にある子は除外します。
    /// </summary>
    private static GameObject[] GetTargetRoots(MenuCommand menuCommand)
    {
        GameObject contextObject = menuCommand.context as GameObject;
        GameObject[] selectedObjects = Selection.gameObjects ?? Array.Empty<GameObject>();

        IEnumerable<GameObject> candidates;

        // 未選択オブジェクトを右クリックした場合は、その右クリック対象のみを処理します。
        if (contextObject != null && !selectedObjects.Contains(contextObject))
        {
            candidates = new[] { contextObject };
        }
        else
        {
            candidates = selectedObjects;
        }

        GameObject[] distinctCandidates = candidates
            .Where(gameObject => gameObject != null)
            .Distinct()
            .ToArray();

        HashSet<GameObject> selectedSet = new HashSet<GameObject>(distinctCandidates);

        return distinctCandidates
            .Where(gameObject => !HasSelectedAncestor(gameObject.transform, selectedSet))
            .OrderBy(GetHierarchySortKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HasSelectedAncestor(Transform transform, HashSet<GameObject> selectedSet)
    {
        Transform parent = transform.parent;

        while (parent != null)
        {
            if (selectedSet.Contains(parent.gameObject))
            {
                return true;
            }

            parent = parent.parent;
        }

        return false;
    }

    /// <summary>
    /// Hierarchyの見た目に近い順序で並べるためのソートキーを作成します。
    /// </summary>
    private static string GetHierarchySortKey(GameObject gameObject)
    {
        List<int> siblingIndices = new List<int>();
        Transform current = gameObject.transform;

        while (current != null)
        {
            siblingIndices.Add(current.GetSiblingIndex());
            current = current.parent;
        }

        siblingIndices.Reverse();

        string scenePath = gameObject.scene.IsValid() ? gameObject.scene.path : string.Empty;

        return scenePath
            + "|"
            + string.Join("/", siblingIndices.Select(index => index.ToString("D8")));
    }

    private static void AppendGameObjectRecursive(
        StringBuilder builder,
        Transform current,
        Transform root,
        int indentLevel,
        ref int objectCount,
        ref int componentCount
    )
    {
        GameObject gameObject = current.gameObject;
        Component[] components = gameObject.GetComponents<Component>();

        objectCount++;
        componentCount += components.Length;

        string indent = new string(' ', indentLevel * 2);
        string propertyIndent = new string(' ', (indentLevel + 1) * 2);
        string componentIndent = new string(' ', (indentLevel + 2) * 2);

        builder.AppendLine(indent + "- name: \"" + EscapeYaml(gameObject.name) + "\"");
        builder.AppendLine(
            propertyIndent + "path: \"" + EscapeYaml(GetRelativePath(current, root)) + "\""
        );

        if (current == root)
        {
            string sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : "(No Scene)";

            builder.AppendLine(propertyIndent + "scene: \"" + EscapeYaml(sceneName) + "\"");
        }

        builder.AppendLine(propertyIndent + "status: \"" + GetActiveStatus(gameObject) + "\"");

        // 非アクティブ状態をLLMが明確に判定できるよう、該当時だけ詳細も出します。
        if (!gameObject.activeInHierarchy)
        {
            builder.AppendLine(
                propertyIndent
                    + "activeSelf: "
                    + gameObject.activeSelf.ToString().ToLowerInvariant()
            );
            builder.AppendLine(propertyIndent + "activeInHierarchy: false");
        }

        builder.AppendLine(propertyIndent + "components:");

        if (components.Length == 0)
        {
            builder.AppendLine(componentIndent + "- \"(None)\"");
        }
        else
        {
            foreach (Component component in components)
            {
                builder.AppendLine(
                    componentIndent + "- \"" + EscapeYaml(GetComponentDisplayName(component)) + "\""
                );
            }
        }

        if (current.childCount > 0)
        {
            builder.AppendLine(propertyIndent + "children:");

            for (int i = 0; i < current.childCount; i++)
            {
                AppendGameObjectRecursive(
                    builder,
                    current.GetChild(i),
                    root,
                    indentLevel + 2,
                    ref objectCount,
                    ref componentCount
                );
            }
        }
    }

    private static string GetRelativePath(Transform current, Transform root)
    {
        Stack<string> names = new Stack<string>();
        Transform cursor = current;

        while (cursor != null)
        {
            names.Push(cursor.name);

            if (cursor == root)
            {
                break;
            }

            cursor = cursor.parent;
        }

        return string.Join("/", names);
    }

    /// <summary>
    /// activeSelf=falseと、親の非アクティブ化による非アクティブを区別します。
    /// </summary>
    private static string GetActiveStatus(GameObject gameObject)
    {
        if (gameObject.activeInHierarchy)
        {
            return "Active";
        }

        if (!gameObject.activeSelf)
        {
            return "Inactive (Self)";
        }

        return "Inactive (By Parent)";
    }

    /// <summary>
    /// 設定値は出さず、型名と簡潔な種別だけを返します。
    /// </summary>
    private static string GetComponentDisplayName(Component component)
    {
        if (component == null)
        {
            return "Missing Script (C#)";
        }

        Type type = component.GetType();
        string typeName = type.Name;
        string namespaceName = type.Namespace ?? string.Empty;

        if (component is Transform)
        {
            return typeName;
        }

        if (namespaceName.StartsWith("UnityEngine.UI", StringComparison.Ordinal))
        {
            return typeName + " (UI)";
        }

        if (namespaceName.StartsWith("TMPro", StringComparison.Ordinal))
        {
            return typeName + " (TextMeshPro)";
        }

        if (namespaceName.StartsWith("UnityEngine.EventSystems", StringComparison.Ordinal))
        {
            return typeName + " (Event System)";
        }

        if (namespaceName.StartsWith("UnityEngine.AI", StringComparison.Ordinal))
        {
            return typeName + " (Navigation)";
        }

        if (namespaceName.StartsWith("Cinemachine", StringComparison.Ordinal))
        {
            return typeName + " (Cinemachine)";
        }

        if (component is Animator || component is Animation)
        {
            return typeName + " (Animation)";
        }

        if (
            component is Rigidbody2D
            || component is Collider2D
            || component is Joint2D
            || component is Effector2D
        )
        {
            return typeName + " (Physics 2D)";
        }

        if (component is Rigidbody || component is Collider || component is Joint)
        {
            return typeName + " (Physics 3D)";
        }

        if (component is AudioSource || component is AudioListener || component is AudioReverbZone)
        {
            return typeName + " (Audio)";
        }

        if (component is ParticleSystem)
        {
            return typeName + " (Effect)";
        }

        if (
            component is Renderer
            || component is MeshFilter
            || component is Camera
            || component is Light
        )
        {
            return typeName + " (Rendering)";
        }

        if (component is Canvas)
        {
            return typeName + " (UI)";
        }

        if (component is MonoBehaviour monoBehaviour)
        {
            MonoScript monoScript = MonoScript.FromMonoBehaviour(monoBehaviour);
            string scriptName = monoScript != null ? monoScript.name : typeName;

            return scriptName + " (C#)";
        }

        return typeName;
    }

    private static string EscapeYaml(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }
}

#endif

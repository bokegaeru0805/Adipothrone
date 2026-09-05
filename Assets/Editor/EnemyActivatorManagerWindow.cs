using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 開いているScene内のEnemyActivatorを一覧表示し、その設定を編集するウィンドウ。
/// </summary>
public class EnemyActivatorManagerWindow : EditorWindow
{
    private const float NameColumnWidth = 210f;
    private const float DetailButtonWidth = 48f;
    private const float AreaColumnWidth = 180f;
    private const float CountColumnWidth = 55f;
    private const float WarningColumnWidth = 55f;

    private readonly List<EnemyActivator> activators = new List<EnemyActivator>();
    private Vector2 fixedListScrollPosition;
    private Vector2 propertyListScrollPosition;
    private Vector2 propertyHeaderScrollPosition;
    private Vector2 detailScrollPosition;
    private float tableBodyTop;
    private string searchText = string.Empty;
    private string selectedGlobalObjectId = string.Empty;
    private bool showOnlyConfigured;
    private bool showOnlyWarnings;
    private bool isShowCameraArea = true;

    [MenuItem("Tools/MyGame/Window/Enemy Activator Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<EnemyActivatorManagerWindow>("Enemy Activators");
        window.minSize = new Vector2(850f, 500f);
        window.RefreshActivators();
    }

    private void OnEnable()
    {
        RefreshActivators();
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorSceneManager.sceneClosed += OnSceneClosed;
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    private void OnDisable()
    {
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneClosed -= OnSceneClosed;
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    private void OnGUI()
    {
        RemoveMissingActivators();
        DrawToolbar();

        EditorGUILayout.Space(3f);
        DrawList();
        EditorGUILayout.Space(5f);
        DrawDetails();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("検索", GUILayout.Width(30f));
        searchText = GUILayout.TextField(searchText, EditorStyles.toolbarSearchField, GUILayout.MinWidth(160f));
        showOnlyConfigured = GUILayout.Toggle(showOnlyConfigured, "設定あり", EditorStyles.toolbarButton);
        showOnlyWarnings = GUILayout.Toggle(showOnlyWarnings, "警告あり", EditorStyles.toolbarButton);
        isShowCameraArea = GUILayout.Toggle(isShowCameraArea, "Camera Area表示", EditorStyles.toolbarButton);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"{GetFilteredActivators().Count} / {activators.Count} 件");

        if (GUILayout.Button("再読込", EditorStyles.toolbarButton, GUILayout.Width(60f)))
        {
            RefreshActivators();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawList()
    {
        DrawListHeader();
        tableBodyTop = GUILayoutUtility.GetLastRect().yMax;
        propertyListScrollPosition.x = propertyHeaderScrollPosition.x;
        List<EnemyActivator> filteredActivators = GetFilteredActivators();
        float listHeight = Mathf.Min(position.height * 0.45f, 330f);

        HandleTableMouseWheel(listHeight);

        EditorGUILayout.BeginHorizontal();
        fixedListScrollPosition = EditorGUILayout.BeginScrollView(
            fixedListScrollPosition,
            GUIStyle.none,
            GUIStyle.none,
            GUILayout.Width(NameColumnWidth + DetailButtonWidth + 8f),
            GUILayout.Height(listHeight)
        );
        foreach (EnemyActivator activator in filteredActivators)
        {
            DrawFixedActivatorRow(activator);
        }
        EditorGUILayout.EndScrollView();

        propertyListScrollPosition = EditorGUILayout.BeginScrollView(
            propertyListScrollPosition,
            true,
            false,
            GUILayout.Height(listHeight)
        );
        foreach (EnemyActivator activator in filteredActivators)
        {
            DrawPropertyActivatorRow(activator);
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndHorizontal();

        fixedListScrollPosition.y = propertyListScrollPosition.y;
        propertyHeaderScrollPosition.x = propertyListScrollPosition.x;
    }

    private void HandleTableMouseWheel(float listHeight)
    {
        Event currentEvent = Event.current;
        var tableRect = new Rect(0f, tableBodyTop, position.width, listHeight);
        if (currentEvent.type != EventType.ScrollWheel || !tableRect.Contains(currentEvent.mousePosition))
            return;

        propertyListScrollPosition.y = Mathf.Max(
            0f,
            propertyListScrollPosition.y + currentEvent.delta.y * EditorGUIUtility.singleLineHeight
        );
        currentEvent.Use();
        Repaint();
    }

    private void DrawListHeader()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginHorizontal(
            EditorStyles.helpBox,
            GUILayout.Width(NameColumnWidth + DetailButtonWidth + 8f)
        );
        GUILayout.Label("識別名 / GameObject", EditorStyles.boldLabel, GUILayout.Width(NameColumnWidth));
        GUILayout.Label("", GUILayout.Width(DetailButtonWidth));
        EditorGUILayout.EndHorizontal();

        propertyHeaderScrollPosition = EditorGUILayout.BeginScrollView(
            propertyHeaderScrollPosition,
            GUIStyle.none,
            GUIStyle.none,
            GUILayout.Height(EditorGUIUtility.singleLineHeight + 8f)
        );
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        if (isShowCameraArea)
            GUILayout.Label("Camera Area", EditorStyles.boldLabel, GUILayout.Width(AreaColumnWidth));
        GUILayout.Label("レア", EditorStyles.boldLabel, GUILayout.Width(CountColumnWidth));
        GUILayout.Label("条件", EditorStyles.boldLabel, GUILayout.Width(CountColumnWidth));
        GUILayout.Label("警告", EditorStyles.boldLabel, GUILayout.Width(WarningColumnWidth));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawFixedActivatorRow(EnemyActivator activator)
    {
        var serializedObject = new SerializedObject(activator);
        SerializedProperty displayNameProperty = serializedObject.FindProperty("editorDisplayName");
        string globalId = GetGlobalObjectId(activator);
        bool isSelected = selectedGlobalObjectId == globalId;

        Color previousColor = GUI.backgroundColor;
        if (isSelected)
            GUI.backgroundColor = new Color(0.65f, 0.85f, 1f);

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUI.backgroundColor = previousColor;

        EditorGUI.BeginChangeCheck();
        string newDisplayName = EditorGUILayout.TextField(
            string.IsNullOrWhiteSpace(displayNameProperty.stringValue)
                ? activator.gameObject.name
                : displayNameProperty.stringValue,
            GUILayout.Width(NameColumnWidth)
        );
        if (EditorGUI.EndChangeCheck())
        {
            displayNameProperty.stringValue = newDisplayName == activator.gameObject.name
                ? string.Empty
                : newDisplayName;
            serializedObject.ApplyModifiedProperties();
        }

        if (GUILayout.Button("詳細", GUILayout.Width(DetailButtonWidth)))
        {
            selectedGlobalObjectId = globalId;
            GUI.FocusControl(null);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawPropertyActivatorRow(EnemyActivator activator)
    {
        var serializedObject = new SerializedObject(activator);
        SerializedProperty areaProperty = serializedObject.FindProperty("targetCameraArea");
        SerializedProperty rareProperty = serializedObject.FindProperty("rareEnemies");
        SerializedProperty conditionalProperty = serializedObject.FindProperty("conditionalEnemyGroups");
        int warningCount = CountWarnings(serializedObject);

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        if (isShowCameraArea)
        {
            EditorGUI.BeginChangeCheck();
            CameraMoveArea newArea = EditorGUILayout.ObjectField(
                areaProperty.objectReferenceValue,
                typeof(CameraMoveArea),
                true,
                GUILayout.Width(AreaColumnWidth)
            ) as CameraMoveArea;
            if (EditorGUI.EndChangeCheck())
            {
                areaProperty.objectReferenceValue = newArea;
                serializedObject.ApplyModifiedProperties();
            }
        }

        GUILayout.Label(rareProperty.arraySize.ToString(), GUILayout.Width(CountColumnWidth));
        GUILayout.Label(conditionalProperty.arraySize.ToString(), GUILayout.Width(CountColumnWidth));
        GUILayout.Label(
            warningCount > 0 ? warningCount.ToString() : "-",
            warningCount > 0 ? EditorStyles.boldLabel : EditorStyles.label,
            GUILayout.Width(WarningColumnWidth)
        );
        EditorGUILayout.EndHorizontal();
    }

    private void DrawDetails()
    {
        EnemyActivator selectedActivator = FindSelectedActivator();
        if (selectedActivator == null)
        {
            EditorGUILayout.HelpBox("一覧の「詳細」から編集するEnemyActivatorを選択してください。", MessageType.Info);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(GetDisplayName(selectedActivator), EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Hierarchyで選択", GUILayout.Width(110f)))
        {
            Selection.activeGameObject = selectedActivator.gameObject;
            EditorGUIUtility.PingObject(selectedActivator.gameObject);
        }

        if (GUILayout.Button("Sceneで表示", GUILayout.Width(90f)))
        {
            Selection.activeGameObject = selectedActivator.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        EditorGUILayout.EndHorizontal();

        detailScrollPosition = EditorGUILayout.BeginScrollView(detailScrollPosition);
        var serializedObject = new SerializedObject(selectedActivator);
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("editorDisplayName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetCameraArea"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rareEnemies"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("conditionalEnemyGroups"), true);

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
        }

        DrawWarnings(serializedObject);
        EditorGUILayout.EndScrollView();
    }

    private List<EnemyActivator> GetFilteredActivators()
    {
        return activators
            .Where(activator => activator != null)
            .Where(MatchesSearch)
            .Where(activator => !showOnlyConfigured || HasRareOrConditionalSettings(activator))
            .Where(activator => !showOnlyWarnings || CountWarnings(new SerializedObject(activator)) > 0)
            .OrderBy(activator => activator.gameObject.scene.name)
            .ThenBy(GetDisplayName)
            .ThenBy(activator => GetHierarchyPath(activator.transform))
            .ToList();
    }

    private bool MatchesSearch(EnemyActivator activator)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        string comparisonText = searchText.Trim();
        var serializedObject = new SerializedObject(activator);
        var area = serializedObject.FindProperty("targetCameraArea").objectReferenceValue as CameraMoveArea;

        return GetDisplayName(activator).IndexOf(comparisonText, StringComparison.OrdinalIgnoreCase) >= 0
            || activator.gameObject.name.IndexOf(comparisonText, StringComparison.OrdinalIgnoreCase) >= 0
            || GetHierarchyPath(activator.transform).IndexOf(comparisonText, StringComparison.OrdinalIgnoreCase) >= 0
            || (area != null && area.gameObject.name.IndexOf(comparisonText, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool HasRareOrConditionalSettings(EnemyActivator activator)
    {
        var serializedObject = new SerializedObject(activator);
        return serializedObject.FindProperty("rareEnemies").arraySize > 0
            || serializedObject.FindProperty("conditionalEnemyGroups").arraySize > 0;
    }

    private static int CountWarnings(SerializedObject serializedObject)
    {
        serializedObject.Update();
        int count = 0;

        if (serializedObject.FindProperty("targetCameraArea").objectReferenceValue == null)
            count++;

        var registeredObjects = new HashSet<UnityEngine.Object>();
        count += CountRareEnemyWarnings(serializedObject.FindProperty("rareEnemies"), registeredObjects);

        SerializedProperty groups = serializedObject.FindProperty("conditionalEnemyGroups");
        for (int groupIndex = 0; groupIndex < groups.arraySize; groupIndex++)
        {
            SerializedProperty group = groups.GetArrayElementAtIndex(groupIndex);
            SerializedProperty flags = group.FindPropertyRelative("requiredFlags");
            SerializedProperty enemies = group.FindPropertyRelative("enemyObjects");
            SerializedProperty rareEnemies = group.FindPropertyRelative("rareEnemies");
            var groupRegisteredObjects = new HashSet<UnityEngine.Object>(registeredObjects);

            if (flags.arraySize == 0)
                count++;

            // 別グループへの同一対象登録はOR条件として有効なので、グループ間では重複扱いにしない。
            count += CountObjectReferenceWarnings(enemies, groupRegisteredObjects);
            count += CountRareEnemyWarnings(rareEnemies, groupRegisteredObjects);
        }

        return count;
    }

    private static int CountObjectReferenceWarnings(
        SerializedProperty arrayProperty,
        HashSet<UnityEngine.Object> registeredObjects
    )
    {
        int count = 0;
        for (int index = 0; index < arrayProperty.arraySize; index++)
        {
            UnityEngine.Object target = arrayProperty.GetArrayElementAtIndex(index).objectReferenceValue;
            if (target == null || !registeredObjects.Add(target))
                count++;
        }

        return count;
    }

    private static int CountRareEnemyWarnings(
        SerializedProperty arrayProperty,
        HashSet<UnityEngine.Object> registeredObjects
    )
    {
        int count = 0;
        for (int index = 0; index < arrayProperty.arraySize; index++)
        {
            SerializedProperty element = arrayProperty.GetArrayElementAtIndex(index);
            UnityEngine.Object target = element.FindPropertyRelative("enemyObject").objectReferenceValue;
            float spawnChance = element.FindPropertyRelative("spawnChance").floatValue;
            if (target == null || !registeredObjects.Add(target))
                count++;

            if (spawnChance <= 0f)
                count++;
        }

        return count;
    }

    private static void DrawWarnings(SerializedObject serializedObject)
    {
        int warningCount = CountWarnings(serializedObject);
        if (warningCount > 0)
        {
            EditorGUILayout.HelpBox(
                $"警告候補が {warningCount} 件あります。Camera Area未設定、対象未設定、重複登録、出現確率0%以下、または条件なしグループを確認してください。",
                MessageType.Warning
            );
        }
    }

    private void RefreshActivators()
    {
        activators.Clear();
        activators.AddRange(
            Resources.FindObjectsOfTypeAll<EnemyActivator>()
                .Where(activator => activator != null)
                .Where(activator => activator.gameObject.scene.IsValid())
                .Where(activator => activator.gameObject.scene.isLoaded)
                .Where(activator => (activator.hideFlags & HideFlags.HideAndDontSave) == 0)
        );
        Repaint();
    }

    private void RemoveMissingActivators()
    {
        activators.RemoveAll(activator => activator == null);
    }

    private EnemyActivator FindSelectedActivator()
    {
        return activators.FirstOrDefault(
            activator => activator != null && GetGlobalObjectId(activator) == selectedGlobalObjectId
        );
    }

    private static string GetDisplayName(EnemyActivator activator)
    {
        var serializedObject = new SerializedObject(activator);
        string displayName = serializedObject.FindProperty("editorDisplayName").stringValue;
        return string.IsNullOrWhiteSpace(displayName) ? activator.gameObject.name : displayName;
    }

    private static string GetGlobalObjectId(EnemyActivator activator)
    {
        return GlobalObjectId.GetGlobalObjectIdSlow(activator).ToString();
    }

    private static string GetHierarchyPath(Transform target)
    {
        var names = new Stack<string>();
        Transform current = target;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private void OnHierarchyChanged()
    {
        RefreshActivators();
    }

    private void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        RefreshActivators();
    }

    private void OnSceneClosed(Scene scene)
    {
        RefreshActivators();
    }

    private void OnUndoRedo()
    {
        RefreshActivators();
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using Fungus;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// 開いているScene内のFungus Invoke Methodコマンドを検査します。
/// </summary>
public class InvokeMethodValidationWindow : EditorWindow
{
    private readonly List<ValidationResult> validationResults = new List<ValidationResult>();
    private Vector2 scrollPosition;
    private bool hasScanned;
    private int scannedCommandCount;

    private class ValidationResult
    {
        public InvokeMethod Command;
        public string SceneName;
        public string FlowchartName;
        public string BlockName;
        public int CommandIndex;
        public string Problem;

        public ValidationResult(
            InvokeMethod command,
            string sceneName,
            string flowchartName,
            string blockName,
            int commandIndex,
            string problem
        )
        {
            Command = command;
            SceneName = sceneName;
            FlowchartName = flowchartName;
            BlockName = blockName;
            CommandIndex = commandIndex;
            Problem = problem;
        }
    }

    [MenuItem("Tools/MyGame/Window/Invoke Method Validator")]
    public static void ShowWindow()
    {
        InvokeMethodValidationWindow window = GetWindow<InvokeMethodValidationWindow>(
            "Invoke Method Validator"
        );
        window.ScanOpenScenes();
        window.Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Invoke Method 設定チェック", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "開いている全Scene（Additive Sceneを含む）のFlowchartを検査します。Sceneやコマンドは変更しません。",
            MessageType.Info
        );

        if (GUILayout.Button("開いているSceneをスキャン", GUILayout.Height(30)))
        {
            ScanOpenScenes();
        }

        if (!hasScanned)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            $"検査したInvoke Method: {scannedCommandCount}件 / 問題: {validationResults.Count}件",
            EditorStyles.boldLabel
        );

        if (validationResults.Count == 0)
        {
            EditorGUILayout.HelpBox("設定不備は見つかりませんでした。", MessageType.Info);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        foreach (ValidationResult result in validationResults)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                $"{result.SceneName} / {result.FlowchartName} / {result.BlockName}",
                EditorStyles.boldLabel
            );
            EditorGUILayout.LabelField($"Command #{result.CommandIndex + 1}: {result.Problem}", EditorStyles.wordWrappedLabel);

            if (GUILayout.Button("該当コマンドを選択", GUILayout.Width(130)))
            {
                Selection.activeObject = result.Command;
                EditorGUIUtility.PingObject(result.Command);
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
    }

    private void ScanOpenScenes()
    {
        validationResults.Clear();
        scannedCommandCount = 0;
        hasScanned = true;

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded)
            {
                continue;
            }

            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                Flowchart[] flowcharts = rootObject.GetComponentsInChildren<Flowchart>(true);
                foreach (Flowchart flowchart in flowcharts)
                {
                    ValidateFlowchart(scene, flowchart);
                }
            }
        }
    }

    private void ValidateFlowchart(Scene scene, Flowchart flowchart)
    {
        Block[] blocks = flowchart.GetComponents<Block>();
        foreach (Block block in blocks)
        {
            List<Command> commands = block.CommandList;
            for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
            {
                InvokeMethod invokeMethod = commands[commandIndex] as InvokeMethod;
                if (invokeMethod == null)
                {
                    continue;
                }

                scannedCommandCount++;
                ValidateCommand(scene, flowchart, block, invokeMethod, commandIndex);
            }
        }
    }

    private void ValidateCommand(
        Scene scene,
        Flowchart flowchart,
        Block block,
        InvokeMethod command,
        int commandIndex
    )
    {
        SerializedObject serializedCommand = new SerializedObject(command);
        SerializedProperty targetObjectProperty = serializedCommand.FindProperty("targetObject");
        SerializedProperty componentTypeProperty = serializedCommand.FindProperty("targetComponentAssemblyName");
        SerializedProperty methodNameProperty = serializedCommand.FindProperty("targetMethod");
        SerializedProperty parametersProperty = serializedCommand.FindProperty("methodParameters");
        SerializedProperty saveReturnValueProperty = serializedCommand.FindProperty("saveReturnValue");
        SerializedProperty returnVariableProperty = serializedCommand.FindProperty("returnValueVariableKey");
        SerializedProperty returnTypeProperty = serializedCommand.FindProperty("returnValueType");

        GameObject targetObject = targetObjectProperty.objectReferenceValue as GameObject;
        string componentTypeName = componentTypeProperty.stringValue;
        string methodName = methodNameProperty.stringValue;

        if (targetObject == null)
        {
            AddResult(scene, flowchart, block, command, commandIndex, "Target Objectがnullです。");
            return;
        }

        if (string.IsNullOrEmpty(componentTypeName))
        {
            AddResult(scene, flowchart, block, command, commandIndex, "Target Componentが未設定です。");
            return;
        }

        Type componentType = ResolveType(componentTypeName);
        if (componentType == null)
        {
            AddResult(scene, flowchart, block, command, commandIndex, $"Component型を解決できません: {componentTypeName}");
            return;
        }

        if (!typeof(Component).IsAssignableFrom(componentType))
        {
            AddResult(scene, flowchart, block, command, commandIndex, $"保存された型がComponentではありません: {componentTypeName}");
            return;
        }

        Component targetComponent = targetObject.GetComponent(componentType);
        if (targetComponent == null)
        {
            AddResult(scene, flowchart, block, command, commandIndex, $"Target Objectに{componentType.Name}がありません。");
            return;
        }

        if (string.IsNullOrEmpty(methodName))
        {
            AddResult(scene, flowchart, block, command, commandIndex, "Target Methodが未設定です。");
            return;
        }

        Type[] parameterTypes = ValidateParameters(
            scene,
            flowchart,
            block,
            command,
            commandIndex,
            parametersProperty
        );

        if (parameterTypes != null)
        {
            MethodInfo method = UnityEvent.GetValidMethodInfo(targetComponent, methodName, parameterTypes);
            if (method == null)
            {
                AddResult(
                    scene,
                    flowchart,
                    block,
                    command,
                    commandIndex,
                    $"現在の{componentType.Name}に一致するmethodがありません: {methodName}"
                );
            }
        }

        if (saveReturnValueProperty.boolValue)
        {
            ValidateVariable(
                scene,
                flowchart,
                block,
                command,
                commandIndex,
                returnVariableProperty.stringValue,
                returnTypeProperty.stringValue,
                "戻り値の保存先"
            );
        }
    }

    private Type[] ValidateParameters(
        Scene scene,
        Flowchart flowchart,
        Block block,
        InvokeMethod command,
        int commandIndex,
        SerializedProperty parametersProperty
    )
    {
        if (parametersProperty == null || !parametersProperty.isArray)
        {
            AddResult(scene, flowchart, block, command, commandIndex, "Parameter情報がnullです。");
            return null;
        }

        Type[] parameterTypes = new Type[parametersProperty.arraySize];
        bool canResolveAllTypes = true;

        for (int parameterIndex = 0; parameterIndex < parametersProperty.arraySize; parameterIndex++)
        {
            SerializedProperty parameter = parametersProperty.GetArrayElementAtIndex(parameterIndex);
            SerializedProperty objectValue = parameter.FindPropertyRelative("objValue");
            SerializedProperty variableKey = parameter.FindPropertyRelative("variableKey");
            string parameterLabel = $"Parameter #{parameterIndex + 1}";

            if (objectValue == null || variableKey == null)
            {
                canResolveAllTypes = false;
                AddResult(scene, flowchart, block, command, commandIndex, $"{parameterLabel}の保存情報がnullです。");
                continue;
            }

            SerializedProperty typeAssemblyName = objectValue.FindPropertyRelative("typeAssemblyname");
            SerializedProperty typeFullName = objectValue.FindPropertyRelative("typeFullname");

            if (typeAssemblyName == null || typeFullName == null)
            {
                canResolveAllTypes = false;
                AddResult(scene, flowchart, block, command, commandIndex, $"{parameterLabel}の型情報がnullです。");
                continue;
            }

            string assemblyQualifiedTypeName = typeAssemblyName.stringValue;
            Type parameterType = ResolveType(assemblyQualifiedTypeName);
            parameterTypes[parameterIndex] = parameterType;

            if (parameterType == null)
            {
                canResolveAllTypes = false;
                AddResult(
                    scene,
                    flowchart,
                    block,
                    command,
                    commandIndex,
                    $"{parameterLabel}の型を解決できません: {assemblyQualifiedTypeName}"
                );
                continue;
            }

            if (!string.IsNullOrEmpty(variableKey.stringValue))
            {
                ValidateVariable(
                    scene,
                    flowchart,
                    block,
                    command,
                    commandIndex,
                    variableKey.stringValue,
                    typeFullName.stringValue,
                    parameterLabel
                );
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(parameterType))
            {
                SerializedProperty objectReference = GetObjectReferenceProperty(objectValue, typeFullName.stringValue);
                if (objectReference == null || objectReference.objectReferenceValue == null)
                {
                    AddResult(scene, flowchart, block, command, commandIndex, $"{parameterLabel}の参照値がnullです。");
                }
            }
        }

        return canResolveAllTypes ? parameterTypes : null;
    }

    private void ValidateVariable(
        Scene scene,
        Flowchart flowchart,
        Block block,
        InvokeMethod command,
        int commandIndex,
        string variableKey,
        string valueTypeFullName,
        string label
    )
    {
        if (string.IsNullOrEmpty(variableKey))
        {
            AddResult(scene, flowchart, block, command, commandIndex, $"{label}のFungus変数が未設定です。");
            return;
        }

        Variable variable = flowchart.GetVariable(variableKey);
        if (variable == null)
        {
            AddResult(scene, flowchart, block, command, commandIndex, $"{label}のFungus変数が存在しません: {variableKey}");
            return;
        }

        Type expectedVariableType = GetExpectedVariableType(valueTypeFullName);
        if (expectedVariableType != null && !expectedVariableType.IsInstanceOfType(variable))
        {
            AddResult(
                scene,
                flowchart,
                block,
                command,
                commandIndex,
                $"{label}のFungus変数型が一致しません: {variableKey} ({variable.GetType().Name})"
            );
        }
    }

    private static SerializedProperty GetObjectReferenceProperty(SerializedProperty objectValue, string typeFullName)
    {
        switch (typeFullName)
        {
            case "UnityEngine.GameObject":
                return objectValue.FindPropertyRelative("gameObjectValue");
            case "UnityEngine.Material":
                return objectValue.FindPropertyRelative("materialValue");
            case "UnityEngine.Sprite":
                return objectValue.FindPropertyRelative("spriteValue");
            case "UnityEngine.Texture":
                return objectValue.FindPropertyRelative("textureValue");
            default:
                return objectValue.FindPropertyRelative("objectValue");
        }
    }

    private static Type GetExpectedVariableType(string valueTypeFullName)
    {
        switch (valueTypeFullName)
        {
            case "System.Int32": return typeof(IntegerVariable);
            case "System.Boolean": return typeof(BooleanVariable);
            case "System.Single": return typeof(FloatVariable);
            case "System.String": return typeof(StringVariable);
            case "UnityEngine.Color": return typeof(ColorVariable);
            case "UnityEngine.GameObject": return typeof(GameObjectVariable);
            case "UnityEngine.Material": return typeof(MaterialVariable);
            case "UnityEngine.Sprite": return typeof(SpriteVariable);
            case "UnityEngine.Texture": return typeof(TextureVariable);
            case "UnityEngine.Vector2": return typeof(Vector2Variable);
            case "UnityEngine.Vector3": return typeof(Vector3Variable);
            default: return typeof(ObjectVariable);
        }
    }

    private static Type ResolveType(string assemblyQualifiedTypeName)
    {
        if (string.IsNullOrEmpty(assemblyQualifiedTypeName))
        {
            return null;
        }

        Type type = Type.GetType(assemblyQualifiedTypeName, false);
        if (type != null)
        {
            return type;
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(assemblyQualifiedTypeName, false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private void AddResult(
        Scene scene,
        Flowchart flowchart,
        Block block,
        InvokeMethod command,
        int commandIndex,
        string problem
    )
    {
        validationResults.Add(
            new ValidationResult(
                command,
                scene.name,
                flowchart.name,
                block.BlockName,
                commandIndex,
                problem
            )
        );
    }
}

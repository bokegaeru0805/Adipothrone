using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditorInternal;

/// <summary>
/// FlagActionコンポーネントのInspector表示をカスタマイズするエディタ。
/// 表示崩れを完全に防ぐため、シンプルで堅牢な複数行レイアウトを採用する。
/// </summary>
[CustomEditor(typeof(FlagAction))]
public class FlagActionEditor : Editor
{
    private static readonly List<Type> boolEnumTypes = new List<Type> { typeof(PrologueTriggeredEvent), typeof(Chapter1TriggeredEvent), typeof(TutorialEvent) };
    private static readonly List<Type> intEnumTypes = new List<Type> { typeof(PrologueCountedEvent), typeof(Chapter1CountedEvent) };
    private static Dictionary<string, string[]> valueNamesCache = new Dictionary<string, string[]>();

    private SerializedProperty operationsProp;
    private ReorderableList reorderableList;

    private void OnEnable()
    {
        operationsProp = serializedObject.FindProperty("operations");

        reorderableList = new ReorderableList(serializedObject, operationsProp, true, true, true, true)
        {
            drawHeaderCallback = (Rect rect) => EditorGUI.LabelField(rect, "実行するフラグ操作"),

            // ▼▼▼ 修正箇所：常に3行分の高さを確保 ▼▼▼
            elementHeightCallback = (int index) => EditorGUIUtility.singleLineHeight * 3 + EditorGUIUtility.standardVerticalSpacing * 4,

            drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                var element = operationsProp.GetArrayElementAtIndex(index);
                var opTypeProp = element.FindPropertyRelative("operationType");
                var enumTypeNameProp = element.FindPropertyRelative("enumTypeName");
                var enumValueNameProp = element.FindPropertyRelative("enumValueName");

                // --- レイアウトを3行に完全に分割 ---
                var line1Rect = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);
                var line2Rect = new Rect(rect.x, line1Rect.yMax + EditorGUIUtility.standardVerticalSpacing, rect.width, EditorGUIUtility.singleLineHeight);
                var line3Rect = new Rect(rect.x, line2Rect.yMax + EditorGUIUtility.standardVerticalSpacing, rect.width, EditorGUIUtility.singleLineHeight);

                // --- 1行目: [操作タイプ] [Enum型] ---
                var opTypeRect = new Rect(line1Rect.x, line1Rect.y, 80, line1Rect.height);
                var enumTypeRect = new Rect(opTypeRect.xMax + 5, line1Rect.y, line1Rect.width - 85, line1Rect.height);
                
                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(opTypeRect, opTypeProp, GUIContent.none);
                if (EditorGUI.EndChangeCheck())
                {
                    enumTypeNameProp.stringValue = null;
                    enumValueNameProp.stringValue = null;
                }
                
                var currentOpType = (FlagOperation.OperationType)opTypeProp.enumValueIndex;
                var relevantEnumTypes = currentOpType == FlagOperation.OperationType.SetBool ? boolEnumTypes : intEnumTypes;
                var displayTypeNames = relevantEnumTypes.Select(t => t.Name).ToArray();
                var fullTypeNames = relevantEnumTypes.Select(t => t.AssemblyQualifiedName).ToArray();

                int currentTypeIndex = Array.IndexOf(fullTypeNames, enumTypeNameProp.stringValue);
                if (currentTypeIndex == -1) currentTypeIndex = 0;
                
                if (fullTypeNames.Length > 0)
                {
                    int newTypeIndex = EditorGUI.Popup(enumTypeRect, currentTypeIndex, displayTypeNames);
                    if (newTypeIndex != currentTypeIndex)
                    {
                        enumTypeNameProp.stringValue = fullTypeNames[newTypeIndex];
                        enumValueNameProp.stringValue = null;
                    }
                }

                // --- 2行目: [Enumの値] ---
                string selectedTypeName = enumTypeNameProp.stringValue;
                if (string.IsNullOrEmpty(selectedTypeName)) return;

                if (!valueNamesCache.ContainsKey(selectedTypeName))
                {
                    Type enumType = Type.GetType(selectedTypeName);
                    if (enumType != null) { valueNamesCache[selectedTypeName] = Enum.GetNames(enumType); }
                }
                string[] valueNames = valueNamesCache.GetValueOrDefault(selectedTypeName, Array.Empty<string>());
                
                int currentValueIndex = Array.IndexOf(valueNames, enumValueNameProp.stringValue);
                if (currentValueIndex == -1) currentValueIndex = 0;
                
                if (valueNames.Length > 0)
                {
                    int newValueIndex = EditorGUI.Popup(line2Rect, currentValueIndex, valueNames);
                    enumValueNameProp.stringValue = valueNames[newValueIndex];
                }

                // --- 3行目: [設定する値] ---
                if (currentOpType == FlagOperation.OperationType.SetBool)
                {
                    var boolProp = element.FindPropertyRelative("boolValueToSet");
                    // PropertyFieldではなく、ラベル付きのToggleを直接描画
                    boolProp.boolValue = EditorGUI.Toggle(line3Rect, "Set Value To", boolProp.boolValue);
                }
                else // Int
                {
                    var intProp = element.FindPropertyRelative("intValueToSet");
                    // PropertyFieldではなく、ラベル付きのIntFieldを直接描画
                    intProp.intValue = EditorGUI.IntField(line3Rect, "Set Value To", intProp.intValue);
                }
            }
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        reorderableList.DoLayoutList();
        serializedObject.ApplyModifiedProperties();
    }
}
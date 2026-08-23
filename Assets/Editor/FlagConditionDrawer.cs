using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(FlagConditionPro))]
public class FlagConditionDrawerPro : PropertyDrawer
{
    private const float NarrowInspectorWidth = 420f;

    private static readonly Dictionary<string, int> arraySizeCache =
        new Dictionary<string, int>();

    // パフォーマンス向上のためのキャッシュ
    private static Dictionary<string, string[]> valueNamesCache =
        new Dictionary<string, string[]>();

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int lineCount = IsNarrowLayout() ? 3 : 2;
        return EditorGUIUtility.singleLineHeight * lineCount
            + EditorGUIUtility.standardVerticalSpacing * (lineCount - 1);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        InitializeNewArrayElements(property);

        var conditionTypeProp = property.FindPropertyRelative("conditionType");
        var enumTypeNameProp = property.FindPropertyRelative("enumTypeName");
        var enumValueNameProp = property.FindPropertyRelative("enumValueName");

        // 空のListへ最初に追加した要素はサイズ比較で検出できないため、
        // Enum型がまだ設定されていない新規要素をここで初期化する。
        if (
            conditionTypeProp.enumValueIndex == (int)FlagConditionPro.ConditionType.Bool
            && string.IsNullOrEmpty(enumTypeNameProp.stringValue)
        )
        {
            InitializeCondition(property);
        }

        bool isNarrowLayout = IsNarrowLayout();

        // --- Inspector幅に応じてレイアウトを2行または3行に分割 ---
        var line1Rect = new Rect(
            position.x,
            position.y,
            position.width,
            EditorGUIUtility.singleLineHeight
        );
        var line2Rect = new Rect(
            position.x,
            line1Rect.yMax + EditorGUIUtility.standardVerticalSpacing,
            position.width,
            EditorGUIUtility.singleLineHeight
        );
        var line3Rect = new Rect(
            position.x,
            line2Rect.yMax + EditorGUIUtility.standardVerticalSpacing,
            position.width,
            EditorGUIUtility.singleLineHeight
        );

        // --- 1行目の描画 ---
        var typeSwitchRect = new Rect(line1Rect.x, line1Rect.y, 60, line1Rect.height);
        var enumTypeRect = new Rect(
            typeSwitchRect.xMax + 5,
            line1Rect.y,
            line1Rect.width - 65,
            line1Rect.height
        );

        EditorGUI.BeginChangeCheck();
        EditorGUI.PropertyField(typeSwitchRect, conditionTypeProp, GUIContent.none);
        bool conditionTypeChanged = EditorGUI.EndChangeCheck();

        var currentConditionType = (FlagConditionPro.ConditionType)conditionTypeProp.enumValueIndex;
        if (currentConditionType == FlagConditionPro.ConditionType.Door)
        {
            DrawDoorCondition(enumTypeRect, line2Rect, line3Rect, property, isNarrowLayout);
            EditorGUI.EndProperty();
            return;
        }

        var relevantEnumTypes =
            currentConditionType == FlagConditionPro.ConditionType.Bool
                ? MyGameSettingsProvider.BoolFlagEnumTypes
                : MyGameSettingsProvider.IntFlagEnumTypes;
        var displayTypeNames = relevantEnumTypes.Select(t => t.Name).ToArray();
        var fullTypeNames = relevantEnumTypes.Select(t => t.AssemblyQualifiedName).ToArray();

        if (conditionTypeChanged)
        {
            Type defaultType = GetDefaultEnumType(currentConditionType);
            enumTypeNameProp.stringValue = defaultType.AssemblyQualifiedName;
            enumValueNameProp.stringValue = Enum.GetNames(defaultType).FirstOrDefault() ?? string.Empty;
        }

        int currentTypeIndex = Array.IndexOf(fullTypeNames, enumTypeNameProp.stringValue);
        if (currentTypeIndex == -1)
        {
            Type defaultType = GetDefaultEnumType(currentConditionType);
            currentTypeIndex = Math.Max(0, Array.IndexOf(fullTypeNames, defaultType.AssemblyQualifiedName));
        }

        GUIContent[] enumTypeLabels = displayTypeNames
            .Select(typeName => new GUIContent(typeName, typeName))
            .ToArray();
        int newTypeIndex = EditorGUI.Popup(enumTypeRect, currentTypeIndex, enumTypeLabels);
        if (newTypeIndex != currentTypeIndex || string.IsNullOrEmpty(enumTypeNameProp.stringValue))
        {
            if (fullTypeNames.Length > 0)
            {
                enumTypeNameProp.stringValue = fullTypeNames[newTypeIndex];
                enumValueNameProp.stringValue = null;
            }
        }

        // --- 2行目の描画 ---
        string selectedTypeName = enumTypeNameProp.stringValue;
        if (string.IsNullOrEmpty(selectedTypeName))
        {
            EditorGUI.EndProperty();
            return;
        }

        if (!valueNamesCache.ContainsKey(selectedTypeName))
        {
            Type enumType = Type.GetType(selectedTypeName);
            if (enumType != null)
            {
                valueNamesCache[selectedTypeName] = Enum.GetNames(enumType);
            }
        }
        string[] valueNames = valueNamesCache.GetValueOrDefault(
            selectedTypeName,
            Array.Empty<string>()
        );

        var valueNameRect = isNarrowLayout
            ? line2Rect
            : new Rect(
                line2Rect.x,
                line2Rect.y,
                line2Rect.width * 0.5f - 2,
                line2Rect.height
            );
        int currentValueIndex = Array.IndexOf(valueNames, enumValueNameProp.stringValue);
        if (currentValueIndex == -1)
            currentValueIndex = 0;

        GUIContent[] valueLabels = valueNames
            .Select(valueName => new GUIContent(valueName, valueName))
            .ToArray();
        int newValueIndex = EditorGUI.Popup(valueNameRect, currentValueIndex, valueLabels);
        if (valueNames.Length > 0)
        {
            enumValueNameProp.stringValue = valueNames[newValueIndex];
        }

        if (currentConditionType == FlagConditionPro.ConditionType.Bool)
        {
            var boolProp = property.FindPropertyRelative("requiredBoolValue");
            if (isNarrowLayout)
            {
                boolProp.boolValue = EditorGUI.Popup(
                    line3Rect,
                    boolProp.boolValue ? 0 : 1,
                    new[] { "True", "False" }
                ) == 0;
            }
            else
            {
                var boolRect = new Rect(valueNameRect.xMax + 5, line2Rect.y, 20, line2Rect.height);
                boolProp.boolValue = EditorGUI.Toggle(boolRect, boolProp.boolValue);
            }
        }
        else // Int
        {
            Rect intSettingsRect = isNarrowLayout ? line3Rect : line2Rect;
            float settingsStartX = isNarrowLayout ? intSettingsRect.x : valueNameRect.xMax + 5;
            float settingsWidth = isNarrowLayout
                ? intSettingsRect.width
                : line2Rect.width - (settingsStartX - line2Rect.x);
            var comparisonRect = new Rect(
                settingsStartX,
                intSettingsRect.y,
                settingsWidth * 0.5f - 2,
                intSettingsRect.height
            );
            var intRect = new Rect(
                comparisonRect.xMax + 5,
                intSettingsRect.y,
                settingsWidth * 0.5f - 3,
                intSettingsRect.height
            );

            var comparisonProp = property.FindPropertyRelative("intComparison");
            var intProp = property.FindPropertyRelative("requiredIntValue");

            // 汎用的なPropertyFieldの代わりに、専用の描画メソッドを使用
            comparisonProp.enumValueIndex = (int)
                (FlagConditionPro.IntComparison)
                    EditorGUI.EnumPopup(
                        comparisonRect,
                        (FlagConditionPro.IntComparison)comparisonProp.enumValueIndex
                    );
            intProp.intValue = EditorGUI.IntField(intRect, intProp.intValue);
        }

        EditorGUI.EndProperty();
    }

    private static void InitializeNewArrayElements(SerializedProperty property)
    {
        const string arrayElementMarker = ".Array.data[";
        int markerIndex = property.propertyPath.LastIndexOf(arrayElementMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
            return;

        string arrayPath = property.propertyPath.Substring(0, markerIndex);
        SerializedProperty arrayProperty = property.serializedObject.FindProperty(arrayPath);
        if (arrayProperty == null || !arrayProperty.isArray)
            return;

        string cacheKey = property.serializedObject.targetObject.GetInstanceID() + ":" + arrayPath;
        if (!arraySizeCache.TryGetValue(cacheKey, out int previousSize))
        {
            arraySizeCache[cacheKey] = arrayProperty.arraySize;
            return;
        }

        if (arrayProperty.arraySize > previousSize)
        {
            for (int i = previousSize; i < arrayProperty.arraySize; i++)
            {
                InitializeCondition(arrayProperty.GetArrayElementAtIndex(i));
            }
        }

        arraySizeCache[cacheKey] = arrayProperty.arraySize;
    }

    private static void InitializeCondition(SerializedProperty conditionProperty)
    {
        var conditionTypeProp = conditionProperty.FindPropertyRelative("conditionType");
        var enumTypeNameProp = conditionProperty.FindPropertyRelative("enumTypeName");
        var enumValueNameProp = conditionProperty.FindPropertyRelative("enumValueName");
        var requiredBoolValueProp = conditionProperty.FindPropertyRelative("requiredBoolValue");

        conditionTypeProp.enumValueIndex = (int)FlagConditionPro.ConditionType.Bool;
        requiredBoolValueProp.boolValue = MyGameSettingsProvider.GetDefaultBoolFlagValue();

        Type defaultType = MyGameSettingsProvider.GetDefaultBoolFlagEnumType();
        enumTypeNameProp.stringValue = defaultType.AssemblyQualifiedName;
        enumValueNameProp.stringValue = Enum.GetNames(defaultType).FirstOrDefault() ?? string.Empty;
    }

    private static Type GetDefaultEnumType(FlagConditionPro.ConditionType conditionType)
    {
        return conditionType == FlagConditionPro.ConditionType.Bool
            ? MyGameSettingsProvider.GetDefaultBoolFlagEnumType()
            : MyGameSettingsProvider.GetDefaultIntFlagEnumType();
    }

    private static void DrawDoorCondition(
        Rect labelRect,
        Rect valueRect,
        Rect narrowStateRect,
        SerializedProperty property,
        bool isNarrowLayout
    )
    {
        EditorGUI.LabelField(labelRect, "ドア解放状態");

        var doorIdProp = property.FindPropertyRelative("doorId");
        var requiredBoolValueProp = property.FindPropertyRelative("requiredBoolValue");
        int[] doorIds = FlagManager.GetDoorConditionIds().ToArray();

        var doorRect = isNarrowLayout
            ? valueRect
            : new Rect(
                valueRect.x,
                valueRect.y,
                valueRect.width * 0.5f - 2,
                valueRect.height
            );
        var stateRect = isNarrowLayout
            ? narrowStateRect
            : new Rect(
                doorRect.xMax + 5,
                valueRect.y,
                valueRect.width * 0.5f - 3,
                valueRect.height
            );

        if (doorIds.Length == 0)
        {
            EditorGUI.LabelField(doorRect, "ドア条件がありません");
        }
        else
        {
            string[] doorLabels = doorIds.Select(id => $"Door {id}").ToArray();
            int currentDoorIndex = Array.IndexOf(doorIds, doorIdProp.intValue);
            if (currentDoorIndex == -1)
                currentDoorIndex = 0;

            int newDoorIndex = EditorGUI.Popup(doorRect, currentDoorIndex, doorLabels);
            doorIdProp.intValue = doorIds[newDoorIndex];
        }

        requiredBoolValueProp.boolValue = EditorGUI.Popup(
            stateRect,
            requiredBoolValueProp.boolValue ? 0 : 1,
            new[] { "解放済み", "未解放" }
        ) == 0;
    }

    private static bool IsNarrowLayout()
    {
        return EditorGUIUtility.currentViewWidth < NarrowInspectorWidth;
    }
}

using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

[CustomPropertyDrawer(typeof(FlagConditionPro))]
public class FlagConditionDrawerPro : PropertyDrawer
{
    // ▼▼▼【重要】新しいEnumフラグを追加したら、このリストに追記してください▼▼▼
    private static readonly List<Type> boolEnumTypes = new List<Type> { typeof(PrologueTriggeredEvent), typeof(Chapter1TriggeredEvent), typeof(TutorialEvent) };
    private static readonly List<Type> intEnumTypes = new List<Type> { typeof(PrologueCountedEvent), typeof(Chapter1CountedEvent) };
    
    // パフォーマンス向上のためのキャッシュ
    private static Dictionary<string, string[]> valueNamesCache = new Dictionary<string, string[]>();

    // 常に2行分の高さを返す
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var conditionTypeProp = property.FindPropertyRelative("conditionType");
        var enumTypeNameProp = property.FindPropertyRelative("enumTypeName");
        var enumValueNameProp = property.FindPropertyRelative("enumValueName");
        
        // --- レイアウトを計算 (2行に分割) ---
        var line1Rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        var line2Rect = new Rect(position.x, line1Rect.yMax + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);

        // --- 1行目の描画 ---
        var typeSwitchRect = new Rect(line1Rect.x, line1Rect.y, 60, line1Rect.height);
        var enumTypeRect = new Rect(typeSwitchRect.xMax + 5, line1Rect.y, line1Rect.width - 65, line1Rect.height);

        EditorGUI.PropertyField(typeSwitchRect, conditionTypeProp, GUIContent.none);
        
        var currentConditionType = (FlagConditionPro.ConditionType)conditionTypeProp.enumValueIndex;
        var relevantEnumTypes = currentConditionType == FlagConditionPro.ConditionType.Bool ? boolEnumTypes : intEnumTypes;
        var displayTypeNames = relevantEnumTypes.Select(t => t.Name).ToArray();
        var fullTypeNames = relevantEnumTypes.Select(t => t.AssemblyQualifiedName).ToArray();

        int currentTypeIndex = Array.IndexOf(fullTypeNames, enumTypeNameProp.stringValue);
        if (currentTypeIndex == -1) currentTypeIndex = 0;

        int newTypeIndex = EditorGUI.Popup(enumTypeRect, currentTypeIndex, displayTypeNames);
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
            if (enumType != null) { valueNamesCache[selectedTypeName] = Enum.GetNames(enumType); }
        }
        string[] valueNames = valueNamesCache.GetValueOrDefault(selectedTypeName, Array.Empty<string>());

        var valueNameRect = new Rect(line2Rect.x, line2Rect.y, line2Rect.width * 0.5f - 2, line2Rect.height);
        int currentValueIndex = Array.IndexOf(valueNames, enumValueNameProp.stringValue);
        if (currentValueIndex == -1) currentValueIndex = 0;
        
        int newValueIndex = EditorGUI.Popup(valueNameRect, currentValueIndex, valueNames);
        if (valueNames.Length > 0)
        {
            enumValueNameProp.stringValue = valueNames[newValueIndex];
        }

        // ▼▼▼ 修正箇所 ▼▼▼
        if (currentConditionType == FlagConditionPro.ConditionType.Bool)
        {
            var boolRect = new Rect(valueNameRect.xMax + 5, line2Rect.y, 20, line2Rect.height);
            var boolProp = property.FindPropertyRelative("requiredBoolValue");
            boolProp.boolValue = EditorGUI.Toggle(boolRect, boolProp.boolValue);
        }
        else // Int
        {
            // レイアウト計算をよりシンプルで正確なものに変更
            var comparisonRect = new Rect(valueNameRect.xMax + 5, line2Rect.y, line2Rect.width * 0.25f - 2, line2Rect.height);
            var intRect = new Rect(comparisonRect.xMax + 5, line2Rect.y, line2Rect.width * 0.25f - 3, line2Rect.height);

            var comparisonProp = property.FindPropertyRelative("intComparison");
            var intProp = property.FindPropertyRelative("requiredIntValue");

            // 汎用的なPropertyFieldの代わりに、専用の描画メソッドを使用
            comparisonProp.enumValueIndex = (int)(FlagConditionPro.IntComparison)EditorGUI.EnumPopup(comparisonRect, (FlagConditionPro.IntComparison)comparisonProp.enumValueIndex);
            intProp.intValue = EditorGUI.IntField(intRect, intProp.intValue);
        }
        // ▲▲▲ 修正箇所 ▲▲▲

        EditorGUI.EndProperty();
    }
}
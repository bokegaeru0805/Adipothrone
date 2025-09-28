using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(BonusMultiplierPopupAttribute))]
public class BonusMultiplierPopupDrawer : PropertyDrawer
{
    // ドロップダウンに表示したい倍率の選択肢
    private readonly float[] optionValues = { 0f, 0.1f, 0.5f, 1.0f };
    private readonly string[] optionLabels;

    public BonusMultiplierPopupDrawer()
    {
        // 選択肢のラベルを作成
        optionLabels = optionValues.Select(value => value.ToString() + "x").ToArray();
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Float)
        {
            EditorGUI.LabelField(position, label.text, "この属性はfloat型にのみ使用できます。");
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        float currentValue = property.floatValue;
        int currentIndex = Array.IndexOf(optionValues, currentValue);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        int newIndex = EditorGUI.Popup(position, label.text, currentIndex, optionLabels);

        if (newIndex != currentIndex)
        {
            property.floatValue = optionValues[newIndex];
        }

        EditorGUI.EndProperty();
    }
}

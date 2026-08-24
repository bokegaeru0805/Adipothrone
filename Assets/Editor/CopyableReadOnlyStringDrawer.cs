using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(CopyableReadOnlyStringAttribute))]
public class CopyableReadOnlyStringDrawer : PropertyDrawer
{
    private const float CopyButtonWidth = 52f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        string fixedValue = ((CopyableReadOnlyStringAttribute)attribute).FixedValue;
        if (property.stringValue != fixedValue)
        {
            property.stringValue = fixedValue;
        }

        Rect valuePosition = EditorGUI.PrefixLabel(position, label);
        Rect textPosition = new Rect(
            valuePosition.x,
            valuePosition.y,
            valuePosition.width - CopyButtonWidth - EditorGUIUtility.standardVerticalSpacing,
            valuePosition.height
        );
        Rect buttonPosition = new Rect(
            textPosition.xMax + EditorGUIUtility.standardVerticalSpacing,
            valuePosition.y,
            CopyButtonWidth,
            valuePosition.height
        );

        EditorGUI.SelectableLabel(textPosition, fixedValue, EditorStyles.textField);

        if (GUI.Button(buttonPosition, "コピー"))
        {
            EditorGUIUtility.systemCopyBuffer = fixedValue;
        }

        EditorGUI.EndProperty();
    }
}

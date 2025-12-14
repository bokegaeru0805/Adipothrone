using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(SeSelector))]
public class SeSelectorDrawer : PropertyDrawer
{
    // 1行の高さ
    private float lineHeight = EditorGUIUtility.singleLineHeight;
    private float verticalSpacing = EditorGUIUtility.standardVerticalSpacing;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // プロパティの取得
        SerializedProperty categoryProp = property.FindPropertyRelative("category");

        // 全体の枠を描画
        Rect rect = new Rect(position.x, position.y, position.width, lineHeight);

        // ラベル表示（変数名）
        EditorGUI.LabelField(rect, label);
        rect.y += lineHeight + verticalSpacing;

        // インデント
        EditorGUI.indentLevel++;

        // 1. カテゴリ選択プルダウンの描画
        EditorGUI.PropertyField(rect, categoryProp, new GUIContent("Category"));
        rect.y += lineHeight + verticalSpacing;

        SerializedProperty targetSeProp = null;

        // 2. カテゴリに応じたSE選択プルダウンの決定
        // (注意: enumの定義順とintValueが一致している前提)
        switch ((SECategory)categoryProp.intValue)
        {
            case SECategory.UI:
                targetSeProp = property.FindPropertyRelative("uiSe");
                break;
            case SECategory.PlayerAction:
                targetSeProp = property.FindPropertyRelative("playerActionSe");
                break;
            case SECategory.EnemyAction:
                targetSeProp = property.FindPropertyRelative("enemyActionSe");
                break;
            case SECategory.Field:
                targetSeProp = property.FindPropertyRelative("fieldSe");
                break;
            case SECategory.SystemEvent:
                targetSeProp = property.FindPropertyRelative("systemEventSe");
                break;
        }

        // 3. 具体的なSE選択プルダウンの描画
        if (targetSeProp != null)
        {
            EditorGUI.PropertyField(rect, targetSeProp, new GUIContent("SE Name"));
        }
        else
        {
            EditorGUI.LabelField(rect, "No Enum definition found for this category.");
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // ラベル行 + カテゴリ行 + SE選択行 + 余白
        return (lineHeight + verticalSpacing) * 3;
    }
}
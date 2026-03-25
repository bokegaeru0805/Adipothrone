using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MaterialItemData))]
public class MaterialItemDataEditor : Editor
{
    // MaterialItemData 専用
    SerializedProperty itemID;

    // BaseItemData 共通
    SerializedProperty itemName;
    SerializedProperty itemSprite;
    SerializedProperty description;

    void OnEnable()
    {
        // MaterialItemData 専用
        itemID = serializedObject.FindProperty("itemID");

        // BaseItemData 共通項目
        itemName = serializedObject.FindProperty("itemName");
        itemSprite = serializedObject.FindProperty("itemSprite");
        description = serializedObject.FindProperty("description");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // IDを最上部に
        EditorGUILayout.PropertyField(itemID);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("【基本情報】", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(itemName, new GUIContent("表示名"));
        EditorGUILayout.PropertyField(itemSprite, new GUIContent("アイコン"));
        EditorGUILayout.PropertyField(description, new GUIContent("説明文"));

        serializedObject.ApplyModifiedProperties();
    }
}

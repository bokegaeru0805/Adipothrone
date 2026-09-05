using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MaterialItemData))]
[CanEditMultipleObjects]
public class MaterialItemDataEditor : BaseItemDataEditor
{
    // MaterialItemData 専用
    private SerializedProperty itemID;
    private bool basicOpen = true;

    protected override void OnEnable()
    {
        base.OnEnable();

        // MaterialItemData 専用
        itemID = serializedObject.FindProperty("itemID");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(itemID, new GUIContent("素材ID"));
        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        basicOpen = EditorGUILayout.Foldout(basicOpen, "基本情報", true, EditorStyles.foldoutHeader);
        if (basicOpen)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(itemName, new GUIContent("表示名"));
            EditorGUILayout.PropertyField(itemSprite, new GUIContent("アイコン"));
            EditorGUILayout.PropertyField(itemRank, new GUIContent("レア度"));
            EditorGUILayout.PropertyField(buyPrice, new GUIContent("購入価格"));
            EditorGUILayout.PropertyField(isSellable, new GUIContent("売却可能"));
            using (new EditorGUI.DisabledScope(!isSellable.hasMultipleDifferentValues && !isSellable.boolValue))
                EditorGUILayout.PropertyField(sellPrice, new GUIContent("売却価格"));
            EditorGUILayout.PropertyField(description, new GUIContent("説明文"));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }
}

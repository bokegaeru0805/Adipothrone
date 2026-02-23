using UnityEditor;
using UnityEngine;
using Fungus.EditorUtils;
using Fungus;

[CustomEditor(typeof(SetPortraitTransformCommand))]
public class SetPortraitTransformCommandEditor : CommandEditor
{
    protected SerializedProperty targetCharacterProp;
    
    protected SerializedProperty changeDirectionProp;
    protected SerializedProperty isLeftProp;
    
    protected SerializedProperty changePositionProp;
    protected SerializedProperty positionXProp;
    
    protected SerializedProperty changeSortOrderProp;
    protected SerializedProperty sortOrderProp;

    public override void OnEnable()
    {
        base.OnEnable();
        
        targetCharacterProp = serializedObject.FindProperty("targetCharacter");
        
        changeDirectionProp = serializedObject.FindProperty("changeDirection");
        isLeftProp = serializedObject.FindProperty("isLeft");
        
        changePositionProp = serializedObject.FindProperty("changePosition");
        positionXProp = serializedObject.FindProperty("positionX");
        
        changeSortOrderProp = serializedObject.FindProperty("changeSortOrder");
        sortOrderProp = serializedObject.FindProperty("sortOrder");
    }

    public override void DrawCommandGUI()
    {
        serializedObject.Update();

        // Fungus標準 Character 選択ドロップダウン
        CommandEditor.ObjectField<Character>(
            targetCharacterProp,
            new GUIContent("Character", "話しているキャラクター"),
            new GUIContent("<None>"), // キャラクターが設定されていない場合の表示
            Character.ActiveCharacters // シーン内の全キャラクターをリストアップ
        );

        EditorGUILayout.Space();

        // --- 向きの設定 (ShowIfの再現) ---
        EditorGUILayout.PropertyField(changeDirectionProp, new GUIContent("Change Direction"));
        if (changeDirectionProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(isLeftProp, new GUIContent("Is Left"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        // --- 配置の設定 (ShowIfの再現) ---
        EditorGUILayout.PropertyField(changePositionProp, new GUIContent("Change Position"));
        if (changePositionProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(positionXProp, new GUIContent("Position X"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        // --- 描画順の設定 (ShowIfの再現) ---
        EditorGUILayout.PropertyField(changeSortOrderProp, new GUIContent("Change Sort Order"));
        if (changeSortOrderProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(sortOrderProp, new GUIContent("Sort Order"));
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
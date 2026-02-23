using Fungus;
using Fungus.EditorUtils;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ControlPortraitCommand))]
public class ControlPortraitCommandEditor : CommandEditor
{
    protected SerializedProperty targetCharacterProp;
    protected SerializedProperty operationProp;
    protected SerializedProperty portraitStringProp;
    protected SerializedProperty fadeDurationProp;

    public override void OnEnable()
    {
        base.OnEnable();

        targetCharacterProp = serializedObject.FindProperty("targetCharacter");
        operationProp = serializedObject.FindProperty("operation");
        portraitStringProp = serializedObject.FindProperty("portraitString");
        fadeDurationProp = serializedObject.FindProperty("fadeDuration");
    }

    public override void DrawCommandGUI()
    {
        serializedObject.Update();

        // Fungus標準 Character 選択ドロップダウン
        CommandEditor.ObjectField<Character>(
            targetCharacterProp,
            new GUIContent("Character", "操作対象のキャラクター"),
            new GUIContent("<None>"), // キャラクターが設定されていない場合の表示
            Character.ActiveCharacters // シーン内の全キャラクターをリストアップ
        );

        EditorGUILayout.Space();

        // 表示/非表示の選択
        EditorGUILayout.PropertyField(
            operationProp,
            new GUIContent("Operation", "表示するか非表示にするかを選択します")
        );

        // Operation が Show の場合のみ Portrait String を表示 (ShowIf機能)
        if (operationProp.enumValueIndex == (int)ControlPortraitCommand.OperationType.Show)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                portraitStringProp,
                new GUIContent(
                    "Portrait String",
                    "空でなければ、指定した文字列で立ち絵を呼び出します（例：Heroin_fat_smile）"
                )
            );
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(
            fadeDurationProp,
            new GUIContent("Fade Duration", "明暗の切り替えにかけるフェード時間")
        );

        serializedObject.ApplyModifiedProperties();
    }
}

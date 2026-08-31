using Fungus;
using Fungus.EditorUtils;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 全身一枚絵用の立ち絵コントローラーに必要な項目だけを表示するInspectorです。
/// </summary>
[CustomEditor(typeof(HeroBattleReadyPortraitController))]
public class HeroBattleReadyPortraitControllerEditor : BasePortraitControllerEditor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        CommandEditor.ObjectField<Character>(
            characterProp,
            new GUIContent("Character", "担当するキャラクター"),
            new GUIContent("<None>"),
            Character.ActiveCharacters
        );

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("bodyImage"),
            new GUIContent("Full Body Image", "全身立ち絵を表示するImage")
        );
        EditorGUILayout.Space();

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (
                iterator.name == "m_Script"
                || iterator.name == "character"
                || iterator.name == "bodyImage"
                || iterator.name == "faceImage"
                || iterator.name == "expressionImage"
                || iterator.name == "portraitSprites"
            )
            {
                continue;
            }

            EditorGUILayout.PropertyField(iterator, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}

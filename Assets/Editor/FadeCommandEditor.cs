using Fungus.EditorUtils;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FadeCommand))]
public class FadeCommandEditor : CommandEditor
{
    private SerializedProperty colorTypeProp;
    private SerializedProperty targetAlphaProp;
    private SerializedProperty durationProp;
    private SerializedProperty waitUntilFinishedProp;

    public override void OnEnable()
    {
        base.OnEnable();

        colorTypeProp = serializedObject.FindProperty("colorType");
        targetAlphaProp = serializedObject.FindProperty("targetAlpha");
        durationProp = serializedObject.FindProperty("duration");
        waitUntilFinishedProp = serializedObject.FindProperty("waitUntilFinished");
    }

    public override void DrawCommandGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("フェード色", EditorStyles.boldLabel);

        int selectedColor = GUILayout.Toolbar(
            colorTypeProp.enumValueIndex,
            new[] { "Black（黒）", "White（白）" }
        );
        if (selectedColor != colorTypeProp.enumValueIndex)
        {
            colorTypeProp.enumValueIndex = selectedColor;
        }

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("画面を覆う強さ（Alpha）", EditorStyles.boldLabel);
        targetAlphaProp.floatValue = EditorGUILayout.Slider(
            new GUIContent("覆う強さ（0～1）", "0 = 透明で何も覆わない、1 = 不透明で完全に覆う"),
            targetAlphaProp.floatValue,
            0f,
            1f
        );
        EditorGUILayout.HelpBox("0 = 透明（何も覆わない） / 1 = 不透明（完全に覆う）", MessageType.None);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("変化時間", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            durationProp,
            new GUIContent("秒数", "フェードの変化にかける時間（秒）")
        );

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("よく使う秒数");
        if (GUILayout.Button("0.25秒"))
        {
            durationProp.floatValue = 0.25f;
        }
        if (GUILayout.Button("0.5秒"))
        {
            durationProp.floatValue = 0.5f;
        }
        if (GUILayout.Button("1秒"))
        {
            durationProp.floatValue = 1f;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(
            waitUntilFinishedProp,
            new GUIContent("完了まで待機", "有効の場合、フェード完了後に次のコマンドへ進みます")
        );

        serializedObject.ApplyModifiedProperties();
    }
}

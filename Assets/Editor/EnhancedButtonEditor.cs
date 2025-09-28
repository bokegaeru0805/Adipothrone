using UnityEditor;
using UnityEditor.UI; // ButtonEditorクラスを使うために必要

// このエディタがどのクラスのためのものかをUnityに教える属性
[CustomEditor(typeof(EnhancedButton))]
public class EnhancedButtonEditor : ButtonEditor
{
    // Inspectorの表示内容をカスタマイズするためにOnInspectorGUIを上書きする
    public override void OnInspectorGUI()
    {
        // まず、元のButtonが持っているInspector項目をすべて描画する
        base.OnInspectorGUI();

        // EnhancedButtonクラスへの参照を取得
        EnhancedButton targetScript = (EnhancedButton)target;

        EditorGUILayout.Space(); // 少しスペースを空けて見やすくする

        // serializedObjectを使って、対象スクリプトのプロパティを取得し、表示する
        // "targetText" の部分は、EnhancedButton.cs内の変数名と完全に一致させる
        SerializedProperty property = serializedObject.FindProperty("targetText");
        EditorGUILayout.PropertyField(property);

        // 加えられた変更を適用する
        serializedObject.ApplyModifiedProperties();
    }
}

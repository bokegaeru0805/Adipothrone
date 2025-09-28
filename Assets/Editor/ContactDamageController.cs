using UnityEditor;

[CustomEditor(typeof(ContactDamageController))]
public class ContactDamageControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // ヘルプボックスをInspectorの一番上に表示
        EditorGUILayout.HelpBox(
            "このスクリプトは\n敵オブジェクト本体にアタッチしてください。\n",
            MessageType.Info // ← 他に Warning, Error も使えます
        );

        // 元のインスペクターの内容をそのまま描画
        DrawDefaultInspector();
    }
}

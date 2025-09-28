using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyHealth))] // ←対象のスクリプト名
public class EnemyHealthEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // ヘルプボックスをInspectorの一番上に表示
        EditorGUILayout.HelpBox(
            "このスクリプトはEnemyActivatorの子オブジェクトとして設置されている\n敵オブジェクト本体にアタッチしてください。\n",
            MessageType.Info // ← 他に Warning, Error も使えます
        );

        // 元のインスペクターの内容をそのまま描画
        DrawDefaultInspector();
    }
}

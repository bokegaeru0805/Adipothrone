using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Robot_move), true)]
[CanEditMultipleObjects]
public class Robot_moveEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // ① 通常のInspector描画（SerializeFieldされた項目）
        DrawDefaultInspector();

        // ② 読み取り専用表示（実行中の内部値を確認）
        Robot_move robot = (Robot_move)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("【実行時情報】", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            // public get-only プロパティ
            // EditorGUILayout.FloatField("offsetX", robot.offsetX);

            // private 変数 afterBlade_Sec をリフレクションで表示
            FieldInfo afterShootField = typeof(Robot_move).GetField(
                "afterShoot_Sec",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (afterShootField != null)
            {
                float value = (float)afterShootField.GetValue(robot);
                EditorGUILayout.FloatField("afterShoot_Sec", value);
                EditorGUILayout.LabelField(
                    "※ 弾の攻撃後、プレイヤーが操作不能になる秒数",
                    EditorStyles.wordWrappedMiniLabel
                );
            }

            FieldInfo afterBladeField = typeof(Robot_move).GetField(
                "afterBlade_Sec",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (afterBladeField != null)
            {
                float value = (float)afterBladeField.GetValue(robot);
                EditorGUILayout.FloatField("afterBlade_Sec", value);
                EditorGUILayout.LabelField(
                    "※ 剣での攻撃後、プレイヤーが操作不能になる秒数",
                    EditorStyles.wordWrappedMiniLabel
                );
            }

            FieldInfo inputWindowTimeField = typeof(Robot_move).GetField(
                "inputWindowTime",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (inputWindowTimeField != null)
            {
                float value = (float)inputWindowTimeField.GetValue(robot);
                EditorGUILayout.FloatField("inputWindowTime", value);
                EditorGUILayout.LabelField(
                    "※ 剣の攻撃入力の受付時間",
                    EditorStyles.wordWrappedMiniLabel
                );
            }

            FieldInfo maxAttackCountField = typeof(Robot_move).GetField(
                "maxAttackCount",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (maxAttackCountField != null)
            {
                int value = (int)maxAttackCountField.GetValue(robot);
                EditorGUILayout.IntField("maxAttackCount", value);
                EditorGUILayout.LabelField(
                    "※ 剣の攻撃回数の最大値",
                    EditorStyles.wordWrappedMiniLabel
                );
            }

            FieldInfo bladeSwingOffsetRadiusField = typeof(Robot_move).GetField(
                "bladeSwingOffsetRadius",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (bladeSwingOffsetRadiusField != null)
            {
                float value = (float)bladeSwingOffsetRadiusField.GetValue(robot);
                EditorGUILayout.FloatField("bladeSwingOffsetRadius", value);
                EditorGUILayout.LabelField(
                    "※ 剣の振り子の半径（オフセット）",
                    EditorStyles.wordWrappedMiniLabel
                );
            }

            FieldInfo smoothTimeField = typeof(Robot_move).GetField(
                "_smoothTime",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (smoothTimeField != null)
            {
                float value = (float)smoothTimeField.GetValue(robot);
                EditorGUILayout.FloatField("smoothTime", value);
                EditorGUILayout.LabelField(
                    "※ ロボットがプレイヤーの左右を移動するのにかかる時間",
                    EditorStyles.wordWrappedMiniLabel
                );
            }

            // 同様に他の private 値（例: maxAttackCount）を表示したい場合も追加可能
        }
    }
}

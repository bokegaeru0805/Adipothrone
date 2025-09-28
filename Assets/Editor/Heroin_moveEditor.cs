using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Heroin_move), true)]
[CanEditMultipleObjects]
public class Heroin_move_moveEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // ① 通常のInspector描画（SerializeFieldされた項目）
        DrawDefaultInspector();

        // ② 読み取り専用表示（実行中の内部値を確認）
        Heroin_move heroin = (Heroin_move)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("【実行時情報】", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            // public get-only プロパティ
            EditorGUILayout.FloatField("通常速度", heroin.m_defaultSpeed);

            // private 変数 afterBlade_Sec をリフレクションで表示

            FieldInfo dashSpeedField = typeof(Heroin_move).GetField(
                "m_dashSpeed",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (dashSpeedField != null)
            {
                float value = (float)dashSpeedField.GetValue(heroin);
                EditorGUILayout.FloatField("ダッシュ速度", value);
            }
            FieldInfo jumpHeightField = typeof(Heroin_move).GetField(
                "jumpHeight",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (jumpHeightField != null)
            {
                float value = (float)jumpHeightField.GetValue(heroin);
                EditorGUILayout.FloatField("ジャンプの高さ", value);
            }

            FieldInfo damageXField = typeof(Heroin_move).GetField(
                "damageX",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (damageXField != null)
            {
                float value = (float)damageXField.GetValue(heroin);
                EditorGUILayout.FloatField("damageX", value);
                EditorGUILayout.LabelField(
                    "※ ダメージを食らったときのx軸の移動具合",
                    EditorStyles.wordWrappedMiniLabel
                );
            }

            FieldInfo moveStartSecField = typeof(Heroin_move).GetField(
                "MoveStart_Sec",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (moveStartSecField != null)
            {
                float value = (float)moveStartSecField.GetValue(heroin);
                EditorGUILayout.FloatField("MoveStart_Sec", value);
                EditorGUILayout.LabelField(
                    "※ ダメージを食らったときの硬直時間",
                    EditorStyles.wordWrappedMiniLabel
                );
            }

            FieldInfo immunityDurationField = typeof(Heroin_move).GetField(
                "immunityDuration",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (immunityDurationField != null)
            {
                float value = (float)immunityDurationField.GetValue(heroin);
                EditorGUILayout.FloatField("immunityDuration", value);
                EditorGUILayout.LabelField("※ 動ける無敵時間", EditorStyles.wordWrappedMiniLabel);
            }

            FieldInfo attackMoveSlowRateField = typeof(Heroin_move).GetField(
                "attackMoveSlowRate",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (attackMoveSlowRateField != null)
            {
                float value = (float)attackMoveSlowRateField.GetValue(heroin);
                EditorGUILayout.FloatField("attackMoveSlowRate", value);
                EditorGUILayout.LabelField(
                    "※ 剣での攻撃中の移動速度の減少率",
                    EditorStyles.wordWrappedMiniLabel
                );
            }

            // 同様に他の private 値（例: maxAttackCount）を表示したい場合も追加可能
        }
    }
}

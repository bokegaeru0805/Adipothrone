using UnityEditor;
using UnityEngine;

/// <summary>
/// ステータス強化アイテム専用のエディタ。
/// </summary>
[CustomEditor(typeof(StatusEnhanceItemData))]
public class StatusEnhanceItemDataEditor : BaseItemDataEditor
{
    private SerializedProperty itemID;
    private SerializedProperty enhanceEffects;

    protected override void OnEnable()
    {
        base.OnEnable();

        // 独自項目の取得
        itemID = serializedObject.FindProperty("itemID");
        enhanceEffects = serializedObject.FindProperty("enhanceEffects");
    }

    protected override void DrawTopSection()
    {
        EditorGUILayout.PropertyField(itemID, new GUIContent("アイテムID"));
    }

    protected override void DrawCustomSection()
    {
        // --- 強化効果の描画（Boxで囲む） ---
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("【強化効果】", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(enhanceEffects, new GUIContent("強化リスト"), true);
        EditorGUILayout.EndVertical();
    }
}
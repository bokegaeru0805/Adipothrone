using UnityEditor;
using UnityEngine;

/// <summary>
/// 回復アイテム専用のエディタ。基本情報はBaseItemDataEditorに任せます。
/// </summary>
[CustomEditor(typeof(HealItemData))]
public class HealItemDataEditor : BaseItemDataEditor
{
    private SerializedProperty itemID;
    private SerializedProperty hpHealAmount;
    private SerializedProperty wpHealAmount;
    private SerializedProperty buffEffects;

    protected override void OnEnable()
    {
        // 親クラスのOnEnableを呼んで共通項目を取得
        base.OnEnable();

        // 独自項目の取得
        itemID = serializedObject.FindProperty("itemID");
        hpHealAmount = serializedObject.FindProperty("hpHealAmount");
        wpHealAmount = serializedObject.FindProperty("wpHealAmount");
        buffEffects = serializedObject.FindProperty("buffEffects");
    }

    protected override void DrawTopSection()
    {
        // 最上部にIDを描画
        EditorGUILayout.PropertyField(itemID, new GUIContent("アイテムID"));
    }

    protected override void DrawCustomSection()
    {
        // --- 回復量の描画（Boxで囲む） ---
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("【回復量】", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(hpHealAmount, new GUIContent("HP回復量"));
        EditorGUILayout.PropertyField(wpHealAmount, new GUIContent("WP回復量"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // --- バフ効果の描画（Boxで囲む） ---
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("【バフ効果】", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(buffEffects, new GUIContent("バフリスト"), true);
        EditorGUILayout.EndVertical();
    }
}
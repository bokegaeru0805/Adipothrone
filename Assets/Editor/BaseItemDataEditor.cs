using UnityEditor;
using UnityEngine;

/// <summary>
/// 全てのアイテムデータに共通するエディタ拡張の親クラス。
/// 基本情報の描画と、視覚的な枠（Box）の提供を行います。
/// </summary>
public class BaseItemDataEditor : Editor
{
    // 共通プロパティ
    protected SerializedProperty itemName;
    protected SerializedProperty itemSprite;
    protected SerializedProperty itemRank;
    protected SerializedProperty buyPrice;
    protected SerializedProperty sellPrice;
    protected SerializedProperty isSellable;
    protected SerializedProperty description;

    protected virtual void OnEnable()
    {
        // BaseItemDataにある共通項目を取得
        itemName = serializedObject.FindProperty("itemName");
        itemSprite = serializedObject.FindProperty("itemSprite");
        itemRank = serializedObject.FindProperty("itemRank");
        buyPrice = serializedObject.FindProperty("buyPrice");
        sellPrice = serializedObject.FindProperty("sellPrice");
        isSellable = serializedObject.FindProperty("isSellable");
        description = serializedObject.FindProperty("description");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 派生クラスで最上部（IDなど）を描画するためのフック
        DrawTopSection();

        EditorGUILayout.Space();

        // --- 基本情報の描画（Boxで囲む） ---
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("【基本情報】", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(itemName, new GUIContent("表示名"));
        EditorGUILayout.PropertyField(itemSprite, new GUIContent("アイコン"));
        EditorGUILayout.PropertyField(itemRank, new GUIContent("レア度"));
        EditorGUILayout.PropertyField(buyPrice, new GUIContent("購入価格"));
        EditorGUILayout.PropertyField(sellPrice, new GUIContent("売却価格"));
        EditorGUILayout.PropertyField(isSellable, new GUIContent("売却可能か"));
        EditorGUILayout.PropertyField(description, new GUIContent("説明文"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // 派生クラスで独自項目を描画するためのフック
        DrawCustomSection();

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// アイテムIDなど、最上部に表示したい項目を描画します（子クラスでオーバーライド）
    /// </summary>
    protected virtual void DrawTopSection() { }

    /// <summary>
    /// 基本情報の下に、そのアイテム専用の項目を描画します（子クラスでオーバーライド）
    /// </summary>
    protected virtual void DrawCustomSection() { }
}
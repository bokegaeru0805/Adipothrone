using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HealItemData))]
public class HealItemDataEditor : Editor
{
    // HealItemData 専用
    SerializedProperty itemID;
    SerializedProperty hpHealAmount;
    SerializedProperty wpHealAmount;
    SerializedProperty buffEffects;

    // BaseItemData 共通
    SerializedProperty itemName;
    SerializedProperty itemSprite;
    SerializedProperty itemRank;
    SerializedProperty buyPrice;
    SerializedProperty sellPrice;
    SerializedProperty description;

    void OnEnable()
    {
        // HealItemData 専用
        itemID = serializedObject.FindProperty("itemID");
        hpHealAmount = serializedObject.FindProperty("hpHealAmount");
        wpHealAmount = serializedObject.FindProperty("wpHealAmount");
        buffEffects = serializedObject.FindProperty("buffEffects");

        // BaseItemData 共通項目
        itemName = serializedObject.FindProperty("itemName");
        itemSprite = serializedObject.FindProperty("itemSprite");
        itemRank = serializedObject.FindProperty("itemRank");
        buyPrice = serializedObject.FindProperty("buyPrice");
        sellPrice = serializedObject.FindProperty("sellPrice");
        description = serializedObject.FindProperty("description");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // IDを最上部に
        EditorGUILayout.PropertyField(itemID);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("【基本情報】", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(itemName, new GUIContent("表示名"));
        EditorGUILayout.PropertyField(itemSprite, new GUIContent("アイコン"));
        EditorGUILayout.PropertyField(buyPrice, new GUIContent("購入価格"));
        EditorGUILayout.PropertyField(sellPrice, new GUIContent("売却価格"));
        EditorGUILayout.PropertyField(itemRank, new GUIContent("レア度"));
        EditorGUILayout.PropertyField(description, new GUIContent("説明文"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("【回復量】", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(hpHealAmount, new GUIContent("HP回復量"));
        EditorGUILayout.PropertyField(wpHealAmount, new GUIContent("WP回復量"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("【バフ効果】", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(buffEffects, true);

        serializedObject.ApplyModifiedProperties();
    }
}

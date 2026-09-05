using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>会話とショップの分岐に必要な設定だけを表示する。</summary>
[CustomEditor(typeof(NPCDialogueTrigger))]
[CanEditMultipleObjects]
public class NPCDialogueTriggerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        bool isHasShop = HasShopTrigger(serializedObject);
        var defaultShop = serializedObject.FindProperty("isDefaultOpenShop");
        var conditions = serializedObject.FindProperty("dialogueConditions");

        EditorGUILayout.LabelField("条件に一致しない場合", EditorStyles.boldLabel);
        if (isHasShop)
            EditorGUILayout.PropertyField(defaultShop, new GUIContent("ショップを開く"));
        else if (defaultShop.boolValue || defaultShop.hasMultipleDifferentValues)
            DrawMissingShopWarning();

        if (!defaultShop.boolValue || defaultShop.hasMultipleDifferentValues)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultBlockName"),
                new GUIContent("会話ブロック名"));

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("条件は下から評価し、最初に一致した動作を実行します。", MessageType.Info);
        EditorGUILayout.PropertyField(conditions, new GUIContent("条件付きの動作"), true);

        // 入力直後のショップ指定をFlowchart欄にも反映する。
        serializedObject.ApplyModifiedProperties();
        if (NeedsFlowchartFields())
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("通常会話のFlowchart", EditorStyles.boldLabel);
            var global = serializedObject.FindProperty("useGlobalFlowchart");
            EditorGUILayout.PropertyField(global, new GUIContent("共通Flowchartを使う"));
            if (!global.boolValue || global.hasMultipleDifferentValues)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("targetFlowchart"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("speechBubbleObject"));
        serializedObject.ApplyModifiedProperties();
    }

    private bool NeedsFlowchartFields()
    {
        foreach (var selected in targets)
        {
            using (var data = new SerializedObject(selected))
            {
                if (!data.FindProperty("isDefaultOpenShop").boolValue)
                    return true;

                var conditions = data.FindProperty("dialogueConditions");
                for (int i = 0; i < conditions.arraySize; i++)
                {
                    if (!conditions.GetArrayElementAtIndex(i).FindPropertyRelative("isOpenShop").boolValue)
                        return true;
                }
            }
        }
        return false;
    }

    internal static bool HasShopTrigger(SerializedObject data)
    {
        foreach (var selected in data.targetObjects)
        {
            if (!(selected is NPCDialogueTrigger npc)
                || npc.GetComponent<ShopInteractionTrigger>() == null)
                return false;
        }
        return true;
    }

    private static void DrawMissingShopWarning()
    {
        EditorGUILayout.HelpBox(
            "ショップ起動が設定されています。同じオブジェクトにShopInteractionTriggerを追加してください。複数選択時は全対象に必要です。",
            MessageType.Warning);
    }
}

/// <summary>条件ごとの店舗起動チェックと会話ブロック名を切り替える。</summary>
[CustomPropertyDrawer(typeof(DialogueCondition))]
public class NPCDialogueConditionDrawer : PropertyDrawer
{
    private const float WarningHeight = 54f;

    // 同じオブジェクトへのコンポーネント追加・削除を表示へ反映する。
    public override bool CanCacheInspectorGUI(SerializedProperty property) => false;

    private static List<SerializedProperty> GetVisibleFields(SerializedProperty property)
    {
        var fields = new List<SerializedProperty>
        {
            property.FindPropertyRelative("requiredFlags")
        };
        var shop = property.FindPropertyRelative("isOpenShop");
        if (NPCDialogueTriggerEditor.HasShopTrigger(property.serializedObject))
            fields.Add(shop);
        if (!shop.boolValue || shop.hasMultipleDifferentValues)
            fields.Add(property.FindPropertyRelative("blockNameToExecute"));
        fields.Add(property.FindPropertyRelative("showBubble"));
        fields.Add(property.FindPropertyRelative("onDialogueTriggered"));
        return fields;
    }

    private static bool IsMissingShop(SerializedProperty property)
    {
        var shop = property.FindPropertyRelative("isOpenShop");
        return (shop.boolValue || shop.hasMultipleDifferentValues)
            && !NPCDialogueTriggerEditor.HasShopTrigger(property.serializedObject);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return height;

        foreach (var child in GetVisibleFields(property))
            height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(child, true);
        if (IsMissingShop(property))
            height += EditorGUIUtility.standardVerticalSpacing + WarningHeight;
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            // 描画途中でチェックが変わっても、このフレームの高さ計算と一致させる。
            var fields = GetVisibleFields(property);
            bool isMissingShop = IsMissingShop(property);
            foreach (var child in fields)
            {
                row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
                row.height = EditorGUI.GetPropertyHeight(child, true);
                var childLabel = child.name == "isOpenShop"
                    ? new GUIContent("ショップを開く", child.tooltip)
                    : new GUIContent(child.displayName, child.tooltip);
                EditorGUI.PropertyField(row, child, childLabel, true);
            }

            if (isMissingShop)
            {
                row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
                row.height = WarningHeight;
                EditorGUI.HelpBox(EditorGUI.IndentedRect(row),
                    "ショップ起動が設定されています。同じオブジェクトにShopInteractionTriggerが必要です。",
                    MessageType.Warning);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}

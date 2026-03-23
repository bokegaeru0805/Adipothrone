using UnityEditor;
using UnityEngine;

/// <summary>
/// レシピアイテム専用のエディタ。
/// レシピは売買不可であるため、価格や売却可否の項目を非表示にし、専用のレイアウトで描画します。
/// </summary>
[CustomEditor(typeof(RecipeItemData))] public class RecipeItemDataEditor : BaseItemDataEditor
{
    private SerializedProperty itemID;
    private SerializedProperty materials;
    private SerializedProperty craftedItem;
    private SerializedProperty maxCraftCount;

    protected override void OnEnable()
    {
        // 親クラスのOnEnableを呼んで共通項目（itemName, itemSpriteなど）を取得
        base.OnEnable();

        // レシピ独自のプロパティを取得
        itemID = serializedObject.FindProperty("itemID");
        materials = serializedObject.FindProperty("materials");
        craftedItem = serializedObject.FindProperty("craftedItem");
        maxCraftCount = serializedObject.FindProperty("maxCraftCount");
    }

    /// <summary>
    /// 親クラスのOnInspectorGUIを完全に上書きし、売買関連の項目を除外して描画します。
    /// </summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // --- 売買不可の強制設定（バックグラウンドで値を固定） ---
        buyPrice.intValue = 0;
        sellPrice.intValue = 0;
        isSellable.boolValue = false;

        // 最上部の描画（レシピID）
        DrawTopSection();

        EditorGUILayout.Space();

        // --- 基本情報の描画（売買関連の項目を除外してBoxで囲む） ---
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("【基本情報】", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(itemName, new GUIContent("表示名"));
        EditorGUILayout.PropertyField(itemSprite, new GUIContent("アイコン"));
        EditorGUILayout.PropertyField(itemRank, new GUIContent("レア度"));
        // ※ buyPrice, sellPrice, isSellable は描画しない
        EditorGUILayout.PropertyField(description, new GUIContent("説明文"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // 独自項目（素材や完成品など）の描画
        DrawCustomSection();

        serializedObject.ApplyModifiedProperties();
    }

    protected override void DrawTopSection()
    {
        EditorGUILayout.PropertyField(itemID, new GUIContent("レシピID"));
    }

    protected override void DrawCustomSection()
    {
        // --- 完成品の描画 ---
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("【完成品】", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(craftedItem, new GUIContent("合成されるアイテム"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // --- 必要素材リストの描画 ---
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("【必要素材リスト】", EditorStyles.boldLabel);
        // trueを渡すことで、Listの中身を展開して表示できるようにする
        EditorGUILayout.PropertyField(materials, new GUIContent("素材と個数"), true);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // --- 合成条件の描画 ---
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("【合成条件】", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(maxCraftCount, new GUIContent("最大合成可能回数"));

        // 0以下の場合は、無制限であることをインスペクター上で分かりやすく明示する
        if (maxCraftCount.intValue <= 0)
        {
            EditorGUILayout.HelpBox("0以下のため、このレシピは【無制限】に合成可能です。", MessageType.Info);
        }
        EditorGUILayout.EndVertical();
    }
}
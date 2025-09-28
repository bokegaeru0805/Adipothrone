using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // LINQを使用するために必要

// PropertyDrawerでInspectorにカスタムUIを描画するために必要
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 会話分岐の単一条件データ。
/// FlagManagerのBoolEnumと、そのEnum値のペアを保存します。
/// </summary>
[System.Serializable]
public class DialogueFlagConditionData
{
    // === Inspectorで表示・設定するためのフィールド ===
    // Enumの型名を文字列で保存（Custom Editorがこの文字列を使ってTypeを取得）
    [SerializeField]
    private string enumTypeName = "";

    // 選択されたEnum型内のEnumメンバー名を文字列で保存
    [SerializeField]
    private string enumValueName = "";

    // === ランタイムで使用するためのキャッシュされたEnumオブジェクト ===
    [NonSerialized] // シリアライズしない（シーンに保存されない）
    public Enum enumValue;

    /// <summary>
    /// エディタでの値変更時やゲーム起動時に呼び出され、enumValueを初期化します。
    /// </summary>
    public void OnValidate()
    {
        CacheEnumValue();
    }

    /// <summary>
    /// シーンロード時やゲーム開始時に呼び出され、enumValueを初期化します。
    /// </summary>
    public void Awake()
    {
        CacheEnumValue();
    }

    // EnumTypeがFlagManagerで管理されているEnumかを判定するための静的リスト
    // このリストはEditorスクリプトから設定されることを想定しています。
    public static List<Type> ManagedEnumTypes { get; set; } = new List<Type>();

    private void CacheEnumValue()
    {
        enumValue = null; // まずリセット

        if (string.IsNullOrEmpty(enumTypeName) || string.IsNullOrEmpty(enumValueName))
        {
            return; // タイプ名か値名がなければ処理しない
        }

        // キャッシュされたManagedEnumTypesからEnumのTypeを検索
        Type type = ManagedEnumTypes.FirstOrDefault(t => t.FullName == enumTypeName);

        if (type == null || !type.IsEnum)
        {
            // デバッグログはDrawer側で出すので、ここでは出さないか、詳細レベルを調整する
            return;
        }

        try
        {
            // Enumの文字列値からEnumオブジェクトをパース
            enumValue = (Enum)Enum.Parse(type, enumValueName);
        }
        catch (ArgumentException)
        {
            // デバッグログはDrawer側で出すので、ここでは出さないか、詳細レベルを調整する
            enumValue = null;
        }
    }
}

/// <summary>
/// 特定のFungusブロックを呼び出すための条件グループ。
/// 複数のDialogueFlagConditionDataを持ち、それら全てがTrueの場合にブロックを起動します。
/// </summary>
[System.Serializable]
public class DialogueBlockConditionGroup
{
    [Tooltip("このグループの条件が全て満たされた場合にFungusで起動するブロックの名前")]
    public string blockToCall;

    [Tooltip("このブロックを起動するために必要な真偽値フラグの条件リスト（AND条件）")]
    public List<DialogueFlagConditionData> conditions = new List<DialogueFlagConditionData>();

    /// <summary>
    /// エディタでの値変更時やゲーム起動時に呼び出され、子条件のEnumオブジェクトを初期化します。
    /// </summary>
    public void OnValidate()
    {
        foreach (var condition in conditions)
        {
            condition.OnValidate();
        }
    }

    /// <summary>
    /// シーンロード時やゲーム開始時に呼び出され、子条件のEnumオブジェクトを初期化します。
    /// </summary>
    public void Awake()
    {
        foreach (var condition in conditions)
        {
            condition.Awake();
        }
    }
}
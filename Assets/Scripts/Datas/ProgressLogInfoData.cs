using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 条件に応じて追記されるログのセットを定義するクラス。
/// </summary>
[System.Serializable]
public class ConditionalLog
{
    [Tooltip("このテキストが追記されるためのフラグ条件（AND条件）")]
    public List<FlagConditionPro> conditions = new List<FlagConditionPro>();

    [Tooltip("条件を満たしたときに追記される文章")]
    [TextArea(2, 5)]
    public string additionalText;

    /// <summary>
    /// このログの表示条件がすべて満たされているかを確認します。
    /// </summary>
    public bool AreConditionsMet()
    {
        foreach (var condition in conditions)
        {
            if (!condition.IsMet())
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>
/// ログの1つの追記項目（セクション）を管理するクラス。
/// フラグの状態によって内容を変化させるため、複数の条件付きテキストを保持します。
/// </summary>
[System.Serializable]
public class ProgressLogSection
{
    [Header("管理用メモ（インスペクター表示用）")]
    public string sectionName;

    [Tooltip(
        "表示するテキストのリスト。下から順（逆順）に評価され、最初に条件を満たしたものが表示されます。"
    )]
    public List<ConditionalLog> conditionalLogs = new List<ConditionalLog>();
}

[CreateAssetMenu(fileName = "ProgressLogData", menuName = "Game/ProgressLog")]
public class ProgressLogInfoData : ScriptableObject
{
    public ProgressLogName logName;

    [TextArea(3, 10)]
    public string logText; // 実際の文章

    [Header("状態変化する追記テキストのリスト")]
    [Tooltip("各追記項目（セクション）ごとに条件を判定し、テキストを構築します")]
    public List<ProgressLogSection> logSections = new List<ProgressLogSection>();
}

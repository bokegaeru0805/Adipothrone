using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// このファイルはMonoBehaviourではないため、どのGameObjectにもアタッチしません。
// プロジェクト内に存在するだけで、他のスクリプトから参照できます。

#region ### フラグ条件定義 ###

/// <summary>
/// 単一のフラグ条件を定義するクラス。Bool型とInt型の条件分岐に対応。
/// </summary>
[Serializable]
public class FlagConditionPro
{
    public enum ConditionType
    {
        Bool,
        Int,
    }

    public enum IntComparison
    {
        EqualTo,
        GreaterThan,
        LessThan,
        NotEqualTo,
    }

    public ConditionType conditionType;

    [HideInInspector]
    public string enumTypeName;
    public string enumValueName;

    [Header("Bool Condition")]
    public bool requiredBoolValue = true;

    [Header("Int Condition")]
    public IntComparison intComparison;
    public int requiredIntValue;

    public bool IsMet()
    {
        if (
            FlagManager.instance == null
            || string.IsNullOrEmpty(enumTypeName)
            || string.IsNullOrEmpty(enumValueName)
        )
            return false;

        try
        {
            Type enumType = Type.GetType(enumTypeName);
            if (enumType == null)
                return false;
            Enum enumValue = (Enum)Enum.Parse(enumType, enumValueName);

            switch (conditionType)
            {
                case ConditionType.Bool:
                    return FlagManager.instance.GetBoolFlag(enumValue) == requiredBoolValue;

                case ConditionType.Int:
                    int currentIntValue = FlagManager.instance.GetIntFlag(enumValue);
                    switch (intComparison)
                    {
                        case IntComparison.EqualTo:
                            return currentIntValue == requiredIntValue;
                        case IntComparison.GreaterThan:
                            return currentIntValue > requiredIntValue;
                        case IntComparison.LessThan:
                            return currentIntValue < requiredIntValue;
                        case IntComparison.NotEqualTo:
                            return currentIntValue != requiredIntValue;
                    }
                    break;
            }
        }
        catch
        {
            return false;
        }
        return false;
    }
}
#endregion

#region ### FlagDrivenStatePro用データ構造 ###

/// <summary>
/// FlagDrivenStateProで適用される「状態」を定義するクラス。
/// </summary>
[Serializable]
public class StatePro
{
    [Header("アクティブ状態")]
    public bool changeActiveState;
    public bool isActive = true;

    [Header("スプライト")]
    public bool changeSprite;
    public Sprite sprite;

    [Header("位置")]
    public bool changePosition;
    public Vector3 position;

    [Tooltip("Trueの場合、次にいずれかのエリアを出るまで位置の変更を遅らせます。")]
    public bool delayPositionUntilAreaExit = false;

    // [Header("アニメーション")]
    // public bool changeAnimation;
    // public string animationTrigger;

    // [Header("コライダー")]
    // public bool changeColliderState;
    // public bool isColliderEnabled = true;

    // [Header("サウンド")]
    // public bool playSound;
    // public AudioClip soundToPlay;

    [Header("カスタムイベント")]
    public bool invokeUnityEvent;
    public UnityEvent onStateApply;
}

/// <summary>
/// FlagDrivenStateProで使われる「条件」と「状態」のペアを定義するクラス。
/// </summary>
[Serializable]
public class StateConditionPro
{
    public List<FlagConditionPro> requiredFlags = new();
    public StatePro stateToApply;

    public bool AreAllFlagsMet()
    {
        if (FlagManager.instance == null)
            return false;
        foreach (var flag in requiredFlags)
        {
            if (!flag.IsMet())
                return false;
        }
        return true;
    }
}

#endregion

#region ### NPCDialogueTrigger用データ構造 ###

/// <summary>
/// NPCDialogueTriggerで使われる「条件」と「会話内容」のペアを定義するクラス。
/// </summary>
[Serializable]
public class DialogueCondition
{
    public List<FlagConditionPro> requiredFlags = new List<FlagConditionPro>();

    [Tooltip("条件が満たされたときに実行するFungusブロックの名前")]
    public string blockNameToExecute;

    [Tooltip("この会話がトリガーされたときに実行される追加イベント")]
    public UnityEvent onDialogueTriggered;

    public bool AreAllFlagsMet()
    {
        if (FlagManager.instance == null)
            return false;
        foreach (var flag in requiredFlags)
        {
            if (!flag.IsMet())
                return false;
        }
        return true;
    }
}
#endregion

#region ### FlagAction用データ構造 ###

/// <summary>
/// 単一のフラグ操作（値の設定）を定義するクラス。
/// </summary>
[System.Serializable]
public class FlagOperation
{
    // FlagConditionProの定義を流用
    public enum OperationType
    {
        SetBool,
        SetInt,
    }

    public OperationType operationType;

    [HideInInspector]
    public string enumTypeName;
    public string enumValueName;

    [Header("Bool Value")]
    public bool boolValueToSet = true;

    [Header("Int Value")]
    public int intValueToSet;
}

#endregion

// 以前作成したFlagSystemData.csファイルを開き、末尾に以下のクラスを追加します。

#region ### ShopDialogue用データ構造 ###

/// <summary>
/// 条件に応じて変化するセリフのセットを定義するクラス。
/// </summary>
[System.Serializable]
public class ConditionalDialogue
{
    [Tooltip("このセリフが表示されるためのフラグ条件（AND条件）")]
    public List<FlagConditionPro> conditions = new List<FlagConditionPro>();

    [Tooltip("表示するセリフの候補リスト。複数入力すると、その中からランダムで1つが選ばれます。")]
    [TextArea(3, 5)]
    public List<string> dialogueOptions = new List<string>();

    /// <summary>
    /// このセリフセットの表示条件がすべて満たされているかを確認します。
    /// </summary>
    public bool AreConditionsMet()
    {
        // FlagConditionProのIsMet()メソッドを使い、すべての条件を評価します。
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

#endregion

#region ### CameraMoveArea用BGMデータ構造 ###

/// <summary>
/// 条件に応じて再生するBGMを定義するクラス。
/// </summary>
[System.Serializable]
public class ConditionalBgm
{
    [Tooltip(
        "このBGMを再生するためのフラグ条件のリスト。リスト内の条件はすべてAND（かつ）で評価されます。"
    )]
    public List<FlagConditionPro> requiredFlags = new List<FlagConditionPro>();

    [Tooltip("上記の条件がすべて満たされたときに再生するBGM")]
    public BGMCategory bgmToPlay;

    /// <summary>
    /// このBGMを再生するための条件がすべて満たされているかを確認します。
    /// </summary>
    /// <returns>すべての条件が満たされていればtrueを返します。</returns>
    public bool AreConditionsMet()
    {
        // 1つでも条件が満たされていないフラグがあれば、falseを返す
        foreach (var flagCondition in requiredFlags)
        {
            if (!flagCondition.IsMet())
            {
                return false;
            }
        }
        // 全ての条件が満たされていれば、trueを返す
        return true;
    }
}

#endregion

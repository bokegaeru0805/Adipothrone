using UnityEngine;
using System;

/// <summary>
/// インスペクターでカテゴリ選択 -> 具体的なSE選択 を行うための汎用クラス
/// </summary>
[Serializable]
public class SeSelector
{
    // まずカテゴリを選ぶ
    public SECategory category = SECategory.UI;

    // カテゴリごとの値を保持するフィールド
    public SE_UI uiSe;
    public SE_PlayerAction playerActionSe;
    public SE_EnemyAction enemyActionSe;
    public SE_Field fieldSe;
    public SE_SystemEvent systemEventSe;

    /// <summary>
    /// 現在のCategory設定に基づいて、具体的なEnum値を返します
    /// </summary>
    public Enum GetSelectedEnum()
    {
        switch (category)
        {
            case SECategory.UI:           return uiSe;
            case SECategory.PlayerAction: return playerActionSe;
            case SECategory.EnemyAction:  return enemyActionSe;
            case SECategory.Field:        return fieldSe;
            case SECategory.SystemEvent:  return systemEventSe;
            default:                      return null;
        }
    }
}
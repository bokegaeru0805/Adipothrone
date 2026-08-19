using System.Collections.Generic;
using Fungus;
using UnityEngine;

/// <summary>
/// 複数のFlagConditionProをANDまたはORで評価するFungus条件コマンドの基底クラス。
/// </summary>
public abstract class CheckFlagConditionPro : Condition
{
    public enum LogicalOperator
    {
        And = 0,
        Or = 1,
    }

    [Tooltip("複数条件の結合方法")]
    [SerializeField]
    protected LogicalOperator logicalOperator = LogicalOperator.And;

    [Tooltip("判定するフラグ条件。空の場合は条件不成立として扱います。")]
    [SerializeField]
    protected List<FlagConditionPro> flagConditions = new List<FlagConditionPro>();

    protected override bool EvaluateCondition()
    {
        if (flagConditions == null || flagConditions.Count == 0)
        {
            Debug.LogWarning(GetLocationIdentifier() + " にフラグ条件が設定されていません。");
            return false;
        }

        switch (logicalOperator)
        {
            case LogicalOperator.And:
                foreach (FlagConditionPro condition in flagConditions)
                {
                    if (condition == null || !condition.IsMet())
                        return false;
                }
                return true;

            case LogicalOperator.Or:
                foreach (FlagConditionPro condition in flagConditions)
                {
                    if (condition != null && condition.IsMet())
                        return true;
                }
                return false;

            default:
                return false;
        }
    }

    public override string GetSummary()
    {
        int conditionCount = flagConditions?.Count ?? 0;
        return $"{logicalOperator.ToString().ToUpperInvariant()} ({conditionCount} conditions)";
    }
}

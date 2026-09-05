using System;
using System.Collections.Generic;
using Fungus;
using NaughtyAttributes;
using UnityEngine;

/// <summary>この店の「会話」選択肢から実行する、フラグ条件付きの会話を設定する。</summary>
public class ShopConversation : MonoBehaviour, IShopConversation
{
    [Serializable]
    public class ConversationCondition
    {
        public List<FlagConditionPro> requiredFlags = new List<FlagConditionPro>();

        [Tooltip("条件がすべて一致した場合に実行する会話ブロック名")]
        public string blockNameToExecute;

        public bool AreAllFlagsMet()
        {
            foreach (var flag in requiredFlags)
            {
                if (flag == null || !flag.IsMet())
                    return false;
            }
            return true;
        }
    }

    [Header("店内会話のFlowchart")]
    [SerializeField]
    private Flowchart targetFlowchart;

    [Header("条件に一致しない場合")]
    [Tooltip("デフォルトの会話ブロック名。空欄の場合、一致する条件がないと「会話」を表示しません。")]
    [SerializeField]
    private string defaultBlockName;

    [Header("条件付きの店内会話")]
    [InfoBox("下から順に評価し、最初に一致したブロックを実行します。")]
    [SerializeField]
    private List<ConversationCondition> conversationConditions = new List<ConversationCondition>();

    /// <summary>現在のフラグで実行可能な店内会話があるか。</summary>
    public bool IsAvailable => this != null && isActiveAndEnabled && GetConversationBlock() != null;

    private Block GetConversationBlock()
    {
        if (targetFlowchart == null)
            return null;

        string blockName = defaultBlockName;
        for (int i = conversationConditions.Count - 1; i >= 0; i--)
        {
            var condition = conversationConditions[i];
            if (condition != null && condition.AreAllFlagsMet())
            {
                blockName = condition.blockNameToExecute;
                break;
            }
        }

        return string.IsNullOrWhiteSpace(blockName) ? null : targetFlowchart.FindBlock(blockName);
    }

    /// <summary>選ばれた会話ブロックを実行する。</summary>
    public bool TryStartConversation()
    {
        if (this == null || !isActiveAndEnabled)
            return false;

        Block block = GetConversationBlock();
        if (block == null)
        {
            Debug.LogWarning("実行できる店内会話がありません。Flowchart・ブロック名・条件を確認してください。", this);
            return false;
        }

        if (block.IsExecuting())
        {
            Debug.LogWarning("指定された店内会話はすでに実行中です。", this);
            return false;
        }

        return targetFlowchart.ExecuteBlock(block);
    }
}

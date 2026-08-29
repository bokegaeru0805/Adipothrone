using Fungus;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 同じBlock内で、初回会話と再会話のコマンド列へ分岐します。
/// 会話済み状態はこのコマンドの実行中メモリにだけ保持され、SaveDataには保存されません。
/// </summary>
[CommandInfo(
    "Custom",
    "First Talk Branch",
    "初回実行時は初回会話Labelへ、それ以降は再会話Labelへジャンプします。"
)]
[AddComponentMenu("")]
public class FirstTalkBranchCommand : Command
{
    [Tooltip("初回会話の先頭に置いたLabel名")]
    [SerializeField]
    private string firstTalkLabel = "";

    [Tooltip("再会話の先頭に置いたLabel名")]
    [SerializeField]
    private string repeatTalkLabel = "";

    [HideInInspector]
    [SerializeField]
    private bool isTemplateCreated;

    // 実行中のScene内だけで保持する状態。Unityシリアライズの対象外のため、Scene再読み込み時にfalseへ戻る。
    private bool isTalked;

#if UNITY_EDITOR
    /// <summary>
    /// Flowchart上へ追加された直後に、初回・再会話用の空テンプレートを1回だけ生成します。
    /// </summary>
    public override void OnCommandAdded(Block parentBlock)
    {
        if (isTemplateCreated || Application.isPlaying)
        {
            return;
        }

        // Fungusの追加処理では、この時点ではまだ自身がCommandListへ入っていないため、後続フレームへ遅延します。
        EditorApplication.delayCall += CreateBranchTemplate;
    }

    private void CreateBranchTemplate()
    {
        if (this == null || isTemplateCreated || ParentBlock == null)
        {
            return;
        }

        Block block = ParentBlock;
        Flowchart flowchart = block.GetFlowchart();
        int branchIndex = block.CommandList.IndexOf(this);

        if (flowchart == null || branchIndex < 0)
        {
            return;
        }

        Undo.SetCurrentGroupName("Create First Talk Branch Template");
        int undoGroup = Undo.GetCurrentGroup();

        Undo.RecordObject(this, "Configure First Talk Branch");
        Undo.RecordObject(block, "Create First Talk Branch Template");

        firstTalkLabel = $"FirstTalk_{ItemId}";
        repeatTalkLabel = $"RepeatTalk_{ItemId}";

        int insertIndex = branchIndex + 1;
        InsertLabel(block, flowchart, insertIndex++, firstTalkLabel);
        InsertCommand<TalkEndCommand>(block, flowchart, insertIndex++);
        // InsertCommand<StopBlock>(block, flowchart, insertIndex++);
        InsertLabel(block, flowchart, insertIndex++, repeatTalkLabel);
        InsertCommand<TalkEndCommand>(block, flowchart, insertIndex++);
        // InsertCommand<StopBlock>(block, flowchart, insertIndex);

        isTemplateCreated = true;
        EditorUtility.SetDirty(this);
        EditorUtility.SetDirty(block);
        PrefabUtility.RecordPrefabInstancePropertyModifications(block);
        Undo.CollapseUndoOperations(undoGroup);
    }

    private static void InsertLabel(Block block, Flowchart flowchart, int index, string labelName)
    {
        Label label = InsertCommand<Label>(block, flowchart, index);
        SerializedObject serializedLabel = new SerializedObject(label);
        serializedLabel.FindProperty("key").stringValue = labelName;
        serializedLabel.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T InsertCommand<T>(Block block, Flowchart flowchart, int index)
        where T : Command
    {
        T command = Undo.AddComponent<T>(block.gameObject);
        command.ParentBlock = block;
        command.ItemId = flowchart.NextItemId();
        command.OnCommandAdded(block);
        block.CommandList.Insert(index, command);
        return command;
    }
#endif

    public override void OnEnter()
    {
        if (ParentBlock == null)
        {
            Debug.LogError("First Talk Branchを実行できません。親Blockが見つかりません。");
            return;
        }

        string targetLabel = isTalked ? repeatTalkLabel : firstTalkLabel;
        int labelIndex = ParentBlock.GetLabelIndex(targetLabel);

        if (labelIndex < 0)
        {
            Debug.LogError($"{GetLocationIdentifier()} の対象Labelが見つかりません: {targetLabel}");
            Continue();
            return;
        }

        isTalked = true;
        Continue(labelIndex + 1);
    }

    public override string GetSummary()
    {
        if (string.IsNullOrEmpty(firstTalkLabel) || string.IsNullOrEmpty(repeatTalkLabel))
        {
            return "Error: 初回会話Labelと再会話Labelを設定してください";
        }

        return $"初回: {firstTalkLabel} / 再会話: {repeatTalkLabel}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(235, 191, 217, 255);
    }
}

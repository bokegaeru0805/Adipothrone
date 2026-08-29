using System.Collections.Generic;
using UnityEngine;

namespace Fungus
{
    /// <summary>
    /// 登録された候補から一つを選んで表示するSayコマンドです。
    /// </summary>
    //[CommandInfo("Narrative", "Random Say", "候補からランダムに一つのセリフを表示します。")]
    [AddComponentMenu("")]
    public class RandomSayCommand : Say
    {
        [Tooltip("ランダムに選ばれるセリフ候補")]
        [SerializeField]
        private List<string> dialogueOptions = new List<string>();

        public IReadOnlyList<string> DialogueOptions => dialogueOptions;

        public void SetDialogueOptions(IEnumerable<string> options)
        {
            dialogueOptions.Clear();
            if (options != null)
                dialogueOptions.AddRange(options);
        }

        public override void OnEnter()
        {
            if (dialogueOptions.Count > 0)
                storyText = dialogueOptions[Random.Range(0, dialogueOptions.Count)];

            base.OnEnter();
        }

        public override string GetSummary()
        {
            return $"RANDOM ({dialogueOptions.Count}) " + base.GetSummary();
        }
    }
}

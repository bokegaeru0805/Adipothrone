using Fungus;
using UnityEngine;

namespace Fungus
{
    /// <summary>
    /// 【基底クラス】GlobalFlowchart内の "DialogueSeed" というInteger変数の値を、指定した整数と比較する共通ロジック。
    /// このクラス自体はコマンドメニューには表示されません。
    /// </summary>
    public abstract class CheckDialogueSeed : Condition
    {
        [Tooltip("使用する比較演算子（等しい、大きい、など）")]
        [SerializeField]
        protected CompareOperator compareOperator = CompareOperator.Equals;

        [Tooltip("比較対象となる整数値")]
        [SerializeField]
        protected IntegerData rhsInteger = new IntegerData(0);

        // GlobalFlowchartとDialogueSeed変数への参照を静的にキャッシュ
        private static Flowchart globalFlowchart;
        private static IntegerVariable dialogueSeedVariable;

        /// <summary>
        /// GlobalFlowchartとDialogueSeed変数を検索し、キャッシュします。
        /// </summary>
        private bool FindAndCacheDialogueSeedVariable()
        {
            if (dialogueSeedVariable != null)
                return true;

            if (globalFlowchart == null)
            {
                GameObject go = GameObject.Find("GlobalFlowchart");
                if (go != null)
                    globalFlowchart = go.GetComponent<Flowchart>();
            }

            if (globalFlowchart != null)
            {
                dialogueSeedVariable = globalFlowchart.GetVariable<IntegerVariable>("DialogueSeed");
            }

            return dialogueSeedVariable != null;
        }

        // --- Fungusの必須オーバーライドメソッド ---

        protected override bool EvaluateCondition()
        {
            if (!FindAndCacheDialogueSeedVariable())
            {
                Debug.LogError(
                    "GlobalFlowchart内に 'DialogueSeed' という名前のInteger変数が見つかりません。"
                );
                return false;
            }

            int lhs = dialogueSeedVariable.Value;
            int rhs = rhsInteger.Value;

            // compareOperatorの値に応じて、switch文で分岐して比較を行う
            switch (compareOperator)
            {
                case CompareOperator.Equals:
                    return lhs == rhs;
                case CompareOperator.NotEquals:
                    return lhs != rhs;
                case CompareOperator.LessThan:
                    return lhs < rhs;
                case CompareOperator.GreaterThan:
                    return lhs > rhs;
                case CompareOperator.LessThanOrEquals:
                    return lhs <= rhs;
                case CompareOperator.GreaterThanOrEquals:
                    return lhs >= rhs;
                default:
                    return false;
            }
        }

        public override string GetSummary()
        {
            return $"DialogueSeed {VariableUtil.GetCompareOperatorDescription(compareOperator)} {rhsInteger.Value}";
        }

        public override Color GetButtonColor()
        {
            return new Color32(235, 191, 217, 255);
        }
    }
}

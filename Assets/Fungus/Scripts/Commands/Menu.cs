// このコードはFungusライブラリ(https://github.com/snozbot/fungus)の一部です
// MITオープンソースライセンス(https://github.com/snozbot/fungus/blob/master/LICENSE)の下で無料で公開されています

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Fungus
{
    /// <summary>
    /// 選択式のメニューにボタンを表示します。
    /// </summary>
    [CommandInfo("Narrative", "Menu", "選択式のメニューにボタンを表示します")]
    [AddComponentMenu("")]
    public class Menu : Command, ILocalizable, IBlockCaller
    {
        [Tooltip("メニューボタンに表示するテキスト")]
        [TextArea()]
        [SerializeField]
        protected string text = "選択肢のテキスト";

        [Tooltip("選択肢のテキストに関するメモ（他の制作者やローカライズ用など）")]
        [SerializeField]
        protected string description = "";

        // 以前のバージョンとの互換性のためにシリアライズ名を維持
        [FormerlySerializedAs("targetSequence")]
        [Tooltip("この選択肢が選ばれたときに実行されるブロック")]
        [SerializeField]
        protected Block targetBlock;

        [Tooltip("ターゲットブロックが以前に実行されたことがある場合、この選択肢を非表示にします")]
        [SerializeField]
        protected bool hideIfVisited;

        [Tooltip("falseの場合、メニューの選択肢は表示されますが、選択できなくなります")]
        [SerializeField]
        protected BooleanData interactable = new BooleanData(true);

        [Tooltip("このメニューの表示に使用するカスタムのメニューダイアログ。以降の全てのMenuコマンドでこのダイアログが使用されます。")]
        [SerializeField]
        protected MenuDialog setMenuDialog;

        [Tooltip("trueの場合、この選択肢はメニューダイアログに渡されますが非表示として扱われます。これはメニューシャッフル機能を維持しつつ選択肢を隠すために使用できます。")]
        [SerializeField]
        protected BooleanData hideThisOption = new BooleanData(false);

        #region 公開メンバー

        /// <summary>
        /// このコマンドで使用するメニューダイアログを設定または取得します。
        /// </summary>
        public MenuDialog SetMenuDialog
        {
            get { return setMenuDialog; }
            set { setMenuDialog = value; }
        }

        /// <summary>
        /// このコマンドが実行されたときに呼び出されるメインの処理です。
        /// </summary>
        public override void OnEnter()
        {
            // もしカスタムのメニューダイアログが設定されていれば
            if (setMenuDialog != null)
            {
                // 現在アクティブなメニューダイアログを上書きする
                MenuDialog.ActiveMenuDialog = setMenuDialog;
            }

            // この選択肢を非表示にするべきかどうかを判定する
            bool hideOption =
                // 「訪問済みなら隠す」がオン かつ ターゲットブロックが設定済み かつ 実行回数が1回以上
                (hideIfVisited && targetBlock != null && targetBlock.GetExecutionCount() > 0)
                // または、「この選択肢を隠す」がオンの場合
                || hideThisOption.Value;

            // 現在アクティブなメニューダイアログを取得
            var menuDialog = MenuDialog.GetMenuDialog();
            if (menuDialog != null)
            {
                // ダイアログをアクティブにする
                menuDialog.SetActive(true);

                // Flowchart内の変数をテキストに反映させる
                var flowchart = GetFlowchart();
                string displayText = flowchart.SubstituteVariables(text);

                // メニューダイアログに、このコマンドで設定した選択肢を追加する
                menuDialog.AddOption(displayText, interactable, hideOption, targetBlock);
            }

            // 次のコマンドへ処理を移す
            Continue();
        }

        /// <summary>
        /// このコマンドが接続しているブロックのリストを取得します。（Fungusエディタ用）
        /// </summary>
        public override void GetConnectedBlocks(ref List<Block> connectedBlocks)
        {
            if (targetBlock != null)
            {
                connectedBlocks.Add(targetBlock);
            }
        }

        /// <summary>
        /// Fungusエディタのコマンドの要約テキストを生成します。
        /// </summary>
        public override string GetSummary()
        {
            if (targetBlock == null)
            {
                return "エラー: ターゲットブロックが選択されていません";
            }

            if (text == "")
            {
                return "エラー: ボタンのテキストが設定されていません";
            }

            return text + " : " + targetBlock.BlockName;
        }

        /// <summary>
        /// Fungusエディタのコマンドの色を設定します。
        /// </summary>
        public override Color GetButtonColor()
        {
            return new Color32(184, 210, 235, 255);
        }

        /// <summary>
        /// このコマンドが指定されたFungus変数を参照しているか確認します。
        /// </summary>
        public override bool HasReference(Variable variable)
        {
            // interactableまたはhideThisOptionで変数が使われているか、
            // もしくは親クラスで参照されているかをチェック
            return interactable.booleanRef == variable
                || hideThisOption.booleanRef == variable
                || base.HasReference(variable);
        }

        /// <summary>
        /// このコマンドが指定されたブロックを呼び出す可能性があるか確認します。
        /// </summary>
        public bool MayCallBlock(Block block)
        {
            return block == targetBlock;
        }

        #endregion

        #region ILocalizableインターフェースの実装

        /// <summary>
        /// ローカライズ対象の標準テキスト（ボタンのテキスト）を取得します。
        /// </summary>
        public virtual string GetStandardText()
        {
            return text;
        }

        /// <summary>
        /// ローカライズされたテキストをボタンのテキストに設定します。
        /// </summary>
        public virtual void SetStandardText(string standardText)
        {
            text = standardText;
        }

        /// <summary>
        /// ローカライズ用の説明文を取得します。
        /// </summary>
        public virtual string GetDescription()
        {
            return description;
        }

        /// <summary>
        /// ローカライズ用の文字列IDを生成します。
        /// </summary>
        public virtual string GetStringId()
        {
            // Menuコマンドの文字列IDは "MENU.<ローカライズID>.<コマンドID>" という形式
            return "MENU." + GetFlowchartLocalizationId() + "." + itemId;
        }

        #endregion

        #region エディタ用キャッシュ
#if UNITY_EDITOR
        /// <summary>
        /// Unityエディタ上で、このコマンドが参照している変数をキャッシュ（一時保存）します。
        /// </summary>
        protected override void RefreshVariableCache()
        {
            base.RefreshVariableCache();

            var f = GetFlowchart();

            // テキスト内で使われている変数を解析し、リストに保存する
            f.DetermineSubstituteVariables(text, referencedVariables);
        }
#endif
        #endregion
    }
}
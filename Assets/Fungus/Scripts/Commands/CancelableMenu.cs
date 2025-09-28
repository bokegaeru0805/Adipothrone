// このコードはFungusライブラリ(https://github.com/snozbot/fungus)のMenu.csを基に作成されています
// MITオープンソースライセンス(https://github.com/snozbot/fungus/blob/master/LICENSE)の下で無料で公開されています

using UnityEngine;

namespace Fungus
{
    /// <summary>
    /// キャンセルキーが押されると即時に実行される、選択式のメニューにボタンを表示します。
    /// </summary>
    [CommandInfo(
        "Narrative",
        "Cancelable Menu",
        "キャンセルキーが押されると即時に実行される、選択式のメニューにボタンを表示します。"
    )]
    [AddComponentMenu("")]
    public class CancelableMenu : Menu
    {
        #region 公開メンバー

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

                // ★ MenuDialogにキャンセル可能な選択肢として追加する
                menuDialog.AddCancelableOption(displayText, interactable, hideOption, targetBlock);
            }

            // 次のコマンドへ処理を移す
            Continue();
        }

        /// <summary>
        /// Fungusエディタのコマンドの要約テキストを生成します。
        /// </summary>
        public override string GetSummary()
        {
            // 親クラスの要約を取得
            string summary = base.GetSummary();

            // エラーでなければ、キャンセルキー情報を先頭に追加
            if (!summary.StartsWith("エラー"))
            {
                return $"{summary}";
            }

            return summary;
        }

        /// <summary>
        /// Fungusエディタのコマンドの色を設定します。
        /// </summary>
        public override Color GetButtonColor()
        {
            // 通常のMenuコマンドと区別するため、色を変更
            return new Color32(120, 150, 190, 255);
        }

        #endregion
    }
}

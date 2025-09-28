// このコードはFungusライブラリ（https://github.com/snozbot/fungus）の一部です。
// MITオープンソースライセンス（https://github.com/snozbot/fungus/blob/master/LICENSE）の下で無料で公開されています。

using UnityEngine;

namespace Fungus
{
    /// <summary>
    /// ダイアログボックスにテキストを表示します。Fungusの最も基本的なコマンドの一つです。
    /// </summary>
    [CommandInfo("Narrative", "Say", "ダイアログボックスにテキストを表示します。")]
    [AddComponentMenu("")]
    public class Say : Command, ILocalizable
    {
        // [Tooltip]属性は、ユーザーからテキストボックスが見えにくくなるとの報告があったため削除されています。
        [TextArea(5, 10)] // 複数行のテキストをインスペクターで入力しやすくするための属性
        [SerializeField]
        protected string storyText = "";

        [Tooltip("このテキストに関する作者向けのメモや、翻訳者への注釈などを記述します。")]
        [SerializeField]
        protected string description = "";

        [Tooltip("このセリフを話すキャラクター。設定すると、名前が表示されます。")]
        [SerializeField]
        protected Character character;

        [Tooltip("このセリフの時に表示するキャラクターの立ち絵や表情。")]
        [SerializeField]
        protected Sprite portrait;

        [Tooltip("【Heroin専用】表情ファイル名を文字列で指定します。")]
        [SerializeField]
        protected string portraitString = "";

        [Tooltip("テキスト表示中に再生するボイスオーバー（セリフ音声）のオーディオクリップ。")]
        [SerializeField]
        protected AudioClip voiceOverClip;

        [Tooltip("このコマンドが複数回実行された場合でも、常にこのSayテキストを表示するか。")]
        [SerializeField]
        protected bool showAlways = true;

        [Tooltip("showAlwaysがfalseの場合、何回までこのSayテキストを表示するか。")]
        [SerializeField]
        protected int showCount = 1;

        [Tooltip("チェックを入れると、前のSayコマンドのテキストに続けて表示します。")]
        [SerializeField]
        protected bool extendPrevious = false;

        [Tooltip("テキスト表示が完了し、入力待ちでない場合にダイアログをフェードアウトさせるか。")]
        [SerializeField]
        protected bool fadeWhenDone = true;

        [Tooltip("テキスト表示後、プレイヤーがクリックするまで待機するか。")]
        [SerializeField]
        protected bool waitForClick = true;

        [Tooltip("テキスト表示が完了したら、ボイスオーバーの再生を停止するか。")]
        [SerializeField]
        protected bool stopVoiceover = true;

        [Tooltip("（ボイスオーバーがある場合）再生が完了するまで待機するか。")]
        [SerializeField]
        protected bool waitForVO = false;

        [Tooltip(
            "このSayコマンドの表示に使用するSayDialogを、シーン内の特定のオブジェクトに設定します。"
        )]
        [SerializeField]
        protected SayDialog setSayDialog;

        /// <summary>
        /// このコマンドが何回実行されたかを記録するカウンター
        /// </summary>
        protected int executionCount;

        #region Public members (他のスクリプトからアクセス可能な公開メンバー)

        /// <summary>
        /// このセリフを話すキャラクターを取得します。
        /// </summary>
        public virtual Character _Character
        {
            get { return character; }
        }

        /// <summary>
        /// このセリフで表示する立ち絵を取得または設定します。
        /// </summary>
        public virtual Sprite Portrait
        {
            get { return portrait; }
            private set { portrait = value; }
        }

        /// <summary>
        /// Heroin専用の表情文字列を取得または設定します。
        /// </summary>
        public virtual string PortraitString
        {
            get { return portraitString; }
            private set { portraitString = value; }
        }

        /// <summary>
        /// 外部スクリプトからキャラクターを設定するための公開メソッド。
        /// </summary>
        /// <param name="newCharacter">設定したい新しいキャラクター</param>
        public virtual void SetCharacter(Character newCharacter)
        {
            this.character = newCharacter;
        }

        /// <summary>
        /// 外部スクリプトから表情（立ち絵）を設定するための公開メソッド。
        /// </summary>
        /// <param name="newPortrait">設定したい新しい表情のSprite</param>
        public virtual void SetPortrait(Sprite newPortrait)
        {
            this.portrait = newPortrait;
        }

        /// <summary>
        /// 外部スクリプトからHeroin専用の表情文字列を設定するための公開メソッド。
        /// </summary>
        /// <param name="newPortraitString">設定したい表情の文字列</param>
        public virtual void SetPortraitString(string newPortraitString)
        {
            this.portraitString = newPortraitString;
        }

        /// <summary>
        /// 前のテキストに続けて表示するかどうかを取得します。
        /// </summary>
        public virtual bool ExtendPrevious
        {
            get { return extendPrevious; }
        }

        public override void OnEnter()
        {
            // --- 【ステップ1】事前チェック ---
            if (!showAlways && executionCount >= showCount)
            {
                Continue();
                return;
            }
            executionCount++;

            // --- 【ステップ2】表示に使用するSayDialog（会話ウィンドウ）を決定 ---
            if (character != null && character.SetSayDialog != null)
            {
                SayDialog.ActiveSayDialog = character.SetSayDialog;
            }
            if (setSayDialog != null)
            {
                SayDialog.ActiveSayDialog = setSayDialog;
            }
            var sayDialog = SayDialog.GetSayDialog();
            if (sayDialog == null)
            {
                Continue();
                return;
            }
            var flowchart = GetFlowchart();
            sayDialog.SetActive(true);
            sayDialog.SetCharacter(character);

            // --- 【ステップ3】立ち絵の表示処理 ---
            // 変更: 直接処理せず、イベントを発行するだけにする
            if (character != null && character.name == "Heroin")
            {
                // Heroinの場合、Fungus標準の立ち絵は使わない
                sayDialog.SetCharacterImage(null);
                // 合図（イベント）を送信する
                FungusCustomSignals.DoRequestDynamicPortrait(portraitString);
            }
            else
            {
                // Heroin以外のキャラクターの場合は、従来通りFungusの機能で立ち絵を表示
                sayDialog.SetCharacterImage(portrait);
                // Heroinの立ち絵は会話が終わるまで消さない
                // FungusCustomSignals.DoRequestHideDynamicPortrait();
            }

            // --- 【ステップ4】表示テキストの準備 ---
            string displayText = storyText;
            var activeCustomTags = CustomTag.activeCustomTags;
            for (int i = 0; i < activeCustomTags.Count; i++)
            {
                var ct = activeCustomTags[i];
                displayText = displayText.Replace(ct.TagStartSymbol, ct.ReplaceTagStartWith);
                if (ct.TagEndSymbol != "" && ct.ReplaceTagEndWith != "")
                {
                    displayText = displayText.Replace(ct.TagEndSymbol, ct.ReplaceTagEndWith);
                }
            }
            string subbedText = flowchart.SubstituteVariables(displayText);

            // --- 【ステップ5】会話ウィンドウに表示を命令 ---
            sayDialog.Say(
                subbedText,
                !extendPrevious,
                waitForClick,
                fadeWhenDone,
                stopVoiceover,
                waitForVO,
                voiceOverClip,
                delegate
                {
                    Continue();
                }
            );
        }

        /// <summary>
        /// Flowchartのインスペクター上で、このコマンドの概要を表示するためのテキストを生成します。
        /// </summary>
        public override string GetSummary()
        {
            string namePrefix = "";
            // キャラクターが設定されていれば、その名前を接頭辞にする
            if (character != null)
            {
                // // Heroinの場合は表情文字列も表示
                // if (character.name == "Heroin" && !string.IsNullOrEmpty(portraitString))
                // {
                //     namePrefix = character.NameText + " [" + portraitString + "]: ";
                // }
                // else
                // {
                //     namePrefix = character.NameText + ": ";
                // }

                namePrefix = character.NameText + ": ";
            }
            // 「続けて表示」が有効なら、"EXTEND"という接頭辞にする
            if (extendPrevious)
            {
                namePrefix = "EXTEND" + ": ";
            }
            // 最終的に「キャラクター名: "セリフ"」の形式で表示する
            return namePrefix + "\"" + storyText + "\"";
        }

        /// <summary>
        /// Flowchartのインスペクター上で、このコマンドのボタン色を返します。
        /// </summary>
        public override Color GetButtonColor()
        {
            return new Color32(184, 210, 235, 255);
        }

        /// <summary>
        /// Flowchartのリセット時に呼び出され、実行回数カウンターをリセットします。
        /// </summary>
        public override void OnReset()
        {
            executionCount = 0;
        }

        /// <summary>
        /// Flowchartの実行が停止されたときに呼び出され、SayDialogの表示を中断します。
        /// </summary>
        public override void OnStopExecuting()
        {
            var sayDialog = SayDialog.GetSayDialog();
            if (sayDialog == null)
            {
                return;
            }

            sayDialog.Stop();
        }

        #endregion

        #region ILocalizable implementation (多言語対応のためのインターフェース実装)

        /// <summary>
        /// 翻訳対象となるメインのテキスト（storyText）を返します。
        /// </summary>
        public virtual string GetStandardText()
        {
            return storyText;
        }

        /// <summary>
        /// 翻訳後のテキストをstoryTextに設定します。
        /// </summary>
        public virtual void SetStandardText(string standardText)
        {
            storyText = standardText;
        }

        /// <summary>
        /// 翻訳者向けの注釈（description）を返します。
        /// </summary>
        public virtual string GetDescription()
        {
            return description;
        }

        /// <summary>
        /// このテキストを一意に識別するためのIDを生成して返します。翻訳データなどで使用されます。
        /// </summary>
        public virtual string GetStringId()
        {
            // Sayコマンドの文字列IDは「SAY.<ローカライズID>.<コマンドID>.[キャラクター名]」の形式
            string stringId = "SAY." + GetFlowchartLocalizationId() + "." + itemId + ".";
            if (character != null)
            {
                stringId += character.NameText;
            }

            return stringId;
        }

        #endregion
    }
}

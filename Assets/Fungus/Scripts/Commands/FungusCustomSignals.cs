using System;

// Say.csと同じFungusの名前空間に所属させます
namespace Fungus
{
    /// <summary>
    /// Fungusのコマンドと外部のゲームシステムを連携させるためのカスタムイベントを管理します。
    /// </summary>
    public static class FungusCustomSignals
    {
        // portraitString ("Heroin_flexible_smile"など) を渡すためのイベント
        public static event Action<string> OnRequestDynamicPortrait;

        // 立ち絵を非表示にするためのイベント
        public static event Action OnRequestHideDynamicPortrait;

        // Blockの種別を通知するためのイベント
        public static event Action<BlockType> OnTalkBlockStart;

        /// <summary>
        /// Sayコマンドなどから呼び出し、立ち絵表示のリクエストを通知します。
        /// </summary>
        public static void DoRequestDynamicPortrait(string portraitString)
        {
            OnRequestDynamicPortrait?.Invoke(portraitString);
        }

        /// <summary>
        /// Sayコマンドなどから呼び出し、立ち絵非表示のリクエストを通知します。
        /// </summary>
        public static void DoRequestHideDynamicPortrait()
        {
            OnRequestHideDynamicPortrait?.Invoke();
        }

        /// <summary>
        /// TalkStartコマンドなどから呼び出し、会話ブロック開始の通知を行います。
        /// </summary>
        public static void DoTalkBlockStart(BlockType blockType)
        {
            OnTalkBlockStart?.Invoke(blockType);
        }
    }
}

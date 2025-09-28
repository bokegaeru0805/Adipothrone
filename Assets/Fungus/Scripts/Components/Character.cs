// このコードはFungusライブラリ（https://github.com/snozbot/fungus）の一部です。
// MITオープンソースライセンス（https://github.com/snozbot/fungus/blob/master/LICENSE）の下で無料で公開されています。

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace Fungus
{
    /// <summary>
    /// Sayコマンド、Conversationコマンド、Portraitコマンドを介して、
    /// 対話で使用できるキャラクターを定義します。
    /// </summary>
    [ExecuteInEditMode] // この属性により、Unityエディタの非再生中でもスクリプトの一部が動作します。（例: OnEnable, OnDisable）
    public class Character : MonoBehaviour, ILocalizable, IComparer<Character>
    {
        [Tooltip("Say Dialogに表示されるキャラクターの名前です。")]
        [SerializeField]
        protected string nameText; // オブジェクト名は表情差分（例: "Hero_Happy", "Hero_Sad"）に使われることがあるため、表示名は別に管理します。

        [Tooltip("Say Dialogに表示されるキャラクター名の色です。")]
        [SerializeField]
        protected Color nameColor = Color.white;

        [Tooltip("このキャラクターが話すときに再生される効果音（ボイス）です。")]
        [SerializeField]
        protected AudioClip soundEffect;

        [Tooltip("このキャラクターが表示できる立ち絵（ポートレート）画像のリストです。")]
        [SerializeField]
        protected List<Sprite> portraits;

        [Tooltip("立ち絵が表示される際の向きです。")]
        [SerializeField]
        protected FacingDirection portraitsFace;

        [Tooltip("このキャラクターが話す際に使用するSay Dialogをシーンから指定します。未指定の場合はデフォルトのものが使われます。")]
        [SerializeField]
        protected SayDialog setSayDialog;

        [FormerlySerializedAs("notes")] // 以前のバージョンで "notes" という名前だった変数を "description" に変更したことを示す属性。後方互換性のために必要です。
        [TextArea(5, 10)]
        [SerializeField]
        protected string description; // 開発者向けのメモ欄。ゲームの動作には影響しません。

        // 現在の立ち絵の表示状態（位置、向き、表示/非表示など）を保持します。
        protected PortraitState portaitState = new PortraitState();

        // シーン内で現在アクティブな全てのCharacterコンポーネントを保持する静的（static）リスト。
        // これにより、どのスクリプトからでもシーン上の全キャラクターにアクセスできます。
        protected static List<Character> activeCharacters = new List<Character>();

        /// <summary>
        /// このコンポーネントが有効になったときに呼び出されます。
        /// </summary>
        protected virtual void OnEnable()
        {
            // [ExecuteInEditMode]属性により、エディタ上でもアクティブ/非アクティブ時に呼び出されます。
            if (!activeCharacters.Contains(this))
            {
                activeCharacters.Add(this);
                activeCharacters.Sort(this); // キャラクターリストを名前順にソート
            }
        }

        /// <summary>
        /// このコンポーネントが無効になったときに呼び出されます。
        /// </summary>
        protected virtual void OnDisable()
        {
            activeCharacters.Remove(this);
        }

        #region Public members (他のスクリプトからアクセス可能な公開メンバー)

        /// <summary>
        /// シーン内でアクティブなキャラクターのリストを取得します。
        /// </summary>
        public static List<Character> ActiveCharacters
        {
            get { return activeCharacters; }
        }

        /// <summary>
        /// Say Dialogに表示されるキャラクター名。
        /// </summary>
        public virtual string NameText
        {
            get { return nameText; }
        }

        /// <summary>
        /// Say Dialogに表示されるキャラクター名の色。
        /// </summary>
        public virtual Color NameColor
        {
            get { return nameColor; }
        }

        /// <summary>
        /// このキャラクターが話すときに再生される効果音（ボイス）。
        /// </summary>
        public virtual AudioClip SoundEffect
        {
            get { return soundEffect; }
        }

        /// <summary>
        /// このキャラクターが表示できる立ち絵画像のリスト。
        /// </summary>
        public virtual List<Sprite> Portraits
        {
            get { return portraits; }
        }

        /// <summary>
        /// 立ち絵が表示される際の向き。
        /// </summary>
        public virtual FacingDirection PortraitsFace
        {
            get { return portraitsFace; }
        }

        /// <summary>
        /// 現在このキャラクターに設定されているプロフィール画像（立ち絵）。
        /// </summary>
        public virtual Sprite ProfileSprite { get; set; }

        /// <summary>
        /// このキャラクターの立ち絵の現在の表示状態。
        /// </summary>
        public virtual PortraitState State
        {
            get { return portaitState; }
        }

        /// <summary>
        /// このキャラクターが話す際に使用する、特定のSay Dialog。
        /// </summary>
        public virtual SayDialog SetSayDialog
        {
            get { return setSayDialog; }
        }

        /// <summary>
        /// このキャラクターがアタッチされているGameObjectの名前を返します。
        /// </summary>
        public string GetObjectName()
        {
            return gameObject.name;
        }

        /// <summary>
        /// キャラクター名が指定された文字列で始まるかどうかを、大文字小文字を区別せずに判定します。
        /// </summary>
        public virtual bool NameStartsWith(string matchString)
        {
// .NETのバージョンによる互換性のための記述
#if NETFX_CORE
            return name.StartsWith(matchString, StringComparison.CurrentCultureIgnoreCase)
                || nameText.StartsWith(matchString, StringComparison.CurrentCultureIgnoreCase);
#else
            return name.StartsWith(matchString, true, System.Globalization.CultureInfo.CurrentCulture)
                || nameText.StartsWith(matchString, true, System.Globalization.CultureInfo.CurrentCulture);
#endif
        }

        /// <summary>
        /// キャラクター名が指定された文字列と完全に一致するかどうかを、大文字小文字を区別せずに判定します。
        /// </summary>
        public virtual bool NameMatch(string matchString)
        {
            return string.Compare(name, matchString, true, CultureInfo.CurrentCulture) == 0
                || string.Compare(nameText, matchString, true, CultureInfo.CurrentCulture) == 0;
        }

        /// <summary>
        /// キャラクター同士を比較するためのメソッド。リストのソート（並び替え）に使われます。
        /// </summary>
        public int Compare(Character x, Character y)
        {
            if (x == y) return 0;
            if (y == null) return 1;
            if (x == null) return -1;

            return x.name.CompareTo(y.name); // GameObjectの名前でアルファベット順にソート
        }

        /// <summary>
        /// 指定された名前（portraitString）に一致する立ち絵（Sprite）をリストから探して返します。
        /// 見つからない場合は警告を出し、代わりとなる立ち絵を探します。
        /// </summary>
        public virtual Sprite GetPortrait(string portraitString)
        {
            // ① portraitStringが指定されていない場合は、何もせずnullを返す
            if (string.IsNullOrEmpty(portraitString))
            {
                return null;
            }

            // ② portraitStringが指定されているのに、立ち絵リストが空の場合のエラー処理
            if (portraits.Count == 0)
            {
                Debug.LogError($"キャラクター「{nameText}」の立ち絵リスト（portraits）が空です。'{portraitString}' を探せませんでした。");
                return null;
            }

            // ③ まずは、指定されたportraitStringと完全に一致する名前を探す
            for (int i = 0; i < portraits.Count; i++)
            {
                if (portraits[i] != null && string.Compare(portraits[i].name, portraitString, true) == 0)
                {
                    return portraits[i]; // 一致するものが見つかれば、それを返す
                }
            }

            // ④ 完全一致するものが無かった場合のフォールバック（代替）処理
            //    1. 名前に "normal" を含む立ち絵を全て抽出する (大文字小文字を区別しない)
            //    2. その中から、"_" の数が最も少ないもので並び替える
            //    3. 最初に見つかったもの（＝最もシンプルな "normal"）をフォールバックとして選択
            var fallbackPortrait = portraits
                .Where(p => p != null && p.name.ToLower().Contains("normal"))
                .OrderBy(p => p.name.Count(c => c == '_'))
                .FirstOrDefault();

            // フォールバック用の立ち絵が見つかった場合
            if (fallbackPortrait != null)
            {
                // どの立ち絵を代わりに使ったか、分かりやすい警告を出す
                Debug.LogWarning($"キャラクター「{nameText}」に、指定された立ち絵 '{portraitString}' が見つかりませんでした。代わりにフォールバック用の '{fallbackPortrait.name}' を使用します。");
                return fallbackPortrait; // フォールバック用の立ち絵を返す
            }

            // ⑤ 完全一致も見つからず、フォールバックも見つからなかった場合はnullを返す
            return null;
        }

        #endregion

        #region ILocalizable implementation (多言語対応のための実装)

        /// <summary>
        /// ローカライズ（翻訳）対象となる標準のテキスト（キャラクター名）を返します。
        /// </summary>
        public virtual string GetStandardText()
        {
            return nameText;
        }

        /// <summary>
        /// ローカライズ（翻訳）されたテキストをキャラクター名として設定します。
        /// </summary>
        public virtual void SetStandardText(string standardText)
        {
            nameText = standardText;
        }

        /// <summary>
        /// 翻訳者向けのコンテキスト情報（説明文）を返します。
        /// </summary>
        public virtual string GetDescription()
        {
            return description;
        }

        /// <summary>
        /// ローカライズシステムで使われる、一意の文字列IDを生成して返します。
        /// </summary>
        public virtual string GetStringId()
        {
            // キャラクター名の文字列IDは "CHARACTER.キャラクター名" という形式になります。
            return "CHARACTER." + nameText;
        }

        #endregion

        /// <summary>
        /// Unityエディタ上で、Inspectorの値が変更されたときに呼び出されます。
        /// </summary>
        protected virtual void OnValidate()
        {
            // 立ち絵リストが更新されたら、自動的に名前順でソートする
            // これにより、Inspector上のリストが整理されて見やすくなります。
            if (portraits != null && portraits.Count > 1)
            {
                portraits.Sort(PortraitUtil.PortraitCompareTo);
            }
        }
    }
}
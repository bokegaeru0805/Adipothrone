// このコードはFungusライブラリ（https://github.com/snozbot/fungus）の一部です。
// MITオープンソースライセンス（https://github.com/snozbot/fungus/blob/master/LICENSE）の下で無料で公開されています。

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fungus
{
    /// <summary>
    /// ビジュアルノベル形式のダイアログボックスに、物語のテキストを表示します。
    /// SayコマンドやMenuコマンドと連携して動作します。
    /// </summary>
    public class SayDialog : MonoBehaviour
    {
        [Tooltip("ダイアログをフェードイン・アウトさせる時間（秒）")]
        [SerializeField]
        protected float fadeDuration = 0.25f;

        [Tooltip("「次へ」や「続く」を示す、クリック待ちの際に表示するボタン")]
        [SerializeField]
        protected Button continueButton;

        [Tooltip("このダイアログが属するCanvasオブジェクト")]
        [SerializeField]
        protected Canvas dialogCanvas;

        // キャラクター名表示関連
        [Tooltip("キャラクター名を表示するためのPanel")]
        [SerializeField]
        protected GameObject nameTextPanel;

        [Tooltip("キャラクター名を表示するためのTextMeshProUGUIコンポーネント")]
        [SerializeField]
        protected TextMeshProUGUI nameText;

        // TextAdapterは、TextMeshProと従来のUI.Textの両方を透過的に扱うための仕組み
        protected TextAdapter nameTextAdapter = new TextAdapter();

        /// <summary>
        /// キャラクター名テキストの取得・設定を行います。
        /// </summary>
        public virtual string NameText
        {
            get { return nameTextAdapter.Text; }
            set { nameTextAdapter.Text = value; }
        }

        // 物語テキスト表示関連
        [Tooltip("物語の本文を表示するためのTextMeshProUGUIコンポーネント")]
        [SerializeField]
        protected TextMeshProUGUI storyText;

        [Tooltip(
            "上記のstoryTextが未設定の場合、このGameObjectからテキスト表示コンポーネントを探します"
        )]
        [SerializeField]
        protected GameObject storyTextGO;
        protected TextAdapter storyTextAdapter = new TextAdapter();

        /// <summary>
        /// 物語テキストの取得・設定を行います。
        /// </summary>
        public virtual string StoryText
        {
            get { return storyTextAdapter.Text; }
            set { storyTextAdapter.Text = value; }
        }

        /// <summary>
        /// 物語テキストのRectTransformコンポーネントを取得します。
        /// </summary>
        public virtual RectTransform StoryTextRectTrans
        {
            get
            {
                // storyTextが直接設定されていればそれを、なければstoryTextGOから取得する
                return storyText != null
                    ? storyText.rectTransform
                    : storyTextGO.GetComponent<RectTransform>();
            }
        }

        // キャラクター画像関連
        [Tooltip(
            "キャラクターの立ち絵やアイテムの絵などを表示するためのPanel。Imageコンポーネントを持つ必要があります"
        )]
        [SerializeField]
        protected Image imageContainer;

        [Tooltip("キャラクターの立ち絵やアイテムの絵などを表示するためのImageコンポーネント")]
        [SerializeField]
        protected Image displayImage;

        /// <summary>
        /// キャラクター画像用のImageコンポーネントを取得します。
        /// </summary>
        public virtual Image CharacterImage
        {
            get { return displayImage; }
        }

        [Tooltip(
            "キャラクター画像が表示されている時、テキストが画像と重ならないように幅を自動調整するか"
        )]
        [SerializeField]
        protected bool fitTextWithImage = true;

        [Tooltip("このダイアログが表示される際、他の全てのSayDialogを非表示にするか")]
        [SerializeField]
        protected bool closeOtherDialogs;

        // テキストボックスの幅を調整するために、初期状態の幅と余白を保存しておく変数
        protected float startStoryTextWidth;
        protected float startStoryTextInset;
        protected float imageContainerWidth;

        // --- 内部で利用するコンポーネントのキャッシュ ---
        protected WriterAudio writerAudio; // テキスト表示中の音声（ボイスや効果音）を管理
        protected Writer writer; // テキストをタイプライター風に一文字ずつ表示する処理を担当
        protected CanvasGroup canvasGroup; // ダイアログ全体のフェード（透明度の変更）を管理

        // --- 状態管理用のフラグやタイマー ---
        protected bool fadeWhenDone = true; // テキスト表示完了後、自動でフェードアウトさせるか
        protected float targetAlpha = 0f; // 目標とする透明度 (0で透明、1で不透明)
        protected float fadeCoolDownTimer = 0f; // 連続するSayコマンド間でちらつきを防ぐためのクールダウンタイマー

        protected Sprite currentCharacterImage; // 現在表示されているキャラクター画像のキャッシュ
        private float baseSize = 0; // アイテム画像のベースサイズ（初期化時に設定）

        /// <summary>
        /// 現在発言中のキャラクター情報。staticなので、どのSayDialogからでも最後に話したキャラクターを参照できる。
        /// </summary>
        protected static Character speakingCharacter;

        /// <summary>
        /// Fungusの変数（例：{$player_name}）を実際の文字列に置換するためのクラス
        /// </summary>
        protected StringSubstituter stringSubstituter = new StringSubstituter();

        /// <summary>
        /// パフォーマンス向上のため、シーン内でアクティブな全てのSayDialogをリストでキャッシュしておく。
        /// これにより、毎回FindObjectOfTypeでシーン全体を検索するのを防ぐ。
        /// </summary>
        protected static List<SayDialog> activeSayDialogs = new List<SayDialog>();

        /// <summary>
        /// このコンポーネントが生成されたときに一度だけ呼ばれる初期化処理
        /// </summary>
        protected virtual void Awake()
        {
            // 自分自身をアクティブなSayDialogのリストに追加
            if (!activeSayDialogs.Contains(this))
            {
                activeSayDialogs.Add(this);
            }

            // TextAdapterを初期化し、テキスト表示用のコンポーネントを探させる
            nameTextAdapter.InitFromGameObject(nameText.gameObject);
            storyTextAdapter.InitFromGameObject(
                storyText != null ? storyText.gameObject : storyTextGO
            );

            if (nameTextPanel != null)
            {
                nameTextPanel.SetActive(true);
            }
            else
            {
                Debug.LogError("NameTextPanelが設定されていません。SayDialogの表示に影響します。");
            }

            // アイテム画像のベースサイズを取得
            RectTransform rectTransform = displayImage.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                baseSize = Mathf.Min(rectTransform.sizeDelta.x, rectTransform.sizeDelta.y); //ベースサイズを取得
            }
            else
            {
                Debug.LogWarning("アイテム画像のRectTransformが取得できませんでした。");
            }

            if (imageContainer != null)
            {
                imageContainerWidth = imageContainer.rectTransform.rect.width;
            }
            else
            {
                Debug.LogError("ImageContainerが設定されていません。");
            }
        }

        /// <summary>
        /// このコンポーネントが破棄されるときに呼ばれる後処理
        /// </summary>
        protected virtual void OnDestroy()
        {
            // 自分自身をアクティブなSayDialogのリストから削除
            activeSayDialogs.Remove(this);
        }

        /// <summary>
        /// Writerコンポーネントを取得する。なければ自動的に追加する。
        /// </summary>
        protected virtual Writer GetWriter()
        {
            if (writer != null)
                return writer; // 既に取得済みならそれを返す

            writer = GetComponent<Writer>();
            if (writer == null)
            {
                writer = gameObject.AddComponent<Writer>();
            }
            return writer;
        }

        /// <summary>
        /// CanvasGroupコンポーネントを取得する。なければ自動的に追加する。
        /// </summary>
        protected virtual CanvasGroup GetCanvasGroup()
        {
            if (canvasGroup != null)
                return canvasGroup;

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            return canvasGroup;
        }

        /// <summary>
        /// WriterAudioコンポーネントを取得する。なければ自動的に追加する。
        /// </summary>
        protected virtual WriterAudio GetWriterAudio()
        {
            if (writerAudio != null)
                return writerAudio;

            writerAudio = GetComponent<WriterAudio>();
            if (writerAudio == null)
            {
                writerAudio = gameObject.AddComponent<WriterAudio>();
            }
            return writerAudio;
        }

        /// <summary>
        /// ゲーム開始時の初期化処理
        /// </summary>
        protected virtual void Start()
        {
            // ダイアログは常に透明な状態で開始し、テキスト表示時にフェードインさせる
            GetCanvasGroup().alpha = 0f;

            // マウスクリックなどのUIイベントを検知するためにGraphicRaycasterがなければ追加する
            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // 他のコンポーネントによって既に設定されている場合を考慮し、未設定の場合のみ初期化
            if (NameText == "")
            {
                SetCharacterName("", Color.white);
            }
            if (currentCharacterImage == null)
            {
                // キャラクター画像はデフォルトで非表示
                SetCharacterImage(null);
            }
        }

        /// <summary>
        /// Updateの後に毎フレーム呼ばれる処理。UIの更新などに使う。
        /// </summary>
        protected virtual void LateUpdate()
        {
            // ダイアログ全体の透明度を更新する
            UpdateAlpha();

            // 「次へ」ボタンは、プレイヤーの入力待ち状態の時だけ表示する
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(GetWriter().IsWaitingForInput);
            }
        }

        /// <summary>
        /// ダイアログの透明度（アルファ値）を目標値に向かって滑らかに変化させる
        /// </summary>
        protected virtual void UpdateAlpha()
        {
            // テキスト表示中は、目標の透明度を1（不透明）にする
            if (GetWriter().IsWriting)
            {
                targetAlpha = 1f;
                // ちらつき防止のクールダウンをリセット
                fadeCoolDownTimer = 0.1f;
            }
            // テキスト表示が完了し、自動フェードアウトが有効で、クールダウンも終わっている場合
            else if (fadeWhenDone && Mathf.Approximately(fadeCoolDownTimer, 0f))
            {
                // 目標の透明度を0（透明）にする
                targetAlpha = 0f;
            }
            else
            {
                // 次のSayコマンドがすぐに来る場合に備え、少しだけ待ってからフェードアウトを開始する
                // これにより、連続するSayコマンド間のちらつきを防ぐ
                fadeCoolDownTimer = Mathf.Max(0f, fadeCoolDownTimer - Time.deltaTime);
            }

            // 実際にCanvasGroupのalpha値を変更する
            CanvasGroup canvasGroup = GetCanvasGroup();
            if (fadeDuration <= 0f)
            {
                // フェード時間が0なら、即座に値を反映
                canvasGroup.alpha = targetAlpha;
            }
            else
            {
                // フェード時間が設定されていれば、滑らかに値を変更
                float delta = (1f / fadeDuration) * Time.deltaTime;
                float alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, delta);
                canvasGroup.alpha = alpha;

                // 完全に透明になったら、パフォーマンスのためにGameObject自体を非アクティブ化する
                if (alpha <= 0f)
                {
                    gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 物語テキストを空にする
        /// </summary>
        protected virtual void ClearStoryText()
        {
            StoryText = "";
        }

        #region Public members (他のスクリプトから呼び出される公開メンバー)

        /// <summary>
        /// 現在発言中のキャラクター情報を取得します。
        /// </summary>
        public Character SpeakingCharacter
        {
            get { return speakingCharacter; }
        }

        /// <summary>
        /// Sayコマンドのテキスト表示に使われる、現在アクティブなSayDialogインスタンス。
        /// </summary>
        public static SayDialog ActiveSayDialog { get; set; }

        /// <summary>
        /// シーン内からSayDialogを探すか、なければ自動生成して返します。
        /// </summary>
        public static SayDialog GetSayDialog()
        {
            if (ActiveSayDialog == null)
            {
                SayDialog sd = null;

                // まずはキャッシュされたアクティブなSayDialogリストから探す
                if (activeSayDialogs.Count > 0)
                {
                    sd = activeSayDialogs[0];
                }

                if (sd != null)
                {
                    ActiveSayDialog = sd;
                }

                //それでも見つからなければ、プレハブから自動生成
                if (ActiveSayDialog == null)
                {
                    GameObject prefab = Resources.Load<GameObject>("Prefabs/SayDialog");
                    if (prefab != null)
                    {
                        GameObject go = Instantiate(prefab) as GameObject;
                        go.SetActive(false);
                        go.name = "SayDialog";
                        ActiveSayDialog = go.GetComponent<SayDialog>();
                    }
                }
            }

            return ActiveSayDialog;
        }

        /// <summary>
        /// 全てのアクティブなキャラクター立ち絵のアニメーション（Tween）を停止します。
        /// </summary>
        public static void StopPortraitTweens()
        {
            // LeanTweenなどのアニメーションライブラリと連携し、キャラクターの動きを即座に止める
            var activeCharacters = Character.ActiveCharacters;
            for (int i = 0; i < activeCharacters.Count; i++)
            {
                var c = activeCharacters[i];
                if (c.State.portraitImage != null)
                {
                    if (LeanTween.isTweening(c.State.portraitImage.gameObject))
                    {
                        LeanTween.cancel(c.State.portraitImage.gameObject, true);
                        PortraitController.SetRectTransform(
                            c.State.portraitImage.rectTransform,
                            c.State.position
                        );
                        if (c.State.dimmed == true)
                        {
                            c.State.portraitImage.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                        }
                        else
                        {
                            c.State.portraitImage.color = Color.white;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// SayDialogのGameObjectの表示/非表示を設定します。
        /// </summary>
        public virtual void SetActive(bool state)
        {
            gameObject.SetActive(state);
        }

        /// <summary>
        /// 発言するキャラクターを設定します。
        /// </summary>
        /// <param name="character">発言者に設定するキャラクター</param>
        public virtual void SetCharacter(Character character)
        {
            // キャラクターがnullなら、名前と画像を非表示にする
            if (character == null)
            {
                if (imageContainer != null)
                    imageContainer.gameObject.SetActive(false);
                if (NameText != null)
                    NameText = "";
                if (nameTextPanel.activeInHierarchy)
                    nameTextPanel.SetActive(false);
                speakingCharacter = null;
            }
            else
            {
                var prevSpeakingCharacter = speakingCharacter;
                speakingCharacter = character;

                // 発言者以外のキャラクターの立ち絵を暗くする（Dim）処理
                var activeStages = Stage.ActiveStages;
                for (int i = 0; i < activeStages.Count; i++)
                {
                    var stage = activeStages[i];
                    if (stage.DimPortraits)
                    {
                        var charactersOnStage = stage.CharactersOnStage;
                        for (int j = 0; j < charactersOnStage.Count; j++)
                        {
                            var c = charactersOnStage[j];
                            if (prevSpeakingCharacter != speakingCharacter)
                            {
                                // 発言者でなければ暗く、発言者なら明るくする
                                stage.SetDimmed(c, c != null && !c.Equals(speakingCharacter));
                            }
                        }
                    }
                }

                // キャラクター名が設定されていなければ、GameObjectの名前をデフォルトとして使用
                string characterName = character.NameText;
                if (characterName == "")
                {
                    characterName = character.GetObjectName();
                }

                SetCharacterName(characterName, character.NameColor);
            }
        }

        /// <summary>
        /// SayDialogに表示するキャラクター画像を設定します。
        /// </summary>
        public virtual void SetCharacterImage(Sprite image)
        {
            if (displayImage == null)
                return;

            if (image != null)
            {
                displayImage.overrideSprite = image;
                imageContainer.gameObject.SetActive(true);
                currentCharacterImage = image;
            }
            else
            {
                // 画像がnullなら非表示にし、テキストボックスの幅を元に戻す
                imageContainer.gameObject.SetActive(false);
                if (startStoryTextWidth != 0)
                {
                    StoryTextRectTrans.SetInsetAndSizeFromParentEdge(
                        RectTransform.Edge.Left,
                        startStoryTextInset,
                        startStoryTextWidth
                    );
                }
            }

            // fitTextWithImageが有効なら、テキストが画像に重ならないように幅を調整する
            if (fitTextWithImage && StoryText != null && imageContainer.gameObject.activeSelf)
            {
                // 初回のみ、元のテキストボックスの幅と余白を記憶
                if (Mathf.Approximately(startStoryTextWidth, 0f))
                {
                    startStoryTextWidth = StoryTextRectTrans.rect.width;
                    startStoryTextInset = StoryTextRectTrans.offsetMin.x;
                }

                // テキストと画像の相対位置によって、テキストボックスの左右どちらを詰めるか決定
                if (StoryTextRectTrans.position.x < displayImage.rectTransform.position.x)
                {
                    // テキストが左、画像が右の場合
                    StoryTextRectTrans.SetInsetAndSizeFromParentEdge(
                        RectTransform.Edge.Left,
                        startStoryTextInset,
                        startStoryTextWidth
                            - imageContainerWidth
                            - startStoryTextInset
                    );
                }
                else
                {
                    // テキストが右、画像が左の場合
                    StoryTextRectTrans.SetInsetAndSizeFromParentEdge(
                        RectTransform.Edge.Right,
                        startStoryTextInset,
                        startStoryTextWidth
                            - imageContainerWidth
                            - startStoryTextInset
                    );
                }
            }
        }

        //SetItemImageは削除し、代わりにSetCharacterImageを使用するように変更
        // これにより、アイテム画像もキャラクター画像と同じ位置に表示されるようになる
        // /// <summary>
        // /// SayDialogに表示するアイテム画像を設定します。
        // /// </summary>
        // public virtual void SetItemImage(Sprite image)
        // {
        //     if (image != null)
        //     {
        //         displayImage.overrideSprite = image;
        //         SetSpriteFitToSquare(displayImage, image, baseSize);
        //         imageContainer.gameObject.SetActive(true);
        //     }
        //     else
        //     {
        //         // 画像がnullなら非表示にし、テキストボックスの幅を元に戻す
        //         imageContainer.gameObject.SetActive(false);
        //     }
        // }

        /// <summary>
        /// 指定されたImageにSpriteを設定し、Spriteの縦横比を維持しつつ、
        /// 正方形のImage内で最大辺がちょうど収まるようにサイズ調整する。
        /// </summary>
        /// <param name="image">表示対象のUI Image（正方形）</param>
        /// <param name="sprite">表示するSprite</param>
        /// <param name="baseSize">正方形Imageの基準サイズ（例：128など）</param>
        private void SetSpriteFitToSquare(Image image, Sprite sprite, float baseSize)
        {
            // nullチェック：どちらかが未設定ならログを出して終了
            if (image == null)
            {
                Debug.LogWarning("UIUtility.SetSpriteFitToSquare: Image is null.");
                return;
            }

            if (sprite == null)
            {
                if (image.gameObject.activeInHierarchy)
                {
                    image.gameObject.SetActive(false);
                }
                return;
            }

            // ImageにSpriteを設定
            image.sprite = sprite;

            // アスペクト比を維持して描画
            image.preserveAspect = true;

            // Spriteの元のピクセルサイズを取得（RectはSpriteの切り抜き範囲）
            float width = sprite.rect.width;
            float height = sprite.rect.height;

            // 縦と横のうち、長い方を基準にしてスケーリング比を計算
            float maxSide = Mathf.Max(width, height);

            // スケール率（1.0を超えないように調整）
            float scaleX = width / maxSide;
            float scaleY = height / maxSide;

            // Imageのサイズ（sizeDelta）を、Spriteに合わせてスケーリング
            // 正方形ベースサイズを元に、縦横比を保ったサイズに変更
            image.rectTransform.sizeDelta = new Vector2(baseSize * scaleX, baseSize * scaleY);

            if (!image.gameObject.activeInHierarchy)
            {
                image.gameObject.SetActive(true); // Imageが非表示なら表示する
            }
        }

        /// <summary>
        /// ダイアログに表示するキャラクター名と色を設定します。
        /// 変数置換（例: John {$surname}）にも対応します。
        /// </summary>
        public virtual void SetCharacterName(string name, Color color)
        {
            if (nameTextPanel.activeInHierarchy == false)
            {
                // キャラクター名パネルが非表示なら、表示する
                nameTextPanel.SetActive(true);
            }

            if (NameText != null)
            {
                var subbedName = stringSubstituter.SubstituteStrings(name);
                NameText = subbedName;
                nameTextAdapter.SetTextColor(color);
            }
        }

        /// <summary>
        /// 物語のテキストをダイアログに表示します。自動的にコルーチンを開始します。
        /// </summary>
        /// <param name="text">表示するテキスト</param>
        /// <param name="clearPrevious">前のテキストを消去するか</param>
        /// <param name="waitForInput">表示後、プレイヤーの入力を待つか</param>
        /// <param name="fadeWhenDone">完了後、ダイアログをフェードアウトさせるか</param>
        /// <param name="stopVoiceover">表示開始前に、再生中のボイスオーバーを停止するか</param>
        /// <param name="waitForVO">（ボイスオーバーがある場合）再生が終わるまで待つか</param>
        /// <param name="voiceOverClip">再生するボイスオーバーのオーディオクリップ</param>
        /// <param name="onComplete">全ての処理が完了したときに呼び出されるコールバック</param>
        public virtual void Say(
            string text,
            bool clearPrevious,
            bool waitForInput,
            bool fadeWhenDone,
            bool stopVoiceover,
            bool waitForVO,
            AudioClip voiceOverClip,
            Action onComplete
        )
        {
            StartCoroutine(
                DoSay(
                    text,
                    clearPrevious,
                    waitForInput,
                    fadeWhenDone,
                    stopVoiceover,
                    waitForVO,
                    voiceOverClip,
                    onComplete
                )
            );
        }

        /// <summary>
        /// Sayメソッドの実際の処理を行うコルーチン。
        /// </summary>
        public virtual IEnumerator DoSay(
            string text,
            bool clearPrevious,
            bool waitForInput,
            bool fadeWhenDone,
            bool stopVoiceover,
            bool waitForVO,
            AudioClip voiceOverClip,
            Action onComplete
        )
        {
            var writer = GetWriter();

            // 既になにか表示中の場合は、一度停止させる
            if (writer.IsWriting || writer.IsWaitingForInput)
            {
                writer.Stop();
                while (writer.IsWriting || writer.IsWaitingForInput)
                {
                    yield return null;
                }
            }

            // 他のSayDialogを閉じる設定が有効なら、自分以外のダイアログを非表示にする
            if (closeOtherDialogs)
            {
                for (int i = 0; i < activeSayDialogs.Count; i++)
                {
                    var sd = activeSayDialogs[i];
                    if (sd.gameObject != gameObject)
                    {
                        sd.SetActive(false);
                    }
                }
            }
            gameObject.SetActive(true);

            this.fadeWhenDone = fadeWhenDone;

            AudioClip soundEffectClip = null;
            // ボイスオーバーが指定されていれば、それを優先して再生
            if (voiceOverClip != null)
            {
                WriterAudio writerAudio = GetWriterAudio();
                writerAudio.OnVoiceover(voiceOverClip);
            }
            // ボイスオーバーがなく、キャラクターにSEが設定されていればそれを再生
            else if (speakingCharacter != null)
            {
                soundEffectClip = speakingCharacter.SoundEffect;
            }

            writer.AttachedWriterAudio = writerAudio;

            // Writerコンポーネントに実際のテキスト表示処理を委任する
            yield return StartCoroutine(
                writer.Write(
                    text,
                    clearPrevious,
                    waitForInput,
                    stopVoiceover,
                    waitForVO,
                    soundEffectClip,
                    onComplete
                )
            );
        }

        /// <summary>
        /// テキスト表示完了後、ダイアログをフェードアウトさせるかどうかを設定します。
        /// </summary>
        public virtual bool FadeWhenDone
        {
            get { return fadeWhenDone; }
            set { fadeWhenDone = value; }
        }

        /// <summary>
        /// テキストの表示処理を中断します。
        /// </summary>
        public virtual void Stop()
        {
            fadeWhenDone = true;
            GetWriter().Stop();
        }

        /// <summary>
        /// テキスト表示を中断し、ダイアログの内容をクリアします。
        /// </summary>
        public virtual void Clear()
        {
            ClearStoryText();
            StopAllCoroutines();
        }

        #endregion
    }
}

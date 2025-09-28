// このコードはFungusライブラリ（https://github.com/snozbot/fungus）の一部です。
// MITオープンソースライセンス（https://github.com/snozbot/fungus/blob/master/LICENSE）の下で無料で公開されています。

using System.Collections;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fungus
{
    /// <summary>
    /// プレイヤーに複数の選択肢ボタンを提示し、その対話を管理するダイアログです。
    /// FungusのMenuコマンドによって使用されます。
    /// </summary>
    public class MenuDialog : MonoBehaviour
    {
        [Tooltip("メニューが表示されたとき、操作可能な最初のボタンを自動で選択状態にするか")]
        [SerializeField]
        protected bool autoSelectFirstButton = false;

        [Header("カスタム設定")]
        [Tooltip("選択肢表示時に有効化する背景オブジェクト")]
        [SerializeField]
        protected GameObject backImage = null;

        // 子オブジェクトから取得した全ての選択肢ボタンをキャッシュ（一時保存）しておく配列
        protected Button[] cachedButtons;

        // 時間制限タイマーとして使用するスライダーをキャッシュしておく変数
        protected Slider cachedSlider;

        // 次に追加される選択肢が、cachedButtons配列の何番目に入るかを示すインデックス
        private int nextOptionIndex;

        /// <summary>
        /// キャンセル可能な選択肢の情報を保持します。
        /// </summary>
        protected struct CancelableOption
        {
            public UnityEngine.Events.UnityAction action;
        }

        public static KeyCode cancelKey = KeyCode.Z; // デフォルトのキャンセルキー
        private const string cancelIconName = "CancelKeyIcon"; // キャンセルアイコンのデフォルト名

        /// <summary>
        /// 現在表示されているキャンセル可能な選択肢。メニュー内に一つだけ存在します。
        /// </summary>
        protected CancelableOption? cancelableOption = null;

        #region Public members (公開メンバー)

        /// <summary>
        /// 現在アクティブになっている、メニュー選択肢を表示するためのMenuDialogインスタンス。
        /// staticなプロパティなので、シーンに一つだけ存在する前提でどこからでもアクセスできます。
        /// </summary>
        public static MenuDialog ActiveMenuDialog { get; set; }

        /// <summary>
        /// キャッシュされた、このメニューダイアログ内のボタンオブジェクトのリスト。
        /// </summary>
        public virtual Button[] CachedButtons
        {
            get { return cachedButtons; }
        }

        /// <summary>
        /// キャッシュされた、時間制限タイマー用のスライダーオブジェクト。
        /// </summary>
        public virtual Slider CachedSlider
        {
            get { return cachedSlider; }
        }

        /// <summary>
        /// MenuDialogのGameObjectのアクティブ状態を設定します。
        /// </summary>
        public virtual void SetActive(bool state)
        {
            // 設定されていれば、背景オブジェクトの表示状態も連動させます
            if (backImage != null)
            {
                backImage.SetActive(state);
            }

            gameObject.SetActive(state);
        }

        /// <summary>
        /// シーン内を検索してMenuDialogを返すか、存在しない場合は新しく生成します。
        /// </summary>
        public static MenuDialog GetMenuDialog()
        {
            if (ActiveMenuDialog == null)
            {
                // まず、シーン内に既存のMenuDialogがあるか検索して使用します
                var md = GameObject.FindObjectOfType<MenuDialog>();
                if (md != null)
                {
                    ActiveMenuDialog = md;
                }

                // シーン内に見つからなかった場合
                if (ActiveMenuDialog == null)
                {
                    // Resources/Prefabs/MenuDialog からプレハブを自動で生成します
                    GameObject prefab = Resources.Load<GameObject>("Prefabs/MenuDialog");
                    if (prefab != null)
                    {
                        GameObject go = Instantiate(prefab) as GameObject;
                        go.SetActive(false);
                        go.name = "MenuDialog";
                        ActiveMenuDialog = go.GetComponent<MenuDialog>();
                    }
                }
            }

            return ActiveMenuDialog;
        }

        protected virtual void Awake()
        {
            // 起動時に、子オブジェクトに含まれる全てのButtonとSliderを取得してキャッシュします
            Button[] optionButtons = GetComponentsInChildren<Button>();
            cachedButtons = optionButtons;

            Slider timeoutSlider = GetComponentInChildren<Slider>();
            cachedSlider = timeoutSlider;

            // ゲーム開始時に背景を非表示にしておきます
            if (backImage != null)
            {
                backImage.SetActive(false);
            }

            // ゲーム実行中のみ、初期化処理を行います
            if (Application.isPlaying)
            {
                // エディタ上ではボタンを自動で無効化しません
                Clear();
            }

            CheckEventSystem();
        }

        /// <summary>
        /// キャンセルキーの入力を監視します。
        /// </summary>
        protected virtual void Update()
        {
            // キャンセル可能な選択肢が設定されていない、またはダイアログが非表示の場合は何もしない
            if (cancelableOption == null || !IsActive())
            {
                return;
            }

            // 設定されたキャンセルキーが押されたかチェック
            if (Input.GetKeyDown(MenuDialog.cancelKey))
            {
                // 対応するアクションを実行
                cancelableOption.Value.action.Invoke();
            }
        }

        // SayやMenuの入力が機能するには、シーンにEventSystemが必要です。
        // このメソッドは、もし存在しない場合に自動で一つ生成します。
        protected virtual void CheckEventSystem()
        {
            EventSystem eventSystem = GameObject.FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                // Resources/Prefabs/EventSystem からプレハブを自動で生成します
                GameObject prefab = Resources.Load<GameObject>("Prefabs/EventSystem");
                if (prefab != null)
                {
                    GameObject go = Instantiate(prefab) as GameObject;
                    go.name = "EventSystem";
                }
            }
        }

        protected virtual void OnEnable()
        {
            // ゲームの最初のフレームでMenuDialogが有効化されると、Canvasの更新に失敗することがあります。
            // これを修正するため、オブジェクトが有効化された際にCanvasの強制更新を行います。
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// 時間制限タイマーを実行するコルーチン
        /// </summary>
        protected virtual IEnumerator WaitForTimeout(float timeoutDuration, Block targetBlock)
        {
            float elapsedTime = 0;
            Slider timeoutSlider = CachedSlider;

            // 制限時間に達するまでループ
            while (elapsedTime < timeoutDuration)
            {
                if (timeoutSlider != null)
                {
                    // スライダーの値を時間の逆進行（1 -> 0）で更新
                    float t = 1f - elapsedTime / timeoutDuration;
                    timeoutSlider.value = t;
                }

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // タイムアウト後の処理
            Clear();
            gameObject.SetActive(false);
            HideSayDialog();

            // 指定されたタイムアウト用のBlockがあれば実行
            if (targetBlock != null)
            {
                targetBlock.StartExecution();
            }
        }

        /// <summary>
        /// 指定されたBlockを次のフレームで実行するコルーチン
        /// </summary>
        protected IEnumerator CallBlock(Block block)
        {
            yield return new WaitForEndOfFrame();
            block.StartExecution();
        }

        /// <summary>
        /// 指定されたLuaクロージャを次のフレームで実行するコルーチン
        /// </summary>
        protected IEnumerator CallLuaClosure(LuaEnvironment luaEnv, Closure callback)
        {
            yield return new WaitForEndOfFrame();
            if (callback != null)
            {
                luaEnv.RunLuaFunction(callback, true);
            }
        }

        /// <summary>
        /// MenuDialogに表示されている全ての選択肢をクリア（非表示に）します。
        /// </summary>
        public virtual void Clear()
        {
            // 実行中のコルーチン（タイマーなど）をすべて停止
            StopAllCoroutines();

            // すでに何かが表示されていた場合は、メニュー終了のシグナルを発行
            if (nextOptionIndex != 0)
                MenuSignals.DoMenuEnd(this);

            //キャンセル可能な選択肢をリセット
            cancelableOption = null;

            // 選択肢をクリアする際に背景も非表示にします
            if (backImage != null)
            {
                backImage.SetActive(false);
            }

            // 次の選択肢インデックスをリセット
            nextOptionIndex = 0;

            // 全てのボタンから、登録されているクリックイベントを削除
            var optionButtons = CachedButtons;
            for (int i = 0; i < optionButtons.Length; i++)
            {
                var button = optionButtons[i];
                button.onClick.RemoveAllListeners();
            }

            // 全てのボタンを非表示にし、ヒエラルキーの順序を元に戻す
            for (int i = 0; i < optionButtons.Length; i++)
            {
                var button = optionButtons[i];
                if (button != null)
                {
                    button.transform.SetSiblingIndex(i);
                    button.gameObject.SetActive(false);
                }
            }

            // タイムアウト用のスライダーも非表示にする
            Slider timeoutSlider = CachedSlider;
            if (timeoutSlider != null)
            {
                timeoutSlider.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 現在表示されている可能性のあるSayDialogを非表示にします。
        /// </summary>
        public virtual void HideSayDialog()
        {
            var sayDialog = SayDialog.GetSayDialog();
            if (sayDialog != null)
            {
                sayDialog.FadeWhenDone = true;
            }
        }

        /// <summary>
        /// 表示される選択肢のリストにオプションを追加します。選択されると指定のBlockを呼び出します。
        /// MenuDialogが非表示の場合、このメソッドが呼ばれると表示状態になります。
        /// </summary>
        /// <returns>オプションが正常に追加されればtrueを返します。</returns>
        /// <param name="text">ボタンに表示するテキスト。</param>
        /// <param name="interactable">falseの場合、ボタンは表示されますが選択できなくなります。</param>
        /// <param name="hideOption">trueの場合、オプションは表示されませんが、メニュー内には存在するものとして扱われます。</param>
        /// <param name="targetBlock">選択されたときに実行されるBlock。</param>
        public virtual bool AddOption(
            string text,
            bool interactable,
            bool hideOption,
            Block targetBlock
        )
        {
            var block = targetBlock;

            // ボタンがクリックされたときに実行される処理を定義
            UnityEngine.Events.UnityAction action = delegate
            {
                EventSystem.current.SetSelectedGameObject(null);
                StopAllCoroutines(); // タイムアウトタイマーなどを停止
                Clear();
                HideSayDialog();

                if (block != null)
                {
                    var flowchart = block.GetFlowchart();
                    gameObject.SetActive(false);
                    // MenuDialogが非アクティブになるため、FlowchartのGameObjectを使って次のBlockを呼び出す
                    flowchart.StartCoroutine(CallBlock(block));
                }
            };

            return AddOption(text, interactable, hideOption, action);
        }

        /// <summary>
        /// 表示される選択肢のリストにオプションを追加します。選択されると指定のLua関数を呼び出します。
        /// </summary>
        public virtual bool AddOption(
            string text,
            bool interactable,
            LuaEnvironment luaEnv,
            Closure callBack
        )
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            // 安全のためローカル変数にコピー
            LuaEnvironment env = luaEnv;
            Closure call = callBack;
            UnityEngine.Events.UnityAction action = delegate
            {
                StopAllCoroutines();
                Clear();
                HideSayDialog();
                // 次のフレームでコールバックを呼び出すためにコルーチンを使用
                StartCoroutine(CallLuaClosure(env, call));
            };

            return AddOption(text, interactable, false, action);
        }

        /// <summary>
        /// 実際にボタンオブジェクトをセットアップする内部メソッド。
        /// </summary>
        private bool AddOption(
            string text,
            bool interactable,
            bool hideOption,
            UnityEngine.Events.UnityAction action
        )
        {
            if (nextOptionIndex >= CachedButtons.Length)
            {
                Debug.LogWarning("メニュー項目を追加できません。ボタンの数が足りません: " + text);
                return false;
            }

            // 最初の選択肢であれば、メニューが開始したことを通知します
            if (nextOptionIndex == 0)
                MenuSignals.DoMenuStart(this);

            var button = cachedButtons[nextOptionIndex];

            // 次の呼び出しのためにインデックスを進める
            nextOptionIndex++;

            // hideOptionがtrueなら、ボタンを有効化せずに処理を終了
            if (hideOption)
                return true;

            button.gameObject.SetActive(true);
            button.interactable = interactable;

            // もし自動選択が有効で、まだ何も選択されていない場合、このボタンを選択状態にする
            if (
                interactable
                && autoSelectFirstButton
                && !cachedButtons
                    .Select(x => x.gameObject)
                    .Contains(EventSystem.current.currentSelectedGameObject)
            )
            {
                EventSystem.current.SetSelectedGameObject(button.gameObject);
            }

            // ボタンのテキストを設定
            TextAdapter textAdapter = new TextAdapter();
            textAdapter.InitFromGameObject(button.gameObject, true);
            if (textAdapter.HasTextObject())
            {
                text = TextVariationHandler.SelectVariations(text);
                textAdapter.Text = text;
            }

            // ボタンのクリックイベントにactionを登録
            button.onClick.AddListener(action);

            var icon = button.transform.Find(cancelIconName)?.GetComponent<Image>();
            if (icon != null)
            {
                icon.gameObject.SetActive(false); // アイコンはデフォルトで非表示
            }
            else
            {
                Debug.LogWarning(
                    $"ボタンの子オブジェクトに '{cancelIconName}' という名前のImageが見つかりません。"
                );
            }

            return true;
        }

        /// <summary>
        /// キャンセル可能なオプションを設定します。選択またはキー押下で指定のBlockを呼び出します。
        /// </summary>
        public virtual bool AddCancelableOption(
            string text,
            BooleanData interactable,
            bool hideOption,
            Block targetBlock
        )
        {
            if (nextOptionIndex >= CachedButtons.Length)
            {
                Debug.LogWarning(
                    "キャンセル可能なメニュー項目を追加できません。ボタンの数が足りません: " + text
                );
                return false;
            }

            // (actionの定義は変更なし) ...
            UnityEngine.Events.UnityAction action = delegate
            {
                EventSystem.current.SetSelectedGameObject(null);
                StopAllCoroutines();
                Clear();
                HideSayDialog();

                if (targetBlock != null)
                {
                    var flowchart = targetBlock.GetFlowchart();
                    gameObject.SetActive(false);
                    flowchart.StartCoroutine(CallBlock(targetBlock));
                }
            };

            // (ボタンのセットアップ処理は変更なし) ...
            if (nextOptionIndex == 0)
                MenuSignals.DoMenuStart(this);

            var button = cachedButtons[nextOptionIndex];
            nextOptionIndex++;

            if (hideOption)
            {
                return true;
            }

            button.gameObject.SetActive(true);
            button.interactable = interactable;

            if (
                interactable
                && autoSelectFirstButton
                && !cachedButtons
                    .Select(x => x.gameObject)
                    .Contains(EventSystem.current.currentSelectedGameObject)
            )
            {
                EventSystem.current.SetSelectedGameObject(button.gameObject);
            }

            TextAdapter textAdapter = new TextAdapter();
            textAdapter.InitFromGameObject(button.gameObject, true);
            if (textAdapter.HasTextObject())
            {
                text = TextVariationHandler.SelectVariations(text);
                textAdapter.Text = text;
            }

            button.onClick.AddListener(action);

            var icon = button.transform.Find(cancelIconName)?.GetComponent<Image>();
            if (icon != null)
            {
                icon.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning(
                    $"ボタンの子オブジェクトに '{cancelIconName}' という名前のImageが見つかりません。"
                );
            }

            // ★修正: キャンセルオプションを変数に設定
            if (interactable)
            {
                // 既に設定されている場合は警告を出す（ルールを明確にするため）
                if (cancelableOption != null)
                {
                    Debug.LogWarning(
                        "CancelableMenuは1つのメニュー内に1つしか設定できません。古い設定は上書きされます。"
                    );
                }
                cancelableOption = new CancelableOption { action = action };
            }

            return true;
        }

        /// <summary>
        /// プレイヤーが選択肢を選べる時間制限タイマーを表示します。時間切れになると指定のBlockを呼び出します。
        /// </summary>
        /// <param name="duration">プレイヤーが選択できる時間（秒）。</param>
        /// <param name="targetBlock">時間内に選択しなかった場合に実行されるBlock。</param>
        public virtual void ShowTimer(float duration, Block targetBlock)
        {
            if (cachedSlider != null)
            {
                cachedSlider.gameObject.SetActive(true);
                gameObject.SetActive(true);
                StopAllCoroutines();
                StartCoroutine(WaitForTimeout(duration, targetBlock));
            }
            else
            {
                Debug.LogWarning("タイマーを表示できません。Sliderが設定されていません。");
            }
        }

        /// <summary>
        /// プレイヤーが選択肢を選べる時間制限タイマーを表示します。時間切れになると指定のLua関数を呼び出します。
        /// </summary>
        public virtual IEnumerator ShowTimer(
            float duration,
            LuaEnvironment luaEnv,
            Closure callBack
        )
        {
            if (CachedSlider == null || duration <= 0f)
            {
                yield break;
            }

            CachedSlider.gameObject.SetActive(true);
            StopAllCoroutines();

            float elapsedTime = 0;
            Slider timeoutSlider = CachedSlider;

            while (elapsedTime < duration)
            {
                if (timeoutSlider != null)
                {
                    float t = 1f - elapsedTime / duration;
                    timeoutSlider.value = t;
                }
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            Clear();
            gameObject.SetActive(false);
            HideSayDialog();

            if (callBack != null)
            {
                luaEnv.RunLuaFunction(callBack, true);
            }
        }

        /// <summary>
        /// MenuDialogが現在表示されている場合にtrueを返します。
        /// </summary>
        public virtual bool IsActive()
        {
            return gameObject.activeInHierarchy;
        }

        /// <summary>
        /// 現在表示されている選択肢の数を返します。
        /// </summary>
        public virtual int DisplayedOptionsCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < cachedButtons.Length; i++)
                {
                    var button = cachedButtons[i];
                    if (button.gameObject.activeSelf)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// キャッシュされたボタンの親子関係の順序をシャッフルします。
        /// これによりボタンの表示順をランダムにできます。順序はClear()で元に戻ります。
        /// </summary>
        public void Shuffle(System.Random r)
        {
            for (int i = 0; i < CachedButtons.Length; i++)
            {
                CachedButtons[i].transform.SetSiblingIndex(r.Next(CachedButtons.Length));
            }
        }

        #endregion
    }
}

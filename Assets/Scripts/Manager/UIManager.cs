using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// ゲームのメニューUI（MenuCanvas）全体の表示・非表示、
/// およびサブパネル群のスタック管理（履歴管理）を行うマネージャークラス。
/// </summary>
public class UIManager : MonoBehaviour, IPanelStackManager
{
    #region シングルトン・イベント定義

    /// <summary>シングルトン用のインスタンス</summary>
    public static UIManager instance { get; private set; }

    /// <summary>メニューの表示状態が変化したときに発行されるイベント</summary>
    public static event System.Action<bool> OnMenuStateChanged;

    #endregion

    #region 公開プロパティ（状態フラグ）

    /// <summary>メニュー画面（MenuCanvas）が開いているかどうかのフラグ</summary>
    public bool isMenuOpen { get; private set; } = false;

    /// <summary>クイックアイテム登録画面が開いているかどうかのフラグ</summary>
    public bool IsQuickItemRegistering { get; private set; } = false;

    #endregion

    #region インスペクター設定・外部コンポーネント参照

    [Header("UI参照のルート")]
    [Tooltip("メニューUIの各種コンポーネントをまとめた参照クラス")]
    [SerializeField]
    private MenuUIRefs uiRefs;

    // --- 他のマネージャークラスへのキャッシュ参照 ---
    private PlayerManager playerManager;
    private PlayerLevelManager playerLevelManager;
    private InputManager inputManager;

    #endregion

    #region 内部状態変数（履歴・制御フラグ）

    // メニューを開いた直後、決定ボタンの連打などによる誤作動（即閉じ）を防ぐためのクールダウン時間（秒）
    private const float menuOpenInputCooldown = 0.1f;

    // MenuCanvasを開いている途中かどうかのフラグ
    private bool isOpeningCanvas;

    // 現在会話中かどうかを保存するローカル変数（会話中はメニューを開けないようにするため）
    private bool isTalking = false;

    // UIのフォーカスが外れた際に復帰させるため、最後に選ばれていたボタンを記憶する変数
    private GameObject lastSelected;

    // 現在開かれているパネルの履歴（階層）を管理するスタック
    private Stack<GameObject> panelStack = new Stack<GameObject>();

    // スタックに積まない独立したポップアップ（アイテム使用確認など）を保持する変数
    private GameObject activePopup = null;

    #endregion

    #region 起動・終了イベント（ライフサイクル）

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            isOpeningCanvas = false;
            IsQuickItemRegistering = false;

            // 現在のシーンのPanelStackManagerとして自身をSaveLoadManagerに登録
            SaveLoadManager.RegisterActiveManager(this);

            if (uiRefs == null)
            {
                Debug.LogError("UIManagerはMenuUIRefsを持っていません");
                return;
            }
        }
        else
        {
            // 既にインスタンスが存在する場合は自身を破棄
            Destroy(this.gameObject);
        }
    }

    private void Start()
    {
        // 各種マネージャーの参照を取得
        playerManager = PlayerManager.instance;
        if (playerManager == null)
        {
            Debug.LogError("PlayerManagerが見つかりません。UIManagerは正常に動作しません。");
            return;
        }

        playerLevelManager = PlayerLevelManager.instance;
        if (playerLevelManager == null)
        {
            Debug.LogError("PlayerLevelManagerが見つかりません。UIManagerは正常に動作しません。");
            return;
        }

        inputManager = InputManager.instance;
        if (inputManager == null)
        {
            Debug.LogError("InputManagerが見つかりません。UIManagerは正常に動作しません。");
            return;
        }

        // イベントを購読する
        GameManager.OnTalkingStateChanged += HandleTalkingStateChanged;
    }

    private void OnDisable()
    {
        // オブジェクトが非アクティブになったら、メモリリーク防止のため購読を解除
        GameManager.OnTalkingStateChanged -= HandleTalkingStateChanged;
    }

    private void OnDestroy()
    {
        // このインスタンスがシングルトンのインスタンスと同一である場合のみ、登録解除処理を行う
        if (instance == this)
        {
            SaveLoadManager.UnregisterActiveManager(this);
            instance = null; // instance もクリア
        }

        // OnDisableと重複するが、OnDestroyで確実に購読解除
        GameManager.OnTalkingStateChanged -= HandleTalkingStateChanged;
    }

    #endregion

    #region メイン更新処理（Update）

    private void Update()
    {
        // uiRefs.MenuCanvas の activeSelf チェック時に null であるとエラーになるため弾く
        if (uiRefs.MenuCanvas == null)
            return;

        // --- メニューを開く判定と処理 ---
        bool canOpenMenu =
            !isTalking
            && // 会話中でない
            !uiRefs.MenuCanvas.activeSelf
            && // 既にメニューが開いていない
            Time.timeScale != 0
            && // ゲームが停止状態（ポーズ中）でない
            inputManager.MenuUIOpen()
            && // メニューを開く入力があった
            !playerManager.isControlLocked; // プレイヤーが操作不能状態ではない

        if (inputManager.MenuUIOpen() && !canOpenMenu)
        {
            Debug.Log(
                "メニューを開く条件を満たしていないため、入力は無視されました。条件詳細: "
                    + $"isTalking={isTalking}, MenuCanvas.activeSelf={uiRefs.MenuCanvas.activeSelf}, Time.timeScale={Time.timeScale}, "
                    + $"playerManager.isControlLocked={playerManager.isControlLocked}"
            );
        }

        if (canOpenMenu)
        {
            isOpeningCanvas = true; // CloseTopPanel()などが誤作動しないように先にフラグをON
            isMenuOpen = true; // メニューが開かれているフラグをON
            OnMenuStateChanged?.Invoke(true); // メニューが開かれたイベントを発行
            TimeManager.instance.RequestPause(); // ゲーム内の時間を止める
            OpenMenuCanvas(); // メニューUIを表示
        }

        // メニューが開かれていない場合はこれ以降のUI処理を行わない
        if (!uiRefs.MenuCanvas.activeSelf)
            return;

        // --- メニュー（パネル）を閉じる判定と処理 ---
        if (
            (inputManager.UIClose() || inputManager.UISelectNo()) // キャンセル系の入力があった
            && !isOpeningCanvas // メニュー画面が展開中（クールダウン中）でない
            && !SaveLoadManager.isDataPrompting // データ保存・ロードの確認画面が出ていない
            && !IsQuickItemRegistering // クイックアイテム登録画面が出ていない
        )
        {
            if (activePopup != null)
            {
                // アクティブなポップアップがあれば、履歴操作をせずに最優先で閉じる
                ClosePopup();
            }
            else
            {
                // 一番上のパネル（トップパネル）を閉じる
                CloseTopPanel();
            }
        }

        // --- UIフォーカスの自動復帰処理 ---
        if (EventSystem.current != null)
        {
            // クイックアイテム登録画面が開いている間は、独自の操作を使用するため意図的にフォーカスを空にしている。
            // そのため、登録中はこの自動復元処理をスキップする。
            if (!IsQuickItemRegistering)
            {
                // ① 現在のUI上の選択が消えてしまった場合
                if (EventSystem.current.currentSelectedGameObject == null)
                {
                    // ② 記憶していた直前のボタン(lastSelected)が破棄されておらず、かつ画面に表示されている場合のみフォーカスを戻す
                    if (lastSelected != null && lastSelected.activeInHierarchy)
                    {
                        EventSystem.current.SetSelectedGameObject(lastSelected);
                    }
                }
                else
                {
                    // ③ 選択が正常に行われている場合は、そのボタンを「最後の選択」として記憶し続ける
                    lastSelected = EventSystem.current.currentSelectedGameObject;
                }
            }
        }
    }

    #endregion

    #region メニューキャンバス全体の制御

    /// <summary>
    /// メニューキャンバス全体を開き、初期状態のパネル（MenuPanel, ProgressLogPanel）をセットアップします。
    /// </summary>
    public void OpenMenuCanvas()
    {
        if (uiRefs.MenuCanvas != null)
        {
            // 誤動作防止のクールダウンを開始
            StartCoroutine(EnableCanvasAfterDelay());
            uiRefs.MenuCanvas.SetActive(true);

            // セーブ機能が無効な場所・タイミングであれば、セーブボタンを押せなくして見た目も暗くする
            if ((!SaveLoadManager.instance?.isEnableSave ?? false) && uiRefs.SaveButton != null)
            {
                uiRefs.SaveButton.interactable = false;
            }

            // 初期状態のパネルを開く
            OpenPanel(uiRefs.MenuPanel, 1); // 階層1: メニューのベースとなるボタン群
            OpenPanel(uiRefs.ProgressLogPanel, 2); // 階層2: 進行状況ログ画面

            // 現在のレベルを取得して表示
            int LvNumber = playerLevelManager.playerLv;
            uiRefs.LvNumberText.text = $"<color=#C6A34C>{LvNumber}</color>";

            // 所持金が変わったときにUIに反映されるようイベントを登録し、現在の所持金を表示
            playerManager.OnChangePlayerMoney += SetCoinText;
            SetCoinText();

            Debug.Log("UIManagerはMenuCanvasを開きました");
        }
        else
        {
            Debug.LogWarning("UIManagerはMenuCanvasゲームオブジェクトを持っていません");
        }
    }

    /// <summary>
    /// メニューキャンバス全体を閉じ、ゲームを再開します。
    /// 開いているすべてのサブパネルも非表示にします。
    /// </summary>
    public void CloseMenuCanvas()
    {
        if (uiRefs.MenuCanvas != null)
        {
            uiRefs.MenuCanvas.SetActive(false); // MenuCanvasを非表示にする

            // スタックに積まれている全てのパネルを取り出して非表示にする
            while (panelStack.Count > 0)
            {
                GameObject top = panelStack.Pop();
                top.SetActive(false);
            }

            uiRefs.SaveButton.interactable = true; // SaveButtonの状態を初期化（次回開いた時のため）
            TimeManager.instance.ReleasePause(); // ゲームの時間を元に戻す（再開）
            isMenuOpen = false; // メニュー画面が開いているかどうかのフラグをOFF

            OnMenuStateChanged?.Invoke(false); // メニューが閉じられたイベントを発行
            playerManager.OnChangePlayerMoney -= SetCoinText; // 所持金監視イベントを解除
        }
        else
        {
            Debug.LogWarning("UIManagerはMenuCanvasゲームオブジェクトを持っていません");
        }
    }

    #endregion

    #region サブパネルのスタック（履歴）制御

    /// <summary>
    /// 指定されたパネルを開き、パネルの履歴（スタック）に追加します。
    /// 必要に応じて、指定された階層（Stage）以上のパネルを自動的に閉じます。
    /// </summary>
    /// <param name="panel">開く対象のパネルオブジェクト</param>
    /// <param name="Stage">パネルの階層レベル（-1の場合は階層を無視して単に上に積む）</param>
    public void OpenPanel(GameObject panel, int Stage = -1)
    {
        // 既に指定された階層（Stage）以上のパネルが開いている場合は、それらを閉じる
        if (panelStack.Count >= Stage && Stage != -1)
        {
            while (panelStack.Count >= Stage)
            {
                GameObject top = panelStack.Pop();
                top.SetActive(false); // パネルを非表示にする
            }
        }

        // パネルがまだ開いていない場合のみ処理を行う
        if (panel.activeSelf == false)
        {
            // 先にスタックに追加しないと、対象パネルの OnEnable 等で別のパネルを開く処理が走った際に不具合が起こる
            panelStack.Push(panel);
            panel.SetActive(true); // パネルを表示する

            // 開いたパネルが IPanelActive を実装していれば、初期フォーカスを設定させる
            var panelActive = panel.GetComponent<IPanelActive>();
            if (panelActive != null)
            {
                panelActive.SelectFirstButton();
            }
        }
    }

    /// <summary>
    /// 現在一番上に表示されているパネル（スタックの先頭）を閉じ、一つ前のパネルにフォーカスを戻します。
    /// パネルがもうない場合は、メニュー全体を閉じます。
    /// </summary>
    public void CloseTopPanel()
    {
        // スタックが空の場合は何もしない（エラー防止）
        if (panelStack.Count == 0)
        {
            return;
        }

        // トップ画面（ProgressLogPanel）を表示中にキャンセルボタンが押された場合は、メニュー全体を閉じる
        if (panelStack.Peek() == uiRefs.ProgressLogPanel)
        {
            CloseMenuCanvas();
            return;
        }

        if (panelStack.Count > 0)
        {
            // 一番上のパネルを取り出して非表示にする
            GameObject top = panelStack.Pop();
            top.SetActive(false);

            if (panelStack.Count > 0)
            {
                // 次の（下に隠れていた）パネルを取得
                top = panelStack.Peek();
                var panelActive = top.GetComponent<IPanelActive>();
                if (panelActive != null)
                {
                    panelActive.SelectFirstButton(); // 復帰したパネルの最初のボタンにフォーカスを戻す
                }

                // 特殊処理：MenuPanel（階層1）まで戻ってしまった場合、自動的に ProgressLogPanel（階層2）を開き直す
                // （ProgressLogPanelはボタンを持たない純粋な表示用パネルのため、MenuPanelのボタンフォーカスの直後に被せて表示する）
                if (panelStack.Count == 1)
                {
                    OpenPanel(uiRefs.ProgressLogPanel, 2);
                }
            }
            else
            {
                // スタックが空になった場合はメニュー全体を閉じる
                CloseMenuCanvas();
            }
        }
    }

    /// <summary>
    /// スタックの操作（パネルの開閉）は行わず、現在一番上にあるパネル（トップパネル）に再びフォーカスを当て直します。
    /// スタックに積まれない特殊なポップアップやダイアログを閉じた後、元の画面に操作を戻す際などに使用します。
    /// </summary>
    public void RefocusTopPanel()
    {
        // スタックが空でないか確認
        if (panelStack.Count > 0)
        {
            // 一番上のパネルを「取り出さずに」見るだけ (Peek)
            GameObject topPanel = panelStack.Peek();

            // パネルが存在し、かつ現在画面に表示されている場合のみ処理
            if (topPanel != null && topPanel.activeInHierarchy)
            {
                var panelActive = topPanel.GetComponent<IPanelActive>();
                if (panelActive != null)
                {
                    panelActive.SelectFirstButton();
                }
            }
        }
    }

    #endregion

    #region 独立ポップアップ（モーダル）制御

    /// <summary>
    /// 履歴（スタック）に積まない独立したポップアップ画面を開きます。
    /// </summary>
    public void OpenPopup(GameObject popup)
    {
        if (popup == null)
            return;

        activePopup = popup;
        popup.SetActive(true);

        // IPanelActiveを実装していれば初期フォーカスを当てる
        var panelActive = popup.GetComponent<IPanelActive>();
        if (panelActive != null)
        {
            panelActive.SelectFirstButton();
        }
    }

    /// <summary>
    /// 現在開かれている独立したポップアップ画面を閉じ、背面のパネルにフォーカスを戻します。
    /// </summary>
    public void ClosePopup()
    {
        if (activePopup != null)
        {
            activePopup.SetActive(false);
            activePopup = null;
            RefocusTopPanel(); // 閉じた後に元の画面にフォーカスをピタッと戻す
        }
    }

    #endregion

    #region UI表示の更新・フラグ操作

    /// <summary>
    /// クイックアイテム登録画面が開いているかどうかの状態をセットします。
    /// （QuickItemRegisterPanel 等から呼び出されます）
    /// </summary>
    public void SetQuickItemRegistering(bool isRegistering)
    {
        IsQuickItemRegistering = isRegistering;
    }

    /// <summary>
    /// プレイヤーの現在の所持金を取得し、UIのテキストに反映します。
    /// </summary>
    private void SetCoinText()
    {
        int currentMoney = playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerMoney);
        // 金色（#C6A34C）のタグをつけて表示
        uiRefs.CoinNumberText.text = $"<color=#C6A34C>{currentMoney}</color>";
    }

    #endregion

    #region コルーチン・イベントハンドラ

    /// <summary>
    /// メニューを開いた直後、一定時間 `isOpeningCanvas` フラグを true に保つコルーチン。
    /// メニューを開くボタンと閉じるボタンが同じ場合などの「即閉じ」を防ぎます。
    /// </summary>
    private IEnumerator EnableCanvasAfterDelay()
    {
        // Time.timeScale = 0（ポーズ中）でも動作するように WaitForSecondsRealtime を使用
        yield return new WaitForSecondsRealtime(menuOpenInputCooldown);
        isOpeningCanvas = false;
    }

    /// <summary>
    /// GameManagerから会話状態の変更通知を受け取り、内部フラグを更新します。
    /// </summary>
    private void HandleTalkingStateChanged(bool talkState)
    {
        isTalking = talkState;
    }

    #endregion
}

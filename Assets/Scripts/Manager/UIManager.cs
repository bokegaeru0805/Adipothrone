using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIManager : MonoBehaviour
{
    public static event System.Action<bool> OnMenuStateChanged; // メニューの表示状態が変化したときに発行されるイベント
    public bool IsQuickItemRegistering { get; private set; } = false; //クイックアイテム登録中かどうかのフラグ

    public void SetQuickItemRegistering(bool isRegistering)
    {
        IsQuickItemRegistering = isRegistering;
    }

    public static UIManager instance { get; private set; }
    private PlayerManager playerManager;
    private PlayerLevelManager playerLevelManager;
    private InputManager inputManager;

    [Header("UI参照のルート")]
    [SerializeField]
    private MenuUIRefs uiRefs;
    private const float menuOpenInputCooldown = 0.1f; //メニューを開いた直後、誤操作で閉じるのを防ぐためのクールダウン時間（秒）
    public bool isMenuOpen { get; private set; } = false; //MenuCanvasが開いているかどうかのフラグ
    private bool isOpeningCanvas; //MenuCanvasを開いている途中かどうかのフラグ
    private bool isTalking = false; // 会話状態を保存するローカル変数

    private GameObject lastSelected; //最後に選ばれていたボタンを保存する変数
    private Stack<GameObject> panelStack = new Stack<GameObject>();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            isOpeningCanvas = false;
            IsQuickItemRegistering = false;

            if (uiRefs == null)
            {
                Debug.LogError("UIManagerはMenuUIRefsを持っていません");
                return;
            }
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    private void Start()
    {
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
        // オブジェクトが非アクティブになったら、購読を解除（メモリリーク防止）
        GameManager.OnTalkingStateChanged -= HandleTalkingStateChanged;
    }

    private void Update()
    {
        //下記のactiveselfのチェック時にnullである必要がある
        if (uiRefs.MenuCanvas == null)
            return;

        // メニューを開ける条件を全て満たすときのみ、メニュー画面を開く
        bool canOpenMenu =
            !isTalking
            && // 会話中でない
            !uiRefs.MenuCanvas.activeSelf
            && // 既にメニューが開いていない
            Time.timeScale != 0
            && // ゲームが停止状態でない
            inputManager.MenuUIOpen()
            && // メニュー入力があった
            !playerManager.isControlLocked; // プレイヤーが操作不能状態ではない

        if (canOpenMenu)
        {
            isOpeningCanvas = true; // CloseTopPanel()などが誤作動しないように先にフラグをON
            isMenuOpen = true; // メニューが開かれているフラグをON
            OnMenuStateChanged?.Invoke(true); // イベントを発行
            TimeManager.instance.RequestPause(); // ゲーム時間を止める
            OpenMenuCanvas(); // メニューUIを表示
        }

        if (!uiRefs.MenuCanvas.activeSelf)
            return;

        if (
            (inputManager.UIClose() || inputManager.UISelectNo())
            && !isOpeningCanvas
            && !SaveLoadManager.isDataPrompting
            && !IsQuickItemRegistering
        )
        {
            //メニュー画面が開いているとき
            //メニュー画面が展開中でないとき
            //データ保存確認画面が出ていないとき
            //クイックアイテム登録画面が出ていないとき
            CloseTopPanel();
        }

        // UI上の選択が消えたら元に戻す
        if (EventSystem.current != null)
        {
            if (EventSystem.current.currentSelectedGameObject == null && lastSelected != null)
            {
                EventSystem.current.SetSelectedGameObject(lastSelected);
            }
            else
            {
                lastSelected = EventSystem.current.currentSelectedGameObject;
            }
        }
    }

    public void OpenMenuCanvas()
    {
        if (uiRefs.MenuCanvas != null)
        {
            StartCoroutine(EnableCanvasAfterDelay());
            uiRefs.MenuCanvas.SetActive(true);
            if ((!SaveLoadManager.instance?.isEnableSave ?? false) && uiRefs.SaveButton != null)
            {
                uiRefs.SaveButton.interactable = false; //押せなくなり、見た目も変わる
            }
            OpenPanel(uiRefs.MenuPanel, 1); // MenuPanelを開く
            OpenPanel(uiRefs.ProgressLogPanel, 2); // ProgressLogPanelを開く

            //現在のレベルを表示する
            int LvNumber = playerLevelManager.playerLv;
            uiRefs.LvNumberText.text = $"<color=#C6A34C>{LvNumber}</color>";
            //所持金が変わったときのイベントを登録
            playerManager.OnChangePlayerMoney += SetCoinText;
            //現在の所持金を表示する
            SetCoinText();
        }
        else
        {
            Debug.LogWarning("UIManagerはMenuCanvasゲームオブジェクトを持っていません");
        }
    }

    public void CloseMenuCanvas()
    {
        if (uiRefs.MenuCanvas != null)
        {
            uiRefs.MenuCanvas.SetActive(false); //MenuCanvasを非表示にする
            while (panelStack.Count > 0)
            {
                GameObject top = panelStack.Pop();
                top.SetActive(false); //パネルを非表示にする
            }
            uiRefs.SaveButton.interactable = true; //SaveButtonの状態を初期化
            TimeManager.instance.ReleasePause(); // 時間を元に戻す
            isMenuOpen = false; //メニュー画面が開いているかどうかのフラグをOFF
            OnMenuStateChanged?.Invoke(false); // イベントを発行
            playerManager.OnChangePlayerMoney -= SetCoinText; //所持金が変わったときのイベントを解除
        }
        else
        {
            Debug.LogWarning("UIManagerはMenuCanvasゲームオブジェクトを持っていません");
        }
    }

    public void OpenPanel(GameObject panel, int Stage = -1)
    {
        if (panelStack.Count >= Stage && Stage != -1)
        {
            //同じ階層までパネルを非表示にする
            while (panelStack.Count >= Stage)
            {
                GameObject top = panelStack.Pop();
                top.SetActive(false); //パネルを非表示にする
            }
        }

        if (panel.activeSelf == false)
        {
            panelStack.Push(panel); //先にスタックに追加しないと、パネルが開いたときに、他のパネルを開く動作が不具合が起こる
            panel.SetActive(true); //パネルを表示する

            IPanelActive panelActive = panel.GetComponent<IPanelActive>();
            if (panelActive != null)
            {
                panelActive.SelectFirstButton(); //最初の選択ボタンを指定する
            }
        }
    }

    public void CloseTopPanel()
    {
        if (panelStack.Peek() == uiRefs.ProgressLogPanel)
        {
            CloseMenuCanvas(); //MenuCanvasを閉じる
        }

        if (panelStack.Count > 0)
        {
            GameObject top = panelStack.Pop();
            top.SetActive(false); //パネルを非表示にする
            if (panelStack.Count > 0)
            {
                top = panelStack.Peek(); //次のパネルを取得
                IPanelActive panelActive = top.GetComponent<IPanelActive>();
                if (panelActive != null)
                {
                    panelActive.SelectFirstButton(); //次のパネルの最初の選択ボタンを指定する
                }

                //ProgressLogPanelはボタンを持たず、またMenuPanelはボタンを持つので
                //最初の選択ボタンを指定した後に開く
                if (panelStack.Count == 1)
                {
                    OpenPanel(uiRefs.ProgressLogPanel, 2); // ProgressLogPanelを開く
                }
            }
            else
            {
                CloseMenuCanvas(); //MenuCanvasを閉じる
            }
        }
    }

    private IEnumerator EnableCanvasAfterDelay()
    {
        yield return new WaitForSecondsRealtime(menuOpenInputCooldown);
        isOpeningCanvas = false;
    }

    //現在の所持金を表示するメソッド
    /// <summary>
    private void SetCoinText()
    {
        // 現在の所持金を取得
        int currentMoney = playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerMoney);
        // 所持金をテキストに設定(金色で表示)
        uiRefs.CoinNumberText.text = $"<color=#C6A34C>{currentMoney}</color>";
    }

    /// <summary>
    /// GameManagerから会話状態の変更通知を受け取る
    /// </summary>
    private void HandleTalkingStateChanged(bool talkState)
    {
        isTalking = talkState;
    }
}

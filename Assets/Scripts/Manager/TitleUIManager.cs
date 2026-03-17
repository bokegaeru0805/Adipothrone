using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class TitleUIManager : MonoBehaviour, IPanelStackManager
{
    public static TitleUIManager instance { get; private set; }

    [SerializeField, Tooltip("セーブファイルがないときの最初のボタン")]
    private GameObject GameStartfirstSelected;

    [SerializeField, Tooltip("通常時の最初のボタン")]
    private GameObject firstSelected;

    [Header("強調表示するUI")]
    [SerializeField, Tooltip("決定キーのUIオブジェクト")]
    private RectTransform confirmButtonUI;

    [Header("バージョン表示")]
    [SerializeField, Tooltip("バージョン表示のテキストオブジェクト")]
    private TextMeshProUGUI versionText;

    [Header("デモ版設定")]
    [SerializeField, Tooltip("デモ版であることを示すUIオブジェクト")]
    private GameObject demoVersionUI;

    // [SerializeField, Tooltip("キャンセルキーのUIオブジェクト")]
    // private RectTransform cancelButtonUI;
    private GameObject lastSelected; //最後に選ばれていたボタンを保存する変数
    private Stack<GameObject> panelStack = new Stack<GameObject>();

    // アニメーションを制御するためのTween変数
    private Tween confirmTween;

    // private Tween cancelTween;

    // UIの初期スケールを保存するための変数を追加
    private Vector3 initialConfirmScale;

    // private Vector3 initialCancelScale;

    private void Awake()
    {
        instance = this;
        panelStack = new Stack<GameObject>();

        // Awakeで、アクティブなManagerとして自身を登録
        SaveLoadManager.RegisterActiveManager(this);

        //アニメーションを開始する前に、UIの初期スケールを記憶しておく
        if (confirmButtonUI != null)
        {
            initialConfirmScale = confirmButtonUI.localScale;
        }

        // if (cancelButtonUI != null)
        // {
        //     initialCancelScale = cancelButtonUI.localScale;
        // }
    }

    private void Start()
    {
        bool isExistSaveFile = true;
        if (SaveLoadManager.instance != null)
        {
            // セーブファイルが少なくとも1つ存在するかどうかを取得 (プレイ時間が0より大きいスロットが1つでもあればtrue)
            isExistSaveFile = SaveLoadManager.FileSlotInfos.Values.Any(slotInfo =>
                slotInfo != null && slotInfo.playTime > 0f
            );

            //BGMとSEの音量を適用
            //シーンが変わるCRIWAREの仕様により、カテゴリの音量がリセットされてしまう
            //そのため、再度音量を適用する必要がある
            SaveLoadManager.instance.ApplyAudioSettings();
        }
        else
        {
            Debug.LogWarning("SaveLoadManagerが存在しません");
        }

        if (isExistSaveFile)
        {
            if (firstSelected != null)
            {
                EventSystem.current.SetSelectedGameObject(firstSelected);
            }
            else
            {
                Debug.LogWarning("タイトル画面で、最初に選ばれるボタンが設定されていません");
            }
        }
        else
        {
            if (firstSelected != null)
            {
                EventSystem.current.SetSelectedGameObject(GameStartfirstSelected);
            }
            else
            {
                Debug.LogWarning(
                    "タイトル画面で、セーブファイルがないときに最初に選ばれるボタンが設定されていません"
                );
            }
        }

        if (versionText != null)
        {
            //バージョン表示を更新
            versionText.text = $"Ver.{Application.version}";
        }

        if (demoVersionUI != null)
        {
            if (demoVersionUI != null)
            {
                demoVersionUI.SetActive(DebugMenuManager.isDebugModeUnlocked);
            }
        }

        // 最初にボタンの強調アニメーションを開始
        StartButtonEmphasis();
    }

    private void OnDestroy()
    {
        // アニメーションを停止
        StopButtonEmphasis();

        if (instance == this)
        {
            SaveLoadManager.UnregisterActiveManager(this);
            instance = null;
        }
    }

    private void Update()
    {
        if (InputManager.instance.UISelectNo() && !SaveLoadManager.isDataPrompting)
        {
            CloseTopPanel();
        }

        // UI上の選択が消えたら元に戻す
        if (EventSystem.current.currentSelectedGameObject == null && lastSelected != null)
        {
            EventSystem.current.SetSelectedGameObject(lastSelected);
        }
        else
        {
            // 選ばれているオブジェクトを記憶
            lastSelected = EventSystem.current.currentSelectedGameObject;
        }
    }

    public void OpenPanel(GameObject panel, int Stage)
    {
        //パネルを開くので、ボタンの強調を停止
        StopButtonEmphasis();

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
            panel.SetActive(true); //パネルを表示する
            panel.GetComponent<IPanelActive>().SelectFirstButton(); //最初の選択ボタンを指定する
            panelStack.Push(panel);
        }
    }

    public void CloseTopPanel()
    {
        if (panelStack.Count > 0)
        {
            GameObject top = panelStack.Pop();
            top.SetActive(false); //パネルを非表示にする
            if (panelStack.Count > 0)
            {
                top = panelStack.Peek();
                top.GetComponent<IPanelActive>().SelectFirstButton(); //最初の選択ボタンを指定する
            }
            else
            {
                EventSystem.current.SetSelectedGameObject(firstSelected);
                //メインメニューに戻ったので、ボタンの強調を再開
                StartButtonEmphasis();
            }
        }
    }

    /// <summary>
    /// ボタンUIの強調アニメーションを開始します。
    /// </summary>
    private void StartButtonEmphasis()
    {
        // 念のため既存のアニメーションは停止
        StopButtonEmphasis();

        // 決定キーのUIを拡縮させる
        if (confirmButtonUI != null)
        {
            //初期スケールに1.1を掛け合わせることで、元の比率を保ったまま拡大
            confirmTween = confirmButtonUI
                .DOScale(initialConfirmScale * 1.1f, 0.4f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        // // キャンセルキーのUIを拡縮させる
        // if (cancelButtonUI != null)
        // {
        //     cancelTween = cancelButtonUI
        //         .DOScale(initialCancelScale * 1.1f, 0.4f)
        //         .SetLoops(-1, LoopType.Yoyo)
        //         .SetEase(Ease.InOutSine);
        // }
    }

    /// <summary>
    /// ボタンUIの強調アニメーションを停止し、元のスケールに戻します。
    /// </summary>
    private void StopButtonEmphasis()
    {
        // 実行中のTweenをKillで停止
        confirmTween?.Kill();
        // cancelTween?.Kill();

        if (confirmButtonUI != null)
        {
            // 保存しておいた初期スケールに戻す
            confirmButtonUI.localScale = initialConfirmScale;
        }
        // if (cancelButtonUI != null)
        // {
        //     // 保存しておいた初期スケールに戻す
        //     cancelButtonUI.localScale = initialCancelScale;
        // }
    }

    /// <summary>
    /// デバッグモードの解放状態が変更されたときに呼び出され、
    /// デモ版（デバッグ用）UIの表示/非表示を即座に更新します。
    /// </summary>
    public void UpdateDebugUI()
    {
        if (demoVersionUI != null)
        {
            demoVersionUI.SetActive(DebugMenuManager.isDebugModeUnlocked);
        }
    }
}

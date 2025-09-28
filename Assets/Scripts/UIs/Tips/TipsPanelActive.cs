using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// メニュー画面の「ヒント」パネルの挙動を制御するクラスです。
/// アンロック済みのヒントをリスト表示し、ページめくり機能を提供します。
/// </summary>
public class TipsPanelActive : MonoBehaviour, IPanelActive
{
    [Header("ヒントリスト関連")]
    [SerializeField, Tooltip("ヒントを選択するためのボタンの配列")]
    private GameObject[] tipsButton;

    [SerializeField, Tooltip("ヒントの情報が格納されたデータベース")]
    private TipsInfoDatabase tipsinfoDatabase;

    [Header("ヒント詳細表示エリア")]
    [SerializeField, Tooltip("ヒントのタイトルを表示するテキスト")]
    private TextMeshProUGUI tipsPanelTitle;

    [SerializeField, Tooltip("ヒントの画像を表示するImageコンポーネント")]
    private Image tipsPanelImage;

    [SerializeField, Tooltip("ヒント画像の背景")]
    private Image tipsPanelImageBackground;

    [Header("テキスト表示エリア")]
    [SerializeField, Tooltip("画像がある場合に使用するテキスト表示エリア")]
    private TextMeshProUGUI tipsPanelTextWithImage;

    [SerializeField, Tooltip("画像がない場合に使用するテキスト表示エリア")]
    private TextMeshProUGUI tipsPanelTextWithoutImage;

    /// <summary>
    /// アンロック済みヒントの表示情報とセーブデータをまとめて保持するための内部クラス
    /// </summary>
    private class UnlockedTip
    {
        public TipsInfoData Info { get; set; }
        public TipsDataEntry Entry { get; set; }
    }

    /// <summary>
    /// ページめくりがどの入力で行われたかを判別するための種類
    /// </summary>
    private enum PageChangeType
    {
        Horizontal, // 左右キーによる入力
        VerticalUp, // 上キーによる入力
        VerticalDown // 下キーによる入力
        ,
    }

    private InputManager inputManager; // InputManagerのインスタンス
    private List<UnlockedTip> allUnlockedTips; // 全てのアンロック済みヒントのリスト
    private List<TipsButtonHelper> buttonHelpers; // ボタンのヘルパースクリプトをキャッシュ

    //インデックス管理とUI要素のキャッシュ
    private int currentTopTipIndex = 0; // 現在表示している一番上のヒントの、リスト全体でのインデックス
    private int tipsPerPage; // 1ページあたりのヒント数
    private int totalPages; // 全体のページ数
    private GameObject topButton; // 一番上のヒントボタン
    private GameObject bottomButton; // 一番下のヒントボタン
    private GameObject previousSelected; // 1フレーム前の選択状態を記憶する変数

    private void Awake()
    {
        if (tipsinfoDatabase == null)
        {
            Debug.LogError("ヒントパネルにTipsDatabaseが設定されていません");
            return;
        }
        if (tipsButton == null || tipsButton.Length == 0)
        {
            Debug.LogError("ヒントパネルにヒントボタンが設定されていません");
            return;
        }
        else
        {
            // ヒントボタンの配列から一番上と一番下のボタンを取得
            topButton = tipsButton[0];
            bottomButton = tipsButton[tipsButton.Length - 1];
        }
        // TextMeshProUGUIのnullチェックを更新
        if (
            tipsPanelTitle == null
            || tipsPanelImage == null
            || tipsPanelImageBackground == null
            || tipsPanelTextWithImage == null
            || tipsPanelTextWithoutImage == null
        )
        {
            Debug.LogError("ヒントパネルの必須UIコンポーネントが設定されていません");
            return;
        }

        // 変数の初期化とコンポーネントの事前キャッシュ ▼▼▼
        tipsPerPage = tipsButton.Length;
        topButton = tipsButton[0];
        bottomButton = tipsButton[tipsPerPage - 1];

        // TipsButtonHelperを事前にキャッシュして、Update中のGetComponent呼び出しをなくす
        buttonHelpers = new List<TipsButtonHelper>();
        foreach (var button in tipsButton)
        {
            TipsButtonHelper helper = button.GetComponent<TipsButtonHelper>();
            if (helper == null)
            {
                helper = button.AddComponent<TipsButtonHelper>();
            }
            buttonHelpers.Add(helper);
        }
    }

    private void Start()
    {
        inputManager = InputManager.instance;
        if (inputManager == null)
        {
            Debug.LogError("InputManagerが見つかりません。TipsPanelが正しく動作しません。");
            return;
        }
    }

    private void OnEnable()
    {
        // パネルを開くたびに最初のページから表示します
        currentTopTipIndex = 0;

        SelectFirstButton(); //最初のボタンを選択
    }

    private void Update()
    {
        if (inputManager == null)
        {
            return; // InputManagerがなければ何もしない
        }

        // 現在選択されているGameObjectを取得
        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        if (selectedObject == null)
            return;

        // 選択が前回と変わったフレームでは、状態を更新するだけで以降の処理は行わない
        if (selectedObject != previousSelected)
        {
            previousSelected = selectedObject;
            return; // 以降の処理は行わない
        }

        // --- ページめくり入力の判定 ---
        if (inputManager.UIMoveRight())
        {
            ChangePage(1, PageChangeType.Horizontal);
            return; // 1フレームに複数の入力を受け付けないようにする
        }

        if (inputManager.UIMoveLeft())
        {
            ChangePage(-1, PageChangeType.Horizontal);
            return;
        }

        // --- 上下キーでのページ循環 ---
        // 現在のページで表示されているアイテム数を計算
        int visibleItemCount = Mathf.Min(tipsPerPage, allUnlockedTips.Count - currentTopTipIndex);
        if (visibleItemCount <= 0)
            return; // 表示アイテムがない場合は何もしない

        // 表示されている最後のボタンを動的に取得
        GameObject lastVisibleButton = tipsButton[visibleItemCount - 1];

        if (inputManager.UIMoveDown() && selectedObject == lastVisibleButton)
        {
            ChangePage(1, PageChangeType.VerticalDown);
        }
        else if (inputManager.UIMoveUp() && selectedObject == topButton)
        {
            ChangePage(-1, PageChangeType.VerticalUp);
        }
    }

    /// <summary>
    /// IPanelActiveインターフェース経由で呼ばれる、パネルの初期化メソッド
    /// </summary>
    public void SelectFirstButton()
    {
        LoadAllUnlockedTips(); // ① 全てのヒント情報を読み込む
        UpdateTipsPage(); // ② 読み込んだ情報をもとに最初のページを表示する
    }

    /// <summary>
    /// セーブデータから全てのアンロック済みヒントを読み込み、リストにキャッシュします。
    /// </summary>
    private void LoadAllUnlockedTips()
    {
        // ヒントリストを初期化
        allUnlockedTips = new List<UnlockedTip>();
        var tipsData = GameManager.instance.savedata.TipsData;

        if (tipsData == null)
        {
            Debug.LogError("TipsDataがnullです");
            return;
        }

        // セーブデータから全てのアンロック済みヒント情報を取得し、リストに追加
        foreach (var entry in tipsData.unlockedTips)
        {
            TipsName tipsID = (TipsName)entry.TipsID;
            TipsInfoData tipsInfo = tipsinfoDatabase.Get(tipsID);

            if (tipsInfo != null)
            {
                // ヒント情報とセーブデータエントリをペアでリストに追加
                allUnlockedTips.Add(new UnlockedTip { Info = tipsInfo, Entry = entry });
            }
            else
            {
                Debug.LogError($"ヒントID {tipsID} の情報が見つかりません");
            }
        }

        // 総ページ数を計算
        if (allUnlockedTips.Count == 0)
        {
            totalPages = 1; // ヒントがなくても1ページとして扱います
        }
        else
        {
            // (ヒントの総数 - 1) / 1ページあたりの数 + 1 で、必要なページ数が計算できます
            totalPages = (allUnlockedTips.Count - 1) / tipsPerPage + 1;
        }
    }

    /// <summary>
    /// 現在のページ番号に基づいて、ヒントボタンの表示を更新します。
    /// </summary>
    private void UpdateTipsPage()
    {
        // 現在のページに表示すべきヒントの数を計算（最終ページで数が少なくなる場合に対応）
        int loopCount = Mathf.Min(tipsPerPage, allUnlockedTips.Count - currentTopTipIndex);

        for (int i = 0; i < tipsPerPage; i++)
        {
            GameObject currentButton = tipsButton[i];
            // 表示すべきヒントがあるボタンのみアクティブにする
            if (i < loopCount)
            {
                int tipIndex = currentTopTipIndex + i;
                UnlockedTip unlockedTip = allUnlockedTips[tipIndex];

                // ボタンのテキストを設定
                currentButton.GetComponentInChildren<TextMeshProUGUI>().text = unlockedTip
                    .Info
                    .tipsTitle;

                // キャッシュしたヘルパーを初期化
                buttonHelpers[i].Initialize(this, unlockedTip.Info, unlockedTip.Entry);
                currentButton.SetActive(true);
            }
            else
            {
                currentButton.SetActive(false);
            }
        }

        // 最初の選択可能なボタンを設定
        if (loopCount > 0)
        {
            EventSystem.current.SetSelectedGameObject(topButton);
            // ページを更新した際に、最初のボタンに対応するTips情報をすぐに表示する
            DisplayTips(allUnlockedTips[currentTopTipIndex].Info);
        }
        else
        {
            // 表示するヒントが一つもない場合は、パネルを空にする
            DisplayTips(null);
        }
    }

    //// <summary>
    /// ページを切り替える
    /// </summary>
    /// <param name="direction">1で次のページ、-1で前のページ</param>
    /// <param name="changeType">どの入力（左右 or 上下）でページがめくられたか</param>
    private void ChangePage(int direction, PageChangeType changeType)
    {
        if (totalPages <= 1)
            return;

        // ページ移動前に選択していたボタンのインデックスを記憶（左右キー操作時に使用）
        GameObject lastSelected = EventSystem.current.currentSelectedGameObject;
        int lastSelectedIndex =
            (lastSelected != null) ? System.Array.IndexOf(tipsButton, lastSelected) : -1;

        // 表示するページの先頭インデックスを更新
        currentTopTipIndex += tipsPerPage * direction;

        // --- インデックスの循環処理 ---
        if (currentTopTipIndex >= allUnlockedTips.Count)
        {
            currentTopTipIndex = 0;
        }
        else if (currentTopTipIndex < 0)
        {
            currentTopTipIndex = (totalPages - 1) * tipsPerPage;
        }

        // 表示を更新
        UpdateTipsPage();

        //入力の種類に応じて、フォーカスを合わせるボタンを制御
        switch (changeType)
        {
            // 左右キーでめくった場合：カーソル位置をできるだけ維持する
            case PageChangeType.Horizontal:
                if (lastSelectedIndex != -1)
                {
                    int newVisibleItemCount = Mathf.Min(
                        tipsPerPage,
                        allUnlockedTips.Count - currentTopTipIndex
                    );
                    if (lastSelectedIndex < newVisibleItemCount)
                    {
                        EventSystem.current.SetSelectedGameObject(tipsButton[lastSelectedIndex]);
                    }
                }
                break;

            // 下キーでめくった場合：次のページの一番上のボタンを選択する
            case PageChangeType.VerticalDown:
                EventSystem.current.SetSelectedGameObject(topButton);
                break;

            // 上キーでめくった場合：前のページで表示されている一番下のボタンを選択する
            case PageChangeType.VerticalUp:
                int visibleItemCount = Mathf.Min(
                    tipsPerPage,
                    allUnlockedTips.Count - currentTopTipIndex
                );
                GameObject lastVisibleButton = tipsButton[visibleItemCount - 1];
                EventSystem.current.SetSelectedGameObject(lastVisibleButton);
                break;
        }
    }

    /// <summary>
    /// 1つずつヒントをスクロールします（上下キー用）
    /// </summary>
    private void ChangeSelectionVertical(int direction)
    {
        if (allUnlockedTips.Count <= tipsPerPage)
            return; // ヒントが1ページに収まる場合は何もしない

        bool looped = false;

        currentTopTipIndex += direction;

        // --- リストの循環処理 ---
        if (currentTopTipIndex >= allUnlockedTips.Count)
        {
            currentTopTipIndex = 0;
            looped = true;
        }
        else if (currentTopTipIndex < 0)
        {
            currentTopTipIndex = allUnlockedTips.Count - 1;
            looped = true;
        }

        UpdateTipsPage();

        // --- ループ時のフォーカス移動 ---
        if (looped)
        {
            if (direction > 0) // 下に移動してループした場合
            {
                EventSystem.current.SetSelectedGameObject(topButton);
            }
            else // 上に移動してループした場合
            {
                int visibleItemCount = Mathf.Min(
                    tipsPerPage,
                    allUnlockedTips.Count - currentTopTipIndex
                );
                // 最後のページは表示数が少ない可能性があるので、表示されている一番下のボタンを選択
                EventSystem.current.SetSelectedGameObject(tipsButton[visibleItemCount - 1]);
            }
        }
    }

    /// <summary>
    /// 指定されたヒントの情報を右側のパネルに表示します。
    /// </summary>
    /// <param name="tipsInfo">表示するヒントの情報。nullの場合は空の状態を表示します。</param>
    public void DisplayTips(TipsInfoData tipsInfo)
    {
        // 表示するヒントがない場合（リストが空など）
        if (tipsInfo == null)
        {
            tipsPanelTitle.text = "ヒントがありません";
            tipsPanelImageBackground.gameObject.SetActive(false);
            tipsPanelTextWithImage.gameObject.SetActive(false);
            tipsPanelTextWithoutImage.gameObject.SetActive(true);
            tipsPanelTextWithoutImage.text = "新しいヒントを見つけましょう。";
            return;
        }

        // タイトルを設定
        tipsPanelTitle.text = tipsInfo.tipsTitle;

        // 画像が存在するかどうかを判定
        bool hasImage = (tipsInfo.tipsImage != null);

        // 画像の表示・非表示を切り替え
        tipsPanelImageBackground.gameObject.SetActive(hasImage);

        // テキストエリアの表示・非表示を切り替え
        tipsPanelTextWithImage.gameObject.SetActive(hasImage);
        tipsPanelTextWithoutImage.gameObject.SetActive(!hasImage);

        // 実際の画像とテキストを設定
        if (hasImage)
        {
            // 画像がある場合の処理
            tipsPanelImage.sprite = tipsInfo.tipsImage;
            tipsPanelTextWithImage.text = tipsInfo.tipsText;
        }
        else
        {
            // 画像がない場合の処理
            tipsPanelTextWithoutImage.text = tipsInfo.tipsText;
        }
    }
}

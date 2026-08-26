using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// タブの選択用スプライトを管理するクラス
/// </summary>
[System.Serializable]
public class TabSpriteSet
{
    public Sprite selected; // 選択中のスプライト
    public Sprite unselected; // 非選択のスプライト
}

/// <summary>
/// 複数のパネルをタブで切り替えて表示・管理する汎用クラス。
/// UIManagerのスタックには積まず、ローカルなサブビューとしてタブを切り替えます。
/// </summary>
public class TabPanelController : MonoBehaviour, IPanelActive
{
    #region 内部参照とUI設定

    private InputManager inputManager;

    [Header("タブに対応するパネルリスト（順番が重要）")]
    [SerializeField]
    private List<GameObject> tabPanels;

    [Header("タブの上部の選択UI")]
    [SerializeField]
    private List<Image> tabButtons;

    [Header("全タブ共通のスプライト設定")]
    [SerializeField]
    private TabSpriteSet commonTabSprites;

    #endregion

    #region 状態変数とオプション

    [Header("有効化時に最初のタブに戻すか")]
    [SerializeField]
    private bool resetOnEnable = false;

    private int currentTabIndex = 0; // 現在選択されているタブのインデックス
    public int CurrentTabIndex => currentTabIndex;
    public event Action<int> OnTabChanged; // タブが切り替わったときに発火するイベント（引数はタブのインデックス）
    private bool isInitialized = false; // 初回起動が完了したかどうかのフラグ
    #endregion

    #region Unity Lifecycle Methods

    private void Awake()
    {
        // 必要な参照が設定されているかチェック
        if (tabPanels == null || tabButtons.Count == 0)
        {
            Debug.LogError("TabPanelController: パネルまたはボタンのリストが設定されていません。");
            return;
        }

        if (
            commonTabSprites == null
            || commonTabSprites.selected == null
            || commonTabSprites.unselected == null
        )
        {
            Debug.LogError("TabPanelController: 共通タブスプライトが設定されていません。");
            return;
        }

        // 初期化処理：一度全てのタブを非表示にして整合性を保つ
        ClearTab();
    }

    private void Start()
    {
        inputManager = InputManager.instance;
        if (inputManager == null)
        {
            Debug.LogError(
                "InputManagerが設定されていません。TabPanelControllerが正しく動作しません。"
            );
        }

        // 初回のイベント発火をStartに遅らせる
        isInitialized = true;
        
        // 全てのスクリプトの準備（Awake/OnEnable）が終わったこのタイミングで
        // 初回のUI更新とイベント発火(OnTabChanged)を確実に行う
        SetTab(currentTabIndex);
    }

    private void OnEnable()
    {
        // オプションに応じて、表示時にタブを0番に戻すか、前回の続きを表示するか決定
        if (resetOnEnable)
        {
            currentTabIndex = 0;
        }

        // 最初の1回目はStart()で確実にイベントを発行するため、ここでは実行しない。
        // メニューを閉じて「2回目以降」開かれた時だけここで更新する。
        if (isInitialized)
        {
            // タブの表示を更新する（SetTabは内部でOnTabChangedも発火させる）
            SetTab(currentTabIndex);
        }
    }

    private void OnDisable()
    {
        // 非表示時はすべてクリアしておく
        ClearTab();
    }

    private void Update()
    {
        if (inputManager == null)
            return;

        // 入力検知によるタブ切り替え
        if (inputManager.GetTabRight())
        {
            ChangeTab(1);
        }
        else if (inputManager.GetTabLeft())
        {
            ChangeTab(-1);
        }
    }

    #endregion

    #region タブ切り替えロジック

    /// <summary>
    /// 現在のタブから指定した方向へ切り替える
    /// </summary>
    /// <param name="direction">1 または -1</param>
    private void ChangeTab(int direction)
    {
        // タブの総数は、設定されているボタンの数を基準にする
        int tabCount = tabButtons.Count;
        if (tabCount == 0)
            return;

        int newIndex = currentTabIndex + direction;

        // 範囲外をループさせる
        if (newIndex < 0)
        {
            newIndex = tabCount - 1;
        }
        else if (newIndex >= tabCount)
        {
            newIndex = 0;
        }

        currentTabIndex = newIndex;
        UpdatePanelVisibility();
    }

    /// <summary>
    /// 指定したインデックスのタブを強制的に表示する
    /// </summary>
    /// <param name="index">タブ番号</param>
    public void SetTab(int index)
    {
        int tabCount = tabButtons.Count;
        if (tabCount == 0)
            return;

        // インデックスの安全確認とループ処理
        if (index < 0)
        {
            index = tabCount - 1;
        }
        else if (index >= tabCount)
        {
            index = 0;
        }

        currentTabIndex = index;
        UpdatePanelVisibility();
    }

    /// <summary>
    /// 現在の currentTabIndex に基づいて表示・非表示を更新する
    /// </summary>
    private void UpdatePanelVisibility()
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            bool isSelected = (i == currentTabIndex);

            // tabPanelsが設定されている場合のみ、GameObjectのアクティブ状態を切り替える
            if (tabPanels != null && i < tabPanels.Count && tabPanels[i] != null)
            {
                // UIManagerのOpenPanelを使わず、子オブジェクトとして直接アクティブ状態を切り替える
                tabPanels[i].SetActive(isSelected);

                // ※各タブコンポーネントの OnEnable() で SelectFirstButton() が呼ばれるため、
                // ここでの手動呼び出しは不要です。
            }

            if (tabButtons[i] != null)
            {
                tabButtons[i].sprite = isSelected
                    ? commonTabSprites.selected
                    : commonTabSprites.unselected;
            }
        }

        // タブの表示が更新された後、外部に現在のタブインデックスを通知する
        OnTabChanged?.Invoke(currentTabIndex);
    }

    /// <summary>
    /// 全てのタブとボタンを非選択状態にする
    /// </summary>
    private void ClearTab()
    {
        for (int i = 0; i < tabPanels.Count; i++)
        {
            if (tabPanels[i] != null)
            {
                tabPanels[i].SetActive(false);
            }
        }

        for (int i = 0; i < tabButtons.Count; i++)
        {
            if (tabButtons[i] != null)
            {
                tabButtons[i].sprite = commonTabSprites.unselected;
            }
        }
    }

    #endregion

    #region IPanelActive 実装

    /// <summary>
    /// IPanelActiveの実装。UIManagerからこの親パネルにフォーカス要求が来た際、
    /// 現在アクティブなタブ（子パネル）へフォーカス処理を委譲します。
    /// </summary>
    public void SelectFirstButton()
    {
        if (tabPanels != null && tabPanels.Count > currentTabIndex)
        {
            GameObject activeTabObj = tabPanels[currentTabIndex];
            if (activeTabObj != null && activeTabObj.activeInHierarchy)
            {
                IPanelActive activeTab = activeTabObj.GetComponent<IPanelActive>();
                if (activeTab != null)
                {
                    activeTab.SelectFirstButton();
                }
            }
        }
    }

    #endregion
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class TabSpriteSet
{
    public Sprite selected;   // 選択中のスプライト
    public Sprite unselected; // 非選択のスプライト
}

/// <summary>
/// 複数のパネルをタブで切り替えて表示・管理する汎用クラス
/// </summary>
public class TabPanelController : MonoBehaviour
{
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

    [Header("有効化時に最初のタブに戻すか")]
    [SerializeField]
    private bool resetOnEnable = false;

    private int currentTabIndex = 0;

    private void Awake()
    {
        if (tabPanels == null || tabButtons == null)
        {
            Debug.LogError("TabPanelController: パネルまたはボタンのリストが設定されていません。");
            return;
        }

        // パネルとボタンの数が一致しているか確認
        if (tabPanels.Count != tabButtons.Count)
        {
            Debug.LogError("TabPanelController: タブパネルとボタンの数が一致しません。");
            return;
        }

        if (commonTabSprites == null || commonTabSprites.selected == null || commonTabSprites.unselected == null)
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
            Debug.LogError("InputManagerが設定されていません。TabPanelControllerが正しく動作しません。");
            return;
        }
    }

    private void OnEnable()
    {
        // オプションに応じて、表示時にタブを0番に戻すか、前回の続きを表示するか決定
        if (resetOnEnable)
        {
            currentTabIndex = 0;
        }

        // 現在のインデックスでタブを描画
        SetTab(currentTabIndex);
    }

    private void OnDisable()
    {
        // 非表示時はすべてクリアしておく
        ClearTab();
    }

    private void Update()
    {
        if (inputManager == null) return;

        // 入力検知
        if (inputManager.GetTabRight())
        {
            ChangeTab(1);
        }
        else if (inputManager.GetTabLeft())
        {
            ChangeTab(-1);
        }
    }

    /// <summary>
    /// 現在のタブから指定した方向へ切り替える
    /// </summary>
    /// <param name="direction">1 または -1</param>
    private void ChangeTab(int direction)
    {
        int newIndex = currentTabIndex + direction;

        // 範囲外をループさせる
        if (newIndex < 0)
        {
            newIndex = tabPanels.Count - 1;
        }
        else if (newIndex >= tabPanels.Count)
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
        // インデックスの安全確認とループ処理
        if (index < 0)
        {
            index = tabPanels.Count - 1;
        }
        else if (index >= tabPanels.Count)
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
        for (int i = 0; i < tabPanels.Count; i++)
        {
            bool isSelected = (i == currentTabIndex);

            if (tabPanels[i] != null)
            {
                tabPanels[i].SetActive(isSelected);
            }

            if (tabButtons[i] != null)
            {
                tabButtons[i].sprite = isSelected ? commonTabSprites.selected : commonTabSprites.unselected;
            }
        }
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
            
            if (tabButtons[i] != null)
            {
                tabButtons[i].sprite = commonTabSprites.unselected;
            }
        }
    }
}
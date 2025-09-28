using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// タブボタン用の選択・非選択スプライトのペアを管理するクラス
/// </summary>
[System.Serializable]
public class TabSpriteSet
{
    public Sprite selected; // 選択中のスプライト
    public Sprite unselected; // 非選択のスプライト
}

public class GuidePanelActive : MonoBehaviour
{
    private InputManager inputManager;

    [Header("タブに対応するパネルリスト（順番が重要）")]
    [SerializeField]
    private List<GameObject> tabPanels;

    [Header("タブの上部の選択UI")]
    [SerializeField]
    private List<Image> tabButtons;

    [Header("タブごとのスプライト設定")]
    [SerializeField]
    private List<TabSpriteSet> tabSprites; // 各タブのスプライトをリストで管理

    private int currentTabIndex = 0;

    private void Awake()
    {
        if (tabPanels == null || tabButtons == null)
        {
            Debug.LogError("GuidePanelのタブのパネルまたはボタンが設定されていません。");
            return;
        }

        // パネル、ボタン、スプライトの数が一致しているか確認
        if (tabPanels.Count != tabButtons.Count || tabPanels.Count != tabSprites.Count)
        {
            Debug.LogError("GuidePanelのタブ、パネル、スプライトの数が一致しません。");
            return;
        }

        for (int i = 0; i < tabPanels.Count; i++)
        {
            // 初期状態では全てのパネルを非表示にする
            ClearTab();
        }
    }

    private void Start()
    {
        inputManager = InputManager.instance;
        if (inputManager == null)
        {
            Debug.LogError("InputManagerが設定されていません。GuidePanelが正しく動作しません。");
            return;
        }
    }

    private void OnEnable()
    {
        //タブの状態を初期化
        SetTab(currentTabIndex);
    }

    private void OnDisable()
    {
        //タブを全て非表示化
        ClearTab();
    }

    private void Update()
    {
        if (inputManager == null)
        {
            Debug.LogError("InputManagerが設定されていません。GuidePanelが正しく動作しません。");
            return;
        }

        if (inputManager.GetTabRight())
        {
            ChangeTab(1);
        }
        else if (inputManager.GetTabLeft())
        {
            ChangeTab(-1);
        }
    }

    private void ChangeTab(int direction)
    {
        int newIndex = currentTabIndex + direction;

        // 範囲外をループさせる（必要ならClampでも可）
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

    private void SetTab(int index)
    {
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

    private void UpdatePanelVisibility()
    {
        for (int i = 0; i < tabPanels.Count; i++)
        {
            if (i == currentTabIndex)
            {
                tabButtons[i].sprite = tabSprites[i].selected; // 選択中のタブの画像を変更
                tabPanels[i].SetActive(true); // 選択中のタブのパネルを表示
            }
            else
            {
                tabButtons[i].sprite = tabSprites[i].unselected; // 選択されていないタブの画像を変更
                tabPanels[i].SetActive(false); // 選択されていないタブのパネルを非表示
            }
        }
    }

    private void ClearTab()
    {
        for (int i = 0; i < tabPanels.Count; i++)
        {
            tabPanels[i].SetActive(false);
            tabButtons[i].sprite = tabSprites[i].unselected; // 選択されていないタブの画像を変更
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// スキルメニュー全体を統括するマネージャークラス
/// 1つのボタンでもフォーカスが逃げないよう、ナビゲーションを厳密に「自分自身」へ拘束します。
/// </summary>
public class SkillMenuManager : MonoBehaviour, IPanelActive
{
    [Header("UI連携の参照")]
    [SerializeField]
    private SkillDetailView detailView;

    [SerializeField]
    private TabPanelController tabPanelController;

    [Header("リスト生成先のコンテナ")]
    [SerializeField]
    private Transform skillListContainer;

    [SerializeField]
    private GameObject skillButtonPrefab;

    private List<GameObject> instantiatedButtons = new List<GameObject>();
    private int currentTabIndex = 0;

    private const int ROW_COUNT = 6; // 縦6行
    private const int COLUMN_COUNT = 3; // 横3列
    private const int MAX_SKILLS = 18;

    private void OnEnable()
    {
        if (tabPanelController != null)
            tabPanelController.OnTabChanged += ReloadList;

        if (detailView != null)
            detailView.UpdateAvailablePoints();

        ReloadList(currentTabIndex);
    }

    private void OnDisable()
    {
        if (tabPanelController != null)
            tabPanelController.OnTabChanged -= ReloadList;
    }

    public void ReloadList(int tabIndex)
    {
        currentTabIndex = tabIndex;

        foreach (var btn in instantiatedButtons)
        {
            Destroy(btn);
        }
        instantiatedButtons.Clear();

        if (SkillManager.instance == null || SkillManager.instance.skillDatabase == null)
            return;

        bool isFirstButtonSet = false;
        SkillCategory targetCategory = (SkillCategory)currentTabIndex;

        int count = 0;
        foreach (var skill in SkillManager.instance.skillDatabase.skills)
        {
            if (skill == null)
                continue;
            if (skill.category == targetCategory)
            {
                if (count >= MAX_SKILLS)
                    break;
                CreateSkillButton(skill, ref isFirstButtonSet);
                count++;
            }
        }

        // 重要：ボタンが1つでもナビゲーションを構築する
        SetupNavigation();

        if (instantiatedButtons.Count == 0 && detailView != null)
        {
            detailView.gameObject.SetActive(false);
        }
    }

    private void CreateSkillButton(SkillData skill, ref bool isFirstButtonSet)
    {
        GameObject obj = Instantiate(skillButtonPrefab, skillListContainer, false);
        obj.transform.localScale = Vector3.one;
        instantiatedButtons.Add(obj);

        var buttonUI = obj.GetComponent<SkillButtonUI>();
        if (buttonUI != null)
        {
            buttonUI.Setup(skill, this);
        }

        if (!isFirstButtonSet)
        {
            EventSystem.current.SetSelectedGameObject(obj);
            isFirstButtonSet = true;
        }
    }

    /// <summary>
    /// ナビゲーションをボタン内のみに限定し、外部へ逃がさないようにします。
    /// ボタンが1つの場合、上下左右すべて「自分自身」を指すように設定し、
    /// 意図しないEventSystemの挙動を防ぎます。
    /// </summary>
    private void SetupNavigation()
    {
        int totalCount = instantiatedButtons.Count;

        // 0個のときは設定できないのでリターン
        if (totalCount == 0)
            return;

        for (int i = 0; i < totalCount; i++)
        {
            Selectable current = instantiatedButtons[i].GetComponent<Selectable>();
            if (current == null)
                continue;

            Navigation nav = new Navigation();
            nav.mode = Navigation.Mode.Explicit; // 手動定義のみを有効にする

            int currentRow = i % ROW_COUNT;
            int currentCol = i / ROW_COUNT;

            // --- 上下の移動先決定 ---
            int nextUp = i - 1;
            if (currentRow == 0)
            {
                // 先頭なら末尾へワープ（要素が1つの場合は i = 0 なので 0 になる）
                nextUp = Mathf.Min((currentCol + 1) * ROW_COUNT - 1, totalCount - 1);
            }

            int nextDown = i + 1;
            if (currentRow == ROW_COUNT - 1 || nextDown >= totalCount)
            {
                // 末尾なら先頭へワープ
                nextDown = currentCol * ROW_COUNT;
            }

            // --- 左右の移動先決定 ---
            int nextRight = i + ROW_COUNT;
            if (nextRight >= totalCount)
            {
                // 右端なら左端（同じ行）へ
                nextRight = currentRow;
                if (nextRight >= totalCount)
                    nextRight = 0; // それでもなければ0番へ
            }

            int nextLeft = i - ROW_COUNT;
            if (nextLeft < 0)
            {
                // 左端なら右端へ
                int maxCol = (totalCount - 1) / ROW_COUNT;
                nextLeft = currentRow + (maxCol * ROW_COUNT);
                if (nextLeft >= totalCount)
                    nextLeft -= ROW_COUNT;
                if (nextLeft < 0)
                    nextLeft = 0; // 最終防衛ライン
            }

            // 全ての方向をリスト内のボタンに強制的に紐付ける
            // totalCount が 1 の場合、これら全てが instantiatedButtons[0] になります
            nav.selectOnUp = instantiatedButtons[nextUp].GetComponent<Selectable>();
            nav.selectOnDown = instantiatedButtons[nextDown].GetComponent<Selectable>();
            nav.selectOnLeft = instantiatedButtons[nextLeft].GetComponent<Selectable>();
            nav.selectOnRight = instantiatedButtons[nextRight].GetComponent<Selectable>();

            current.navigation = nav;
        }
    }

    public void UpdateDetailView(SkillData skill)
    {
        if (detailView != null)
        {
            if (!detailView.gameObject.activeSelf)
                detailView.gameObject.SetActive(true);
            detailView.UpdateView(skill);
        }
    }

    public void SelectFirstButton()
    {
        if (instantiatedButtons.Count > 0)
        {
            EventSystem.current.SetSelectedGameObject(instantiatedButtons[0]);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// スキルメニュー全体を統括するマネージャークラス
/// パネル展開時のNEWフラグ状態を記憶し、表示の一貫性を保ちます。
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

    // ▼ 追加：パネルを開いた時点でNEWだったスキルIDを保持するリスト
    private HashSet<int> newSkillsAtOpen = new HashSet<int>();

    private const int ROW_COUNT = 6;
    private const int COLUMN_COUNT = 3;
    private const int MAX_SKILLS = 18;

    private void OnEnable()
    {
        if (tabPanelController != null)
            tabPanelController.OnTabChanged += ReloadList;

        if (detailView != null)
            detailView.UpdateAvailablePoints();

        // ▼ 追加：パネルを開いた瞬間に、NEWフラグが立っているスキルを記憶する
        RecordNewSkills();

        ReloadList(currentTabIndex);
    }

    private void OnDisable()
    {
        if (tabPanelController != null)
            tabPanelController.OnTabChanged -= ReloadList;

        // ※ 前回の一括削除(ClearAllNewFlags)は廃止しました
    }

    /// <summary>
    /// メニューを開いた時点でのNEWスキルの状態をスナップショットとして記憶します。
    /// </summary>
    private void RecordNewSkills()
    {
        newSkillsAtOpen.Clear();
        if (GameManager.instance == null || GameManager.instance.savedata == null)
            return;

        foreach (var skill in GameManager.instance.savedata.SkillData.knownSkills)
        {
            if (skill.isNew)
            {
                newSkillsAtOpen.Add(skill.skillID);
            }
        }
    }

    /// <summary>
    /// 指定したスキルが、このパネルを開いた時点でNEWだったかどうかを返します。
    /// ボタンのUI表示（RefreshUI）から参照されます。
    /// </summary>
    public bool WasNewOnOpen(int skillID)
    {
        return newSkillsAtOpen.Contains(skillID);
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

    private void SetupNavigation()
    {
        int totalCount = instantiatedButtons.Count;

        if (totalCount == 0)
            return;

        for (int i = 0; i < totalCount; i++)
        {
            Selectable current = instantiatedButtons[i].GetComponent<Selectable>();
            if (current == null)
                continue;

            Navigation nav = new Navigation();
            nav.mode = Navigation.Mode.Explicit;

            int currentRow = i % ROW_COUNT;
            int currentCol = i / ROW_COUNT;

            int nextUp = i - 1;
            if (currentRow == 0)
            {
                nextUp = Mathf.Min((currentCol + 1) * ROW_COUNT - 1, totalCount - 1);
            }

            int nextDown = i + 1;
            if (currentRow == ROW_COUNT - 1 || nextDown >= totalCount)
            {
                nextDown = currentCol * ROW_COUNT;
            }

            int nextRight = i + ROW_COUNT;
            if (nextRight >= totalCount)
            {
                nextRight = currentRow;
                if (nextRight >= totalCount)
                    nextRight = 0;
            }

            int nextLeft = i - ROW_COUNT;
            if (nextLeft < 0)
            {
                int maxCol = (totalCount - 1) / ROW_COUNT;
                nextLeft = currentRow + (maxCol * ROW_COUNT);
                if (nextLeft >= totalCount)
                    nextLeft -= ROW_COUNT;
                if (nextLeft < 0)
                    nextLeft = 0;
            }

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

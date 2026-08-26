using System.Collections.Generic;
using UnityEngine;

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

    private int currentTabIndex = 0;
    private SkillName selectedSkillID = SkillName.None;
    private SkillListView skillListView;

    // ▼ 追加：パネルを開いた時点でNEWだったスキルIDを保持するリスト
    private HashSet<int> newSkillsAtOpen = new HashSet<int>();

    private const int ROW_COUNT = 6;
    private const int MAX_SKILLS = 18;

    private static readonly SkillCategory[] TabCategories =
    {
        SkillCategory.Mobility,
        SkillCategory.Exploration,
        SkillCategory.Attack,
        SkillCategory.Defense,
        SkillCategory.Reward,
        SkillCategory.Tool,
        SkillCategory.Special,
    };

    private void Awake()
    {
        skillListView = new SkillListView(
            skillListContainer,
            skillButtonPrefab,
            ROW_COUNT,
            MAX_SKILLS
        );
    }

    private void OnEnable()
    {
        if (tabPanelController != null)
            tabPanelController.OnTabChanged += ReloadList;

        if (SkillManager.instance != null)
            SkillManager.instance.OnSkillStateChanged += HandleSkillStateChanged;

        if (detailView != null)
            detailView.UpdateAvailablePoints();

        // ▼ 追加：パネルを開いた瞬間に、NEWフラグが立っているスキルを記憶する
        RecordNewSkills();

        int activeTabIndex =
            tabPanelController != null ? tabPanelController.CurrentTabIndex : currentTabIndex;
        ReloadList(activeTabIndex);
    }

    private void OnDisable()
    {
        if (tabPanelController != null)
            tabPanelController.OnTabChanged -= ReloadList;

        if (SkillManager.instance != null)
            SkillManager.instance.OnSkillStateChanged -= HandleSkillStateChanged;

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

        if (SkillManager.instance == null || SkillManager.instance.skillDatabase == null)
            return;

        if (currentTabIndex < 0 || currentTabIndex >= TabCategories.Length)
        {
            Debug.LogWarning($"SkillMenuManager: 未定義のタブインデックスです: {currentTabIndex}");
            return;
        }

        SkillCategory targetCategory = TabCategories[currentTabIndex];
        skillListView.ShowSkills(
            SkillManager.instance.skillDatabase.skills,
            targetCategory,
            selectedSkillID,
            CreateUIState,
            HandleSkillSelected,
            HandleSkillSubmitted
        );

        if (skillListView.Count == 0 && detailView != null)
        {
            detailView.gameObject.SetActive(false);
        }
        else if (skillListView.Count > 0)
        {
            if (!skillListView.ContainsSkill(selectedSkillID))
                HandleSkillSelected(skillListView.FirstSkillID);
            else
                RefreshSelectedDetail();
        }
    }

    private SkillUIState CreateUIState(SkillData skill)
    {
        if (skill == null || SkillManager.instance == null)
            return default;

        int id = EnumIDUtility.ToID(skill.skillID);
        return new SkillUIState(
            skill,
            SkillManager.instance.IsSkillUnlocked(skill.skillID),
            SkillManager.instance.IsSkillActive(skill.skillID),
            newSkillsAtOpen.Contains(id),
            SkillManager.instance.GetSkillLevel(skill.skillID)
        );
    }

    public void UpdateDetailView(SkillData skill)
    {
        if (skill != null)
            HandleSkillSelected(skill.skillID);
    }

    private void HandleSkillSelected(SkillName skillID)
    {
        selectedSkillID = skillID;
        SkillManager.instance?.MarkSkillAsSeen(skillID);
        RefreshSelectedDetail();
    }

    private void HandleSkillSubmitted(SkillName skillID)
    {
        if (SkillManager.instance == null)
        {
            Debug.LogWarning($"[SkillUI診断] SkillManagerが存在しません: SkillID={skillID}", this);
            return;
        }

        bool isUnlocked = SkillManager.instance.IsSkillUnlocked(skillID);
        bool wasEquipped = SkillManager.instance.IsSkillActive(skillID);
        Debug.Log(
            $"[SkillUI診断] 着脱処理を受信: SkillID={skillID}, "
                + $"isUnlocked={isUnlocked}, isEquippedBefore={wasEquipped}",
            this
        );

        if (!isUnlocked)
            return;

        if (wasEquipped)
        {
            bool isSucceeded = SkillManager.instance.UnequipSkill(skillID);
            Debug.Log(
                $"[SkillUI診断] 解除結果: SkillID={skillID}, "
                    + $"success={isSucceeded}, "
                    + $"isEquippedAfter={SkillManager.instance.IsSkillActive(skillID)}",
                this
            );
            return;
        }

        bool isEquipped = SkillManager.instance.TryEquipSkill(skillID, out SkillEquipResult result);
        Debug.Log(
            $"[SkillUI診断] 装備結果: SkillID={skillID}, success={isEquipped}, result={result}, "
                + $"isEquippedAfter={SkillManager.instance.IsSkillActive(skillID)}",
            this
        );
        if (!isEquipped)
            detailView?.ShowEquipFailure(result);
    }

    private void RefreshSelectedDetail()
    {
        if (detailView == null || SkillManager.instance == null)
            return;

        SkillData selectedSkill = SkillManager.instance.GetSkillData(selectedSkillID);
        if (selectedSkill == null)
            return;

        if (!detailView.gameObject.activeSelf)
            detailView.gameObject.SetActive(true);
        detailView.UpdateView(CreateUIState(selectedSkill));
    }

    private void HandleSkillStateChanged()
    {
        if (detailView != null)
            detailView.UpdateAvailablePoints();

        skillListView?.Refresh(CreateUIState);
        RefreshSelectedDetail();
    }

    public void SelectFirstButton()
    {
        skillListView?.SelectFirstButton();
    }
}

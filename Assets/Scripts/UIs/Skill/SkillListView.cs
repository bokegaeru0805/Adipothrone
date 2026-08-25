using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Skillボタンの生成・再利用とナビゲーション設定を担当します。
/// </summary>
public sealed class SkillListView
{
    private readonly Transform container;
    private readonly GameObject buttonPrefab;
    private readonly int rowCount;
    private readonly int maxSkills;
    private readonly List<SkillButtonUI> buttons = new List<SkillButtonUI>();

    public SkillListView(Transform container, GameObject buttonPrefab, int rowCount, int maxSkills)
    {
        this.container = container;
        this.buttonPrefab = buttonPrefab;
        this.rowCount = Mathf.Max(1, rowCount);
        this.maxSkills = Mathf.Max(1, maxSkills);
    }

    public int Count { get; private set; }
    public SkillName FirstSkillID => Count > 0 ? buttons[0].SkillID : SkillName.None;

    public bool ContainsSkill(SkillName skillID)
    {
        for (int i = 0; i < Count; i++)
        {
            if (buttons[i].SkillID == skillID)
                return true;
        }

        return false;
    }

    public void ShowSkills(
        IReadOnlyList<SkillData> skills,
        SkillCategory category,
        SkillName selectedSkillID,
        Func<SkillData, SkillUIState> createState,
        Action<SkillName> onSelected,
        Action<SkillName> onSubmitted
    )
    {
        Count = 0;
        if (skills == null || container == null || buttonPrefab == null)
            return;

        foreach (SkillData skill in skills)
        {
            if (skill == null || skill.category != category || Count >= maxSkills)
                continue;

            SkillButtonUI button = GetOrCreateButton(Count);
            if (button == null)
                break;

            button.gameObject.SetActive(true);
            button.Setup(createState(skill), onSelected, onSubmitted);
            Count++;
        }

        for (int i = Count; i < buttons.Count; i++)
            buttons[i].gameObject.SetActive(false);

        SetupNavigation();
        SelectButton(selectedSkillID);
    }

    public void Refresh(Func<SkillData, SkillUIState> createState)
    {
        for (int i = 0; i < Count; i++)
            buttons[i].RefreshUI(createState(buttons[i].SkillData));
    }

    public void SelectFirstButton()
    {
        if (Count > 0 && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
    }

    private SkillButtonUI GetOrCreateButton(int index)
    {
        if (index < buttons.Count)
            return buttons[index];

        GameObject buttonObject = UnityEngine.Object.Instantiate(buttonPrefab, container, false);
        buttonObject.transform.localScale = Vector3.one;
        if (!buttonObject.TryGetComponent(out SkillButtonUI button))
        {
            Debug.LogError("スキルボタンPrefabにSkillButtonUIが設定されていません。", buttonObject);
            UnityEngine.Object.Destroy(buttonObject);
            return null;
        }

        buttons.Add(button);
        return button;
    }

    private void SelectButton(SkillName selectedSkillID)
    {
        if (EventSystem.current == null || Count == 0)
            return;

        for (int i = 0; i < Count; i++)
        {
            if (buttons[i].SkillID == selectedSkillID)
            {
                EventSystem.current.SetSelectedGameObject(buttons[i].gameObject);
                return;
            }
        }

        SelectFirstButton();
    }

    private void SetupNavigation()
    {
        for (int i = 0; i < Count; i++)
        {
            Selectable current = buttons[i].GetComponent<Selectable>();
            if (current == null)
                continue;

            int currentRow = i % rowCount;
            int currentColumn = i / rowCount;
            int nextUp = currentRow == 0
                ? Mathf.Min((currentColumn + 1) * rowCount - 1, Count - 1)
                : i - 1;
            int nextDown = currentRow == rowCount - 1 || i + 1 >= Count
                ? currentColumn * rowCount
                : i + 1;
            int nextRight = i + rowCount;
            if (nextRight >= Count)
                nextRight = currentRow < Count ? currentRow : 0;

            int nextLeft = i - rowCount;
            if (nextLeft < 0)
            {
                int maxColumn = (Count - 1) / rowCount;
                nextLeft = currentRow + maxColumn * rowCount;
                if (nextLeft >= Count)
                    nextLeft -= rowCount;
                nextLeft = Mathf.Max(0, nextLeft);
            }

            current.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = GetSelectable(nextUp),
                selectOnDown = GetSelectable(nextDown),
                selectOnLeft = GetSelectable(nextLeft),
                selectOnRight = GetSelectable(nextRight),
            };
        }
    }

    private Selectable GetSelectable(int index)
    {
        return index >= 0 && index < Count ? buttons[index].GetComponent<Selectable>() : null;
    }
}

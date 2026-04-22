using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// スキルリストに並ぶ個別のボタンUI
/// 解放/未解放、装備/非装備、選択/未選択の組み合わせによる6状態のスプライト切り替えに対応。
/// </summary>
public class SkillButtonUI
    : MonoBehaviour,
        ISelectHandler,
        IDeselectHandler,
        ISubmitHandler,
        IPointerClickHandler
{
    [Header("UI参照")]
    [SerializeField]
    private TextMeshProUGUI nameText;

    [SerializeField]
    private GameObject equippedMark;

    [Tooltip("新しく入手した際に表示するアイコン")]
    [SerializeField]
    private GameObject newIcon;

    [Header("ボタンスプライト設定")]
    [SerializeField]
    private Image buttonBackgroundImage;

    [Header("1. 未解放(Locked)状態")]
    [SerializeField]
    private Sprite lockedSelectedSprite; // 未解放 ＋ 選択時

    [SerializeField]
    private Sprite lockedUnselectedSprite; // 未解放 ＋ 非選択時

    [Header("2. 装備(Equipped)状態")]
    [SerializeField]
    private Sprite equippedSelectedSprite; // 装備 ＋ 選択時

    [SerializeField]
    private Sprite equippedUnselectedSprite; // 装備 ＋ 非選択時

    [Header("3. 非装備(Unequipped)状態")]
    [SerializeField]
    private Sprite unequippedSelectedSprite; // 非装備 ＋ 選択時

    [SerializeField]
    private Sprite unequippedUnselectedSprite; // 非装備 ＋ 非選択時

    [Header("コスト表示 (オブジェクト生成用)")]
    [SerializeField]
    private Transform costContainer;

    [SerializeField]
    private GameObject pointIconPrefab;

    private SkillData currentSkill;
    private SkillMenuManager menuManager;
    private bool isSelected = false;

    private List<PointIconUI> generatedCostIcons = new List<PointIconUI>();

    public void Setup(SkillData skill, SkillMenuManager manager)
    {
        currentSkill = skill;
        menuManager = manager;
        RefreshUI();
    }

    /// <summary>
    /// 解放・装備・選択の全状態を統合して、ボタンの見た目を更新します。
    /// </summary>
    public void RefreshUI()
    {
        if (currentSkill == null || SkillManager.instance == null)
            return;

        int id = EnumIDUtility.ToID(currentSkill.skillID);
        bool isUnlocked = SkillManager.instance.IsSkillUnlocked(currentSkill.skillID);
        bool isEquipped = SkillManager.instance.IsSkillActive(currentSkill.skillID);

        // --- 1. テキストとNewアイコンの更新 ---
        if (isUnlocked)
        {
            nameText.text = currentSkill.skillName;
            if (costContainer != null)
                costContainer.gameObject.SetActive(true);

            // マネージャーが記憶している「開いた時の状態」で表示を固定
            if (newIcon != null)
            {
                newIcon.SetActive(menuManager.WasNewOnOpen(id));
            }
        }
        else
        {
            nameText.text = "？？？";
            if (costContainer != null)
                costContainer.gameObject.SetActive(false);
            if (newIcon != null)
                newIcon.SetActive(false); // 未解放はNewも出さない
        }

        // --- 2. クリスタルアイコンの生成と状態更新 ---
        if (isUnlocked && costContainer != null && pointIconPrefab != null)
        {
            while (generatedCostIcons.Count < currentSkill.requiredPoints)
            {
                GameObject obj = Instantiate(pointIconPrefab, costContainer);
                obj.transform.localScale = Vector3.one;
                PointIconUI iconUI = obj.GetComponent<PointIconUI>();
                if (iconUI != null)
                    generatedCostIcons.Add(iconUI);
            }
            for (int i = 0; i < generatedCostIcons.Count; i++)
            {
                if (i < currentSkill.requiredPoints)
                {
                    generatedCostIcons[i].gameObject.SetActive(true);
                    generatedCostIcons[i].SetState(isEquipped); // 同期機能付き
                }
                else
                {
                    generatedCostIcons[i].gameObject.SetActive(false);
                }
            }
        }

        if (equippedMark != null)
            equippedMark.SetActive(isEquipped);

        // --- 3. 背景スプライトの切り替え (6状態判定) ---
        if (buttonBackgroundImage != null)
        {
            if (!isUnlocked)
            {
                // 未解放状態も選択/非選択で分ける
                buttonBackgroundImage.sprite = isSelected
                    ? lockedSelectedSprite
                    : lockedUnselectedSprite;
            }
            else if (isEquipped)
            {
                // 装備状態
                buttonBackgroundImage.sprite = isSelected
                    ? equippedSelectedSprite
                    : equippedUnselectedSprite;
            }
            else
            {
                // 非装備状態
                buttonBackgroundImage.sprite = isSelected
                    ? unequippedSelectedSprite
                    : unequippedUnselectedSprite;
            }
        }
    }

    // =======================================================
    // EventSystem インターフェース
    // =======================================================

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        menuManager.UpdateDetailView(currentSkill);

        // セーブデータ上の既読処理（表示自体は閉じられるまで維持）
        MarkAsSeen();

        RefreshUI();
    }

    private void MarkAsSeen()
    {
        if (currentSkill == null || GameManager.instance.savedata == null)
            return;
        int id = EnumIDUtility.ToID(currentSkill.skillID);
        var entry = GameManager.instance.savedata.SkillData.knownSkills.Find(s => s.skillID == id);
        if (entry != null && entry.isNew)
            entry.isNew = false;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        RefreshUI();
    }

    public void OnSubmit(BaseEventData eventData) => ToggleSkill();

    public void OnPointerClick(PointerEventData eventData) => ToggleSkill();

    private void ToggleSkill()
    {
        if (currentSkill == null)
            return;
        if (!SkillManager.instance.IsSkillUnlocked(currentSkill.skillID))
            return;

        if (SkillManager.instance.IsSkillActive(currentSkill.skillID))
        {
            SkillManager.instance.UnequipSkill(currentSkill.skillID);
        }
        else
        {
            if (!SkillManager.instance.EquipSkill(currentSkill.skillID))
                return;
        }

        RefreshUI();
        menuManager.UpdateDetailView(currentSkill);
    }
}

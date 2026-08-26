using System;
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
    private SkillUIState currentState;
    private Action<SkillName> onSelected;
    private Action<SkillName> onSubmitted;
    private bool isSelected = false;

    private SkillPointView costPointView;

    public SkillData SkillData => currentSkill;
    public SkillName SkillID => currentSkill != null ? currentSkill.skillID : SkillName.None;

    public void Setup(
        SkillUIState state,
        Action<SkillName> selectedCallback,
        Action<SkillName> submittedCallback
    )
    {
        currentSkill = state.SkillData;
        onSelected = selectedCallback;
        onSubmitted = submittedCallback;
        if (costPointView == null)
            costPointView = new SkillPointView(costContainer, pointIconPrefab);
        RefreshUI(state);
    }

    public void RefreshUI(SkillUIState state)
    {
        currentState = state;
        RefreshUI();
    }

    /// <summary>
    /// 解放・装備・選択の全状態を統合して、ボタンの見た目を更新します。
    /// </summary>
    public void RefreshUI()
    {
        if (currentSkill == null)
            return;

        bool isUnlocked = currentState.IsUnlocked;
        bool isEquipped = currentState.IsEquipped;

        // --- 1. テキストとNewアイコンの更新 ---
        if (isUnlocked)
        {
            if (nameText != null)
                nameText.text = currentSkill.skillName;
            if (costContainer != null)
                costContainer.gameObject.SetActive(true);

            // マネージャーが記憶している「開いた時の状態」で表示を固定
            if (newIcon != null)
            {
                newIcon.SetActive(currentState.IsNew);
            }
        }
        else
        {
            if (nameText != null)
                nameText.text = SkillUIText.LockedName;
            if (costContainer != null)
                costContainer.gameObject.SetActive(false);
            if (newIcon != null)
                newIcon.SetActive(false); // 未解放はNewも出さない
        }

        // --- 2. クリスタルアイコンの生成と状態更新 ---
        if (isUnlocked)
            costPointView?.SetPoints(currentState.RequiredPoints, isEquipped);

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
        if (currentSkill == null)
            return;

        isSelected = true;
        onSelected?.Invoke(currentSkill.skillID);

        RefreshUI();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        RefreshUI();
    }

    public void OnSubmit(BaseEventData eventData) => SubmitSkill();

    public void OnPointerClick(PointerEventData eventData) => SubmitSkill();

    private void SubmitSkill()
    {
        if (currentSkill == null)
        {
            Debug.LogWarning("[SkillUI診断] SubmitSkillが呼ばれましたが、currentSkillがnullです。", this);
            return;
        }

        Debug.Log(
            $"[SkillUI診断] ボタン送信: SkillID={currentSkill.skillID}, "
                + $"callbackRegistered={onSubmitted != null}",
            this
        );

        onSubmitted?.Invoke(currentSkill.skillID);
    }
}

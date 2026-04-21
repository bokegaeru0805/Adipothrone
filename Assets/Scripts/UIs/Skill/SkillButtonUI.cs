using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// スキルリストに並ぶ個別のボタンUI
/// 装備状態と選択状態に応じてスプライトを切り替え、コストをクリスタルで表現します。
/// </summary>
public class SkillButtonUI
    : MonoBehaviour,
        ISelectHandler,
        IDeselectHandler,
        ISubmitHandler,
        IPointerClickHandler
{
    [Header("テキスト参照")]
    [SerializeField]
    private TextMeshProUGUI nameText;

    // ※不要になった costText 変数は削除しました

    [Tooltip("装備中であることを示すマークや枠（任意）")]
    [SerializeField]
    private GameObject equippedMark;

    [Header("ボタンスプライト設定")]
    [Tooltip("ボタンの背景画像（スプライトを切り替える対象のImageコンポーネント）")]
    [SerializeField]
    private Image buttonBackgroundImage;

    [SerializeField]
    private Sprite lockedSprite; // 1. 未開放時のスプライト

    [SerializeField]
    private Sprite equippedSelectedSprite; // 2. 装備時 ＋ 選択時のスプライト

    [SerializeField]
    private Sprite equippedUnselectedSprite; // 3. 装備時 ＋ 未選択時のスプライト

    [SerializeField]
    private Sprite unequippedSelectedSprite; // 4. 非装備時 ＋ 選択時のスプライト

    [SerializeField]
    private Sprite unequippedUnselectedSprite; // 5. 非装備時 ＋ 未選択時のスプライト

    [Header("コスト表示 (オブジェクト生成用)")]
    [Tooltip("クリスタルを並べる親オブジェクト (HorizontalLayoutGroup推奨)")]
    [SerializeField]
    private Transform costContainer;

    [Tooltip("生成するクリスタルのプレハブ")]
    [SerializeField]
    private GameObject pointIconPrefab;

    private SkillData currentSkill;
    private SkillMenuManager menuManager;
    private bool isSelected = false; // EventSystemで現在選択されているかどうかのフラグ

    // 生成したクリスタルアイコンを保持しておくリスト
    private List<PointIconUI> generatedCostIcons = new List<PointIconUI>();

    /// <summary>
    /// マネージャーから呼ばれ、初期設定を行う
    /// </summary>
    public void Setup(SkillData skill, SkillMenuManager manager)
    {
        currentSkill = skill;
        menuManager = manager;
        RefreshUI();
    }

    /// <summary>
    /// 現在の解放状態、装備状態、選択状態を総合して、ボタンの見た目を更新する
    /// </summary>
    public void RefreshUI()
    {
        if (currentSkill == null)
            return;

        bool isUnlocked = SkillManager.instance.IsSkillUnlocked(currentSkill.skillID);
        bool isEquipped = SkillManager.instance.IsSkillActive(currentSkill.skillID);

        // --- テキストとコンテナの更新 ---
        if (isUnlocked)
        {
            nameText.text = currentSkill.skillName;

            if (costContainer != null)
                costContainer.gameObject.SetActive(true); // 解放済みの場合はクリスタルの親を表示
        }
        else
        {
            nameText.text = "？？？";

            if (costContainer != null)
                costContainer.gameObject.SetActive(false); // 未解放の場合はクリスタルの親ごと非表示
        }

        // --- コストアイコン(クリスタル)の生成と状態更新 ---
        if (isUnlocked && costContainer != null && pointIconPrefab != null)
        {
            // 足りない分だけ生成する（毎回全削除すると重いため）
            while (generatedCostIcons.Count < currentSkill.requiredPoints)
            {
                GameObject obj = Instantiate(pointIconPrefab, costContainer);
                obj.transform.localScale = Vector3.one;
                PointIconUI iconUI = obj.GetComponent<PointIconUI>();
                if (iconUI != null)
                    generatedCostIcons.Add(iconUI);
            }
            // 多すぎる分は非表示にし、必要な分だけアニメーション状態をセット
            for (int i = 0; i < generatedCostIcons.Count; i++)
            {
                if (i < currentSkill.requiredPoints)
                {
                    generatedCostIcons[i].gameObject.SetActive(true);
                    // 装備状態に応じてアニメーションを切り替える
                    generatedCostIcons[i].SetState(isEquipped);
                }
                else
                {
                    generatedCostIcons[i].gameObject.SetActive(false);
                }
            }
        }

        // --- 装備マークの更新 (使用する場合) ---
        if (equippedMark != null)
        {
            equippedMark.SetActive(isEquipped);
        }

        // --- 【重要】背景スプライトの5状態切り替え ---
        if (buttonBackgroundImage != null)
        {
            if (!isUnlocked)
            {
                // 1. 未開放状態（選択・未選択にかかわらず固定）
                buttonBackgroundImage.sprite = lockedSprite;
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
    // EventSystem インターフェースの実装
    // =======================================================

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;

        // カーソルが合った瞬間に右側の詳細ビューを更新
        menuManager.UpdateDetailView(currentSkill);

        // 選択状態のスプライトに切り替えるためにUI更新
        RefreshUI();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;

        // 未選択状態のスプライトに戻すためにUI更新
        RefreshUI();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        ToggleSkill();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ToggleSkill();
    }

    /// <summary>
    /// スキルの着脱処理を即座に実行する
    /// </summary>
    private void ToggleSkill()
    {
        if (currentSkill == null)
            return;

        // 未解放のスキルは弾く
        if (!SkillManager.instance.IsSkillUnlocked(currentSkill.skillID))
        {
            Debug.Log("未解放のスキルのため操作できません。");
            return;
        }

        bool isEquipped = SkillManager.instance.IsSkillActive(currentSkill.skillID);

        if (isEquipped)
        {
            SkillManager.instance.UnequipSkill(currentSkill.skillID);
        }
        else
        {
            bool success = SkillManager.instance.EquipSkill(currentSkill.skillID);
            if (!success)
            {
                Debug.Log("スキルポイントが不足しているため装備できません。");
                return;
            }
        }

        // 着脱によって装備状態が変わったので、スプライトを更新
        RefreshUI();

        // 残りポイントなどが変化したため、詳細ビューも更新
        menuManager.UpdateDetailView(currentSkill);
    }
}

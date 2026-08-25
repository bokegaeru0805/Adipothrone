using TMPro;
using UnityEngine;

/// <summary>
/// 選択されたスキルの詳細情報と、現在の残りスキルポイントを表示するクラス
/// </summary>
public class SkillDetailView : MonoBehaviour
{
    [Header("スキル情報")]
    [Tooltip("スキルの名称")]
    [SerializeField]
    private TextMeshProUGUI skillNameText;

    [Tooltip("スキルのカテゴリ名")]
    [SerializeField]
    private TextMeshProUGUI categoryText;

    [Tooltip("装備に必要なコスト")]
    [SerializeField]
    private TextMeshProUGUI costText;

    [Tooltip("スキルの説明文")]
    [SerializeField]
    private TextMeshProUGUI descriptionText;

    [Header("ポイント表示 (オブジェクト生成用)")]
    [Tooltip("合計ポイントのクリスタルを並べる親オブジェクト")]
    [SerializeField]
    private Transform totalPointsContainer;

    [Tooltip("生成するクリスタルのプレハブ")]
    [SerializeField]
    private GameObject pointIconPrefab;

    private SkillPointView totalPointView;

    private void Awake()
    {
        totalPointView = new SkillPointView(totalPointsContainer, pointIconPrefab);
    }

    /// <summary>
    /// 現在の残りスキルポイントを再計算してクリスタルを並べる
    /// </summary>
    public void UpdateAvailablePoints()
    {
        if (
            SkillManager.instance == null
            || totalPointsContainer == null
            || pointIconPrefab == null
        )
            return;

        int points = SkillManager.instance.GetAvailableSkillPoints();

        totalPointView.SetPoints(points, true);
    }

    /// <summary>
    /// スキルが選択された際、または着脱操作が行われた際に呼ばれる
    /// </summary>
    public void UpdateView(SkillData skillData)
    {
        if (skillData == null || SkillManager.instance == null)
            return;

        UpdateView(
            new SkillUIState(
                skillData,
                SkillManager.instance.IsSkillUnlocked(skillData.skillID),
                SkillManager.instance.IsSkillActive(skillData.skillID),
                false,
                SkillManager.instance.GetSkillLevel(skillData.skillID)
            )
        );
    }

    public void UpdateView(SkillUIState state)
    {
        UpdateAvailablePoints();

        SkillData skillData = state.SkillData;
        if (skillData == null)
            return;

        // 各UIがインスペクターで設定されているか確認しながら書き換える（安全対策）
        if (state.IsUnlocked)
        {
            // 解放済みの場合はすべての情報を表示
            if (skillNameText != null)
                skillNameText.text = skillData.skillName;
            if (categoryText != null)
                categoryText.text = SkillUIText.GetCategoryName(skillData.category);

            if (costText != null)
            {
                costText.text = SkillUIText.GetCostText(state.RequiredPoints);
                costText.gameObject.SetActive(true);
            }

            if (descriptionText != null)
                descriptionText.text = skillData.description;
        }
        else
        {
            // 未解放の場合は情報を隠蔽する
            if (skillNameText != null)
                skillNameText.text = SkillUIText.LockedName;
            if (categoryText != null)
                categoryText.text = SkillUIText.LockedName;

            if (costText != null)
            {
                costText.gameObject.SetActive(false); // コストを完全に非表示
            }

            if (descriptionText != null)
                descriptionText.text = SkillUIText.LockedDescription;
        }
    }

    public void ShowEquipFailure(SkillEquipResult result)
    {
        if (descriptionText != null)
            descriptionText.text = SkillUIText.GetEquipFailureMessage(result);
    }
}

using System.Collections.Generic;
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

    // 生成したクリスタルを保持するリスト
    private List<PointIconUI> generatedTotalPointsIcons = new List<PointIconUI>();

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

        // 必要な数までプレハブを生成
        while (generatedTotalPointsIcons.Count < points)
        {
            GameObject obj = Instantiate(pointIconPrefab, totalPointsContainer);
            obj.transform.localScale = Vector3.one;
            PointIconUI iconUI = obj.GetComponent<PointIconUI>();
            if (iconUI != null)
                generatedTotalPointsIcons.Add(iconUI);
        }

        // ポイントの数だけアクティブにして、アニメーションを「Default」にする
        for (int i = 0; i < generatedTotalPointsIcons.Count; i++)
        {
            if (i < points)
            {
                generatedTotalPointsIcons[i].gameObject.SetActive(true);
                generatedTotalPointsIcons[i].SetState(true); // 所持ポイントは常にキラキラ(Default)させる
            }
            else
            {
                generatedTotalPointsIcons[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// スキルが選択された際、または着脱操作が行われた際に呼ばれる
    /// </summary>
    public void UpdateView(SkillData skillData)
    {
        // 常に最新のポイント表示に更新
        UpdateAvailablePoints();

        if (skillData == null)
            return;

        bool isUnlocked = SkillManager.instance.IsSkillUnlocked(skillData.skillID);

        // 各UIがインスペクターで設定されているか確認しながら書き換える（安全対策）
        if (isUnlocked)
        {
            // 解放済みの場合はすべての情報を表示
            if (skillNameText != null)
                skillNameText.text = skillData.skillName;
            if (categoryText != null)
                categoryText.text = GetCategoryName(skillData.category);

            if (costText != null)
            {
                costText.text = $"コスト: {skillData.requiredPoints}";
                costText.gameObject.SetActive(true);
            }

            if (descriptionText != null)
                descriptionText.text = skillData.description;
        }
        else
        {
            // 未解放の場合は情報を隠蔽する
            if (skillNameText != null)
                skillNameText.text = "？？？";
            if (categoryText != null)
                categoryText.text = "？？？";

            if (costText != null)
            {
                costText.gameObject.SetActive(false); // コストを完全に非表示
            }

            if (descriptionText != null)
                descriptionText.text = "条件を満たすと詳細が判明します。";
        }
    }

    /// <summary>
    /// カテゴリのEnumから表示用のテキストを取得する
    /// </summary>
    private string GetCategoryName(SkillCategory category)
    {
        switch (category)
        {
            case SkillCategory.Basic:
                return "基本型";
            case SkillCategory.Exploration:
                return "探索型";
            case SkillCategory.Attack:
                return "攻撃型";
            case SkillCategory.Defense:
                return "防御型";
            case SkillCategory.Luck:
                return "幸運型";
            case SkillCategory.Item:
                return "アイテム型";
            case SkillCategory.Special:
                return "特殊型";
            default:
                return category.ToString();
        }
    }
}

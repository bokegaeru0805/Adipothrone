using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スキルポイントアイコンの生成・再利用・表示更新を担当します。
/// </summary>
public sealed class SkillPointView
{
    private readonly Transform container;
    private readonly GameObject iconPrefab;
    private readonly List<PointIconUI> icons = new List<PointIconUI>();
    private bool hasLoggedInvalidPrefab;

    public SkillPointView(Transform container, GameObject iconPrefab)
    {
        this.container = container;
        this.iconPrefab = iconPrefab;
    }

    public void SetPoints(int count, bool isAnimated)
    {
        count = Mathf.Max(0, count);
        if (container == null || iconPrefab == null)
            return;

        EnsureCapacity(count);

        float normalizedTime = 0f;
        if (isAnimated)
        {
            foreach (PointIconUI icon in icons)
            {
                if (icon != null && icon.TryGetAnimationNormalizedTime(out normalizedTime))
                    break;
            }
        }

        for (int i = 0; i < icons.Count; i++)
        {
            PointIconUI icon = icons[i];
            if (icon == null)
                continue;

            bool isVisible = i < count;
            icon.gameObject.SetActive(isVisible);
            if (isVisible)
                icon.SetState(isAnimated, normalizedTime);
        }
    }

    private void EnsureCapacity(int count)
    {
        while (icons.Count < count)
        {
            GameObject iconObject = Object.Instantiate(iconPrefab, container);
            iconObject.transform.localScale = Vector3.one;
            if (!iconObject.TryGetComponent(out PointIconUI icon))
            {
                if (!hasLoggedInvalidPrefab)
                {
                    Debug.LogError(
                        "ポイントアイコンPrefabにPointIconUIが設定されていません。",
                        iconObject
                    );
                    hasLoggedInvalidPrefab = true;
                }

                Object.Destroy(iconObject);
                return;
            }

            icons.Add(icon);
        }
    }
}

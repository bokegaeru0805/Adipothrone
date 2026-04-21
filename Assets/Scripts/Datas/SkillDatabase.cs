using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillDatabase", menuName = "Skills/Skill Database")]
public class SkillDatabase : ScriptableObject
{
    // インスペクターではエディタ拡張側でカテゴリ別に描画するため非表示にする
    [HideInInspector]
    public List<SkillData> skills = new List<SkillData>();

    // IDからスキルデータを取得（存在しなければnull）
    public SkillData GetSkillByID(Enum id)
    {
        if (id is SkillName skillID)
        {
            // nullチェックも併せて行うと安全です
            return skills.Find(item => item != null && item.skillID == skillID);
        }

        return null;
    }
}

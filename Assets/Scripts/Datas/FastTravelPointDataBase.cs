using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "FastTravelPointDataBase",
    menuName = "Fast Travel/Fast Travel Point Data Base"
)]

public class FastTravelPointDataBase : ScriptableObject
{
    public List<FastTravelPointData> fastTravelPoints = new List<FastTravelPointData>();

    // IDからファストトラベルポイントを取得（存在しなければnull）
    public FastTravelPointData GetFastTravelPointByID(Enum id)
    {
        if (id is FastTravelName fastTravelID)
        {
            return fastTravelPoints.Find(item => item.fastTravelId == fastTravelID);
        }

        return null;
    }
}
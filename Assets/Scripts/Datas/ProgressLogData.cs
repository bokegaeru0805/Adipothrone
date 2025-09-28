using UnityEngine;

[CreateAssetMenu(fileName = "ProgressLogData", menuName = "Game/ProgressLog")]
public class ProgressLogInfoData : ScriptableObject
{
    public ProgressLogName logName; // 進行度の名前

    [TextArea(3, 10)]
    public string logText; // 実際の文章
}
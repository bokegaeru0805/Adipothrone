using UnityEngine;

/// <summary>
/// 雪原に配置するフィールドイベントを管理します。
/// </summary>
public class SnowFieldEvent : BaseFieldEvent
{
    #region インスペクター設定

    [SerializeField]
    private FieldName fieldName = FieldName.None;

    #endregion

    #region イベント定義

    private enum FieldName
    {
        None = 0,
        EventField1 = 1, // 雪原のイベントフィールド1
        VillageEntrance = 2, // 雪原の村入口
        VillageHouse = 3, // 雪原の村の家
    }

    #endregion

    #region プロパティ

    protected override string EventName => fieldName.ToString();

    #endregion

    #region Unityライフサイクル

    protected override void Awake()
    {
        base.Awake();

        if (fieldName == FieldName.None)
        {
            Debug.LogWarning(
                $"{gameObject.name} の SnowFieldEvent にフィールド名が設定されていません。",
                this
            );
        }
    }

    #endregion

    #region イベント処理

    protected override void HandleEvent()
    {
        switch (fieldName)
        {
            case FieldName.EventField1:
                FungusHelper.ExecuteBlock(targetFlowchart, "HeadToSnowCountry");
                isEventTriggered = true; // イベントがトリガーされたことを記録
                break;
            case FieldName.VillageEntrance:
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.FirstEnteredSnowVillage))
                {
                    flagManager.SetBoolFlag(Chapter3TriggeredEvent.FirstEnteredSnowVillage, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "EnterSnowVillage");
                }
                break;
            case FieldName.VillageHouse:
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.FirstTalkedToVillageChief))
                {
                    flagManager.SetBoolFlag(Chapter3TriggeredEvent.FirstTalkedToVillageChief, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstTalkedToVillageChief");
                }
                break;
        }
    }

    #endregion
}

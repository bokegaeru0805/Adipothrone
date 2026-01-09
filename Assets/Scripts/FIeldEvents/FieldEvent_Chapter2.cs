using UnityEngine;

public class FieldEvent_Chapter2 : BaseFieldEvent
{
    [SerializeField]
    private FieldName fieldname = FieldName.None; // フィールド名を設定するための変数

    private enum FieldName
    {
        None = 0, // フィールド名が設定されていない場合の初期値

        // Chapter2StartField = 1, // 第二章開始フィールド
        VillageEntranceField = 2, // 村の入り口フィールド
        CoachmanField = 3, // 馬車の御者フィールド
        WaterSourceField = 4, // オアシスの源泉フィールド
    }

    protected override string EventName => fieldname.ToString();

    protected override void Awake()
    {
        base.Awake();

        if (fieldname == FieldName.None)
        {
            Debug.LogWarning(
                $"{this.gameObject.name} の FieldEvent_Chapter2 にフィールド名が設定されていません。",
                this
            );
        }
    }

    protected override void HandleEvent()
    {
        switch (fieldname)
        {
            case FieldName.VillageEntranceField:
                if (!flagManager.GetBoolFlag(Chapter2TriggeredEvent.FirstEnteredVillage))
                {
                    flagManager.SetBoolFlag(Chapter2TriggeredEvent.FirstEnteredVillage, true);
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                    GameManager.instance.savedata.FastTravelData.RegisterFastTravelData(
                        FastTravelName.DesertVillage
                    ); // ファストトラベル地点を登録
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstEnteredVillage");
                }
                break;
            case FieldName.CoachmanField:
                if (!flagManager.GetBoolFlag(Chapter2TriggeredEvent.FirstMetCoachman))
                {
                    flagManager.SetBoolFlag(Chapter2TriggeredEvent.FirstMetCoachman, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstMetCoachman");
                }
                break;
            case FieldName.WaterSourceField:
                if (!flagManager.GetBoolFlag(Chapter2TriggeredEvent.OasisSpringEnemiesDefeated))
                {
                    isEventTriggered = true; // イベントがトリガーされたことを記録
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstMetFill");
                }
                break;
        }
    }
}

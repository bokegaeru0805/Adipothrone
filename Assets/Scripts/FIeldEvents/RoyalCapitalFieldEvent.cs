using UnityEngine;

/// <summary>
/// 王都に配置するフィールドイベントを管理します。
/// </summary>
public class RoyalCapitalFieldEvent : BaseFieldEvent
{
    #region インスペクター設定

    [SerializeField]
    private FieldName fieldName = FieldName.None;

    #endregion

    #region イベント定義

    private enum FieldName
    {
        None = 0,
        GuildEntrance = 1, // ギルド入口
        GuildReception = 2, // ギルド受付
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
                $"{gameObject.name} の RoyalCapitalFieldEvent にフィールド名が設定されていません。",
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
            case FieldName.None:
                break;
            case FieldName.GuildEntrance:
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.FirstEnteredGuild))
                {
                    flagManager.SetBoolFlag(Chapter3TriggeredEvent.FirstEnteredGuild, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstEnteredGuild");
                }
                break;
            case FieldName.GuildReception:
                if (!flagManager.GetBoolFlag(Chapter3TriggeredEvent.FirstMetGuildReceptionist))
                {
                    flagManager.SetBoolFlag(Chapter3TriggeredEvent.FirstMetGuildReceptionist, true);
                    FungusHelper.ExecuteBlock(targetFlowchart, "FirstMetGuildReceptionist");
                }
                else if (
                    flagManager.GetBoolFlag(Chapter3TriggeredEvent.GuildInquiryCompleteAll)
                    && !flagManager.GetBoolFlag(
                        Chapter3TriggeredEvent.AskedReceptionistAboutNextDestination
                    )
                )
                {
                    flagManager.SetBoolFlag(
                        Chapter3TriggeredEvent.AskedReceptionistAboutNextDestination,
                        true
                    );
                    FungusHelper.ExecuteBlock(
                        targetFlowchart,
                        "AskedReceptionistAboutNextDestination"
                    );
                }
                break;
        }
    }

    #endregion

    #region 第3章のギルドでの聞き込み関連の共通処理
    /// <summary>
    /// ギルドでの聞き込みが全て完了しているかを確認し、完了していればフラグを立ててイベントを発火させます。
    /// </summary>
    public void CheckChapter3GuildInquiryComplete()
    {
        // 既に完了イベントが発火済みなら何もしない
        if (flagManager.GetBoolFlag(Chapter3TriggeredEvent.GuildInquiryCompleteAll))
            return;

        // 全てのの聞き込みフラグがTrueか確認
        bool isComplete =
            flagManager.GetBoolFlag(Chapter3TriggeredEvent.GuildInquiryComplete1)
            && flagManager.GetBoolFlag(Chapter3TriggeredEvent.GuildInquiryComplete2);

        if (isComplete)
        {
            flagManager.SetBoolFlag(Chapter3TriggeredEvent.GuildInquiryCompleteAll, true);
            FungusHelper.ExecuteBlock(targetFlowchart, "GuildInquiryCompleteAll");
        }
    }
    #endregion
}

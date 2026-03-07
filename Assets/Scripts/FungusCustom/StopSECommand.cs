using System;
using Fungus;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// SEManagerを介してSEを停止・フェードアウトするための統合Fungusコマンド。
/// 同じBlock内で直前に鳴らしたSEを自動で特定して止めることができます。
/// </summary>
[CommandInfo("Audio", "Stop SE (ADX2)", "指定したSE、または直前に鳴らしたSEを停止します")]
public class StopSECommand : Command
{
    [Tooltip("同じBlock内で最後に Play SE (ADX2) で鳴らしたSEを自動的に停止するかどうか")]
    [SerializeField]
    private bool stopLastPlayedInBlock = false;

    // --- NaughtyAttributes用の表示制御メソッド ---
    // stopLastPlayedInBlock が true の時は、手動で指定するUIをすべて隠します
    private bool ShowCategory() => !stopLastPlayedInBlock;

    private bool ShowUI() => !stopLastPlayedInBlock && category == PlaySECommand.SECategoryType.UI;

    private bool ShowPlayerAction() =>
        !stopLastPlayedInBlock && category == PlaySECommand.SECategoryType.PlayerAction;

    private bool ShowEnemyAction() =>
        !stopLastPlayedInBlock && category == PlaySECommand.SECategoryType.EnemyAction;

    private bool ShowField() =>
        !stopLastPlayedInBlock && category == PlaySECommand.SECategoryType.Field;

    private bool ShowSystemEvent() =>
        !stopLastPlayedInBlock && category == PlaySECommand.SECategoryType.SystemEvent;

    [Tooltip("停止するSEのカテゴリ")]
    [AllowNesting]
    [SerializeField]
    [ShowIf("ShowCategory")]
    private PlaySECommand.SECategoryType category = PlaySECommand.SECategoryType.UI;

    [SerializeField]
    [AllowNesting]
    [ShowIf("ShowUI")]
    [Label("SE Name")]
    private SE_UI uiSE;

    [SerializeField]
    [AllowNesting]
    [ShowIf("ShowPlayerAction")]
    [Label("SE Name")]
    private SE_PlayerAction playerActionSE;

    [SerializeField]
    [AllowNesting]
    [ShowIf("ShowEnemyAction")]
    [Label("SE Name")]
    private SE_EnemyAction enemyActionSE;

    [SerializeField]
    [AllowNesting]
    [ShowIf("ShowField")]
    [Label("SE Name")]
    private SE_Field fieldSE;

    [SerializeField]
    [AllowNesting]
    [ShowIf("ShowSystemEvent")]
    [Label("SE Name")]
    private SE_SystemEvent systemEventSE;

    // --- フェード（リリース）設定 ---
    [Tooltip(
        "Atom Craft側で設定されたフェードアウト（リリース）時間を無視して、即座に音をブツッと切るかどうか\n※基本はfalse（フェードアウトさせる）を推奨します"
    )]
    [SerializeField]
    private bool immediateStop = false;

    // このコマンドが実行されたときに呼ばれる処理
    public override void OnEnter()
    {
        if (SEManager.instance == null)
        {
            Debug.LogError("[StopSECommand] SEManagerが見つかりません！");
            Continue();
            return;
        }

        Enum targetSE = null;

        if (stopLastPlayedInBlock)
        {
            // 同じBlock内で最後に鳴らしたSEを辞書から取得
            if (PlaySECommand.lastPlayedSEPerBlock.TryGetValue(ParentBlock, out Enum lastSE))
            {
                targetSE = lastSE;
            }
            else
            {
                Debug.LogWarning(
                    $"[StopSECommand] Block '{ParentBlock.BlockName}' 内でまだ PlaySECommand が実行されていないため、停止するSEが見つかりません。"
                );
                Continue();
                return;
            }
        }
        else
        {
            // Inspectorで手動指定されたSEを取得
            targetSE = GetSelectedEnum();
        }

        // 取得したSEを停止（immediateStopフラグを渡す）
        SEManager.instance.StopEx(targetSE, immediateStop);

        Continue();
    }

    /// <summary>
    /// 現在のカテゴリ設定に応じて、選択されているEnumを返します
    /// </summary>
    private Enum GetSelectedEnum()
    {
        switch (category)
        {
            case PlaySECommand.SECategoryType.UI:
                return uiSE;
            case PlaySECommand.SECategoryType.PlayerAction:
                return playerActionSE;
            case PlaySECommand.SECategoryType.EnemyAction:
                return enemyActionSE;
            case PlaySECommand.SECategoryType.Field:
                return fieldSE;
            case PlaySECommand.SECategoryType.SystemEvent:
                return systemEventSE;
            default:
                return uiSE;
        }
    }

    public override string GetSummary()
    {
        // Flowchart上で「何をどう止めるか」が一目でわかるように要約を表示
        string targetName = stopLastPlayedInBlock
            ? "Last Played in this Block"
            : GetSelectedEnum().ToString();
        string fadeInfo = immediateStop ? " (Immediate)" : " (Fade/Release)";

        return $"Stop: {targetName}{fadeInfo}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(220, 120, 100, 255);
    }

    [Header("検索・クイック設定")]
    [Tooltip("SE名の一部を入力すると、下のリストが絞り込まれます（大文字小文字は区別しません）")]
    [SerializeField]
    [ShowIf("ShowCategory")] // 直前のSEを止める設定の時は隠す
    private string searchKeyword = "";

    [Tooltip("検索結果から選択すると、自動でカテゴリと該当SEが設定されます")]
    [Dropdown("GetSearchResults")]
    [OnValueChanged("OnSearchResultSelected")]
    [SerializeField]
    [AllowNesting]
    [ShowIf("ShowCategory")] // 直前のSEを止める設定の時は隠す
    private string selectSearchResult = "";

    /// <summary>
    /// 検索キーワードに基づいてドロップダウンのリストを動的に生成します
    /// </summary>
    private DropdownList<string> GetSearchResults()
    {
        var list = new DropdownList<string>();
        list.Add("--- ここから検索結果を選択 ---", "");

        string keyword = searchKeyword != null ? searchKeyword.ToLower() : "";
        int matchCount = 0; // 追加した要素を数えるカウンタ

        // PlaySECommand側で定義されているカテゴリを参照して走査し、追加した数を足し合わせる
        matchCount += AddMatchesToList<SE_UI>(PlaySECommand.SECategoryType.UI, keyword, list);
        matchCount += AddMatchesToList<SE_PlayerAction>(
            PlaySECommand.SECategoryType.PlayerAction,
            keyword,
            list
        );
        matchCount += AddMatchesToList<SE_EnemyAction>(
            PlaySECommand.SECategoryType.EnemyAction,
            keyword,
            list
        );
        matchCount += AddMatchesToList<SE_Field>(PlaySECommand.SECategoryType.Field, keyword, list);
        matchCount += AddMatchesToList<SE_SystemEvent>(
            PlaySECommand.SECategoryType.SystemEvent,
            keyword,
            list
        );

        // 一致する項目が0件で、かつ検索キーワードが入力されている場合
        if (matchCount == 0 && !string.IsNullOrEmpty(keyword))
        {
            list.Add("(一致するSEが見つかりません)", "");
        }

        return list;
    }

    /// <summary>
    /// 指定したEnumの要素を検索し、キーワードに一致すればリストに追加する補助メソッド
    /// 追加した件数を返します
    /// </summary>
    private int AddMatchesToList<T>(
        PlaySECommand.SECategoryType cat,
        string keyword,
        DropdownList<string> list
    )
        where T : Enum
    {
        int addedCount = 0;
        foreach (var name in Enum.GetNames(typeof(T)))
        {
            if (string.IsNullOrEmpty(keyword) || name.ToLower().Contains(keyword))
            {
                list.Add($"{cat} / {name}", $"{cat},{name}");
                addedCount++;
            }
        }
        return addedCount; // 追加した件数を返す
    }

    /// <summary>
    /// 検索結果のドロップダウンから項目が選択されたときに呼ばれるコールバック
    /// </summary>
    private void OnSearchResultSelected()
    {
        if (string.IsNullOrEmpty(selectSearchResult))
            return;

        string[] parts = selectSearchResult.Split(',');
        if (parts.Length == 2)
        {
            if (Enum.TryParse(parts[0], out PlaySECommand.SECategoryType parsedCat))
            {
                category = parsedCat;
                string enumName = parts[1];

                switch (category)
                {
                    case PlaySECommand.SECategoryType.UI:
                        if (Enum.TryParse(enumName, out SE_UI ui))
                            uiSE = ui;
                        break;
                    case PlaySECommand.SECategoryType.PlayerAction:
                        if (Enum.TryParse(enumName, out SE_PlayerAction pa))
                            playerActionSE = pa;
                        break;
                    case PlaySECommand.SECategoryType.EnemyAction:
                        if (Enum.TryParse(enumName, out SE_EnemyAction ea))
                            enemyActionSE = ea;
                        break;
                    case PlaySECommand.SECategoryType.Field:
                        if (Enum.TryParse(enumName, out SE_Field f))
                            fieldSE = f;
                        break;
                    case PlaySECommand.SECategoryType.SystemEvent:
                        if (Enum.TryParse(enumName, out SE_SystemEvent sys))
                            systemEventSE = sys;
                        break;
                }
            }
        }

        searchKeyword = "";
        selectSearchResult = "";
    }
}

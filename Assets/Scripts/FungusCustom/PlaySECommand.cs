using System;
using System.Collections.Generic;
using System.Linq;
using Fungus;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// SEManagerを介してあらゆるカテゴリのSEを再生するための統合Fungusコマンド。
/// NaughtyAttributesを使用して、カテゴリごとにInspectorの表示を切り替えます。
/// </summary>
[CommandInfo("Audio", "Play SE (ADX2)", "SEManagerを使用して指定したカテゴリのSEを再生します")]
public class PlaySECommand : Command
{
    // どのカテゴリのSEを鳴らすか選ぶためのEnum
    public enum SECategoryType
    {
        UI,
        PlayerAction,
        EnemyAction,
        Field,
        SystemEvent,
    }

    [Tooltip("再生するSEのカテゴリ")]
    [SerializeField]
    private SECategoryType category = SECategoryType.UI;

    // --- 各カテゴリごとのSE選択用変数 ---
    // [ShowIf] を使って、categoryの選択に応じて表示/非表示を切り替えます
    // [Label] でInspector上の表示名を統一しています

    [SerializeField]
    [AllowNesting]
    [ShowIf("category", SECategoryType.UI)]
    [Label("SE Name")]
    private SE_UI uiSE;

    [SerializeField]
    [AllowNesting]
    [ShowIf("category", SECategoryType.PlayerAction)]
    [Label("SE Name")]
    private SE_PlayerAction playerActionSE;

    [SerializeField]
    [AllowNesting]
    [ShowIf("category", SECategoryType.EnemyAction)]
    [Label("SE Name")]
    private SE_EnemyAction enemyActionSE;

    [SerializeField]
    [AllowNesting]
    [ShowIf("category", SECategoryType.Field)]
    [Label("SE Name")]
    private SE_Field fieldSE;

    [SerializeField]
    [AllowNesting]
    [ShowIf("category", SECategoryType.SystemEvent)]
    [Label("SE Name")]
    private SE_SystemEvent systemEventSE;

    // --- オプション設定（音量・ピッチのオーバーライド） ---

    [Tooltip("標準の音量を上書きして再生するかどうか")]
    [SerializeField]
    private bool overrideVolume = false;

    [SerializeField]
    [AllowNesting]
    [ShowIf("overrideVolume")]
    [Range(0f, 2f)]
    [Tooltip("上書きする音量（1.0が標準）")]
    private float volume = 1.0f;

    [Tooltip("標準のピッチを上書きして再生するかどうか")]
    [SerializeField]
    private bool overridePitch = false;

    [SerializeField]
    [AllowNesting]
    [ShowIf("overridePitch")]
    [Range(-1200f, 1200f)]
    [Tooltip("上書きするピッチ（0が標準。単位はセントなどADX2の仕様に依存）")]
    private float pitch = 0f;

    [Tooltip(
        "次のコマンドへ進むのを待つかどうか（Wait Until Finishedの簡易版）\n※通常SEは鳴らしっぱなしで進むためデフォルトはfalseです"
    )]
    [SerializeField]
    private bool waitUntilFinished = false;

    /// <summary>
    /// Blockごとに最後に再生したSEを記録しておくための辞書。
    /// StopSECommandで「最後に鳴らしたSEを止める」機能で使用します。
    /// </summary>
    public static Dictionary<Block, Enum> lastPlayedSEPerBlock = new Dictionary<Block, Enum>();

    // このコマンドが実行されたときに呼ばれる処理
    public override void OnEnter()
    {
        if (SEManager.instance == null)
        {
            Debug.LogError(
                "[PlaySECommand] SEManagerが見つかりません！シーンに配置されているか確認してください。"
            );
            Continue();
            return;
        }

        Enum selectedSE = GetSelectedEnum();

        // 共通のPlayExメソッドを使って、音量とピッチのオプションとともに再生
        SEManager.instance.PlayEx(selectedSE, overrideVolume, volume, overridePitch, pitch);

        // このBlockで鳴らした最新のSEとして記録
        lastPlayedSEPerBlock[ParentBlock] = selectedSE;

        // 再生終了を待つ設定がない場合は、すぐに次のコマンドへ
        if (!waitUntilFinished)
        {
            Continue();
        }
        else
        {
            // ※必要であれば、SEManager側で再生終了コールバックを実装してここで待機する処理を書けます。
            // 現状のSEManagerの仕様では再生時間を厳密に測るのが難しいため、すぐにContinueします。
            Debug.LogWarning(
                "[PlaySECommand] waitUntilFinished は現在未対応のため、すぐに次の処理へ進みます。"
            );
            Continue();
        }
    }

    /// <summary>
    /// 現在のカテゴリ設定に応じて、選択されているEnumを返します
    /// </summary>
    private Enum GetSelectedEnum()
    {
        switch (category)
        {
            case SECategoryType.UI:
                return uiSE;
            case SECategoryType.PlayerAction:
                return playerActionSE;
            case SECategoryType.EnemyAction:
                return enemyActionSE;
            case SECategoryType.Field:
                return fieldSE;
            case SECategoryType.SystemEvent:
                return systemEventSE;
            default:
                return uiSE;
        }
    }

    public override string GetSummary()
    {
        // Flowchart上で「どのカテゴリの、どのSEを鳴らすか」が一目でわかるように要約を表示
        string seName = GetSelectedEnum().ToString();

        // オプションが有効な場合はそれも表示する
        string options = "";
        if (overrideVolume)
            options += $" Vol:{volume:F1}";
        if (overridePitch)
            options += $" Pitch:{pitch}";

        return $"{category} : {seName}{options}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(255, 160, 140, 255);
    }

    [Header("検索・クイック設定")]
    [Tooltip("SE名の一部を入力すると、下のリストが絞り込まれます（大文字小文字は区別しません）")]
    [SerializeField]
    private string searchKeyword = "";

    [Tooltip("検索結果から選択すると、自動でカテゴリと該当SEが設定されます")]
    [Dropdown("GetSearchResults")]
    [OnValueChanged("OnSearchResultSelected")]
    [SerializeField]
    [AllowNesting]
    private string selectSearchResult = "";

    /// <summary>
    /// 検索キーワードに基づいてドロップダウンのリストを動的に生成します
    /// </summary>
    private DropdownList<string> GetSearchResults()
    {
        var list = new DropdownList<string>();
        list.Add("--- ここから検索結果を選択 ---", ""); // デフォルトの空要素

        string keyword = searchKeyword != null ? searchKeyword.ToLower() : "";
        int matchCount = 0; // 追加した要素を数えるカウンタ

        // 各カテゴリのEnumを走査してリストに追加し、追加した数を足し合わせる
        // ※ StopSECommand.cs に貼り付ける場合は、ここの引数を PlaySECommand.SECategoryType.UI などに変更してください
        matchCount += AddMatchesToList<SE_UI>(SECategoryType.UI, keyword, list);
        matchCount += AddMatchesToList<SE_PlayerAction>(SECategoryType.PlayerAction, keyword, list);
        matchCount += AddMatchesToList<SE_EnemyAction>(SECategoryType.EnemyAction, keyword, list);
        matchCount += AddMatchesToList<SE_Field>(SECategoryType.Field, keyword, list);
        matchCount += AddMatchesToList<SE_SystemEvent>(SECategoryType.SystemEvent, keyword, list);

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
    // ※ StopSECommand.cs に貼り付ける場合は、第1引数を PlaySECommand.SECategoryType cat に変更してください
    private int AddMatchesToList<T>(SECategoryType cat, string keyword, DropdownList<string> list)
        where T : Enum
    {
        int addedCount = 0;
        foreach (var name in Enum.GetNames(typeof(T)))
        {
            if (string.IsNullOrEmpty(keyword) || name.ToLower().Contains(keyword))
            {
                // 表示名: "カテゴリ / SE名", 内部値: "カテゴリ,SE名" として保存
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

        // 保存しておいた内部値（"カテゴリ,SE名"）を分割
        string[] parts = selectSearchResult.Split(',');
        if (parts.Length == 2)
        {
            if (Enum.TryParse(parts[0], out SECategoryType parsedCat))
            {
                // 1. カテゴリを自動設定
                category = parsedCat;
                string enumName = parts[1];

                // 2. 該当するEnumを自動設定
                switch (category)
                {
                    case SECategoryType.UI:
                        if (Enum.TryParse(enumName, out SE_UI ui))
                            uiSE = ui;
                        break;
                    case SECategoryType.PlayerAction:
                        if (Enum.TryParse(enumName, out SE_PlayerAction pa))
                            playerActionSE = pa;
                        break;
                    case SECategoryType.EnemyAction:
                        if (Enum.TryParse(enumName, out SE_EnemyAction ea))
                            enemyActionSE = ea;
                        break;
                    case SECategoryType.Field:
                        if (Enum.TryParse(enumName, out SE_Field f))
                            fieldSE = f;
                        break;
                    case SECategoryType.SystemEvent:
                        if (Enum.TryParse(enumName, out SE_SystemEvent sys))
                            systemEventSE = sys;
                        break;
                }
            }
        }

        // 選択後、連続して検索しやすくするために検索状態をリセット
        searchKeyword = "";
        selectSearchResult = "";
    }
}

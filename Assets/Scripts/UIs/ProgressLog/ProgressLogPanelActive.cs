using System.Text;
using TMPro;
using UnityEngine;

public class ProgressLogPanelActive : MonoBehaviour, IPanelActive
{
    [Header("ゲームの進行度の文章")]
    [SerializeField]
    private ProgressLogDatabase progressLogDatabase;

    [SerializeField]
    private TextMeshProUGUI progressLogText;

    private void Awake()
    {
        if (progressLogText == null || progressLogDatabase == null)
        {
            Debug.LogError("GuidePanelの進行度のテキストまたはデータベースが設定されていません。");
            return;
        }

        progressLogText.text = ""; // 初期状態ではテキストを空にする
    }

    private void OnEnable()
    {
        UpdateProgressLogText(); // 進行度のテキストを更新
    }

    /// <summary>
    /// IPanelActiveインターフェース経由で呼ばれる、パネルの初期化メソッド
    /// </summary>
    public void SelectFirstButton()
    {
        UpdateProgressLogText(); // 進行度のテキストを更新
    }

    /// <summary>
    /// ゲームの進行度に応じたテキストをProgressLogDatabaseから取得し、UIに反映するメソッド
    /// </summary>
    private void UpdateProgressLogText()
    {
        var progressData = GameManager.instance.savedata.ProgressLogData;

        if (
            progressData != null
            && progressData.progressRecords != null
            && progressData.progressRecords.Count > 0
        )
        {
            int progressID = progressData.progressRecords[0].progressID;
            if (progressID == 0)
            {
                Debug.LogWarning(
                    "ProgressLogPanelActive: 進行度IDが0のため、情報を表示できません。"
                );
                progressLogText.text = "ゲーム進行度に関する情報を表示できません。";
                return;
            }
            // ProgressLogDatabaseから進行度の情報を取得
            ProgressLogInfoData progressLogInfo = progressLogDatabase.Get(progressID);

            if (progressLogInfo != null)
            {
                // StringBuilderを使って文字列を効率的に結合
                StringBuilder sb = new StringBuilder();

                // まずはベースの文章を追加
                sb.Append(progressLogInfo.logText);

                // 各追記項目（セクション）ごとに判定
                if (progressLogInfo.logSections != null)
                {
                    foreach (var section in progressLogInfo.logSections)
                    {
                        if (section.conditionalLogs != null)
                        {
                            // リストを「後ろから（進行度が後のものから）」逆順に評価するようにする
                            for (int i = section.conditionalLogs.Count - 1; i >= 0; i--)
                            {
                                var conditionalLog = section.conditionalLogs[i];
                                if (conditionalLog.AreConditionsMet())
                                {
                                    // 最初に条件を満たした（＝一番進行度が高い）テキストを追記して、このセクションの判定を終了（break）する
                                    sb.AppendLine();
                                    sb.Append(conditionalLog.additionalText);
                                    break;
                                }
                            }
                        }
                    }
                }

                // 最終的な文字列をテキストUIに反映
                progressLogText.text = sb.ToString();
            }
            else
            {
                Debug.LogWarning(
                    $"ProgressLogPanelActive: 進行度ID {progressID} に対応する情報がProgressLogDatabaseに見つかりません。"
                );
                progressLogText.text = "ゲーム進行度に関する情報を表示できません。";
            }
        }
    }
}

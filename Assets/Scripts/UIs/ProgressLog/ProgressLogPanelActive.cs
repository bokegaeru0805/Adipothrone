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
                progressLogText.text = "ゲーム進行度に関する情報を表示できません。";
                return;
            }
            // ProgressLogDatabaseから進行度の情報を取得
            ProgressLogInfoData progressLogInfo = progressLogDatabase.Get(progressID);
            progressLogText.text =
                progressLogInfo != null
                    ? progressLogInfo.logText
                    : "ゲーム進行度に関する情報を表示できません。";
        }
    }
}

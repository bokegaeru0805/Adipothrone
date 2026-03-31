using Fungus;
using UnityEngine;

public class GlobalFlowchartController : MonoBehaviour
{
    public static GlobalFlowchartController instance = null;

    [HideInInspector]
    public Flowchart globalFlowchart = null;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
            globalFlowchart = this.GetComponent<Flowchart>();
            if (globalFlowchart == null)
            {
                Debug.LogError("GlobalFlowchartControllerにFlowchartが設定されていません。", this);
            }
            SetBuildDateToFlowchart();
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    /// <summary>
    /// アプリケーションのビルド日時を取得し、Flowchartの変数にセットします。
    /// Resourcesフォルダに保存されたテキストデータを読み込みます。
    /// </summary>
    private void SetBuildDateToFlowchart()
    {
        if (globalFlowchart == null)
            return;

        int year = 0,
            month = 0,
            day = 0;

#if UNITY_EDITOR
        // エディタ上では常に現在の日時を使用
        System.DateTime now = System.DateTime.Now;
        year = now.Year;
        month = now.Month;
        day = now.Day;
#else
        // ビルド済みの場合は、ビルド時に自動生成されたテキストファイルから読み込む
        TextAsset buildDateText = Resources.Load<TextAsset>("BuildDate");

        if (buildDateText != null)
        {
            // "2026,3,31" のような文字列をカンマで分割して数値に変換
            string[] dateParts = buildDateText.text.Split(',');
            if (dateParts.Length >= 3)
            {
                int.TryParse(dateParts[0], out year);
                int.TryParse(dateParts[1], out month);
                int.TryParse(dateParts[2], out day);
            }
        }
        else
        {
            Debug.LogWarning("Resourcesフォルダに BuildDate.txt が見つかりませんでした。");
        }
#endif

        // 変数が存在するか確認してから、0以外の正しい値が入っていればセットする
        if (year > 0 && globalFlowchart.HasVariable("BuildYear"))
            globalFlowchart.SetIntegerVariable("BuildYear", year);

        if (month > 0 && globalFlowchart.HasVariable("BuildMonth"))
            globalFlowchart.SetIntegerVariable("BuildMonth", month);

        if (day > 0 && globalFlowchart.HasVariable("BuildDay"))
            globalFlowchart.SetIntegerVariable("BuildDay", day);

        // Debug.Log(
        //     $"ビルド日時をFlowchartに設定しました: {buildDate.Year}年{buildDate.Month}月{buildDate.Day}日"
        // );
    }
}

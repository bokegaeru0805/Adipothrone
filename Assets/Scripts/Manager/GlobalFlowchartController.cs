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
    /// </summary>
    private void SetBuildDateToFlowchart()
    {
        if (globalFlowchart == null)
            return;

        System.DateTime buildDate;

#if UNITY_EDITOR
        // エディタ上では現在の日時を使用
        buildDate = System.DateTime.Now;
#else
        // ビルド済みの場合はデータフォルダの最終更新日時からビルド日時を取得
        string path = Application.dataPath;

        // Macの場合は.appフォルダ自体を指すようにパスを調整
        if (Application.platform == RuntimePlatform.OSXPlayer)
        {
            path = path + "/../../";
        }

        buildDate = System.IO.File.GetLastWriteTime(path);
#endif

        // 変数が存在するか確認してから値をセットする
        if (globalFlowchart.HasVariable("BuildYear"))
            globalFlowchart.SetIntegerVariable("BuildYear", buildDate.Year);

        if (globalFlowchart.HasVariable("BuildMonth"))
            globalFlowchart.SetIntegerVariable("BuildMonth", buildDate.Month);

        if (globalFlowchart.HasVariable("BuildDay"))
            globalFlowchart.SetIntegerVariable("BuildDay", buildDate.Day);

        // Debug.Log(
        //     $"ビルド日時をFlowchartに設定しました: {buildDate.Year}年{buildDate.Month}月{buildDate.Day}日"
        // );
    }
}

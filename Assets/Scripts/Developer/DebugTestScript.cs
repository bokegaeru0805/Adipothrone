#if UNITY_EDITOR
using UnityEngine;
using System.Collections;

public class DebugTestScript : MonoBehaviour
{
    // 更新間隔（秒）
    [SerializeField]
    private float updateInterval = 1.0f;

    private float accumulatedTime = 0.0f; // 経過時間
    private int frameCount = 0; // 経過フレーム数
    private float lastFps = 0.0f; // 計算されたFPS

    // public Text fpsText; // InspectorからUI Textをアタッチする場合

    void Update()
    {
        // 経過時間を加算
        accumulatedTime += Time.deltaTime;
        // 経過フレーム数を加算
        frameCount++;

        // 更新間隔（1秒）を超えたら、FPSを計算してリセット
        if (accumulatedTime >= updateInterval)
        {
            // 平均FPSを計算
            lastFps = frameCount / accumulatedTime;

            // ログに出力
            Debug.LogFormat("Average: {0} fps", lastFps);

            // UIテキストに表示する場合
            // if (fpsText != null)
            // {
            //     fpsText.text = $"FPS: {lastFps:F1}"; // 小数点以下1桁まで表示
            // }

            // 変数をリセット
            accumulatedTime = 0.0f;
            frameCount = 0;
        }
    }
}

#endif

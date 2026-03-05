using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

public class CustomProfilerLogger : EditorWindow
{
    private bool isLogging = false;
    private int lastProcessedFrame = -1;
    private float gcThresholdKb = 1.0f;
    private float timeThresholdMs = 2.0f;
    private string logFilePath = "Logs/ProfilerReport.txt";

    [MenuItem("Tools/Custom Profiler Logger")]
    public static void ShowWindow()
    {
        GetWindow<CustomProfilerLogger>("Profiler Logger");
    }

    private void OnGUI()
    {
        GUILayout.Label("プロファイラーログ設定", EditorStyles.boldLabel);

        gcThresholdKb = EditorGUILayout.FloatField("GC閾値 (KB)", gcThresholdKb);
        timeThresholdMs = EditorGUILayout.FloatField("時間閾値 (ms)", timeThresholdMs);
        logFilePath = EditorGUILayout.TextField("出力先パス", logFilePath);

        if (isLogging)
        {
            if (GUILayout.Button("ログ記録を停止", GUILayout.Height(40)))
            {
                StopLogging();
            }
            GUILayout.Label("現在記録中です...", EditorStyles.helpBox);
        }
        else
        {
            if (GUILayout.Button("ログ記録を開始", GUILayout.Height(40)))
            {
                StartLogging();
            }
        }
    }

    private void StartLogging()
    {
        isLogging = true;
        lastProcessedFrame = ProfilerDriver.lastFrameIndex;
        EditorApplication.update += OnEditorUpdate;
        Debug.Log("プロファイラーのログ記録を開始しました。");

        // ディレクトリが存在しない場合は作成する
        string dir = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private void StopLogging()
    {
        isLogging = false;
        EditorApplication.update -= OnEditorUpdate;
        Debug.Log("プロファイラーのログ記録を停止しました。");
    }

    private void OnEditorUpdate()
    {
        if (!isLogging)
            return;

        int currentFrame = ProfilerDriver.lastFrameIndex;
        if (currentFrame <= lastProcessedFrame)
            return;

        // エディターが重くなるのを防ぐため、最大でも過去10フレーム分のみ処理する
        int startFrame = Mathf.Max(lastProcessedFrame + 1, currentFrame - 10);

        for (int i = startFrame; i <= currentFrame; i++)
        {
            ProcessFrame(i);
        }

        lastProcessedFrame = currentFrame;
    }

    private void ProcessFrame(int frameIndex)
    {
        // ProfilerDriver.GetHierarchyFrameDataViewを使用してメインスレッド(0)のデータを取得するように修正
        using (
            var frameData = ProfilerDriver.GetHierarchyFrameDataView(
                frameIndex,
                0,
                HierarchyFrameDataView.ViewModes.Default,
                HierarchyFrameDataView.columnTotalTime,
                false
            )
        )
        {
            if (frameData == null || !frameData.valid)
                return;

            int rootId = frameData.GetRootItemID();
            StringBuilder sb = new StringBuilder();
            bool hasLogged = false;

            // ツリーの走査を開始
            TraverseAndLog(frameData, rootId, 0, sb, ref hasLogged);

            if (hasLogged)
            {
                // 日付とフレーム番号をヘッダーとしてファイルに追記
                string header = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Frame: {frameIndex}\n";
                File.AppendAllText(logFilePath, header + sb.ToString() + "\n");
            }
        }
    }

    private void TraverseAndLog(
        HierarchyFrameDataView frameData,
        int itemId,
        int depth,
        StringBuilder sb,
        ref bool hasLogged
    )
    {
        string name = frameData.GetItemName(itemId);

        if (
            string.IsNullOrEmpty(name)
            || name.Contains("EditorLoop")
            || name.StartsWith("Profiler.")
            || name.StartsWith("GUI.")
            || name.StartsWith("Event.")
        )
        {
            return;
        }

        // 時間とGC Allocの取得（ProfilerColumn ではなく HierarchyFrameDataView.column... を使用するように修正）
        float totalTimeMs = frameData.GetItemColumnDataAsFloat(
            itemId,
            HierarchyFrameDataView.columnTotalTime
        );
        float gcAllocBytes = frameData.GetItemColumnDataAsFloat(
            itemId,
            HierarchyFrameDataView.columnGcMemory
        );
        float gcAllocKb = gcAllocBytes / 1024f;

        // 閾値を超えているか判定
        bool isHeavy = totalTimeMs >= timeThresholdMs || gcAllocKb >= gcThresholdKb;
        
        if (isHeavy)
        {
            hasLogged = true;

            // インデントとプレフィックスでツリー構造を見やすく整形
            string indent = new string(' ', depth * 2);
            string prefix = depth > 0 ? "└─ " : "";

            sb.AppendLine(
                $"{indent}{prefix}[GC: {gcAllocKb:F2} KB] [Time: {totalTimeMs:F2} ms] {name}"
            );
        }

        // 子要素も再帰的に走査して調査
        if (frameData.HasItemChildren(itemId))
        {
            List<int> children = new List<int>();
            frameData.GetItemChildren(itemId, children);
            foreach (int childId in children)
            {
                // 親が記録対象でなくても、子要素で重いものがあるかもしれないので深さを調整して再帰
                TraverseAndLog(frameData, childId, isHeavy ? depth + 1 : depth, sb, ref hasLogged);
            }
        }
    }
}

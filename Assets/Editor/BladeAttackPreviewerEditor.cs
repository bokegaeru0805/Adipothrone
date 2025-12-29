using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BladeAttackPreviewer))]
public class BladeAttackPreviewerEditor : Editor
{
    private bool isPlaying = false;
    private float playTime = 0f;
    private float lastFrameTime = 0f;

    public override void OnInspectorGUI()
    {
        BladeAttackPreviewer previewer = (BladeAttackPreviewer)target;

        // デフォルトのインスペクターを表示
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview Controls", EditorStyles.boldLabel);

        // --- シークバー ---
        EditorGUI.BeginChangeCheck();
        float newSeek = EditorGUILayout.Slider("Seek Frame", previewer.seekPosition, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            previewer.seekPosition = newSeek;
            // シークバーを動かしたら即座に反映
            previewer.UpdatePreview(previewer.seekPosition);
            // シーンビューを更新
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space();

        // --- 再生・停止ボタン ---
        if (!isPlaying)
        {
            if (GUILayout.Button("Play Animation", GUILayout.Height(30)))
            {
                StartPlaying(previewer);
            }
        }
        else
        {
            if (GUILayout.Button("Stop", GUILayout.Height(30)))
            {
                StopPlaying(previewer);
            }
        }
        
        // --- リセットボタン ---
        if (GUILayout.Button("Reset Position"))
        {
            previewer.ResetPreview();
            SceneView.RepaintAll();
        }
    }

    private void StartPlaying(BladeAttackPreviewer previewer)
    {
        if (previewer.actionData == null)
        {
            Debug.LogWarning("Action Dataが設定されていません");
            return;
        }

        isPlaying = true;
        playTime = 0f;
        lastFrameTime = (float)EditorApplication.timeSinceStartup;
        EditorApplication.update += OnEditorUpdate;
    }

    private void StopPlaying(BladeAttackPreviewer previewer)
    {
        isPlaying = false;
        EditorApplication.update -= OnEditorUpdate;
        previewer.ResetPreview();
    }

    // エディタの更新ループでアニメーションを進める
    private void OnEditorUpdate()
    {
        BladeAttackPreviewer previewer = (BladeAttackPreviewer)target;

        if (previewer == null || previewer.actionData == null)
        {
            StopPlaying(previewer);
            return;
        }

        float currentTime = (float)EditorApplication.timeSinceStartup;
        float deltaTime = currentTime - lastFrameTime;
        lastFrameTime = currentTime;

        // 全体の所要時間を、各ステップのattackTimeの合計値にする
        float totalDuration = 0f;
        foreach (var step in previewer.actionData.attackSteps)
        {
            totalDuration += step.attackTime;
        }
        
        if (totalDuration <= 0) totalDuration = 1f;

        playTime += deltaTime;
        
        // 正規化時間 (0~1)
        float normalizedTime = playTime / totalDuration;

        if (normalizedTime >= 1f)
        {
            // 再生終了
            normalizedTime = 1f;
            previewer.seekPosition = 1f;
            previewer.UpdatePreview(1f);
            StopPlaying(previewer);
        }
        else
        {
            // 更新
            previewer.seekPosition = normalizedTime;
            previewer.UpdatePreview(normalizedTime);
        }

        // インスペクターとシーンビューの再描画
        Repaint(); 
        SceneView.RepaintAll(); 
    }
    
    private void OnDisable()
    {
        // 選択が外れたら再生を止める
        if (isPlaying)
        {
            BladeAttackPreviewer previewer = (BladeAttackPreviewer)target;
            StopPlaying(previewer);
        }
    }
}
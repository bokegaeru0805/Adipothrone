using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 特定のシーン内にある NPCDialogueTrigger の conditions リストを一括で反転させるエディタ拡張
/// </summary>
public class NPCDialogueReverser : Editor
{
    // 処理対象のシーン名リスト
    private static readonly string[] TARGET_SCENES = new string[]
    {
        "TutorialStartScene",
        "Chapter1Scene",
        "DesertScene",
    };

    [MenuItem("Tools/NPC Dialogue/Reverse Conditions in Target Scenes")]
    public static void ReverseConditionsInTargetScenes()
    {
        // 実行前のセーフティチェック
        if (
            !EditorUtility.DisplayDialog(
                "最終確認",
                "指定された3つのシーンの NPCDialogueTrigger の条件リストをすべて反転させます。\n\n※実行前にGitでのコミットを強く推奨します。\n実行しますか？",
                "はい（実行する）",
                "いいえ（キャンセル）"
            )
        )
        {
            return;
        }

        // 現在開いているシーンの未保存の変更があれば保存を促す
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log("処理がキャンセルされました。");
            return;
        }

        int totalModifiedCount = 0;

        foreach (string sceneName in TARGET_SCENES)
        {
            // シーンのアセットパスを検索
            string[] guids = AssetDatabase.FindAssets("t:Scene " + sceneName);
            if (guids.Length == 0)
            {
                Debug.LogWarning($"シーン '{sceneName}' が見つかりませんでした。スキップします。");
                continue;
            }

            string scenePath = AssetDatabase.GUIDToAssetPath(guids[0]);

            // シーンを開く (単一モードで開くことで対象シーンの中身だけを確実に取得)
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Debug.Log($"--- {scene.name} を処理中 ---");

            // シーン内のすべてのNPCDialogueTriggerを取得（非アクティブなオブジェクトも含む）
            NPCDialogueTrigger[] triggers = Object.FindObjectsOfType<NPCDialogueTrigger>(true);

            int modifiedCountInScene = 0;

            foreach (var trigger in triggers)
            {
                // リフレクションを用いて private な dialogueConditions を取得
                FieldInfo fieldInfo = typeof(NPCDialogueTrigger).GetField(
                    "dialogueConditions",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (fieldInfo != null)
                {
                    // リストを取得
                    List<DialogueCondition> conditions =
                        fieldInfo.GetValue(trigger) as List<DialogueCondition>;

                    // 要素が2つ以上ある場合のみ反転処理を行う（1つ以下の場合は反転の意味がないため）
                    if (conditions != null && conditions.Count > 1)
                    {
                        conditions.Reverse();

                        // 変更があったことをUnityに通知
                        EditorUtility.SetDirty(trigger);
                        modifiedCountInScene++;
                    }
                }
            }

            // 変更があった場合のみシーンを保存
            if (modifiedCountInScene > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log(
                    $"{scene.name}: {modifiedCountInScene} 個のオブジェクトのリストを反転し、上書き保存しました。"
                );
                totalModifiedCount += modifiedCountInScene;
            }
            else
            {
                Debug.Log(
                    $"{scene.name}: 変換対象のオブジェクト（条件が2つ以上）はありませんでした。"
                );
            }
        }

        Debug.Log(
            $"\n=== 一括処理が完了しました！ ===\n合計 {totalModifiedCount} 個の NPCDialogueTrigger が更新されました。"
        );
    }
}

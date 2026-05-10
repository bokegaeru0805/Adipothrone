using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// エディタでのシーン再生中、指定した名前のオブジェクトが初期配置・または動的生成(Instantiate)された際に、
/// 自動的に表示状態(Active)にする監視スクリプト。
/// </summary>
[InitializeOnLoad]
public class AutoShowObjectsOnPlay
{
    // --- 対象となるオブジェクトの名前リスト ---
    private static readonly string[] TargetNames = new string[]
    {
        "MenuCanvas",
        "GameUICanvas",
        "SayDialog",
    };

    // 既に処理したオブジェクトのIDを記録し、ゲーム本来の表示切り替えロジックと競合するのを防ぐ
    private static HashSet<int> processedInstanceIDs = new HashSet<int>();

    static AutoShowObjectsOnPlay()
    {
        // 再生モードの切り替えイベント
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        // ヒエラルキー（オブジェクトの増減など）の変化イベント
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            processedInstanceIDs.Clear(); // 再生開始時に記録をリセット
            CheckAndShowObjects(); // 初期配置されているものをチェック
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            processedInstanceIDs.Clear(); // 念のため終了時にもリセット
        }
    }

    private static void OnHierarchyChanged()
    {
        // 実行中（Play Mode）に、他スクリプトによってInstantiate等が行われた瞬間にチェックする
        if (EditorApplication.isPlaying)
        {
            CheckAndShowObjects();
        }
    }

    private static void CheckAndShowObjects()
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            // シーンに配置されていないもの（プレハブ本体）や、システム用の隠しオブジェクトは除外
            if (
                string.IsNullOrEmpty(obj.scene.name)
                || (obj.hideFlags & HideFlags.HideAndDontSave) != 0
            )
            {
                continue;
            }

            // 既に表示チェック・処理を行ったインスタンスはスキップ（重くならないための工夫）
            if (processedInstanceIDs.Contains(obj.GetInstanceID()))
            {
                continue;
            }

            foreach (string targetName in TargetNames)
            {
                // 名前にキーワードが含まれているか
                if (obj.name.Contains(targetName))
                {
                    // 発見したオブジェクトを「処理済み」として記録
                    processedInstanceIDs.Add(obj.GetInstanceID());

                    // オブジェクトが画面上で非表示になっている場合のみ処理
                    if (!obj.activeInHierarchy)
                    {
                        // 自身から親へ遡り、非表示になっているものがあれば全て表示状態（true）にする
                        Transform current = obj.transform;
                        while (current != null)
                        {
                            if (!current.gameObject.activeSelf)
                            {
                                current.gameObject.SetActive(true);
                                // Debug.Log(
                                //     $"【自動表示】 '{current.gameObject.name}' を生成/検知時に自動表示しました。"
                                // );
                            }
                            current = current.parent;
                        }
                    }
                    break; // 1つのキーワードにヒットしたら次のオブジェクトへ
                }
            }
        }
    }
}

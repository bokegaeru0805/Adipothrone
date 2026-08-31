using NaughtyAttributes.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// DialogueUpdaterコンポーネントのInspectorの表示をカスタマイズするエディタ拡張クラス。
/// </summary>
[CustomEditor(typeof(DialogueUpdater))] // このエディタがどのクラスを対象にするかを指定
public class DialogueUpdaterEditor : NaughtyInspector
{
    /// <summary>
    /// InspectorのGUIを描画する際にUnityから呼び出されるメソッド。
    /// </summary>
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "重要設定: 動的立ち絵キャラクターはコードで管理されています。下部の一覧は確認用で、Inspectorからは変更できません。",
            MessageType.Info
        );

        // Flowchart、CSV、確認用の動的立ち絵キャラクター一覧を表示する
        base.OnInspectorGUI();

        // スペースを少し空けて、見た目を整える
        EditorGUILayout.Space();

        // --- ダイアログ更新ボタン ---
        // ボタンを描画する。if文で囲むことで、ボタンが押された瞬間に中身が実行される
        if (GUILayout.Button("CSVからダイアログを更新", GUILayout.Height(40)))
        {
            // ボタンが押されたら、UpdateDialogueメソッドを呼び出す
            ((DialogueUpdater)target).UpdateDialogue();
        }
    }

    /// <summary>
    /// ツールバー（Toolsメニュー）から、現在開いているシーン内のすべてのDialogueUpdaterを実行します。
    /// </summary>
    [MenuItem("Tools/MyGame/Scene/Update Dialogues From CSV")]
    public static void UpdateAllDialoguesFromToolbar()
    {
        // シーン内のすべてのDialogueUpdaterコンポーネントを取得
        DialogueUpdater[] updaters = FindObjectsOfType<DialogueUpdater>();

        if (updaters.Length == 0)
        {
            Debug.LogWarning(
                "現在のシーン内に DialogueUpdater コンポーネントが見つかりませんでした。"
            );
            return;
        }

        // 見つかったすべてのDialogueUpdaterに対して更新処理を実行
        int count = 0;
        foreach (DialogueUpdater updater in updaters)
        {
            updater.UpdateDialogue();
            count++;
        }

        // 結果のログ表示
        Debug.Log($"シーン内の {count} 個の DialogueUpdater の更新処理を一括実行しました。");
    }
}

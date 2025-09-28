using Fungus;
using UnityEngine;

/// <summary>
/// SaveLoadManagerを呼び出してオートセーブを実行します。
/// </summary>
[CommandInfo(
    "Custom", // コマンドのカテゴリ名
    "Auto Save", // コマンド名
    "現在のゲームの進行状況をオートセーブします。重要な会話の途中などで使用します。" // コマンドの説明
)]
[AddComponentMenu("")]
public class AutoSaveCommand : Command
{
    /// <summary>
    /// このコマンドが実行されたときに呼び出されるメインの処理です。
    /// </summary>
    public override void OnEnter()
    {
        // SaveLoadManagerのインスタンスが存在するか確認
        if (SaveLoadManager.instance != null)
        {
            // オートセーブ処理を呼び出す
            SaveLoadManager.instance.ExecuteAutoSave();
        }
        else
        {
            Debug.LogError(
                "SaveLoadManagerが見つからないため、オートセーブを実行できませんでした。"
            );
        }

        // 次のコマンドへ処理を移す
        Continue();
    }

    /// <summary>
    /// Fungusエディタのコマンドに表示される要約テキストを返します。
    /// </summary>
    public override string GetSummary()
    {
        return "オートセーブを実行";
    }

    /// <summary>
    /// Fungusエディタでのコマンドの色を返します。
    /// </summary>
    public override Color GetButtonColor()
    {
        // セーブの成功やデータ保存のイメージに合う、薄い緑色に設定
        return new Color32(191, 235, 191, 255);
    }
}

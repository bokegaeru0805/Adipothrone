using Fungus;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 指定されたタイトルシーンへ移行します。
/// </summary>
[CommandInfo(
    "Custom", // コマンドのカテゴリ名
    "ReturnToTitle", // コマンド名
    "タイトルシーンをロードします。また、自動的にフェードアウトを開始します。" // コマンドの説明
)] // コマンドの説明
[AddComponentMenu("")]
public class ReturnToTitle : Command
{
    /// <summary>
    /// このコマンドが実行されたときに呼び出されるメインの処理です。
    /// </summary>
    public override void OnEnter()
    {
        // 指定された名前のシーンをロードする
        SceneManager.LoadScene(GameConstants.SceneName_Title);

        FadeCanvas.instance.FadeIn(1f); // フェードアウトを開始

        // 注意：LoadSceneが実行されると現在のシーンは破棄されるため、
        // この後にContinue()を呼び出す必要はありません。
    }

    /// <summary>
    /// Fungusエディタのコマンドに表示される要約テキストを返します。
    /// </summary>
    public override string GetSummary()
    {
        return "シーンへ移行: " + GameConstants.SceneName_Title;
    }

    /// <summary>
    /// Fungusエディタでのコマンドの色を返します。
    /// </summary>
    public override Color GetButtonColor()
    {
        // Fungus標準のシーン制御系コマンドと同じ色（薄いピンク色）に設定
        return new Color32(235, 191, 217, 255);
    }
}

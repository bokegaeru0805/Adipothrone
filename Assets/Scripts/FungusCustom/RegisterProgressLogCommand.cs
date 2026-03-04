using Fungus;
using UnityEngine;

[CommandInfo("Custom", "Register Progress Log", "ゲームの進行度ログをセーブデータに登録します。")]
public class RegisterProgressLogCommand : Command
{
    [Tooltip("登録したい進行度を選択してください")]
    [SerializeField]
    private ProgressLogName progressLogName; // インスペクター上でEnumのプルダウンになる

    // このコマンドがFungusで実行されたときに呼ばれる処理
    public override void OnEnter()
    {
        // GameManager経由でセーブデータにEnumを渡して登録する
        if (GameManager.instance != null && GameManager.instance.savedata != null)
        {
            GameManager.instance.savedata.ProgressLogData.RegisterProgressData(progressLogName);
        }
        else
        {
            Debug.LogError("GameManager またはセーブデータが見つかりません。");
        }

        // 次のFungusコマンドへ処理を進める（必須）
        Continue();
    }

    // Fungusのブロック上に表示される要約テキスト（見やすさのため）
    public override string GetSummary()
    {
        return $"進行度: {progressLogName} を登録";
    }

    public override Color GetButtonColor()
    {
        return new Color32(255, 235, 150, 255);
    }
}

using Fungus;
using UnityEngine;

/// <summary>
/// フィールドに関するSEを再生するコマンド
/// </summary>
[CommandInfo("SE", "Play FieldSE", "フィールドに関するSEを再生します")]
public class PlayFieldSECommand : Command
{
    [Tooltip("流すSE")]
    public SE_Field FieldSE;

    public override void OnEnter()
    {
        if (SEManager.instance != null)
        {
            SEManager.instance.PlayFieldSE(FieldSE);
        }
        else
        {
            Debug.LogError("SEManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override string GetSummary()
    {
        return $"流すSEは {FieldSE}";
    }
}

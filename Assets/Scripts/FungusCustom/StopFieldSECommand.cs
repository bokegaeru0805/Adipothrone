using Fungus;
using UnityEngine;

/// <summary>
/// フィールドに関するSEを停止するコマンド
/// </summary>
[CommandInfo("SE", "Stop FieldSE", "フィールドに関するSEを停止します")]
public class StopFieldSECommand : Command
{
    [Tooltip("止めるSE")]
    public SE_Field FieldSE;

    public override void OnEnter()
    {
        if (SEManager.instance != null)
        {
            SEManager.instance.StopFieldSE(FieldSE);
        }
        else
        {
            Debug.LogError("SEManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override string GetSummary()
    {
        return $"止めるSEは {FieldSE}";
    }
}

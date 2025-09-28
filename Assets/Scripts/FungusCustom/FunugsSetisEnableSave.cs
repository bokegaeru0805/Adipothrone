using Fungus;
using UnityEngine;

// --------------------------------
// セーブできるかどうかを決定するコマンド
// --------------------------------
[CommandInfo("Custom", "Set isEnableSave", "セーブできるかどうかを決定するコマンド")]
public class FungusSetisEnableSave : Command
{
    [Tooltip("セーブできるかどうか")]
    public bool isEnableSave = true; // セーブできるかどうか

    public override void OnEnter()
    {
        if (SaveLoadManager.instance != null)
        {
            if (isEnableSave)
            {
                SaveLoadManager.instance.EnableSave(); // セーブできるようにする
            }
            else
            {
                SaveLoadManager.instance.DisableSave(); // セーブできないようにする
            }
        }
        else
        {
            Debug.LogError("SaveLoadManagerのインスタンスが見つかりません！");
        }

        Continue();
    }

    public override string GetSummary()
    {
        if (isEnableSave)
        {
            return "セーブ可能(Enable)にする";
        }
        else
        {
            return "セーブ不可能(Disable)にする";
        }
    }
}

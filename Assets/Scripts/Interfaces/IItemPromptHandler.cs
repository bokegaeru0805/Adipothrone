using System;
using UnityEngine.UI;

/// <summary>
/// アイテム使用確認プロンプトを開く機能を持つパネルに実装するインターフェース
/// </summary>
public interface IItemPromptHandler
{
    void SetPromptPanel(Enum itemID, Button selectedButton);
}
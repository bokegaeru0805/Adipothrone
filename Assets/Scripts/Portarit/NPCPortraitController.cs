using Fungus;
using UnityEngine;

/// <summary>
/// 汎用NPCの立ち絵を管理するコントローラー。
/// BasePortraitControllerを継承しています。
/// </summary>
public class NPCPortraitController : BasePortraitController
{
    // 今後、NPC専用の「立ち絵表示イベント（例えば FungusCustomSignals.OnRequestNPCPortrait 等）」
    // を作成した場合は、ここでイベントを購読し、base.ShowPortrait() を呼び出します。
    
    // 例:
    // protected override void Start()
    // {
    //     base.Start();
    //     FungusCustomSignals.OnRequestNPCPortrait += HandleNPCShowRequest;
    // }
    //
    // private void HandleNPCShowRequest(string targetName, string body, string face, string expression)
    // {
    //     if (targetName == characterName)
    //     {
    //         ShowPortrait(body, face, expression);
    //     }
    // }
}
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "NewShop", menuName = "Shops/ShopData")]
public class ShopData : ScriptableObject
{
    public ShopName shopID; // 店のID
    public string shopName; // 店の表示名
    public BaseItemData[] shopItems; // 売っているアイテムのリスト

    [Header("デフォルトの始めの会話")]
    [Tooltip("どの条件にも一致しなかった場合に表示される会話。")]
    [TextArea(3, 5)]
    public string defaultStartingDialogue;

    [Header("デフォルトの終わりの会話")]
    [Tooltip("どの条件にも一致しなかった場合に表示される会話。")]
    [TextArea(3, 5)]
    public string defaultEndingDialogue;

    [Header("開始時の会話")]
    [Tooltip("店の始めの会話リスト。上から順に評価され、最初に条件が一致したものが使われます。")]
    public List<ConditionalDialogue> startingDialogues;

    [Header("終了時の会話")]
    [Tooltip("店の終わりの会話リスト。上から順に評価され、最初に条件が一致したものの候補からランダムで選ばれます。")]
    public List<ConditionalDialogue> endingDialogues;

    /// <summary>
    /// 現在のフラグ状態に応じた「始めの会話」を取得します。
    /// </summary>
    /// <returns>表示すべき会話テキスト</returns>
    public string GetStartingDialogue()
    {
        //共通化されたメソッドに、開始時の会話リストと、デフォルトの開始会話を渡す
        return GetDialogueFromList(startingDialogues, defaultStartingDialogue);
    }

    /// <summary>
    /// 現在のフラグ状態に応じた「終わりの会話」を取得します。
    /// </summary>
    /// <returns>表示すべき会話テキスト</returns>
    public string GetEndingDialogue()
    {
        // 共通化されたメソッドに、終了時の会話リストと、デフォルトの終了会話を渡す
        return GetDialogueFromList(endingDialogues, defaultEndingDialogue);
    }

    /// <summary>
    /// 条件付き会話リストを評価し、適切なセリフを1つ返します。（★ ロジックを共通化・最適化）
    /// </summary>
    /// <param name="dialogueList">評価する会話リスト</param>
    /// <param name="defaultDialogue">どの条件にも一致しなかった場合に使う会話</param>
    /// <returns>表示すべき会話テキスト</returns>
    private string GetDialogueFromList(List<ConditionalDialogue> dialogueList, string defaultDialogue)
    {
        // LINQを使い、条件を満たす最初の会話セットを検索する
        var validDialogueSet = dialogueList.FirstOrDefault(dialogue => dialogue.AreConditionsMet());

        // 条件を満たす会話セットが見つかった場合
        if (validDialogueSet != null && validDialogueSet.dialogueOptions != null && validDialogueSet.dialogueOptions.Count > 0)
        {
            // セリフ候補の中からランダムで1つ選んで返す
            int randomIndex = Random.Range(0, validDialogueSet.dialogueOptions.Count);
            return validDialogueSet.dialogueOptions[randomIndex];
        }

        // どの条件にも一致しなかった場合、引数で渡されたデフォルトの会話を返す
        if (!string.IsNullOrEmpty(defaultDialogue))
        {
            return defaultDialogue;
        }

        // デフォルトの会話も設定されていない場合の最終的な返答
        return "......";
    }
}
using System.Collections.Generic;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// ショップで販売するアイテムをインスペクターで見やすくするためのラッパークラス
/// </summary>
[System.Serializable]
public class ShopItemEntry
{
    [HideInInspector]
    public string _inspectorLabel; // インスペクター表示用の隠し変数

    [Tooltip("販売するアイテムのデータ")]
    public BaseItemData item;
}

[CreateAssetMenu(fileName = "NewShop", menuName = "Shops/ShopData")]
public class ShopData : ScriptableObject
{
    #region エディタ表示用
    [Tooltip("ショップの概要（自動更新）")]
    [ReadOnly]
    public string shopSummaryLabel; // インスペクター確認用のラベル
    #endregion

    public ShopName shopID; // 店のID
    public string shopName; // 店の表示名

    [Header("販売アイテムリスト")]
    public List<ShopItemEntry> shopItems = new List<ShopItemEntry>();

    [Header("デフォルトの始めの会話")]
    [Tooltip("どの条件にも一致しなかった場合に表示される会話。")]
    [TextArea(3, 5)]
    public string defaultStartingDialogue;

    [Header("デフォルトの終わりの会話")]
    [Tooltip("どの条件にも一致しなかった場合に表示される会話。")]
    [TextArea(3, 5)]
    public string defaultEndingDialogue;

    [Header("開始時の会話")]
    [Tooltip(
        "店の始めの会話リスト。下から順（逆順）に評価され、最初に条件が一致したものが使われます。"
    )]
    public List<ConditionalDialogue> startingDialogues;

    [Header("終了時の会話")]
    [Tooltip(
        "店の終わりの会話リスト。下から順（逆順）に評価され、最初に条件が一致したものの候補からランダムで選ばれます。"
    )]
    public List<ConditionalDialogue> endingDialogues;

    #region エディタ専用処理
#if UNITY_EDITOR
    /// <summary>
    /// インスペクター上で値が変更された際に自動的に呼ばれるメソッド。
    /// ショップの概要や、各会話リストのプレビューラベルを更新します。
    /// </summary>
    private void OnValidate()
    {
        // 1. ショップ概要のラベル更新 (Listになったため .Count を使用)
        int itemCount = shopItems != null ? shopItems.Count : 0;
        string nameLabel = string.IsNullOrEmpty(shopName) ? "名前未設定" : shopName;
        shopSummaryLabel = $"【{nameLabel}】 販売アイテム: {itemCount}種";

        // 販売アイテムリストのラベル更新処理
        if (shopItems != null)
        {
            foreach (var entry in shopItems)
            {
                if (entry == null)
                    continue;

                if (entry.item != null)
                {
                    // // 例: "回復薬 (買値: 100G)" のように表示させる
                    // entry._inspectorLabel = $"{entry.item.itemName} (買値: {entry.item.buyPrice}G)";
                    // 例: "回復薬" のようにアイテム名だけ表示させる
                    entry._inspectorLabel = $"{entry.item.itemName}";
                }
                else
                {
                    entry._inspectorLabel = "未設定 (Empty)";
                }
            }
        }

        // 2. 会話リストのラベル更新処理を呼び出す
        UpdateDialogueLabels(startingDialogues);
        UpdateDialogueLabels(endingDialogues);
    }

    /// <summary>
    /// 条件付き会話リストの要素名に、セリフのプレビューを反映させます。
    /// </summary>
    private void UpdateDialogueLabels(List<ConditionalDialogue> dialogueList)
    {
        if (dialogueList == null)
            return;

        foreach (var diag in dialogueList)
        {
            if (diag == null)
                continue;

            int optionCount = diag.dialogueOptions != null ? diag.dialogueOptions.Count : 0;
            string previewText = "未設定 (Empty)";

            if (optionCount > 0 && !string.IsNullOrEmpty(diag.dialogueOptions[0]))
            {
                // 最初のセリフを取得し、長すぎる場合は10文字でカットして「...」を付ける
                previewText = diag.dialogueOptions[0];
                if (previewText.Length > 10)
                {
                    previewText = previewText.Substring(0, 10) + "...";
                }
            }

            // インスペクター上のリスト要素名を更新（例: "[候補3つ] いらっしゃい..."）
            diag._inspectorLabel = $"[候補{optionCount}つ] {previewText}";
        }
    }
#endif
    #endregion

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
    /// 条件付き会話リストを評価し、適切なセリフを1つ返します。
    /// </summary>
    /// <param name="dialogueList">評価する会話リスト</param>
    /// <param name="defaultDialogue">どの条件にも一致しなかった場合に使う会話</param>
    /// <returns>表示すべき会話テキスト</returns>
    private string GetDialogueFromList(
        List<ConditionalDialogue> dialogueList,
        string defaultDialogue
    )
    {
        // リストを下から順（新しい/進行度が高い条件）に評価する
        for (int i = dialogueList.Count - 1; i >= 0; i--)
        {
            var dialogueSet = dialogueList[i];

            if (dialogueSet.AreConditionsMet())
            {
                // 条件を満たす会話セットが見つかった場合
                if (dialogueSet.dialogueOptions != null && dialogueSet.dialogueOptions.Count > 0)
                {
                    // セリフ候補の中からランダムで1つ選んで返す
                    int randomIndex = Random.Range(0, dialogueSet.dialogueOptions.Count);
                    return dialogueSet.dialogueOptions[randomIndex];
                }
            }
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

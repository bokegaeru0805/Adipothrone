using Fungus;
using UnityEngine;

public class ShopConversation : MonoBehaviour, IShopConversation
{
    [SerializeField]
    private Flowchart targetFlowchart;
    private FlagManager flagManager = null;
    private ShopName shopName = ShopName.None; // デフォルトのショップ名

    private void Awake()
    {
        if (targetFlowchart == null)
        {
            Debug.LogError("ShopConversationにFlowchartが設定されていません。", this);
            return;
        }
    }

    private void Start()
    {
        // FlagManagerのインスタンスを取得
        flagManager = FlagManager.instance;
        if (flagManager == null)
        {
            Debug.LogWarning(
                "FlagManager.instance が見つかりません。ShopConversation_Prologueの機能が制限される可能性があります。"
            );
        }
    }

    public void StartShopConversation(ShopName shopID)
    {
        shopName = shopID;
        TryExecuteDialogue();
    }

    /// <summary>
    /// 設定された会話条件を評価し、適切なFungusブロックを実行する。
    /// </summary>
    private void TryExecuteDialogue()
    {
        if (targetFlowchart == null)
        {
            Debug.LogError("ターゲットのFlowchartが設定されていません。", this);
            return;
        }

        if (shopName == ShopName.None)
        {
            Debug.LogError("ショップの名前が設定されていません。", this);
            return;
        }

        FungusHelper.ExecuteBlock(targetFlowchart, GetBlockNameByShop(shopName));
    }

    private string GetBlockNameByShop(ShopName shop)
    {
        if (flagManager == null)
        {
            flagManager = FlagManager.instance;
            if (flagManager == null)
            {
                Debug.LogWarning(
                    "FlagManager.instance が見つかりません。デフォルトの会話ブロック名を返します。",
                    this
                );
                return GameConstants.DefaultNpcDialogueBlockName; // デフォルトの会話ブロック名を返す
            }
        }

        switch (shop)
        {
            case ShopName.VillageGirl_Shop:
                return GetShopGirlDialogueBlockName();
            default:
                // 未定義のショップ名に対するデフォルト処理
                Debug.LogWarning(
                    $"未定義のショップ名: {shop} です。デフォルトの会話ブロック名を返します。",
                    this
                );
                return GameConstants.DefaultNpcDialogueBlockName; // デフォルトの会話ブロック名を返す
        }
    }

    // ショップの女の子専用の会話ブロック名を取得するメソッド
    private string GetShopGirlDialogueBlockName()
    {
        // FlagManagerが初期化されていない場合の対応（念のため）
        if (flagManager == null)
            flagManager = FlagManager.instance;

        // FlagManagerがない場合のフォールバック
        if (flagManager == null)
            return "Village_ShopGirl_Default";

        if (flagManager.GetBoolFlag(Chapter1TriggeredEvent.HeardRumorAboutShopGirl))
        {
            return "Village_ShopGirl_RiverQuestCompleted";
        }
        if (flagManager.GetBoolFlag(Chapter1TriggeredEvent.UpperRiverReached))
        {
            return "Village_ShopGirl_ArrivedUpstream";
        }
        else if (flagManager.GetBoolFlag(Chapter1TriggeredEvent.WellQuestComplete))
        {
            return "Village_ShopGirl_CompletedWellQuest";
        }

        return "Village_ShopGirl_Default";
    }
}

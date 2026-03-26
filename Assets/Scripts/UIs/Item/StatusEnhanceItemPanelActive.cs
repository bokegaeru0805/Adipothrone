using System;
using UnityEngine;
using UnityEngine.UI;

#region 派生クラス：ステータス強化アイテムパネル
/// <summary>
/// ステータス強化アイテムを管理するパネルクラス
/// アイテム使用時のプロンプト表示（Standardモード）を実装しています。
/// </summary>
public class StatusEnhanceItemPanelActive : ItemPanelActiveBase, IItemPromptHandler
{
    #region シリアライズフィールド
    [Header("アイテム使用確認パネル")]
    [Tooltip("アイテム使用確認パネルの位置調整用オフセット")]
    [SerializeField]
    private Vector2 offset = Vector2.zero;

    [Tooltip("アイテム使用確認パネル全体を管理するスクリプト")]
    [SerializeField]
    private ItemUsePromptPanel itemUsePromptPanel;
    #endregion

    #region 基底クラスの実装
    /// <summary>
    /// このパネルが扱うアイテムタイプを指定します
    /// </summary>
    protected override InventoryItemData.ItemType TargetItemType =>
        InventoryItemData.ItemType.StatusEnhanceItem;

    /// <summary>
    /// 初期選択完了時に呼ばれるフック処理。使用確認パネルを非表示にします。
    /// </summary>
    protected override void OnSelectFirstButtonFinished()
    {
        if (itemUsePromptPanel != null && itemUsePromptPanel.gameObject.activeSelf)
        {
            itemUsePromptPanel.gameObject.SetActive(false); //アイテム使用確認パネルを非表示化
        }
    }
    #endregion

    #region 固有メソッド (IItemPromptHandler)
    /// <summary>
    /// アイテム使用の確認パネルを表示します。
    /// </summary>
    public void SetPromptPanel(Enum itemID, Button selectedButton)
    {
        if (!gameObject.activeSelf || selectedButton == null)
            return;

        // ポップアップを開く直前に、現在の選択状態を確実に記憶する
        lastSelectedItemID = EnumIDUtility.ToID(itemID);
        lastSelectedIndex = buttonList.IndexOf(selectedButton);

        if (itemUsePromptPanel != null)
        {
            // まず、クリックされたボタンのワールド座標を取得
            Vector3 buttonWorldPosition = selectedButton.transform.position;

            // offsetをコピーして、変更があっても元の値に影響しないようにする
            Vector2 finalOffset = offset;

            // もしクリックされたボタンが「右側のボタンリスト」に含まれていたら
            if (rightSideButtonList.Contains(selectedButton))
            {
                // offsetのx座標の正負を反転させる
                finalOffset.x *= -1;
            }

            // 最終的なoffsetを使ってパネルの位置を決定
            RectTransform promptRect = itemUsePromptPanel.GetComponent<RectTransform>();

            // 1. パネルの中心を、ボタンのワールド座標にピタッと合わせる
            promptRect.position = buttonWorldPosition;

            // 2. その状態から、インスペクターで設定した offset 分だけローカル座標でズラす
            promptRect.anchoredPosition += finalOffset;

            // パネルにアイテムIDを渡して、内容を更新する (Standardモード)
            itemUsePromptPanel.SetupPrompt(itemID, ItemUsePromptPanel.PromptMode.Standard);

            UIManager.instance.OpenPopup(itemUsePromptPanel.gameObject); //ポップアップとして開く
        }
    }
    #endregion

    #region 将来拡張用
    /// <summary>
    /// 【将来拡張用】所持しているステータス強化アイテムを一括で使用する
    /// 別のボタンコンポーネント等から呼び出されることを想定
    /// </summary>
    public void OnClickUseAllItems()
    {
        Debug.Log("ステータス強化アイテムの一括使用処理が呼ばれました");

        // TODO: 所持しているステータス強化アイテムを全て消費し、ステータスに反映する処理を実装
        // PlayerManager.instance.UseAllStatusEnhanceItems(itemList); のようなイメージ

        // 使用後、リストとUIをリフレッシュしてフォーカスを当て直す
        SelectFirstButton();
    }
    #endregion
}
#endregion

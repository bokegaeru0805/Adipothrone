using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 各ヒントボタンにアタッチされ、選択された際に自身の情報を親パネルに伝えるヘルパー。
/// </summary>
public class TipsButtonHelper : MonoBehaviour, ISelectHandler
{
    [Header("UIパーツ")]
    [Tooltip("「New」と表示するためのImageコンポーネント")]
    [SerializeField]
    private Image newIcon;
    private TipsPanelActive panelController;
    private TipsInfoData associatedTipsInfo;
    private TipsDataEntry associatedTipsDataEntry;

    /// <summary>
    /// 親パネルから、自身が担当するヒント情報を受け取り、初期化する。
    /// </summary>
    public void Initialize(TipsPanelActive controller, TipsInfoData data, TipsDataEntry dataEntry)
    {
        panelController = controller;
        associatedTipsInfo = data;
        associatedTipsDataEntry = dataEntry;

        // 新規フラグに応じて「New」アイコンの表示を切り替える
        if (newIcon != null)
        {
            newIcon.enabled = associatedTipsDataEntry.isNew;
        }
        else
        {
            Debug.LogWarning("TipsボタンにNewアイコンが設定されていません。", this);
        }
    }

    /// <summary>
    /// このボタンがEventSystemによって選択されたときに呼び出される。
    /// </summary>
    public void OnSelect(BaseEventData eventData)
    {
        // 親パネルに、自分のヒント情報を表示するよう依頼する
        panelController.DisplayTips(associatedTipsInfo);

        // もしこのTipsが新規(isNew)なら、フラグを更新して保存する
        if (associatedTipsDataEntry != null && associatedTipsDataEntry.isNew)
        {
            // セーブデータ側のフラグを更新
            GameManager.instance.savedata.TipsData.MarkAsRead(associatedTipsDataEntry.TipsID);

            // 自身のフラグも更新
            associatedTipsDataEntry.isNew = false;
        }
    }
}
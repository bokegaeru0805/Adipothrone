using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 各敵ボタンにアタッチされ、選択時に自身の情報を親パネルに伝えるヘルパー
/// </summary>
public class EnemyDexButtonHelper : MonoBehaviour, ISelectHandler
{
    [SerializeField] private Image newIcon;
    private EnemyDexPanelActive panelController;
    private EnemyData associatedEnemyData;
    private EnemyRecordEntry associatedSaveEntry;

    public void Initialize(EnemyDexPanelActive controller, EnemyData masterData, EnemyRecordEntry saveEntry)
    {
        panelController = controller;
        associatedEnemyData = masterData;
        associatedSaveEntry = saveEntry;

        if (newIcon != null)
        {
            newIcon.enabled = associatedSaveEntry.isNew;
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        // 親パネルに詳細表示を依頼
        // 討伐数の情報(associatedSaveEntry)も一緒に渡す
        panelController.DisplayEnemyDetails(associatedEnemyData, associatedSaveEntry);


        // 新規(isNew)なら、フラグを更新して保存
        if (associatedSaveEntry != null && associatedSaveEntry.isNew)
        {
            GameManager.instance.savedata.EnemyRecordData.MarkAsSeen(associatedSaveEntry.enemyIdValue);
            associatedSaveEntry.isNew = false; // 表示上のフラグも更新
            if (newIcon != null) newIcon.enabled = false;
        }
    }
}
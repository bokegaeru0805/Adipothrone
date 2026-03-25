using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 敵図鑑パネルの挙動を制御するクラス
/// </summary>
public class EnemyDexPanelActive : MonoBehaviour, IPanelActive
{
    [Header("敵リスト関連")]
    [SerializeField]
    private GameObject[] enemyButtons; // 敵を選択するためのボタン配列

    [SerializeField]
    private EnemyDatabase enemyDatabase; // 敵のマスターデータ

    [Header("敵詳細表示エリア")]
    [SerializeField]
    private TextMeshProUGUI enemyNameText;

    [SerializeField]
    private Image enemyImage;

    // [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField]
    private TextMeshProUGUI statsText; // レベル, HP, EXP, Moneyなどを表示

    [SerializeField]
    private TextMeshProUGUI dropItemsText; // ドロップアイテム一覧

    [Header("空の状態の表示")]
    [SerializeField]
    private GameObject detailGroup; // 詳細表示エリアの親オブジェクト

    [SerializeField]
    private GameObject emptyPanel; // 何も登録されていない時に表示するパネル

    /// <summary>
    /// ページめくりがどの入力で行われたかを判別するための種類
    /// </summary>
    private enum PageChangeType
    {
        Horizontal, // 左右キーによる入力
        VerticalUp, // 上キーによる入力
        VerticalDown // 下キーによる入力
        ,
    }

    // 内部クラスと変数
    private class UnlockedEnemy
    {
        public EnemyData MasterData { get; set; } // EnemyDataそのもの
        public EnemyRecordEntry SaveEntry { get; set; } // セーブデータ内の記録
    }

    private InputManager inputManager;
    private List<UnlockedEnemy> allUnlockedEnemies;
    private List<EnemyDexButtonHelper> buttonHelpers;
    private float baseSize = 0; // 敵の画像のベースサイズ（初期化時に設定）
    private int currentTopIndex = 0;
    private int itemsPerPage;
    private int totalPages;
    private GameObject topButton;
    private GameObject previousSelected;

    private void Awake()
    {
        itemsPerPage = enemyButtons.Length;
        buttonHelpers = new List<EnemyDexButtonHelper>();
        foreach (var button in enemyButtons)
        {
            var helper =
                button.GetComponent<EnemyDexButtonHelper>()
                ?? button.AddComponent<EnemyDexButtonHelper>();
            buttonHelpers.Add(helper);
        }

        // 敵の画像のベースサイズを取得
        RectTransform rectTransform = enemyImage.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            baseSize = rectTransform.sizeDelta.x; // 横幅をベースサイズとして使用
        }
        else
        {
            Debug.LogWarning("敵画像のRectTransformが取得できませんでした。");
        }

        topButton = enemyButtons[0];
    }

    private void Start()
    {
        inputManager = InputManager.instance;
    }

    private void OnEnable()
    {
        currentTopIndex = 0;
        SelectFirstButton();
    }

    private void Update()
    {
        if (inputManager == null)
            return;

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        if (selectedObject == null)
            return;

        // 選択が前回と変わったフレームは、入力処理をスキップ
        if (selectedObject != previousSelected)
        {
            previousSelected = selectedObject;
            return;
        }

        // --- ページめくり入力の判定 ---
        if (inputManager.UIMoveRight())
        {
            ChangePage(1, PageChangeType.Horizontal);
            return;
        }

        if (inputManager.UIMoveLeft())
        {
            ChangePage(-1, PageChangeType.Horizontal);
            return;
        }

        // --- 上下キーでのページ循環 ---
        int visibleItemCount = Mathf.Min(itemsPerPage, allUnlockedEnemies.Count - currentTopIndex);
        if (visibleItemCount <= 0)
            return;

        GameObject lastVisibleButton = enemyButtons[visibleItemCount - 1];

        if (inputManager.UIMoveDown() && selectedObject == lastVisibleButton)
        {
            ChangePage(1, PageChangeType.VerticalDown);
        }
        else if (inputManager.UIMoveUp() && selectedObject == topButton)
        {
            ChangePage(-1, PageChangeType.VerticalUp);
        }
    }

    public void SelectFirstButton()
    {
        LoadAllUnlockedEnemies();
        UpdateEnemyListPage();
    }

    private void LoadAllUnlockedEnemies()
    {
        allUnlockedEnemies = new List<UnlockedEnemy>();
        var enemyRecordData = GameManager.instance.savedata.EnemyRecordData;

        // 1. EnemyDatabaseに登録されている全ての敵リストを取得する（これが表示順の基準になる）
        foreach (var masterData in enemyDatabase.enemies)
        {
            // 2. その敵が遭遇済みかどうかをセーブデータで確認する
            if (enemyRecordData.IsEncountered(masterData.enemyID))
            {
                // 3. 遭遇済み、かつ「図鑑に表示する」設定の敵だけをリストに追加する
                if (masterData.isListedInDex)
                {
                    // 遭遇データなどのセーブデータも取得
                    var saveEntry = enemyRecordData.enemyRecords.Find(e =>
                        e.enemyIdValue == (int)masterData.enemyID
                    );

                    // 表示用のリストに追加
                    allUnlockedEnemies.Add(
                        new UnlockedEnemy { MasterData = masterData, SaveEntry = saveEntry }
                    );
                }
            }
        }

        // 総ページ数を計算
        totalPages =
            (allUnlockedEnemies.Count > 0) ? (allUnlockedEnemies.Count - 1) / itemsPerPage + 1 : 1;
    }

    private void UpdateEnemyListPage()
    {
        int loopCount = Mathf.Min(itemsPerPage, allUnlockedEnemies.Count - currentTopIndex);

        for (int i = 0; i < itemsPerPage; i++)
        {
            if (i < loopCount)
            {
                int enemyIndex = currentTopIndex + i;
                UnlockedEnemy unlockedEnemy = allUnlockedEnemies[enemyIndex];

                // 討伐数が0（遭遇のみ）の場合は名前を隠す
                if (unlockedEnemy.SaveEntry.killCount == 0)
                {
                    enemyButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = "未登録";
                }
                else
                {
                    enemyButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = unlockedEnemy
                        .MasterData
                        .enemyName;
                }

                buttonHelpers[i]
                    .Initialize(this, unlockedEnemy.MasterData, unlockedEnemy.SaveEntry);
                enemyButtons[i].SetActive(true);
            }
            else
            {
                enemyButtons[i].SetActive(false);
            }
        }

        if (loopCount > 0)
        {
            detailGroup.SetActive(true);
            emptyPanel.SetActive(false);
            EventSystem.current.SetSelectedGameObject(enemyButtons[0]);

            // ページ更新時にも討伐数データを渡す
            UnlockedEnemy firstEnemy = allUnlockedEnemies[currentTopIndex];
            DisplayEnemyDetails(firstEnemy.MasterData, firstEnemy.SaveEntry);
        }
        else
        {
            detailGroup.SetActive(false);
            emptyPanel.SetActive(true);
        }
    }

    /// <summary>
    /// ページを切り替える
    /// </summary>
    private void ChangePage(int direction, PageChangeType changeType)
    {
        if (totalPages <= 1)
            return;

        GameObject lastSelected = EventSystem.current.currentSelectedGameObject;
        int lastSelectedIndex =
            (lastSelected != null) ? System.Array.IndexOf(enemyButtons, lastSelected) : -1;

        currentTopIndex += itemsPerPage * direction;

        // --- インデックスの循環処理 ---
        if (currentTopIndex >= allUnlockedEnemies.Count)
        {
            currentTopIndex = 0;
        }
        else if (currentTopIndex < 0)
        {
            currentTopIndex = (totalPages - 1) * itemsPerPage;
        }

        UpdateEnemyListPage();

        // --- 入力の種類に応じて、フォーカスを合わせるボタンを制御 ---
        switch (changeType)
        {
            case PageChangeType.Horizontal:
                if (lastSelectedIndex != -1)
                {
                    int newVisibleCount = Mathf.Min(
                        itemsPerPage,
                        allUnlockedEnemies.Count - currentTopIndex
                    );
                    if (lastSelectedIndex < newVisibleCount)
                    {
                        EventSystem.current.SetSelectedGameObject(enemyButtons[lastSelectedIndex]);
                    }
                }
                break;
            case PageChangeType.VerticalDown:
                EventSystem.current.SetSelectedGameObject(topButton);
                break;
            case PageChangeType.VerticalUp:
                int visibleCount = Mathf.Min(
                    itemsPerPage,
                    allUnlockedEnemies.Count - currentTopIndex
                );
                EventSystem.current.SetSelectedGameObject(enemyButtons[visibleCount - 1]);
                break;
        }
    }

    /// <summary>
    /// 敵の詳細情報を表示する
    /// </summary>
    /// <param name="enemyData">敵のデータ</param>
    /// <param name="saveEntry">敵のセーブデータエントリー</param>
    public void DisplayEnemyDetails(EnemyData enemyData, EnemyRecordEntry saveEntry)
    {
        if (enemyData == null)
            return;

        // --- 未討伐（遭遇のみ）の場合の表示処理 ---
        if (saveEntry.killCount == 0)
        {
            // 名前は未登録
            enemyNameText.text = "未登録";

            // 画像はシルエット（真っ黒）
            UIUtility.SetSpriteFitToSquare(enemyImage, enemyData.encyclopediaSprite, baseSize);
            enemyImage.color = Color.black;

            // ステータスは不明
            StringBuilder unknownStats = new StringBuilder();
            unknownStats.AppendLine("レベル　: 不明");
            unknownStats.AppendLine("ＨＰ　　: 不明");
            unknownStats.AppendLine("経験値　: 不明");
            unknownStats.AppendLine("コイン　: 不明");
            unknownStats.AppendLine($"討伐数　: {saveEntry.killCount}"); // 0を表示
            statsText.text = unknownStats.ToString();

            // ドロップアイテムは不明：中央揃えに設定
            if (dropItemsText != null)
            {
                dropItemsText.alignment = TextAlignmentOptions.Center;
                dropItemsText.text = "不明";
            }

            // これ以降の処理（詳細なドロップ表示など）はスキップ
            return;
        }

        // 画像の色を白（通常）に戻す（重要：オブジェクト再利用のため）
        enemyImage.color = Color.white;

        // --- 基本情報の表示 ---
        enemyNameText.text = enemyData.enemyName;
        UIUtility.SetSpriteFitToSquare(enemyImage, enemyData.encyclopediaSprite, baseSize);
        // descriptionText.text = enemyData.description;

        // --- ステータスの表示 (StringBuilderで効率的に文字列を結合) ---
        StringBuilder statsBuilder = new StringBuilder();
        statsBuilder.AppendLine($"レベル　: {enemyData.requiredLevel}");
        statsBuilder.AppendLine($"ＨＰ　　: {enemyData.enemyHP}");
        statsBuilder.AppendLine($"経験値　: {enemyData.rewardExp}");
        statsBuilder.AppendLine($"コイン　: {enemyData.dropMoney}");
        statsBuilder.AppendLine($"討伐数　: {saveEntry.killCount}");
        statsText.text = statsBuilder.ToString();

        // --- ドロップアイテムの表示 ---
        StringBuilder dropItemsBuilder = new StringBuilder();

        if (enemyData.dropItems != null && enemyData.dropItems.Count > 0)
        {
            // 通常のリスト表示：左上揃えに設定
            if (dropItemsText != null)
            {
                dropItemsText.alignment = TextAlignmentOptions.TopLeft;
            }

            // 最大6種類のアイテムを順番に縦一列で構築
            for (int i = 0; i < enemyData.dropItems.Count; i++)
            {
                var item = enemyData.dropItems[i];
                string displayName = "？？？";
                string conditionText = "";

                // 1. アイテムのID取得
                Enum itemEnum = item.baseItemData.GetItemID();
                if (itemEnum != null)
                {
                    int itemID = EnumIDUtility.ToID(itemEnum);

                    // 2. ドロップ条件チェック
                    if (item.hasCondition && item.conditionType != DropConditionType.None)
                    {
                        bool isConditionUnlocked =
                            GameManager.instance.savedata.EnemyRecordData.IsItemConditionUnlocked(
                                enemyData.enemyID,
                                itemID
                            );
                        if (!isConditionUnlocked)
                        {
                            switch (item.conditionType)
                            {
                                case DropConditionType.KillCountOver:
                                    conditionText =
                                        $" (<color=red>{item.conditionValue}</color>体以上撃破)";
                                    break;
                                case DropConditionType.PlayerLevelUnder:
                                    conditionText =
                                        $" (総合ランク<color=red>{item.conditionValue}</color>以下撃破)";
                                    break;
                                case DropConditionType.NoDamage:
                                    conditionText = " (ノーダメージ撃破)";
                                    break;
                            }
                        }
                    }

                    // 3. 取得済みチェック
                    if (
                        GameManager.instance.savedata.EnemyRecordData.IsDropUnlocked(
                            enemyData.enemyID,
                            itemID
                        )
                    )
                    {
                        displayName = item.baseItemData.itemName;
                        conditionText = "";
                    }
                }

                if (item.isUnique)
                {
                    displayName = $"<color=#FFD700>{displayName}</color>";
                }

                // 4. 表示行の作成
                string lineText;
                if (!string.IsNullOrEmpty(conditionText))
                {
                    lineText = $"・{displayName}{conditionText}";
                }
                else
                {
                    float displayChance;
                    if (item.maxDropCount > 1)
                    {
                        float singleChance = item.dropChance / 100.0f;
                        float noDropChance = 1.0f - singleChance;
                        float allFailChance = Mathf.Pow(noDropChance, item.maxDropCount);
                        displayChance = (1.0f - allFailChance) * 100f;
                    }
                    else
                    {
                        displayChance = item.dropChance;
                    }
                    lineText = $"・{displayName} ({displayChance:F1}%)";
                }

                // すべて同じビルダーに追加（縦一列になる）
                dropItemsBuilder.AppendLine(lineText);
            }
        }
        else
        {
            // アイテムなし：中央揃えに設定
            if (dropItemsText != null)
            {
                dropItemsText.alignment = TextAlignmentOptions.Center;
            }
            dropItemsBuilder.AppendLine("ドロップアイテムなし");
        }

        // 統合されたテキストコンポーネントに一括反映
        if (dropItemsText != null)
        {
            dropItemsText.text = dropItemsBuilder.ToString();
        }
    }
}

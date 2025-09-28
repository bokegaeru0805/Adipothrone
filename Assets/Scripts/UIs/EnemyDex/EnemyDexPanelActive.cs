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
    [SerializeField] private GameObject[] enemyButtons; // 敵を選択するためのボタン配列
    [SerializeField] private EnemyDatabase enemyDatabase; // 敵のマスターデータ

    [Header("敵詳細表示エリア")]
    [SerializeField] private TextMeshProUGUI enemyNameText;
    [SerializeField] private Image enemyImage;
    // [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI statsText; // レベル, HP, EXP, Moneyなどを表示
    [SerializeField] private TextMeshProUGUI dropItemsText; // ドロップアイテム一覧

    [Header("空の状態の表示")]
    [SerializeField] private GameObject detailGroup; // 詳細表示エリアの親オブジェクト
    [SerializeField] private GameObject emptyPanel;  // 何も登録されていない時に表示するパネル

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
            var helper = button.GetComponent<EnemyDexButtonHelper>() ?? button.AddComponent<EnemyDexButtonHelper>();
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
        if (inputManager == null) return;

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        if (selectedObject == null) return;

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
        if (visibleItemCount <= 0) return;

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
            // 2. その敵が討伐済み(Unlocked)かどうかをセーブデータで確認する
            if (enemyRecordData.IsUnlocked(masterData.enemyID))
            {
                // 3. 討伐済み、かつ「図鑑に表示する」設定の敵だけをリストに追加する
                if (masterData.isListedInDex)
                {
                    // 討伐数などのセーブデータも取得
                    var saveEntry = enemyRecordData.enemyRecords.Find(e => e.enemyIdValue == (int)masterData.enemyID);

                    // 表示用のリストに追加
                    allUnlockedEnemies.Add(new UnlockedEnemy { MasterData = masterData, SaveEntry = saveEntry });
                }
            }
        }

        // 総ページ数を計算
        totalPages = (allUnlockedEnemies.Count > 0) ? (allUnlockedEnemies.Count - 1) / itemsPerPage + 1 : 1;
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

                enemyButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = unlockedEnemy.MasterData.enemyName;
                buttonHelpers[i].Initialize(this, unlockedEnemy.MasterData, unlockedEnemy.SaveEntry);
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
        if (totalPages <= 1) return;

        GameObject lastSelected = EventSystem.current.currentSelectedGameObject;
        int lastSelectedIndex = (lastSelected != null) ? System.Array.IndexOf(enemyButtons, lastSelected) : -1;

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
                    int newVisibleCount = Mathf.Min(itemsPerPage, allUnlockedEnemies.Count - currentTopIndex);
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
                int visibleCount = Mathf.Min(itemsPerPage, allUnlockedEnemies.Count - currentTopIndex);
                EventSystem.current.SetSelectedGameObject(enemyButtons[visibleCount - 1]);
                break;
        }
    }

    public void DisplayEnemyDetails(EnemyData enemyData, EnemyRecordEntry saveEntry)
    {
        if (enemyData == null) return;

        // --- 基本情報の表示 ---
        enemyNameText.text = enemyData.enemyName;
        UIUtility.SetSpriteFitToSquare(enemyImage, enemyData.encyclopediaSprite, baseSize);
        // descriptionText.text = enemyData.description;

        // --- ステータスの表示 (StringBuilderで効率的に文字列を結合) ---
        StringBuilder statsBuilder = new StringBuilder();
        statsBuilder.AppendLine($"レベル: {enemyData.requiredLevel}");
        statsBuilder.AppendLine($"HP: {enemyData.enemyHP}");
        statsBuilder.AppendLine($"経験値: {enemyData.rewardExp}");
        statsBuilder.AppendLine($"コイン: {enemyData.dropMoney}");
        statsBuilder.AppendLine($"討伐数: {saveEntry.killCount}");
        statsText.text = statsBuilder.ToString();

        // --- ドロップアイテムの表示 ---
        StringBuilder dropsBuilder = new StringBuilder();
        if (enemyData.dropItems.Count > 0)
        {
            foreach (var item in enemyData.dropItems)
            {
                // BaseItemDataにitemNameプロパティがあると仮定
                string itemName = item.baseItemData.itemName;

                // maxDropCountが1より大きいかどうかで表示を分岐させる
                if (item.maxDropCount > 1)
                {
                    // 1より大きい場合：個数を表示に追加する
                    dropsBuilder.AppendLine($"・{itemName} (1〜{item.maxDropCount}個) (各{item.dropChance}%)");
                }
                else
                {
                    // 1の場合（従来通り）：個数は表示しない
                    dropsBuilder.AppendLine($"・{itemName} ({item.dropChance}%)");
                }
            }
        }
        else
        {
            dropsBuilder.AppendLine("ドロップアイテムなし");
        }
        dropItemsText.text = dropsBuilder.ToString();
    }
}
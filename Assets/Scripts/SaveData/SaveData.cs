using System;
using System.Collections.Generic;

/// <summary>
/// ゲーム全体のセーブデータをまとめたクラス
/// </summary>
[Serializable]
public class SaveData
{
    // ===== ゲームのバージョン =====
    public string GameVersion = "";

    // ===== セーブした日付・時刻 =====
    public string SaveDateTime = "";

    // ===== プレイヤーの状態 =====
    public PlayerStatusData PlayerStatus = new PlayerStatusData();

    // ===== 宝箱やギミックの開封状態 =====
    public TreasureData TreasureData = new TreasureData();

    // //===== クエスト進行度 =====
    // public QuestData questData = new QuestData();

    // ===== ゲーム全体の進行度 =====
    public ProgressLogData ProgressLogData = new ProgressLogData();

    // ===== Tipsの進行度 =====
    public TipsData TipsData = new TipsData();

    // ===== 所持アイテム =====
    public InventoryItemData ItemInventoryData = new InventoryItemData();

    // ===== クィックリスト登録使用アイテム =====
    public InventoryItemData QuickItemData = new InventoryItemData();

    // ===== 所持武器情報 =====
    public InventoryWeaponData WeaponInventoryData = new InventoryWeaponData();

    // ===== 装備武器情報 =====
    public InventoryWeaponData WeaponEquipmentData = new InventoryWeaponData();

    // ===== ファストトラベル情報 =====
    public FastTravelData FastTravelData = new FastTravelData();

    // ===== 敵の記録 =====
    public EnemyRecordData EnemyRecordData = new EnemyRecordData();

    // ===== レシピ・合成進行度 =====
    public RecipeSaveData RecipeData = new RecipeSaveData();

    // ===== スキル情報 =====
    public SkillSaveData SkillData = new SkillSaveData();

    /// <summary>
    /// データの整合性をチェックし、nullの箇所があれば初期化して修復する
    /// ロード直後に呼び出すこと
    /// </summary>
    public void Validate()
    {
        if (PlayerStatus == null)
            PlayerStatus = new PlayerStatusData();
        if (TreasureData == null)
            TreasureData = new TreasureData();
        if (ProgressLogData == null)
            ProgressLogData = new ProgressLogData();
        if (TipsData == null)
            TipsData = new TipsData();
        if (ItemInventoryData == null)
            ItemInventoryData = new InventoryItemData();
        if (QuickItemData == null)
            QuickItemData = new InventoryItemData();
        if (WeaponInventoryData == null)
            WeaponInventoryData = new InventoryWeaponData();
        if (WeaponEquipmentData == null)
            WeaponEquipmentData = new InventoryWeaponData();
        if (FastTravelData == null)
            FastTravelData = new FastTravelData();
        if (EnemyRecordData == null)
            EnemyRecordData = new EnemyRecordData();
        if (RecipeData == null)
            RecipeData = new RecipeSaveData();
        if (SkillData == null)
            SkillData = new SkillSaveData();
    }
}

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
}

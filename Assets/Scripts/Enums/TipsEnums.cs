public enum TipsName
{
    //表示の順番はTipsInfoDatabaseのtipsリストの順番に依存します。
    None = 0, // 無効なTips

    // --- 操作系 ---
    BasicControls = 18001, // 基本操作方法

    UIControls = 18002, // UI操作方法

    HudDisplay = 18003, // HP/WPの表示

    // --- 戦闘系 ---
    EnemyTypes = 18004, // 敵の種類

    // --- システム・機能系 ---
    ItemUsage = 18005, // アイテム使用
    QuickSlot = 18006, // クイックアイテム登録
    ItemDetail = 18007, // アイテム詳細
    WeaponTypeChange = 18008, // 攻撃武器変化の種類
    WeaponChange = 18009, // 装備武器変更（戦闘中の切り替えなど）
    GameOver = 18010, // ゲームオーバーの説明
    GuideMenu = 18011, // ガイドメニュー
    InteractionIcons = 18012, // 吹き出しの種類
    CurrentEffects = 18013, // 現在の状態異常
    EffectTypes1 = 18014, // 状態異常の種類1
    FastTravel = 18015, // ファストトラベルの説明
    Shield = 18016, // シールドの説明
    StatusLevel = 18017, // ステータスレベルの説明
}

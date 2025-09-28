//操作関連のTipsは0番台
//ゲームシステム系のTipsは100～200番台
//装備・アイテム活用Tipsは300～400番台
//戦闘関連のTipsは500番台
//敵のTipsは600番台
//探索・謎解き系Tipsは700番台
//その他のTipsは800番台

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
}

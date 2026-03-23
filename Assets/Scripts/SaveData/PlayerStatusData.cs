using System;
using System.Collections.Generic;

/// <summary>
/// プレイヤーのセーブデータに保存されるステータス情報を管理するクラス
/// 注意：
/// このクラスの各フィールド名は、enum（PlayerStatusBoolName / PlayerStatusIntName 等）の
/// ToString() 結果と完全一致している必要があります。
/// → そうでないと、PlayerManager等でのリフレクションによる動的アクセスが失敗します。
/// 例：enumの "BodyState" は、ここでも "BodyState" という名前でなければならない。
/// </summary>
[Serializable]
public class PlayerStatusData
{
    #region 基礎ステータス関連
    public int playerCurrentHP; // 現在のプレイヤーのHP
    public int playerCurrentWP; // 現在のプレイヤーのWP
    public float wpConsumptionBuffer; // WPを減らすために蓄積される数値
    public int playerExp; // プレイヤーの経験値
    public int playerMoney; // プレイヤーの持つ金額
    public PlayerAttackType playerAttackType; // プレイヤーの攻撃方法
    #endregion

    #region バフレベル上限関連
    public int attackBuffLimitLevel; // 攻撃力バフの上限のレベル
    public int defenceBuffLimitLevel; // 防御力バフの上限のレベル
    public int speedBuffLimitLevel; // スピードバフの上限のレベル
    public int luckBuffLimitLevel; // 運バフの上限のレベル
    #endregion

    #region ステータスレベル関連
    // HP
    public int hpMaxLevel; // アイテム消費で到達したHPの最大レベル
    public int hpCurrentLevel; // プレイヤーが任意に設定（下降）しているHPの現在レベル

    // 攻撃力
    public int attackMaxLevel; // アイテム消費で到達した攻撃力の最大レベル
    public int attackCurrentLevel; // プレイヤーが任意に設定（下降）している攻撃力の現在レベル

    // 防御力
    public int defenceMaxLevel; // アイテム消費で到達した防御力の最大レベル
    public int defenceCurrentLevel; // プレイヤーが任意に設定（下降）している防御力の現在レベル

    // 素早さ
    public int speedMaxLevel; // アイテム消費で到達した素早さの最大レベル
    public int speedCurrentLevel; // プレイヤーが任意に設定（下降）している素早さの現在レベル

    // 幸運
    public int luckMaxLevel; // アイテム消費で到達した幸運の最大レベル
    public int luckCurrentLevel; // プレイヤーが任意に設定（下降）している幸運の現在レベル
    #endregion

    #region 状態フラグ関連
    public bool isChangeAttackType; // プレイヤーの攻撃方法を変更できるかどうか
    public bool isChangeWP; // プレイヤーのWPを変更できるかどうか
    public bool isRobotmove; // Robotが動けるか
    public bool isRobotattack; // Robotが攻撃できるか
    public bool isCanUseShield; // シールドを使用できるかどうか
    #endregion

    #region エフェクト（状態異常・バフ）関連
    public List<PlayerEffectStates> playerEffectStates; // プレイヤーの効果状態を保存する変数
    #endregion

    public PlayerStatusData()
    {
        // --- 基礎ステータス初期化 ---
        // playerMaxHP = 100;
        playerCurrentHP =
            GameConstants.STATUS_HP_INITIAL_BASE
            + UnityEngine.Mathf.RoundToInt(
                1
                    * (
                        GameConstants.STATUS_HP_BASE_INCREASE
                        + 1 * GameConstants.STATUS_HP_MAX_LEVEL_BONUS
                    )
            );
        playerCurrentWP = 0;
        wpConsumptionBuffer = 0f;
        playerExp = 0;
        playerMoney = 0;
        playerAttackType = PlayerAttackType.Shoot;

        // --- バフレベル上限初期化 ---
        attackBuffLimitLevel = 10;
        defenceBuffLimitLevel = 10;
        speedBuffLimitLevel = 10;
        luckBuffLimitLevel = 5;

        // --- ステータスレベル初期化 ---
        hpMaxLevel = 1;
        hpCurrentLevel = 1;
        attackMaxLevel = 1;
        attackCurrentLevel = 1;
        defenceMaxLevel = 1;
        defenceCurrentLevel = 1;
        speedMaxLevel = 1;
        speedCurrentLevel = 1;
        luckMaxLevel = 1;
        luckCurrentLevel = 1;

        // --- 状態フラグ初期化 ---
        isChangeAttackType = false;
        isChangeWP = true; // WPの変更を許可(チュートリアルで体形変化するため)
        isRobotmove = false;
        isRobotattack = false;
        isCanUseShield = false;

        // --- エフェクト初期化 ---
        playerEffectStates = new List<PlayerEffectStates>();
    }
}

/// <summary>
/// プレイヤーの攻撃方法を定義する列挙型
/// </summary>
public enum PlayerAttackType
{
    None = 0,
    Blade = 10,
    Shoot = 20,
    Magic = 30,
}

/// <summary>
/// プレイヤーに付与されている個別の効果状態を管理するクラス
/// </summary>
[Serializable]
public class PlayerEffectStates
{
    public int effectTypeNumber; // 効果の種類
    public float deltaValue; // 効果の値
    public float remainingTime; // 残り時間

    // 明示的な値を渡す用
    public PlayerEffectStates(int effectTypeNumber, float deltaValue, float remainingTime)
    {
        this.effectTypeNumber = effectTypeNumber; // 効果の種類
        this.deltaValue = deltaValue; // 効果の値
        this.remainingTime = remainingTime; // 残り時間
    }

    // デフォルト（全部0）で初期化
    public PlayerEffectStates()
    {
        this.effectTypeNumber = 0;
        this.deltaValue = 0f;
        this.remainingTime = 0f;
    }
}

/// <summary>
/// bool型のプレイヤーステータス名
/// </summary>
public enum PlayerStatusBoolName
{
    isRobotmove = 20, // Robotが動けるか
    isRobotattack = 30, // Robotが攻撃できるか
    isChangeAttackType = 40, // プレイヤーの攻撃方法を変更できるかどうか
    isCanUseShield = 50, // シールドを使用できるかどうか
}

/// <summary>
/// int型のプレイヤーステータス名
/// </summary>
public enum PlayerStatusIntName
{
    // playerMaxHP = 500,        // プレイヤーの最大HP
    playerCurrentHP = 501, // プレイヤーの現在のHP

    // playerMaxWP = 503,        // プレイヤーの最大WP
    playerCurrentWP = 504, // プレイヤーの現在のWP

    playerExp = 505, // プレイヤーの経験値

    // playerLv = 502,           // プレイヤーのレベル

    playerMoney = 600, // 所持金

    attackBuffLimitLevel = 701, // 攻撃力バフの上限
    defenceBuffLimitLevel = 703, // 防御力バフの上限
    speedBuffLimitLevel = 705, // スピードバフの上限
    luckBuffLimitLevel = 707, // 運バフの上限

    hpMaxLevel = 710, // HPの最大レベル
    hpCurrentLevel = 711, // HPの現在レベル
    attackMaxLevel = 720, // 攻撃力の最大レベル
    attackCurrentLevel = 721, // 攻撃力の現在レベル
    defenceMaxLevel = 7230, // 防御力の最大レベル
    defenceCurrentLevel = 731, // 防御力の現在レベル
    speedMaxLevel = 740, // 素早さの最大レベル
    speedCurrentLevel = 741, // 素早さの現在レベル
    luckMaxLevel = 750, // 幸運の最大レベル
    luckCurrentLevel = 751, // 幸運の現在レベル
}

/// <summary>
/// float型のプレイヤーステータス名
/// </summary>
public enum PlayerStatusFloatName
{
    wpConsumptionBuffer = 1001, // WPを減らすために蓄積される数値
}

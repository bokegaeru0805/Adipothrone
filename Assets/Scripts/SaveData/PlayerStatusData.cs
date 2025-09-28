using System;
using System.Collections.Generic;

[Serializable]
public class PlayerStatusData
{
    public int playerCurrentHP; //現在のプレイヤーのHP
    public int playerCurrentWP; //現在のプレイヤーのWP
    public float wpConsumptionBuffer; //WPを減らすために蓄積される数値
    public int playerExp; //プレイヤーの経験値
    public PlayerAttackType playerAttackType; //プレイヤーの攻撃方法
    public int attackBuffLimitLevel; //攻撃力バフの上限のレベル
    public int defenceBuffLimitLevel; //防御力バフの上限のレベル
    public int speedBuffLimitLevel; //スピードバフの上限のレベル
    public int luckBuffLimitLevel; //運バフの上限のレベル
    public int playerMoney; //プレイヤーの持つ金額
    public bool isChangeAttackType; //プレイヤーの攻撃方法を変更できるかどうか
    public bool isChangeWP; //プレイヤーのWPを変更できるかどうか
    public bool isRobotmove; //Robotが動けるか
    public bool isRobotattack; //Robotが攻撃できるか
    public List<PlayerEffectStates> playerEffectStates; //プレイヤーの効果状態を保存する変数

    // 注意：
    // このクラスの各フィールド名は、enum（PlayerStatusBoolName / PlayerStatusIntName）の
    // ToString() 結果と完全一致している必要があります。
    // → そうでないと、PlayerManager等でのリフレクションによる動的アクセスが失敗します。
    // 例：enumの "BodyState" は、ここでも "BodyState" という名前でなければならない。

    public PlayerStatusData()
    {
        // playerMaxHP = 100;
        playerCurrentHP = GameConstants.GetMaxHP(1);
        playerCurrentWP = 0;
        wpConsumptionBuffer = 0f;
        playerExp = 0;
        playerAttackType = PlayerAttackType.Shoot;
        attackBuffLimitLevel = 10;
        defenceBuffLimitLevel = 10;
        speedBuffLimitLevel = 10;
        luckBuffLimitLevel = 5;

        playerMoney = 0;
        isChangeAttackType = false;
        isChangeWP = true; // WPの変更を許可(チュートリアルで体形変化するため)
        isRobotmove = false;
        isRobotattack = false;
        playerEffectStates = new List<PlayerEffectStates>();
    }
}

public enum PlayerAttackType
{
    None = 0,
    Blade = 10,
    Shoot = 20,
    Magic = 30,
}

[System.Serializable]
public class PlayerEffectStates
{
    public int effectTypeNumber;
    public float deltaValue;
    public float remainingTime;

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

public enum PlayerStatusBoolName
{
    isRobotmove = 20, //Robotが動けるか
    isRobotattack =
        30 //Robotが攻撃できるか
    ,
    isChangeAttackType =
        40 //プレイヤーの攻撃方法を変更できるかどうか
    ,
}

public enum PlayerStatusIntName
{
    // playerMaxHP = 500, //プレイヤーの最大HP
    playerCurrentHP = 501, //プレイヤーの現在のHP
    // playerMaxWP = 503, //プレイヤーの最大WP
    playerCurrentWP = 504, //プレイヤーの現在のWP

    // playerLv = 502, //プレイヤーのレベル
    attackBuffLimitLevel = 701, //攻撃力バフの上限
    defenceBuffLimitLevel = 703, //防御力バフの上限
    speedBuffLimitLevel = 705, //スピードバフの上限
    luckBuffLimitLevel = 707, //運バフの上限
    playerMoney =
        600 //所持金
    ,
}

public enum PlayerStatusFloatName
{
    wpConsumptionBuffer = 1001, //WPを減らすために蓄積される数値
}

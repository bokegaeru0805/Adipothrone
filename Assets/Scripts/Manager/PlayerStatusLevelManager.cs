using UnityEngine;

/// <summary>
/// プレイヤーの「アイテムによるステータスレベル（攻撃・防御・素早さ・幸運）」を専門に管理するクラス。
/// 最大レベルの拡張、現在レベルの増減、および実際の基礎ステータス値の算出を行います。
/// </summary>
public class PlayerStatusLevelManager : MonoBehaviour
{
    private PlayerManager playerManager;
    private PlayerLevelManager playerLevelManager;

    private void Awake()
    {
        playerManager = GetComponent<PlayerManager>();
        playerLevelManager = GetComponent<PlayerLevelManager>();

        if (playerManager == null || playerLevelManager == null)
        {
            Debug.LogError(
                "PlayerManager または PlayerLevelManager が同じGameObjectに見つかりません！"
            );
        }
    }

    #region 基礎ステータス算出プロパティ

    /// <summary>
    /// UI表示および内部計算用の「プレイヤー最大HP」
    /// 計算式: 初期ベース値 + 現在レベル * (基礎増加値 + 最大レベル * ボーナス係数)
    /// </summary>
    public int TotalBaseHP
    {
        get
        {
            int currentHpLv = playerManager.GetPlayerIntStatus(PlayerStatusIntName.hpCurrentLevel);
            int maxHpLv = playerManager.GetPlayerIntStatus(PlayerStatusIntName.hpMaxLevel);

            float calculated =
                currentHpLv
                * (
                    GameConstants.STATUS_HP_BASE_INCREASE
                    + maxHpLv * GameConstants.STATUS_HP_MAX_LEVEL_BONUS
                );
            return GameConstants.STATUS_HP_INITIAL_BASE + Mathf.RoundToInt(calculated);
        }
    }

    /// <summary>
    /// UI表示および内部計算用の「プレイヤー基礎攻撃力」
    /// 計算式: 現在レベル * 基礎増加値 + √(現在レベル * 最大レベル) * ボーナス係数
    /// </summary>
    public int TotalBaseAttackPower
    {
        get
        {
            int currentAttackLv = playerManager.GetPlayerIntStatus(
                PlayerStatusIntName.attackCurrentLevel
            );
            int maxAttackLv = playerManager.GetPlayerIntStatus(PlayerStatusIntName.attackMaxLevel);

            // 基礎増加分
            float basePower = currentAttackLv * GameConstants.STATUS_ATTACK_BASE_INCREASE;

            // ボーナス分（平方根を用いることで通常プレイ時は完全な線形成長となる）
            float bonusPower =
                Mathf.Sqrt(currentAttackLv * maxAttackLv)
                * GameConstants.STATUS_ATTACK_MAX_LEVEL_BONUS;

            float calculated = basePower + bonusPower;
            return Mathf.RoundToInt(calculated);
        }
    }

    /// <summary>
    /// UI表示および内部計算用の「プレイヤー基礎防御力」
    /// 計算式: 現在レベル * (基礎増加値 + 最大レベル * ボーナス係数)
    /// </summary>
    public int TotalBaseDefensePower
    {
        get
        {
            int currentDefLv = playerManager.GetPlayerIntStatus(
                PlayerStatusIntName.defenceCurrentLevel
            );
            int maxDefLv = playerManager.GetPlayerIntStatus(PlayerStatusIntName.defenceMaxLevel);

            float calculated =
                currentDefLv
                * (
                    GameConstants.STATUS_DEFENSE_BASE_INCREASE
                    + maxDefLv * GameConstants.STATUS_DEFENSE_MAX_LEVEL_BONUS
                );
            return Mathf.RoundToInt(calculated);
        }
    }

    /// <summary>
    /// 基礎素早さの追加ボーナス
    /// 計算式: 現在レベル * (基礎増加値 + 最大レベル * ボーナス係数)
    /// </summary>
    public float SpeedBonus
    {
        get
        {
            int currentSpeedLv = playerManager.GetPlayerIntStatus(
                PlayerStatusIntName.speedCurrentLevel
            );
            int maxSpeedLv = playerManager.GetPlayerIntStatus(PlayerStatusIntName.speedMaxLevel);

            return currentSpeedLv
                * (
                    GameConstants.STATUS_SPEED_BASE_INCREASE
                    + maxSpeedLv * GameConstants.STATUS_SPEED_MAX_LEVEL_BONUS
                );
        }
    }

    /// <summary>
    /// 基礎幸運の追加ボーナス
    /// 計算式: 現在レベル * (基礎増加値 + 最大レベル * ボーナス係数)
    /// </summary>
    public float LuckBonus
    {
        get
        {
            int currentLuckLv = playerManager.GetPlayerIntStatus(
                PlayerStatusIntName.luckCurrentLevel
            );
            int maxLuckLv = playerManager.GetPlayerIntStatus(PlayerStatusIntName.luckMaxLevel);

            return currentLuckLv
                * (
                    GameConstants.STATUS_LUCK_BASE_INCREASE
                    + maxLuckLv * GameConstants.STATUS_LUCK_MAX_LEVEL_BONUS
                );
        }
    }

    #region 総合ステータスレベル算出

    /// <summary>
    /// 敵のドロップ判定等に使用する「総合ステータスレベル」を算出します。
    /// HP、攻撃力、防御力の現在レベルを使用し、「平均値」と「最大値」の中間（最大値ブレンド法）を取ることで、
    /// 1つのステータスだけを極端に上げる（極振り）による抜け道を塞ぎます。
    /// </summary>
    public int TotalStatusLevel
    {
        get
        {
            // 戦闘に直結する3つの現在レベルを取得
            int hpLevel = playerManager.GetPlayerIntStatus(PlayerStatusIntName.hpCurrentLevel);
            int attackLevel = playerManager.GetPlayerIntStatus(
                PlayerStatusIntName.attackCurrentLevel
            );
            int defenseLevel = playerManager.GetPlayerIntStatus(
                PlayerStatusIntName.defenceCurrentLevel
            );

            // 1. 平均値を算出
            float averageLevel = (hpLevel + attackLevel + defenseLevel) / 3f;

            // 2. 3つの中の最大値を算出
            int maxLevel = Mathf.Max(hpLevel, attackLevel, defenseLevel);

            // 3. 平均値と最大値を足して2で割り、四捨五入して整数にする
            float totalLevel = (averageLevel + maxLevel) / 2f;
            return Mathf.RoundToInt(totalLevel);
        }
    }

    #endregion
    #endregion

    #region ステータスレベル操作メソッド

    /// <summary>
    /// 専用アイテムを使用して、指定したステータスの「最大レベル」を1つ上げます。
    /// 同時に現在レベルも最大レベルに合わせて引き上げます。
    /// </summary>
    public void IncreaseMaxStatusLevel(
        PlayerStatusIntName maxLevelName,
        PlayerStatusIntName currentLevelName
    )
    {
        int currentMax = playerManager.GetPlayerIntStatus(maxLevelName);
        playerManager.SetPlayerIntStatus(maxLevelName, currentMax + 1);

        // 最大レベルが上がったので、現在レベルもそれに合わせる
        playerManager.SetPlayerIntStatus(currentLevelName, currentMax + 1);

        // Debug.Log($"{maxLevelName} の上限が {currentMax + 1} に解放されました。");
    }

    /// <summary>
    /// UIの操作等から、ステータスの「現在レベル」を任意の値に変更します。
    /// 最大レベルを超えることはできません。
    /// </summary>
    public void SetCurrentStatusLevel(
        PlayerStatusIntName maxLevelName,
        PlayerStatusIntName currentLevelName,
        int targetLevel
    )
    {
        int maxLevel = playerManager.GetPlayerIntStatus(maxLevelName);

        // 1 から 最大レベル の間にクランプする
        int clampedLevel = Mathf.Clamp(targetLevel, 1, maxLevel);

        playerManager.SetPlayerIntStatus(currentLevelName, clampedLevel);

        // HPレベルが変更された場合、PlayerManagerの最大HPを即座に更新する
        if (currentLevelName == PlayerStatusIntName.hpCurrentLevel)
        {
            ApplyHPLevelChange();
        }

        // Debug.Log(
        //     $"{currentLevelName} の現在レベルを {clampedLevel} に変更しました。（上限: {maxLevel}）"
        // );
    }

    /// <summary>
    /// HPのレベルが変動した際に、実際の最大HPを更新し、現在HPが上限を超えていればクランプ（切り捨て）する
    /// </summary>
    private void ApplyHPLevelChange()
    {
        int newMaxHP = TotalBaseHP;
        playerManager.SetMaxHP(newMaxHP);

        int currentHP = playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP);
        if (currentHP > newMaxHP)
        {
            // 最大HPが下がって現在HPがはみ出た場合、最大HPと同じ値に強制上書きする
            playerManager.ForceSetHP(newMaxHP);
        }
    }

    /// <summary>
    /// 指定されたステータスの最大レベルを、引数で渡された「指定レベル」まで引き上げます。
    /// （現在の最大レベルが既に指定レベルに達している、または超えている場合は何もしません）
    /// </summary>
    /// <param name="targetStatus">引き上げ対象のステータスの種類</param>
    /// <param name="targetLevel">目標とする指定レベル</param>
    public void UpgradeMaxStatusLevelTo(EnhanceTargetStatus targetStatus, int targetLevel)
    {
        PlayerStatusIntName maxEnum = PlayerStatusIntName.attackMaxLevel;
        PlayerStatusIntName currentEnum = PlayerStatusIntName.attackCurrentLevel;

        // ターゲットとなるステータスのEnumを決定
        switch (targetStatus)
        {
            case EnhanceTargetStatus.HP:
                maxEnum = PlayerStatusIntName.hpMaxLevel;
                currentEnum = PlayerStatusIntName.hpCurrentLevel;
                break;
            case EnhanceTargetStatus.Attack:
                maxEnum = PlayerStatusIntName.attackMaxLevel;
                currentEnum = PlayerStatusIntName.attackCurrentLevel;
                break;
            case EnhanceTargetStatus.Defense:
                maxEnum = PlayerStatusIntName.defenceMaxLevel;
                currentEnum = PlayerStatusIntName.defenceCurrentLevel;
                break;
            case EnhanceTargetStatus.Speed:
                maxEnum = PlayerStatusIntName.speedMaxLevel;
                currentEnum = PlayerStatusIntName.speedCurrentLevel;
                break;
            case EnhanceTargetStatus.Luck:
                maxEnum = PlayerStatusIntName.luckMaxLevel;
                currentEnum = PlayerStatusIntName.luckCurrentLevel;
                break;
            default:
                Debug.LogError("未知のステータスが指定されました: " + targetStatus);
                return;
        }

        int currentMax = playerManager.GetPlayerIntStatus(maxEnum);

        // 現在の最大レベルが指定レベル未満の場合のみ引き上げる
        if (currentMax < targetLevel)
        {
            playerManager.SetPlayerIntStatus(maxEnum, targetLevel);
            // 現在レベルも引き上げた最大レベルに合わせて同期する
            playerManager.SetPlayerIntStatus(currentEnum, targetLevel);

            // HPの最大レベルが変動した場合は、実際のプレイヤーの最大HP実数値を即座に再計算・適用する
            if (targetStatus == EnhanceTargetStatus.HP)
            {
                ApplyHPLevelChange();
            }

            // Debug.Log($"{targetStatus} の上限レベルが {targetLevel} に引き上げられました。");
        }
    }

    #endregion

    #region アイテムによる強化処理

    /// <summary>
    /// ステータス強化アイテムを使用した際の処理。
    /// アイテムに設定された効果リストを順番に適用します。
    /// </summary>
    /// <param name="itemData">使用した強化アイテムのデータ</param>
    public void UseEnhanceItem(StatusEnhanceItemData itemData)
    {
        if (itemData == null || itemData.enhanceEffects == null)
            return;

        foreach (var effect in itemData.enhanceEffects)
        {
            ApplyEnhanceEffect(effect);
        }

        // Debug.Log($"{itemData.itemName} を使用し、ステータスが強化されました！");
    }

    /// <summary>
    /// 個別の強化効果をプレイヤーのステータスに適用します。
    /// </summary>
    private void ApplyEnhanceEffect(EnhanceEffect effect)
    {
        PlayerStatusIntName maxEnum = PlayerStatusIntName.attackMaxLevel;
        PlayerStatusIntName currentEnum = PlayerStatusIntName.attackCurrentLevel;

        // ターゲットとなるステータスのEnumを決定
        switch (effect.targetStatus)
        {
            case EnhanceTargetStatus.HP:
                maxEnum = PlayerStatusIntName.hpMaxLevel;
                currentEnum = PlayerStatusIntName.hpCurrentLevel;
                break;
            case EnhanceTargetStatus.Attack:
                maxEnum = PlayerStatusIntName.attackMaxLevel;
                currentEnum = PlayerStatusIntName.attackCurrentLevel;
                break;
            case EnhanceTargetStatus.Defense:
                maxEnum = PlayerStatusIntName.defenceMaxLevel;
                currentEnum = PlayerStatusIntName.defenceCurrentLevel;
                break;
            case EnhanceTargetStatus.Speed:
                maxEnum = PlayerStatusIntName.speedMaxLevel;
                currentEnum = PlayerStatusIntName.speedCurrentLevel;
                break;
            case EnhanceTargetStatus.Luck:
                maxEnum = PlayerStatusIntName.luckMaxLevel;
                currentEnum = PlayerStatusIntName.luckCurrentLevel;
                break;
            default:
                Debug.LogError("未知のステータスが指定されました: " + effect.targetStatus);
                return;
        }

        // 強化の種類に応じた処理
        switch (effect.enhanceType)
        {
            case EnhanceType.MaxLevelUp:
                // amount の分だけ最大レベルを上げる
                IncreaseMaxStatusLevelAmount(maxEnum, currentEnum, effect.amount);
                break;

            // case EnhanceType.BonusValueUp:
            //     // 将来用：基礎上昇値を底上げする処理をここに書く
            //     Debug.LogWarning("BonusValueUp は未実装です。");
            //     break;
        }
    }

    /// <summary>
    /// 指定した量だけ最大レベルと現在レベルを引き上げます。
    /// </summary>
    private void IncreaseMaxStatusLevelAmount(
        PlayerStatusIntName maxLevelName,
        PlayerStatusIntName currentLevelName,
        int amount
    )
    {
        int currentMax = playerManager.GetPlayerIntStatus(maxLevelName);
        int newMax = currentMax + amount;

        playerManager.SetPlayerIntStatus(maxLevelName, newMax);
        playerManager.SetPlayerIntStatus(currentLevelName, newMax); // 現在レベルも最大値に合わせる

        // Debug.Log($"{maxLevelName} の上限が {amount} 上がり、{newMax} になりました。");
    }

    #endregion
}

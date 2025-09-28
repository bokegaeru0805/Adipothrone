using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーの一時的な状態変化（バフ、デバフ、毒など）を専門に管理するクラス。
/// プレイヤーの基本的なステータスは PlayerManager が担当します。
/// </summary>
public class PlayerEffectManager : MonoBehaviour
{
    // シングルトンインスタンス
    public static PlayerEffectManager instance { get; private set; }

    [Header("毒の数値")]
    [SerializeField]
    private int poisonDamageRate = 0; // 毒のダメージ量

    [SerializeField]
    private float poisonInterval = 0; // 毒のダメージ間隔

    // 各ステータスの現在かかっている効果を保持する
    public PlayerEffectStates attackEffectStates { get; private set; } = new PlayerEffectStates();
    public PlayerEffectStates defenseEffectStates { get; private set; } = new PlayerEffectStates();
    public PlayerEffectStates speedEffectStates { get; private set; } = new PlayerEffectStates();
    public PlayerEffectStates luckEffectStates { get; private set; } = new PlayerEffectStates();
    public PlayerEffectStates poisonEffectStates { get; private set; } = new PlayerEffectStates();

    // 各ステータスのバフ上限値
    public int attackBuffLimitLevel { get; private set; } =
        GameConstants.DefaultAttackBuffLimitLevel; // 攻撃力バフの上限
    public int defenceBuffLimitLevel { get; private set; } =
        GameConstants.DefaultDefenseBuffLimitLevel; // 防御力バフの上限
    public int speedBuffLimitLevel { get; private set; } = GameConstants.DefaultSpeedBuffLimitLevel; // スピードバフの上限
    public int luckBuffLimitLevel { get; private set; } = GameConstants.DefaultLuckBuffLimitLevel; // 運バフの上限

    private Coroutine poisonCoroutine = null; // 毒の効果を管理するコルーチン
    #region Events
    public event Action<StatusEffectType> OnBuffApplied; // バフが適用されたときに呼び出されるイベント
    public event Action OnChangeBuffLimit; // バフの上限が変化したときに呼び出されるイベント
    public event Action OnSpeedEffectChanged; // スピードエフェクトが変化したときに呼び出されるイベント
    #endregion

    /// <summary>
    /// プレイヤーの基本ステータスを管理するPlayerManagerへの参照。
    /// </summary>
    private PlayerManager playerManager;

    /// <summary>
    /// プレイヤーのレベルと経験値を管理するPlayerLevelManagerへの参照。
    /// </summary>
    private PlayerLevelManager playerLevelManager;

    /// <summary>
    /// プレイヤーの体形とWPに基づくステータス倍率を管理するPlayerBodyManagerへの参照。
    /// </summary>
    private PlayerBodyManager playerBodyManager;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 同一GameObjectにアタッチされているPlayerManagerを取得し、連携する
        playerManager = GetComponent<PlayerManager>();
        if (playerManager == null)
        {
            Debug.LogError("PlayerManagerが同じGameObjectにアタッチされていません！");
        }
        // 同一GameObjectにアタッチされているPlayerLevelManagerを取得し、連携する
        playerLevelManager = GetComponent<PlayerLevelManager>();
        if (playerLevelManager == null)
        {
            Debug.LogError("PlayerLevelManagerが同じGameObjectにアタッチされていません！");
        }
        // 同一GameObjectにアタッチされているPlayerBodyManagerを取得し、連携する
        playerBodyManager = GetComponent<PlayerBodyManager>();
        if (playerBodyManager == null)
        {
            Debug.LogError("PlayerBodyManagerが同じGameObjectにアタッチされていません！");
        }
    }

    private void Start()
    {
        RefreshBuffLimit();
    }

    private void Update()
    {
        // ゲームがポーズ中でなく、メニューも開いていない場合のみ効果を更新
        if (
            Time.timeScale > 0
            && (!UIManager.instance?.isMenuOpen ?? false)
            && !GameManager.IsTalking
        )
        {
            UpdatePlayerEffects();

            // 毒状態であれば、継続ダメージのコルーチンを開始/停止する
            if (poisonEffectStates.remainingTime > 0 && poisonCoroutine == null)
            {
                poisonCoroutine = StartCoroutine(ApplyPoisonEffect());
            }
            else if (poisonEffectStates.remainingTime <= 0 && poisonCoroutine != null)
            {
                StopCoroutine(poisonCoroutine);
                poisonCoroutine = null;
            }
        }
    }

    #region Buff/Debuff Management
    // バフの上限を更新する関数
    public void UpdateBuffLimitLevel(StatusEffectType statusEffectType, int plus)
    {
        var status = GameManager.instance.savedata.PlayerStatus;
        if (status == null)
        {
            Debug.LogError("PlayerStatusDataがnullです");
            return;
        }

        switch (statusEffectType)
        {
            case StatusEffectType.Attack:
                status.attackBuffLimitLevel += plus;
                break;
            case StatusEffectType.Defense:
                status.defenceBuffLimitLevel += plus;
                break;
            case StatusEffectType.Speed:
                status.speedBuffLimitLevel += plus;
                break;
            case StatusEffectType.Luck:
                status.luckBuffLimitLevel += plus;
                break;
        }

        RefreshBuffLimit(); // バフの上限をリフレッシュ(イベントの発火も兼ねている)
    }

    // バフの上限レベルをリフレッシュして、イベントを発火する関数
    public void RefreshBuffLimit()
    {
        var status = GameManager.instance.savedata.PlayerStatus;
        if (status == null)
        {
            Debug.LogError("PlayerStatusDataがnullです");
            return;
        }

        attackBuffLimitLevel = status.attackBuffLimitLevel;
        defenceBuffLimitLevel = status.defenceBuffLimitLevel;
        speedBuffLimitLevel = status.speedBuffLimitLevel;
        luckBuffLimitLevel = status.luckBuffLimitLevel;

        OnChangeBuffLimit?.Invoke(); // バフの上限が変化したときに呼び出されるイベントを発火
    }

    //バフ・デバフの効果を適用する関数
    public void ApplyBuffDebuff(
        StatusEffectType statusEffectType,
        float multiplier,
        StatusEffectRank rank
    )
    {
        if (GameManager.instance.savedata.PlayerStatus.playerEffectStates == null)
        {
            GameManager.instance.savedata.PlayerStatus.playerEffectStates =
                new List<PlayerEffectStates>();
        }

        int statusEffectTypeNumber = (int)statusEffectType; // 効果の種類を取得
        float statusEffectduration = StatusEffectUtility.GetDurationByRank(rank); // 効果の持続時間を取得
        if (statusEffectduration <= 0)
        {
            return;
        }
        var effectList = GameManager.instance.savedata.PlayerStatus.playerEffectStates;
        var existingEffect = effectList.Find(e => e.effectTypeNumber == statusEffectTypeNumber);
        float effectAmount = existingEffect != null ? existingEffect.deltaValue : 0f;

        // 効果の数値を加算する（ただし、上限を超えないようにする）
        switch (statusEffectType)
        {
            case StatusEffectType.Attack:
                effectAmount = Mathf.Min(
                    effectAmount + multiplier,
                    attackBuffLimitLevel * GameConstants.AttackBuffValuePerLevel
                );
                OnBuffApplied?.Invoke(statusEffectType); // バフが適用されたときにイベントを発火
                break;
            case StatusEffectType.Defense:
                effectAmount = Mathf.Min(
                    effectAmount + multiplier,
                    defenceBuffLimitLevel * GameConstants.DefenseBuffValuePerLevel
                );
                OnBuffApplied?.Invoke(statusEffectType); // バフが適用されたときにイベントを発火
                break;
            case StatusEffectType.Speed:
                effectAmount = Mathf.Min(
                    effectAmount + multiplier,
                    speedBuffLimitLevel * GameConstants.SpeedBuffValuePerLevel
                );
                OnBuffApplied?.Invoke(statusEffectType); // バフが適用されたときにイベントを発火
                break;
            case StatusEffectType.Luck:
                effectAmount = Mathf.Min(
                    effectAmount + multiplier,
                    luckBuffLimitLevel * GameConstants.LuckBuffValuePerLevel
                );
                OnBuffApplied?.Invoke(statusEffectType); // バフが適用されたときにイベントを発火
                break;
        }

        if (existingEffect != null)
        {
            // 効果を上書き更新
            existingEffect.deltaValue = effectAmount;
            // 効果時間を更新（既存の効果時間と新しい効果時間の最大値を取る）
            existingEffect.remainingTime = Mathf.Max(
                existingEffect.remainingTime,
                statusEffectduration
            );
        }
        else
        {
            // 新規追加
            effectList.Add(
                new PlayerEffectStates(statusEffectTypeNumber, effectAmount, statusEffectduration)
            );
        }

        SEManager.instance?.PlayPlayerActionSE(SE_PlayerAction.Buff1); // バフの効果音を再生
    }

    //バフ・デバフの効果を時間によって管理する関数
    public void UpdatePlayerEffects()
    {
        var effectList = GameManager.instance.savedata.PlayerStatus.playerEffectStates;
        if (effectList == null || effectList.Count == 0)
            return;

        // 1. 効果時間を減らす
        foreach (var effect in effectList)
        {
            if (effect.remainingTime > 0)
            {
                effect.remainingTime -= Time.deltaTime;
            }
        }

        // 2. 効果をこのマネージャーのプロパティに反映
        foreach (var effect in effectList)
        {
            switch (effect.effectTypeNumber)
            {
                case (int)StatusEffectType.Attack:
                    attackEffectStates.deltaValue =
                        effect.remainingTime > 0 ? effect.deltaValue : 0;
                    attackEffectStates.remainingTime = effect.remainingTime;
                    break;
                case (int)StatusEffectType.Defense:
                    defenseEffectStates.deltaValue =
                        effect.remainingTime > 0 ? effect.deltaValue : 0;
                    defenseEffectStates.remainingTime = effect.remainingTime;
                    break;
                case (int)StatusEffectType.Speed:
                    bool wasActive = speedEffectStates.remainingTime > 0;
                    bool isActive = effect.remainingTime > 0;
                    speedEffectStates.remainingTime = effect.remainingTime;
                    speedEffectStates.deltaValue = isActive ? effect.deltaValue : 0;
                    if (wasActive != isActive)
                    {
                        OnSpeedEffectChanged?.Invoke(); // スピードエフェクトが変化したときに呼び出されるイベントを発火
                    }
                    break;
                case (int)StatusEffectType.Luck:
                    luckEffectStates.deltaValue = effect.remainingTime > 0 ? effect.deltaValue : 0;
                    luckEffectStates.remainingTime = effect.remainingTime;
                    break;
                case (int)StatusEffectType.Poison:
                    poisonEffectStates.deltaValue =
                        effect.remainingTime > 0 ? effect.deltaValue : 0;
                    poisonEffectStates.remainingTime = effect.remainingTime;
                    break;
            }
        }
    }

    // 毒の効果を適用するコルーチン
    private IEnumerator ApplyPoisonEffect()
    {
        while (true)
        {
            // 毒のダメージを適用
            playerManager.DamageHP(poisonDamageRate);
            //待機する
            yield return new WaitForSeconds(poisonInterval);
        }
    }
    #endregion

    #region Final Status Calculation
    /// <summary>
    /// 最終的な攻撃力を計算
    /// </summary>
    public int CalculateFinalAttackPower(int baseAttackPower)
    {
        float multiplier = 1;
        float effectDelta =
            GameConstants.PlayerAttackEffectMultiplier * attackEffectStates.deltaValue;

        // PlayerManagerからレベル補正値を取得し、バフ効果と合算
        multiplier += playerLevelManager.attackLvActualDeltaValue + effectDelta;

        if (multiplier > 1)
        {
            // PlayerBodyManagerからWP倍率を取得して反映
            multiplier *= playerBodyManager.attackWpScale;
        }
        else if (multiplier <= 0)
        {
            multiplier = GameConstants.MinAttackPowerMultiplier;
        }

        int totalDamage = (int)(baseAttackPower * multiplier);
        return Mathf.Max(1, totalDamage); // ダメージ量は1以上にする
    }

    /// <summary>
    /// 最終的な防御力を計算
    /// </summary>
    public int CalculateFinalDefensePower()
    {
        int totalDefense = 0;
        int effectDelta = (int)(
            GameConstants.PlayerDefenseEffectMultiplier * defenseEffectStates.deltaValue
        );

        // PlayerLevelManagerからレベル補正値を取得し、バフ効果と合算
        totalDefense = playerLevelManager.defenseLvActualDeltaValue + effectDelta;

        if (totalDefense > 0)
        {
            // PlayerBodyManagerからWP倍率を取得して反映
            totalDefense = (int)(totalDefense * playerBodyManager.defenseWpScale);
        }

        return totalDefense;
    }

    /// <summary>
    /// プレイヤーの最終的な移動速度を計算
    /// </summary>
    public float CalculateFinalPlayerMoveSpeed(float baseSpeed)
    {
        if (speedEffectStates.deltaValue == 0)
        {
            return baseSpeed;
        }

        float effectDelta =
            GameConstants.PlayerMoveSpeedEffectMultiplier * speedEffectStates.deltaValue;

        // PlayerBodyManagerからWP倍率を取得して反映
        float finalSpeed = baseSpeed * playerBodyManager.speedWpScale * (1f + effectDelta);

        return Mathf.Min(finalSpeed, GameConstants.PlayerMoveMaxSpeed); // 最大速度を超えないようにする
    }

    /// <summary>
    /// 剣の最終的な攻撃速度を計算
    /// </summary>
    public float CalculateFinalBladeMoveSpeed(float baseSpeed)
    {
        if (speedEffectStates.deltaValue == 0)
        {
            return baseSpeed;
        }

        float effectDelta =
            GameConstants.PlayerWeaponSpeedEffectMultiplier * speedEffectStates.deltaValue;

        // PlayerBodyManagerからWP倍率を取得して反映
        float finalSpeed = baseSpeed / (playerBodyManager.speedWpScale * (1f + effectDelta));

        return Mathf.Max(finalSpeed, GameConstants.PlayerBladeMinSpeed); // 最小速度を下回らないようにする
    }
    #endregion
}

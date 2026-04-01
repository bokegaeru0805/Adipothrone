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
    private float poisonInterval = 0; // 毒のダメージ間隔

    /// <summary>
    /// 現在アクティブなすべてのエフェクトをリアルタイムで管理する辞書
    /// </summary>
    private Dictionary<StatusEffectType, PlayerEffectStates> activeEffects = new();

    // 各ステータスのバフ上限値
    public int attackBuffLimitLevel { get; private set; } =
        GameConstants.DEFAULT_ATTACK_BUFF_LIMIT_LEVEL; // 攻撃力バフの上限
    public int defenceBuffLimitLevel { get; private set; } =
        GameConstants.DEFAULT_DEFENSE_BUFF_LIMIT_LEVEL; // 防御力バフの上限
    public int speedBuffLimitLevel { get; private set; } =
        GameConstants.DEFAULT_SPEED_BUFF_LIMIT_LEVEL; // スピードバフの上限
    public int luckBuffLimitLevel { get; private set; } =
        GameConstants.DEFAULT_LUCK_BUFF_LIMIT_LEVEL; // 運バフの上限

    private Coroutine poisonCoroutine = null; // 毒の効果を管理するコルーチン
    #region Events
    public event Action<StatusEffectType> OnBuffApplied; // バフが適用されたときに呼び出されるイベント
    public event Action OnChangeBuffLimit; // バフの上限が変化したときに呼び出されるイベント
    public event Action OnSpeedEffectChanged; // スピードエフェクトが変化したときに呼び出されるイベント
    #endregion

    private PlayerManager playerManager;
    private PlayerLevelManager playerLevelManager;
    private PlayerBodyManager playerBodyManager;
    private bool isTalking = false; // 会話状態を保存するローカル変数

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

        // イベントを購読する
        GameManager.OnTalkingStateChanged += HandleTalkingStateChanged;

        //起動時にローカルデータを初期化する
        InitializeLocalEffects();
    }

    private void OnDisable()
    {
        // オブジェクトが非アクティブになったら、購読を解除（メモリリーク防止）
        GameManager.OnTalkingStateChanged -= HandleTalkingStateChanged;
    }

    private void Update()
    {
        // ゲームがポーズ中でなく、メニューも開いていない場合のみ効果を更新
        if (Time.timeScale > 0 && (!UIManager.instance?.isMenuOpen ?? false) && !isTalking)
        {
            UpdatePlayerEffects();

            // 毒状態であれば、継続ダメージのコルーチンを開始/停止する
            if (GetRemainingTime(StatusEffectType.Poison) > 0 && poisonCoroutine == null)
            {
                poisonCoroutine = StartCoroutine(ApplyPoisonEffect());
            }
            else if (GetRemainingTime(StatusEffectType.Poison) <= 0 && poisonCoroutine != null)
            {
                StopCoroutine(poisonCoroutine);
                poisonCoroutine = null;
            }
        }
    }

    /// <summary>
    /// マネージャーが管理するローカルのエフェクト辞書を初期化・ロードする
    /// </summary>
    private void InitializeLocalEffects()
    {
        activeEffects.Clear();

        // activeEffectsにセーブデータを反映
        // PlayerEffectStatesはクラス（参照型）であるため、
        // セーブデータのインスタンスを直接格納すると、リアルタイム更新時に
        // セーブデータ（savedata）まで変更されてしまう。
        // それを防ぐため、必ず new で新しいインスタンスを作成し、値をコピーする。
        activeEffects[StatusEffectType.Attack] = new PlayerEffectStates(
            (int)StatusEffectType.Attack,
            0,
            0
        );
        activeEffects[StatusEffectType.Defense] = new PlayerEffectStates(
            (int)StatusEffectType.Defense,
            0,
            0
        );
        activeEffects[StatusEffectType.Speed] = new PlayerEffectStates(
            (int)StatusEffectType.Speed,
            0,
            0
        );
        activeEffects[StatusEffectType.Luck] = new PlayerEffectStates(
            (int)StatusEffectType.Luck,
            0,
            0
        );
        activeEffects[StatusEffectType.Poison] = new PlayerEffectStates(
            (int)StatusEffectType.Poison,
            0,
            0
        );

        var effectSaveData = GameManager.instance.savedata.PlayerStatus.playerEffectStates;
        if (effectSaveData != null)
        {
            // セーブデータから効果状態をロードして反映
            foreach (var savedEffect in effectSaveData)
            {
                if (activeEffects.ContainsKey((StatusEffectType)savedEffect.effectTypeNumber))
                {
                    activeEffects[(StatusEffectType)savedEffect.effectTypeNumber] =
                        new PlayerEffectStates(
                            savedEffect.effectTypeNumber,
                            savedEffect.deltaValue,
                            savedEffect.remainingTime
                        );
                }
            }
        }
        else
        {
            Debug.LogError("PlayerEffectStatesのセーブデータが見つかりません。", this);
        }
    }

    #region Buff/Debuff Management
    /// <summary>
    /// 指定したステータスのバフ上限レベルをセーブデータ上で加算（または減算）します。
    /// </summary>
    /// <param name="statusEffectType">対象のステータスタイプ</param>
    /// <param name="plus">加算するレベル量（減算する場合は負の値を指定）</param>
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

    /// <summary>
    /// セーブデータから最新のバフ上限値を読み込み、ローカルプロパティを更新し、
    /// OnChangeBuffLimitイベントを発火させます。
    /// </summary>
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

    /// <summary>
    /// プレイヤーにバフまたはデバフ効果を適用します。
    /// 効果量は上限値まで加算され、効果時間は既存の時間と比較して長い方が採用されます。
    /// （注：毒(Poison)はこの関数では処理されません）
    /// </summary>
    /// <param name="statusEffectType">適用する効果のタイプ</param>
    /// <param name="multiplier">適用する効果量</param>
    /// <param name="rank">効果のランク（持続時間を決定するため）</param>
    public void ApplyBuffDebuff(
        StatusEffectType statusEffectType,
        float multiplier,
        StatusEffectRank rank
    )
    {
        if (!activeEffects.TryGetValue(statusEffectType, out PlayerEffectStates existingEffect))
        {
            // 本来 InitializeLocalEffects で初期化されているはず
            Debug.LogError($"{statusEffectType} が activeEffects に登録されていません。");
            return;
        }

        float statusEffectduration = StatusEffectUtility.GetDurationByRank(rank); // 効果の持続時間を取得
        if (statusEffectduration <= 0)
        {
            return;
        }

        float effectAmount = existingEffect.deltaValue;

        // 効果の数値を加算する（ただし、上限を超えないようにする）
        switch (statusEffectType)
        {
            case StatusEffectType.Attack:
                effectAmount = Mathf.Min(
                    effectAmount + multiplier,
                    attackBuffLimitLevel * GameConstants.ATTACK_BUFF_VALUE_PER_LEVEL
                );
                OnBuffApplied?.Invoke(statusEffectType); // バフが適用されたときにイベントを発火
                break;
            case StatusEffectType.Defense:
                effectAmount = Mathf.Min(
                    effectAmount + multiplier,
                    defenceBuffLimitLevel * GameConstants.DEFENSE_BUFF_VALUE_PER_LEVEL
                );
                OnBuffApplied?.Invoke(statusEffectType); // バフが適用されたときにイベントを発火
                break;
            case StatusEffectType.Speed:
                effectAmount = Mathf.Min(
                    effectAmount + multiplier,
                    speedBuffLimitLevel * GameConstants.SPEED_BUFF_VALUE_PER_LEVEL
                );
                OnBuffApplied?.Invoke(statusEffectType); // バフが適用されたときにイベントを発火
                break;
            case StatusEffectType.Luck:
                effectAmount = Mathf.Min(
                    effectAmount + multiplier,
                    luckBuffLimitLevel * GameConstants.LUCK_BUFF_VALUE_PER_LEVEL
                );
                OnBuffApplied?.Invoke(statusEffectType); // バフが適用されたときにイベントを発火
                break;
        }

        // 効果を上書き更新
        existingEffect.deltaValue = effectAmount;
        // 効果時間を更新（既存の効果時間と新しい効果時間の最大値を取る）
        existingEffect.remainingTime = Mathf.Max(
            existingEffect.remainingTime,
            statusEffectduration
        );

        SEManager.instance?.PlayPlayerActionSE(SE_PlayerAction.Buff1); // バフの効果音を再生

        // Debug.Log(
        //     $" {statusEffectType} 効果が適用されました。効果量: {effectAmount}, 残り時間: {existingEffect.remainingTime}s"
        // );
    }

    /// <summary>
    /// 指定されたバフタイプの現在の残り時間を取得します。
    /// </summary>
    /// <returns>残り時間（秒）</returns>
    public float GetRemainingTime(StatusEffectType type)
    {
        if (activeEffects.TryGetValue(type, out PlayerEffectStates effect))
        {
            return effect.remainingTime;
        }

        return 0f;
    }

    /// <summary>
    /// 指定されたバフタイプの現在の効果量（DeltaValue）を取得します。
    /// </summary>
    /// <returns>効果量。時間切れの場合は0を返す。</returns>
    public float GetDeltaValue(StatusEffectType type)
    {
        if (activeEffects.TryGetValue(type, out PlayerEffectStates effect))
        {
            // 時間切れなら 0 を返す
            return effect.remainingTime > 0 ? effect.deltaValue : 0f;
        }

        return 0f;
    }

    /// <summary>
    /// リアルタイム管理下にある全エフェクトの効果時間を更新（減算）します。
    /// 時間がゼロになったエフェクトは効果量を0にリセットします。
    /// </summary>
    public void UpdatePlayerEffects()
    {
        // 効果時間を減らす
        foreach (var effect in activeEffects.Values)
        {
            if (effect.remainingTime > 0)
            {
                effect.remainingTime -= Time.deltaTime;

                // 時間が切れた瞬間の処理
                if (effect.remainingTime <= 0)
                {
                    effect.remainingTime = 0;
                    effect.deltaValue = 0; // 効果量も0に戻す

                    // スピードエフェクトが切れた場合のみイベントを発火
                    // [コメント] Speedは移動速度や攻撃速度など、
                    // 他のシステムがリアルタイムで参照する値であるため、
                    // ON/OFFの切り替わりを通知する必要がある。
                    if (effect.effectTypeNumber == (int)StatusEffectType.Speed)
                    {
                        OnSpeedEffectChanged?.Invoke();
                    }
                }
            }
        }
    }

    /// <summary>
    /// セーブ用に、現在のすべてのアクティブなエフェクト状態をリストとして返します。
    /// </summary>
    /// <returns>現在の全エフェクト（Poison含む）のコピー（List型）</returns>
    public List<PlayerEffectStates> GetCurrentEffectStatesForSave()
    {
        // Dictionary の Values (PlayerEffectStates) をそのまま新しい List にコピーして返す
        // （Poisonなどもすべて含まれる）
        return new List<PlayerEffectStates>(activeEffects.Values);
    }

    /// <summary>
    /// 毒の継続ダメージ処理を行うコルーチン。
    /// Update()メソッドによって開始・停止が管理されます。
    /// </summary>
    private IEnumerator ApplyPoisonEffect()
    {
        // この while(true) ループは、毒の残り時間が0になった際に
        // Update() メソッド側で StopCoroutine(poisonCoroutine) が
        // 呼び出されることを前提としています。
        while (true)
        {
            //  毒ダメージ量はリアルタイムで参照する
            // （例：毒耐性アイテムなどで効果量が変動する可能性があるため）
            int currentPoisonDamage = (int)GetDeltaValue(StatusEffectType.Poison);

            // 毒のダメージを適用
            if (currentPoisonDamage > 0)
            {
                playerManager.TakeNormalDamage(currentPoisonDamage);
            }

            // 次のダメージ実行まで待機する
            yield return new WaitForSeconds(poisonInterval);
        }
    }
    #endregion

    #region Final Status Calculation
    /// <summary>
    /// 最終的な攻撃力を計算
    /// </summary>
    public int CalculateFinalAttackPower(int baseAttackPower) // ここでのbaseAttackPowerは「武器の基本攻撃力」を指します
    {
        // PlayerStatusLevelManagerからプレイヤーの基礎攻撃力（整数）を取得
        int playerBaseAttack = playerManager.StatusLevelManager.TotalBaseAttackPower;

        // プレイヤーの基礎攻撃力を、武器威力を引き出す倍率に変換（例：100なら 1.0 = +100%ボーナス）
        float statMultiplier =
            1.0f + (playerBaseAttack / GameConstants.PLAYER_ATTACK_TO_MULTIPLIER_RATE);

        // バフ効果の倍率を取得
        float effectDelta =
            GameConstants.PLAYER_ATTACK_EFFECT_MULTIPLIER * GetDeltaValue(StatusEffectType.Attack);

        // 最終的な基礎倍率
        float multiplier = statMultiplier + effectDelta;

        if (multiplier > 1)
        {
            // PlayerBodyManagerからWP倍率を取得して反映
            multiplier *= playerBodyManager.attackWpScale;
        }
        else if (multiplier <= 0)
        {
            multiplier = GameConstants.MIN_ATTACK_POWER_MULTIPLIER;
        }

        // 武器の基本攻撃力 × 倍率
        int totalDamage = (int)(baseAttackPower * multiplier);
        return Mathf.Max(1, totalDamage); // ダメージ量は1以上にする
    }

    /// <summary>
    /// 最終的な防御力を計算
    /// </summary>
    public int CalculateFinalDefensePower()
    {
        // PlayerStatusLevelManagerからプレイヤーの基礎防御力（経験値レベル分＋ステータスレベル分）を取得
        int totalDefense = playerManager.StatusLevelManager.TotalBaseDefensePower;

        int effectDelta = (int)(
            GameConstants.PLAYER_DEFENSE_EFFECT_MULTIPLIER * GetDeltaValue(StatusEffectType.Defense)
        );

        // バフ効果を加算
        totalDefense += effectDelta;

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
        // ステータスレベルによる素早さの固定値ボーナスを加算
        float statusSpeedBonus = playerManager.StatusLevelManager.SpeedBonus;
        float currentBaseSpeed = baseSpeed + statusSpeedBonus;

        // スピードエフェクトの変化量を取得
        float deltaValue = GetDeltaValue(StatusEffectType.Speed);

        // 変化量が0ならステータスレベル込みの基本速度を返す
        if (deltaValue == 0)
        {
            return currentBaseSpeed;
        }

        float effectDelta = GameConstants.PLAYER_MOVE_SPEED_EFFECT_MULTIPLIER * deltaValue;

        // PlayerBodyManagerからWP倍率を取得して反映
        float finalSpeed = currentBaseSpeed * (1f + effectDelta);

        return Mathf.Min(finalSpeed, GameConstants.PLAYER_MOVE_MAX_SPEED); // 最大速度を超えないようにする
    }

    /// <summary>
    /// 剣の最終的な攻撃速度を計算
    /// </summary>
    public float CalculateFinalBladeMoveSpeed(float baseSpeed)
    {
        // スピードエフェクトの変化量を取得
        float deltaValue = GetDeltaValue(StatusEffectType.Speed);

        // 変化量が0なら基本速度を返す
        if (deltaValue == 0)
        {
            return baseSpeed;
        }

        float effectDelta = GameConstants.PLAYER_WEAPON_SPEED_EFFECT_MULTIPLIER * deltaValue;

        // PlayerBodyManagerからWP倍率を取得して反映
        float finalSpeed = baseSpeed / (1f + effectDelta); // スピードエフェクトは攻撃速度を上げるほど数値が大きくなるが、実際の速度は速くなるように逆数で反映させる

        return Mathf.Max(finalSpeed, GameConstants.PLAYER_BLADE_MIN_SPEED); // 最小速度を下回らないようにする
    }
    #endregion

    /// <summary>
    /// GameManagerから会話状態の変更通知を受け取る
    /// </summary>
    private void HandleTalkingStateChanged(bool talkState)
    {
        isTalking = talkState;
    }
}

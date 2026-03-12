using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MyGame.CameraControl;
using UnityEngine;

/// <summary>
/// プレイヤーの基本的なステータス（HP, WP, レベル等）とアクション（移動、アイテム使用等）を管理するクラス。
/// バフ・デバフなどの一時的な効果は PlayerEffectManager が担当します。
/// </summary>
public class PlayerManager : MonoBehaviour
{
    #region Singleton & Components

    // シングルトンインスタンス
    public static PlayerManager instance { get; private set; }

    /// <summary>
    /// バフ・デバフなど一時的な効果を管理するマネージャーへの参照。
    /// </summary>
    public PlayerEffectManager EffectManager { get; private set; }

    [Header("References")]
    [SerializeField]
    private HealItemDatabase healItemDatabase;

    private FastTravelManager fastTravelManager;
    private Heroin_move heroinMove;
    private GameObject playerGameObject;

    /// <summary>
    /// プレイヤーのGameObjectへの参照を取得します。
    /// 最初のアクセス時にタグ検索で見つけてキャッシュします。
    /// </summary>
    public GameObject PlayerGameObject
    {
        get
        {
            if (playerGameObject == null)
                playerGameObject = GameObject.FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME);
            return playerGameObject;
        }
    }

    #endregion

    #region Status Parameters

    // プレイヤーが操作不能状態（強制移動中など）のときtrue
    public bool isControlLocked { get; private set; } = false;

    // 被弾時に付与される無敵時間（秒）
    private float damageInvincibilityTime = 1.25f;

    // ステータス関連
    public int playerMaxHP { get; private set; } = GameConstants.GetMaxHP(1); // プレイヤーの最大HP
    public int playerMaxWP { get; private set; } = GameConstants.GetMaxWP(1); // プレイヤーの最大WP
    public float LastDamageTime { get; private set; } = float.MinValue; // 最後にダメージを受けた時間

    // 演出用パラメータ
    private float fadeOutDuration = 2f; // フェードアウトにかかる時間
    private bool isDying = false; // 死亡演出が進行中かどうかのフラグ
    private bool isTalking = false; // 会話中かどうかのフラグ

    // インベントリソート用辞書（アイテムID -> 並び順インデックス）
    private Dictionary<int, int> itemSortOrderMap;

    // シールドコントローラーへの参照
    private PlayerShieldController shieldController;

    #endregion

    #region Events

    public event Action OnQuickSlotAssigned; // クイックスロットが割り当てられたとき
    public event Action<int> OnChangeHP; // HPが変化したとき
    public event Action<int> OnChangeMaxHP; // 最大HPが変化したとき
    public event Action<int> OnChangeMaxWP; // 最大WPが変化したとき
    public event Action<int> OnChangeWP; // WPが変化したとき
    public event Action<PlayerAttackType> OnChangeAttackType; // 攻撃方法が変化したとき
    public event Action<KnockbackData> OnDamageReaction; // ダメージリアクション時
    public event Action OnChangePlayerMoney; // 所持金が変化したとき
    public event Action OnPlayerDied; // 死亡時
    public event Action OnPlayerRevived; // 復活時
    public event Action<PlayerStatusBoolName, bool> OnBoolStatusChanged;
    #endregion

    #region Unity Lifecycle Methods

    private void Awake()
    {
        // このPlayerManagerはシーンごとに配置し、DontDestroyOnLoadは使用しません。
        // プレイヤーデータ（HP/WP/バフ等）はGameManager.savedataに集約されており、
        // 毎シーンAwake時に同期を行うことでステータスを維持します。
        // 毒やバフなどの一時効果は保存データと連携し、シーンまたぎの継続性も確保しています。
        // シーンごとの参照（UI, Cameraなど）との依存関係を避けるため、シーンローカルの設計としています。
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 同一GameObjectにアタッチされているPlayerEffectManagerを取得し、連携する
        EffectManager = GetComponent<PlayerEffectManager>();
        if (EffectManager == null)
        {
            Debug.LogError("PlayerEffectManagerが同じGameObjectにアタッチされていません！");
        }

        isControlLocked = false; // 初期状態では操作可能

        if (healItemDatabase == null)
            Debug.LogError("HealItemDatabaseが設定されていません");

        // Awakeの最後に、ソート順マップの初期化処理を実行
        InitializeItemSortOrderMap();
    }

    public void Start()
    {
        // プレイヤー参照の初期化
        if (playerGameObject == null)
        {
            playerGameObject = GameObject.FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME);
            if (playerGameObject == null)
            {
                Debug.LogError("PlayerGameObjectが見つかりません");
            }
            else
            {
                heroinMove = playerGameObject.GetComponent<Heroin_move>();
                shieldController = playerGameObject.GetComponent<PlayerShieldController>();
            }
        }

        // ファストトラベルマネージャーの取得
        fastTravelManager = PersistentManagers.instance.GetComponentInChildren<FastTravelManager>();
        if (fastTravelManager == null)
        {
            Debug.LogError("FastTravelManagerが見つかりません");
        }

        // シーン遷移後のプレイヤー移動処理（スポーン地点が指定されている場合）
        if (GameManager.instance.crossScenePlayerSpawnPoint != null)
        {
            StartCoroutine(PlayerMove(GameManager.instance.crossScenePlayerSpawnPoint.Value));
            GameManager.instance.crossScenePlayerSpawnPoint = null; // 一度使用したらリセット
        }

        // 会話状態の変更イベントを購読
        GameManager.OnTalkingStateChanged += HandleTalkingStateChanged;
    }

    private void OnDestroy()
    {
        GameManager.OnTalkingStateChanged -= HandleTalkingStateChanged;
    }

    #endregion

    #region Status Data Accessors (SaveData Bridge)
    // ここではリフレクションを使用して、Enum名に対応する PlayerStatusData のフィールドへアクセスしています。
    // これにより、Enum定義とフィールド名が一致していれば、自動的に値の取得・設定が可能です。

    // --- Bool Status ---
    public bool GetPlayerBoolStatus(PlayerStatusBoolName flag)
    {
        var field = typeof(PlayerStatusData).GetField(flag.ToString());
        if (field != null && field.FieldType == typeof(bool))
        {
            return (bool)field.GetValue(GameManager.instance.savedata.PlayerStatus);
        }
        Debug.LogError($"[GetBool] 無効なPlayerStatusBoolName: {flag}");
        return false;
    }

    public void SetPlayerBoolStatus(PlayerStatusBoolName flag, bool value)
    {
        var field = typeof(PlayerStatusData).GetField(flag.ToString());
        if (field != null && field.FieldType == typeof(bool))
        {
            bool oldValue = (bool)field.GetValue(GameManager.instance.savedata.PlayerStatus);
            if (oldValue == value)
                return; // 値が変わらなければ何もしない

            field.SetValue(GameManager.instance.savedata.PlayerStatus, value);
            OnBoolStatusChanged?.Invoke(flag, value); // 汎用イベントを発行
        }
        else
        {
            Debug.LogError($"[SetBool] 無効なPlayerStatusBoolName: {flag}");
        }
    }

    // --- Int Status ---
    public int GetPlayerIntStatus(PlayerStatusIntName flag)
    {
        var field = typeof(PlayerStatusData).GetField(flag.ToString());
        if (field != null && field.FieldType == typeof(int))
        {
            return (int)field.GetValue(GameManager.instance.savedata.PlayerStatus);
        }
        Debug.LogError($"[GetInt] 無効なPlayerStatusIntName: {flag}");
        return 0;
    }

    public void SetPlayerIntStatus(PlayerStatusIntName flag, int value)
    {
        var field = typeof(PlayerStatusData).GetField(flag.ToString());
        if (field != null && field.FieldType == typeof(int))
        {
            field.SetValue(GameManager.instance.savedata.PlayerStatus, value);
        }
        else
        {
            Debug.LogError($"[SetInt] 無効なPlayerStatusIntName: {flag}");
        }
    }

    // --- Float Status ---
    public float GetPlayerFloatStatus(PlayerStatusFloatName flag)
    {
        var field = typeof(PlayerStatusData).GetField(flag.ToString());
        if (field != null && field.FieldType == typeof(float))
        {
            return (float)field.GetValue(GameManager.instance.savedata.PlayerStatus);
        }
        Debug.LogError($"[GetFloat] 無効なPlayerStatusFloatName: {flag}");
        return 0f;
    }

    public void SetPlayerFloatStatus(PlayerStatusFloatName flag, float value)
    {
        var field = typeof(PlayerStatusData).GetField(flag.ToString());
        if (field != null && field.FieldType == typeof(float))
        {
            field.SetValue(GameManager.instance.savedata.PlayerStatus, value);
        }
        else
        {
            Debug.LogError($"[SetFloat] 無効なPlayerStatusFloatName: {flag}");
        }
    }

    // --- Attack Type ---
    public void SetPlayerAttackType(PlayerAttackType attackType)
    {
        var status = GameManager.instance.savedata.PlayerStatus;
        if (status == null)
        {
            Debug.LogWarning("PlayerStatusDataがnullです");
            return;
        }
        status.playerAttackType = attackType;
        OnChangeAttackType?.Invoke(attackType); // 攻撃方法が変化したときに呼び出されるイベントを発火
    }

    public PlayerAttackType GetPlayerAttackType()
    {
        var status = GameManager.instance.savedata.PlayerStatus;
        if (status == null)
        {
            Debug.LogWarning("PlayerStatusDataがnullです");
            return PlayerAttackType.None; // デフォルト値を返す
        }
        return status.playerAttackType;
    }

    // --- Max Status ---
    /// <summary>
    /// 外部システム（PlayerLevelManagerなど）から最大HPを更新し、イベントを発行します。
    /// </summary>
    public void SetMaxHP(int newMaxHP)
    {
        if (playerMaxHP == newMaxHP)
            return;
        playerMaxHP = newMaxHP;
        OnChangeMaxHP?.Invoke(playerMaxHP);
    }

    /// <summary>
    /// 外部システム（PlayerLevelManagerなど）から最大WPを更新し、イベントを発行します。
    /// </summary>
    public void SetMaxWP(int newMaxWP)
    {
        if (playerMaxWP == newMaxWP)
            return;
        playerMaxWP = newMaxWP;
        OnChangeMaxWP?.Invoke(playerMaxWP);
    }

    #endregion

    #region HP Management & Damage Logic

    /// <summary>
    /// 【通常ダメージ】を受け付け、防御力を考慮した最終ダメージを計算して適用します。
    /// 外部（敵の攻撃やプレイヤーの被弾処理）からはこの関数を呼び出します。
    /// </summary>
    public void TakeNormalDamage(int baseDamage, KnockbackData knockbackData = default)
    {
        // PlayerEffectManagerから最終的な防御力を取得
        int damageReduction = EffectManager.CalculateFinalDefensePower();

        // 最終ダメージを計算（最低でも0ダメージ）
        int finalDamage = Mathf.Max(0, baseDamage - damageReduction);

        // ダメージが1以上あれば、実際にHPを減らす処理を呼び出す
        if (finalDamage > 0)
        {
            ApplyDamage(finalDamage, knockbackData);
        }
    }

    /// <summary>
    /// 【最大HP】に対する割合でダメージを与えます（例: 0.25f = 25%）。
    /// </summary>
    public void DamageHPByMaxHPRatio(float damageRatio, KnockbackData knockbackData = default)
    {
        if (damageRatio <= 0)
            return;

        // 最低でも1ダメージは保証する
        int damageAmount = Mathf.Max(1, Mathf.RoundToInt(playerMaxHP * damageRatio));
        ApplyDamage(damageAmount, knockbackData);
    }

    /// <summary>
    /// 【現在HP】に対する割合でダメージを与えます（例: 0.5f = 50%）。
    /// HPが低いほどダメージ量が減るため、この攻撃単体で倒されることはありません。
    /// </summary>
    public void DamageHPByCurrentHPRatio(float damageRatio, KnockbackData knockbackData = default)
    {
        if (damageRatio <= 0)
            return;

        int currentHP = GetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP);
        // 最低でも1ダメージは保証する
        int damageAmount = Mathf.Max(1, Mathf.RoundToInt(currentHP * damageRatio));
        ApplyDamage(damageAmount, knockbackData);
    }

    /// <summary>
    /// 計算済みの最終ダメージをHPに適用し、死亡判定などを行う内部専用関数。
    /// </summary>
    /// <param name="damage">適用するダメージ量</param>
    /// <param name="knockbackData">ノックバック情報</param>
    private void ApplyDamage(int damage, KnockbackData knockbackData = default)
    {
        // 全てのダメージ処理の入口で、まず無敵状態をチェックする
        if (heroinMove != null && heroinMove.IsImmune)
        {
            // プレイヤーが無敵状態なら、ダメージ処理を一切行わずに終了
            return;
        }

        // 【多段ヒット防止の安全装置】
        // Heroin_move側の無敵フラグの反映遅れや、参照外れに備えて、
        // 前回ダメージを受けてから指定時間（damageInvincibilityTime）経過していなければ確実にはじく
        if (Time.time - LastDamageTime < damageInvincibilityTime)
        {
            return;
        }

        // 会話中はダメージを受けないようにする
        if (isTalking)
        {
            return;
        }

        // シールド展開中のダメージ処理
        if (
            shieldController != null
            && shieldController.isShieldActive
            && !shieldController.isBroken
        )
        {
            // ダメージの割合を計算 (damage / playerMaxHP)
            float shieldDamageRatio = (float)damage / playerMaxHP;

            // シールドの耐久値を減らす
            shieldController.TakeShieldDamage(shieldDamageRatio);

            // シールドでダメージを防いだため、HPの減少処理をここでスキップする
            // ※必要であれば、ノックバック（OnDamageReaction?.Invoke(knockbackData);）や
            // シールド被弾用のSEをここで再生することも可能です。
            return;
        }

        // 既に死亡処理が始まっている場合は、重複して実行しない
        if (isDying)
            return;

        // ダメージが1以上発生する場合、被弾時刻を更新する
        if (damage > 0)
        {
            LastDamageTime = Time.time;
        }

        int hpBeforeDamage = GetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP);
        int playerCurrentMaxHP = playerMaxHP;

        // HPがGutsEffectThresholdの閾値以上あるかどうかの条件を確認（食いしばり効果）
        bool hasGutsEffect =
            (float)hpBeforeDamage / playerCurrentMaxHP >= GameConstants.GUTS_EFFECT_THRESHOLD;

        SEManager.instance?.PlayPlayerActionSE(SE_PlayerAction.Damage1); // ダメージの効果音を鳴らす

        int hpAfterDamage = hpBeforeDamage - damage;
        SetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP, hpAfterDamage); // HPを更新

        // HPを減らした直後に、リアクションを促すイベントを発行する
        OnDamageReaction?.Invoke(knockbackData);

        // HPが変化したときに呼び出されるイベントを発火
        // 復活の処理の関係から、OnPlayerDiedイベントの前に発火させる
        OnChangeHP?.Invoke(hpAfterDamage);

        // 死亡判定
        if (hpAfterDamage <= 0)
        {
            if (hasGutsEffect)
            {
                // 閾値以上だった場合、HPを1にして耐える
                SetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP, 1);
                OnChangeHP?.Invoke(1);
            }
            else
            {
                // 死亡処理を開始
                StartDeathProcess();
            }
        }
    }

    /// <summary>
    /// 死亡処理を開始します。
    /// </summary>
    private void StartDeathProcess()
    {
        isDying = true; // 死亡処理フラグを立て、重複実行を防ぐ
        SetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP, 0); // HPを0に確定

        SEManager.instance.PlayPlayerActionSE(SE_PlayerAction.Death1);

        // プレイヤーが死亡したときに呼び出されるイベントを発火
        // 復活処理の関係から、ExecuteDeathFastTravelの前に発火させる
        OnPlayerDied?.Invoke();

        bool isEnableSave = SaveLoadManager.instance?.isEnableSave ?? false;
        StartCoroutine(DeathSequenceCoroutine(isEnableSave));
    }

    /// <summary>
    /// 死亡演出（時間停止、フェード、遅延）を順次実行します。
    /// </summary>
    private IEnumerator DeathSequenceCoroutine(bool isSaveEnabled)
    {
        // 1. 時間を停止
        TimeManager.instance?.SetEnemyMovePaused(true); // 敵の動きを停止

        // 2. フェードアウトを開始
        if (FadeCanvas.instance != null)
        {
            FadeCanvas.instance.FadeOut(isSaveEnabled ? fadeOutDuration : fadeOutDuration - 0.5f);
        }
        else
        {
            Debug.LogWarning("FadeCanvasのインスタンスが見つかりません。");
        }

        // 3. さらに指定したfadeOutDuration秒数待機
        yield return new WaitForSecondsRealtime(
            isSaveEnabled ? fadeOutDuration : fadeOutDuration - 0.5f
        );

        // 4. 時間の停止を解除し、最終処理を実行
        TimeManager.instance?.SetEnemyMovePaused(false); // 敵の動きを再開

        if (isSaveEnabled)
        {
            fastTravelManager?.ExecuteDeathFastTravel(); // 死亡時のファストトラベルを実行
        }
        else
        {
            GameOverUIManager.instance.StartGameOver(); // ゲームオーバーの関数を呼び出す
        }

        // 5. 死亡処理フラグをリセット
        isDying = false;
    }

    /// <summary>
    /// 指定値をHP回復します。死亡からの蘇生判定もここで行います。
    /// </summary>
    public void HealHP(int heal)
    {
        // 0未満の回復量は無意味なので終了
        if (heal < 0)
            return;

        int currentHP = GetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP);
        int maxHP = playerMaxHP;
        bool wasDead = currentHP <= 0; // 回復前に死亡していたかを記録

        // すでにHPが満タンで、かつ死んでいない場合は回復不要
        if (currentHP >= maxHP && !wasDead)
            return;

        // 回復後のHPを計算し、0と最大値の間に収める
        int newHP = Mathf.Clamp(currentHP + heal, 0, maxHP);

        // HPに変化がなければイベント不要
        if (newHP == currentHP)
            return;

        SetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP, newHP);

        // 死亡状態から復活した場合のイベントを発火
        if (wasDead && newHP > 0)
        {
            OnPlayerRevived?.Invoke();
        }

        OnChangeHP?.Invoke(newHP);
    }

    /// <summary>
    /// HPを全回復します。
    /// </summary>
    public void RestoreFullHP()
    {
        int maxHP = playerMaxHP;
        int HP = GetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP);
        int recoverAmount = maxHP - HP;
        if (recoverAmount > 0)
        {
            HealHP(recoverAmount);
        }
    }

    /// <summary>
    /// 現在のプレイヤーのHP割合（0.0f ～ 1.0f）を取得します。
    /// UIの更新や条件分岐（HPが半分以下など）に使用します。
    /// </summary>
    /// <returns>現在のHP割合</returns>
    public float GetNormalizedHP()
    {
        // ゼロ除算を防止
        if (playerMaxHP <= 0)
        {
            return 0f;
        }

        int currentHP = GetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP);

        // 計算結果が0～1の間に収まるようにClamp01を使用
        return Mathf.Clamp01((float)currentHP / playerMaxHP);
    }

    /// <summary>
    /// デバッグや特殊イベント用：最大HPの制限を無視して、指定した値にHPを強制設定します。
    /// 死亡状態からの蘇生判定およびイベント発行も行います。
    /// </summary>
    /// <param name="targetHP">設定したいHPの値</param>
    public void ForceSetHP(int targetHP)
    {
        int currentHP = GetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP);
        bool wasDead = currentHP <= 0; // 変更前に死亡していたかを記録

        // マイナス値にはならないように下限のみ0で制限
        int newHP = Mathf.Max(0, targetHP);

        // HPに変化がなければイベント不要
        if (newHP == currentHP)
            return;

        SetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP, newHP);

        // 死亡状態から復活した場合のイベントを発火
        if (wasDead && newHP > 0)
        {
            OnPlayerRevived?.Invoke();
        }

        OnChangeHP?.Invoke(newHP);
    }

    #endregion

    #region WP Management Logic

    /// <summary>
    /// WPを回復します。
    /// </summary>
    public void HealWP(int heal)
    {
        int maxWP = playerMaxWP;
        int currentWP = GetPlayerIntStatus(PlayerStatusIntName.playerCurrentWP);

        // すでにWPが満タンなら何もしない
        if (currentWP >= maxWP)
            return;

        int newWP = Mathf.Min(currentWP + heal, maxWP);
        SetPlayerIntStatus(PlayerStatusIntName.playerCurrentWP, newWP);
        OnChangeWP?.Invoke(newWP);
    }

    /// <summary>
    /// WPを消費します。
    /// </summary>
    public void DamageWP(int damage)
    {
        if (!(GameManager.instance?.savedata?.PlayerStatus?.isChangeWP ?? false))
            return;

        int currentWP = GetPlayerIntStatus(PlayerStatusIntName.playerCurrentWP);
        int newWP = Mathf.Max(0, currentWP - damage); // 0未満にならないようにする

        GameManager.instance.savedata.PlayerStatus.playerCurrentWP = newWP;
        OnChangeWP?.Invoke(newWP);
    }

    /// <summary>
    /// WPの値を直接設定します（演出用）。
    /// </summary>
    public void SetWP(int wp)
    {
        if (!(GameManager.instance?.savedata?.PlayerStatus?.isChangeWP ?? false))
        {
            Debug.LogWarning(
                "WPの変更が無効化されています。PlayerStatusDataのisChangeWPを確認してください。"
            );
            return;
        }

        SetPlayerIntStatus(PlayerStatusIntName.playerCurrentWP, wp);
        OnChangeWP?.Invoke(wp);
    }

    /// <summary>
    /// WP消費のバッファを加算し、1以上になった場合は整数部分だけWPにダメージを与え、
    /// 余りをバッファとして保存します。
    /// (持続ダメージなどで小数点のWP消費を扱うために使用します)
    /// </summary>
    /// <param name="addedBufferValue">加算するWP消費のバッファ値（小数対応）</param>
    public void AddWpConsumptionBuffer(float addedBufferValue)
    {
        float currentWpConsumptionBuffer = GetPlayerFloatStatus(
            PlayerStatusFloatName.wpConsumptionBuffer
        );
        currentWpConsumptionBuffer += addedBufferValue;

        // 合計値が1以上であれば、整数部分をWPダメージとして反映
        if (currentWpConsumptionBuffer >= 1f)
        {
            int intPart = Mathf.FloorToInt(currentWpConsumptionBuffer); // 整数部分を取得
            currentWpConsumptionBuffer -= intPart; // 小数部分のみを残す
            DamageWP(intPart); // WPにダメージを加える
        }

        SetPlayerFloatStatus(PlayerStatusFloatName.wpConsumptionBuffer, currentWpConsumptionBuffer);
    }

    #endregion

    #region Item & Inventory System

    /// <summary>
    /// 指定した回復アイテムを使用し、HP・WPの回復および特殊効果を適用します。
    /// </summary>
    /// <param name="ID">使用する回復アイテムのID</param>
    /// <returns>アイテムの使用に成功したかどうか</returns>
    public bool UseHealItem(Enum ID)
    {
        var ItemInventory = GameManager.instance.savedata.ItemInventoryData;
        if (ItemInventory.ownedItems == null)
        {
            Debug.Log("ItemInventoryが存在しません");
            return false;
        }

        // アイテムを消費（個数を減らす）
        if (!ItemInventory.UseItem(ID, 1))
        {
            return false;
        }

        SEManager.instance?.PlayPlayerActionSE(SE_PlayerAction.HealItem1); // 効果音を鳴らす

        HealItemData item = healItemDatabase.GetItemByID(ID); // itemのDataを取得
        if (item != null)
        {
            if (item.hpHealAmount > 0)
                HealHP(item.hpHealAmount);
            if (item.wpHealAmount > 0)
                HealWP(item.wpHealAmount);

            // 特殊効果の適用をPlayerEffectManagerに委任する
            foreach (var effect in item.buffEffects)
            {
                effect.EffectApply();
            }
        }
        return true; // アイテムの使用に成功
    }

    /// <summary>
    /// 即座に使用できるアイテムを入れ替える関数（クイックスロットへの登録）
    /// </summary>
    public void AssignItemToQuickSlot(Enum ID, int quickSlotIndex)
    {
        int IDNumber = EnumIDUtility.ToID(ID);
        var sourceList = GameManager.instance.savedata.ItemInventoryData.ownedItems;
        var quickList = GameManager.instance.savedata.QuickItemData.ownedItems;

        var item = sourceList.Find(e => e.itemID == IDNumber);
        if (item == null)
        {
            Debug.LogWarning($"ItemID {IDNumber} は所持していません");
            return;
        }

        while (quickList.Count <= quickSlotIndex)
        {
            quickList.Add(null); // 空スロット埋め
        }

        quickList[quickSlotIndex] = item;
        SEManager.instance?.PlayUISE(SE_UI.Register1); // 登録の効果音を鳴らす
        OnQuickSlotAssigned?.Invoke();
    }

    /// <summary>
    /// HealItemDatabaseからアイテムの正しい表示順を読み込み、辞書としてキャッシュします。
    /// これによりソート処理を高速化します。
    /// </summary>
    private void InitializeItemSortOrderMap()
    {
        itemSortOrderMap = new Dictionary<int, int>();
        if (healItemDatabase == null)
        {
            Debug.LogError("HealItemDatabaseがPlayerManagerに設定されていません。");
            return;
        }

        // データベースのリストの順番（i）が、そのまま並び順の優先度となる
        for (int i = 0; i < healItemDatabase.healItems.Count; i++)
        {
            // Enumをintに変換してIDを取得
            int itemId = (int)healItemDatabase.healItems[i].itemID;
            if (!itemSortOrderMap.ContainsKey(itemId))
            {
                itemSortOrderMap.Add(itemId, i);
            }
        }
    }

    /// <summary>
    /// 所持アイテムリストを、データベースの定義順に並び替えます。
    /// </summary>
    public void SortOwnedItems()
    {
        var inventory = GameManager.instance?.savedata?.ItemInventoryData;
        if (inventory == null)
        {
            Debug.LogError("SaveDataのItemInventoryDataが存在しません。");
            return;
        }

        // LINQのOrderByを使い、キャッシュした辞書の並び順に従ってリストをソート
        inventory.ownedItems = inventory
            .ownedItems.OrderBy(item =>
                // 辞書からアイテムIDに対応する並び順の番号を取得する
                // もし辞書にないアイテム（＝データベースにない未知のアイテム）の場合、
                // int.MaxValueを返すことで、必ずリストの末尾に来るようにする
                itemSortOrderMap.TryGetValue(item.itemID, out int order)
                    ? order
                    : int.MaxValue
            )
            .ToList();
    }

    #endregion

    #region Movement & Physics Control

    /// <summary>
    /// 強制移動などの開始（操作ロック）
    /// </summary>
    public void LockControl()
    {
        isControlLocked = true;
    }

    /// <summary>
    /// 強制移動などの終了（操作ロック解除）
    /// </summary>
    public void UnlockControl()
    {
        isControlLocked = false;
    }

    /// <summary>
    /// プレイヤーの物理挙動（移動・ジャンプなど）を有効/無効に切り替えます。
    /// Rigidbody2DのisKinematicを操作して物理演算の影響を制御します。
    /// </summary>
    /// <param name="isActive">trueで有効化、falseで無効化</param>
    public void SetPlayerPhysicsActive(bool isActive)
    {
        var rb = PlayerGameObject?.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            if (!isActive)
            {
                rb.isKinematic = true;
                rb.velocity = Vector2.zero;
            }
            else
            {
                rb.isKinematic = false; // 必要なら元の状態を保存して復元するロジックにする
                rb.velocity = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// プレイヤーを強制的に指定座標へ移動させます。
    /// カメラの追従が完了するまで待機します。
    /// </summary>
    public IEnumerator PlayerMove(Vector2 targetPoint)
    {
        if (PlayerGameObject == null)
        {
            Debug.LogError("PlayerGameObjectが見つかりません");
            yield break; // PlayerGameObjectが見つからない場合は処理を中止
        }

        playerGameObject.transform.position = new Vector2(targetPoint.x, targetPoint.y); // プレイヤーの座標を移動

        // 座標移動直後に物理演算のトランスフォーム同期を強制実行する
        // これにより、CameraMoveAreaのOnTriggerEnter2Dが即座に呼ばれ、
        // カメラ移動開始前に「新しいエリアのConfiner設定」が完了するようになります。
        Physics2D.SyncTransforms();

        if (CameraManager.instance != null)
        {
            // CameraMoveコルーチンが完了するまで待つ
            yield return CameraManager.instance.StartCoroutine(CameraManager.instance.CameraMove());
        }
        else
        {
            Debug.LogError("CameraManagerが存在しません");
        }
    }

    /// <summary>
    /// プレイヤーの現在のワールド座標をVector2で返します。
    /// プレイヤーが見つからない場合は(0, 0)を返します。
    /// </summary>
    public Vector2 GetPlayerPosition()
    {
        // playerGameObjectがまだキャッシュされていなければ、念のため探す
        if (playerGameObject == null)
        {
            playerGameObject = GameObject.FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME);
        }

        // それでも見つからなければ、警告を出してデフォルト値を返す
        if (playerGameObject == null)
        {
            Debug.LogWarning("プレイヤーのGameObjectが見つからないため、座標を取得できません。");
            return Vector2.zero;
        }

        // 見つかれば、その座標を返す
        return playerGameObject.transform.position;
    }

    #endregion

    #region Other Actions

    /// <summary>
    /// プレイヤーの所持金を変更する関数
    /// </summary>
    /// <param name="number">所持金の増減値（正で増加、負で減少）</param>
    public void ChangeMoney(int number)
    {
        var status = GameManager.instance.savedata.PlayerStatus;
        if (status == null)
        {
            Debug.LogWarning("PlayerStatusDataがnullです");
            return;
        }
        status.playerMoney += number;
        if (status.playerMoney < 0)
        {
            status.playerMoney = 0;
        }
        OnChangePlayerMoney?.Invoke(); // 所持金が変化したときに呼び出されるイベントを発火
    }

    /// <summary>
    /// 指定した時間だけプレイヤーを無敵状態にします。
    /// Heroin_moveコンポーネントのEnableInvincibilityメソッドを呼び出します。
    /// </summary>
    /// <param name="time">無敵状態にする時間（秒）</param>
    public void EnableInvincibility(float time)
    {
        if (playerGameObject != null)
        {
            Heroin_move heroin_Move = playerGameObject.GetComponent<Heroin_move>();
            if (heroin_Move != null)
            {
                heroin_Move.EnableInvincibility(time);
            }
        }
    }

    #endregion
    #region Event Handlers
    /// <summary>
    /// 会話状態の変更を受け取る
    /// </summary>
    private void HandleTalkingStateChanged(bool talkState)
    {
        isTalking = talkState;
    }
    #endregion
}

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
    // シングルトンインスタンス
    public static PlayerManager instance { get; private set; }

    // プレイヤーが操作不能状態（強制移動中など）のときtrue
    public bool isControlLocked { get; private set; } = false;

    [SerializeField]
    private HealItemDatabase healItemDatabase;
    private FastTravelManager fastTravelManager;
    private GameObject playerGameObject;
    private Heroin_move heroinMove;
    public int playerMaxHP { get; private set; } = GameConstants.GetMaxHP(1); // プレイヤーの最大HP
    public int playerMaxWP { get; private set; } = GameConstants.GetMaxWP(1); // プレイヤーの最大WP
    private float fadeOutDuration = 2f; // フェードアウトにかかる時間
    private bool isDying = false; //死亡演出が進行中かどうかのフラグ
    #region Events
    public event Action OnQuickSlotAssigned; // クイックスロットが割り当てられたときに呼び出されるイベント
    public event Action<int> OnChangeHP; // HPが変化したときに呼び出されるイベント
    public event Action<int> OnChangeMaxHP; // 最大HPが変化したときに呼び出されるイベント
    public event Action<int> OnChangeMaxWP; // 最大WPが変化したときに呼び出されるイベント
    public event Action<int> OnChangeWP; // WPが変化したときに呼び出されるイベント
    public event Action<PlayerAttackType> OnChangeAttackType; // 攻撃方法が変化したときに呼び出されるイベント
    public event Action OnDamageReaction; // プレイヤーがダメージを受けたときに呼び出されるイベント
    public event Action OnChangePlayerMoney; // プレイヤーの所持金が変化したときに呼び出されるイベント
    public event Action OnPlayerDied; // プレイヤーが死亡したときに呼び出されるイベント
    public event Action OnPlayerRevived; // プレイヤーが復活したときに呼び出されるイベント
    public event Action<PlayerStatusBoolName, bool> OnBoolStatusChanged; // Boolステータスが変化したときに呼び出されるイベント
    #endregion

    /// <summary>
    /// バフ・デバフなど一時的な効果を管理するマネージャーへの参照。
    /// </summary>
    public PlayerEffectManager EffectManager { get; private set; }

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

        //Awakeの最後に、ソート順マップの初期化処理を追加
        InitializeItemSortOrderMap();
    }

    public void Start()
    {
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
            }
        }

        fastTravelManager = PersistentManagers.instance.GetComponentInChildren<FastTravelManager>();
        if(fastTravelManager == null)
        {
            Debug.LogError("FastTravelManagerが見つかりません");
        }

        // シーン後の遷移先座標が設定されていれば移動
        if (GameManager.instance.crossScenePlayerSpawnPoint != null)
        {
            StartCoroutine(PlayerMove(GameManager.instance.crossScenePlayerSpawnPoint.Value)); // プレイヤーを次のスポーン位置に移動
            GameManager.instance.crossScenePlayerSpawnPoint = null; // 一度使用したらリセット
        }
    }

    #region PlayerStatusData Accessors
    // Boolの取得
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

    // Boolの設定
    public void SetPlayerBoolStatus(PlayerStatusBoolName flag, bool value)
    {
        var field = typeof(PlayerStatusData).GetField(flag.ToString());
        if (field != null && field.FieldType == typeof(bool))
        {
            bool oldValue = (bool)field.GetValue(GameManager.instance.savedata.PlayerStatus);
            if (oldValue == value)
                return; // 値が変わらなければ何もしない

            field.SetValue(GameManager.instance.savedata.PlayerStatus, value);
            OnBoolStatusChanged?.Invoke(flag, value); //汎用イベントを発行
        }
        else
        {
            Debug.LogError($"[SetBool] 無効なPlayerStatusBoolName: {flag}");
        }
    }

    // Intの取得
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

    // Intの設定
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

    // Floatの取得
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

    // Floatの設定
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

    //攻撃方法の設定
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

    //攻撃方法の取得
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
    #endregion

    #region Core Actions & Status
    // プレイヤーの所持金を変更する関数
    // number: 所持金の増減値、正の値で増加、負の値で減少
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

    //プレイヤーを強制的に移動させる関数
    public IEnumerator PlayerMove(Vector2 targetPoint)
    {
        if (playerGameObject == null)
        {
            playerGameObject = GameObject.FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME);
            if (playerGameObject == null)
            {
                Debug.LogError("PlayerGameObjectが見つかりません");
                yield break; // PlayerGameObjectが見つからない場合は処理を中止
            }
        }

        playerGameObject.transform.position = new Vector2(targetPoint.x, targetPoint.y); //プレイヤーの座標をtargetPointに移動
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
    /// 【通常ダメージ】を受け付け、防御力を考慮した最終ダメージを計算して適用します。
    /// 外部（敵の攻撃やプレイヤーの被弾処理）からはこの関数を呼び出します。
    /// </summary>
    /// <param name="baseDamage">防御計算前の基本ダメージ量</param>
    public void TakeNormalDamage(int baseDamage)
    {
        // PlayerEffectManagerから最終的な防御力を取得
        int damageReduction = EffectManager.CalculateFinalDefensePower();

        // 最終ダメージを計算（最低でも0ダメージ）
        int finalDamage = Mathf.Max(0, baseDamage - damageReduction);

        // ダメージが1以上あれば、実際にHPを減らす処理を呼び出す
        if (finalDamage > 0)
        {
            ApplyDamage(finalDamage);
        }
    }

    /// <summary>
    /// 【最大HP】に対する割合でダメージを与えます。
    /// </summary>
    /// <param name="damageRatio">最大HPに対するダメージの割合（例: 0.25f = 25%）</param>
    public void DamageHPByMaxHPRatio(float damageRatio)
    {
        // ダメージ割合がマイナスや0の場合は処理を中断
        if (damageRatio <= 0)
            return;

        // プレイヤーの最大HPを基準に、実際のダメージ量を計算
        // 最低でも1ダメージは保証する
        int damageAmount = Mathf.Max(1, Mathf.RoundToInt(playerMaxHP * damageRatio));

        // 既存のダメージ処理関数を呼び出す
        ApplyDamage(damageAmount);
    }

    /// <summary>
    /// 【現在HP】に対する割合でダメージを与えます。HPが低いほどダメージ量が減るため、この攻撃単体で倒されることはありません。
    /// </summary>
    /// <param name="damageRatio">現在HPに対するダメージの割合（例: 0.5f = 50%）</param>
    public void DamageHPByCurrentHPRatio(float damageRatio)
    {
        // ダメージ割合がマイナスや0の場合は処理を中断
        if (damageRatio <= 0)
            return;

        // プレイヤーの現在HPを取得
        int currentHP = GetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP);

        // 現在HPを基準に、実際のダメージ量を計算
        // 最低でも1ダメージは保証する
        int damageAmount = Mathf.Max(1, Mathf.RoundToInt(currentHP * damageRatio));

        // 既存のダメージ処理関数を呼び出す
        ApplyDamage(damageAmount);
    }

    /// <summary>
    /// 計算済みの最終ダメージをHPに適用し、死亡判定などを行う内部専用関数。
    /// </summary>
    /// <param name="damage">適用する最終ダメージ量</param>
    private void ApplyDamage(int damage)
    {
        // 全てのダメージ処理の入口で、まず無敵状態をチェックする
        if (heroinMove != null && heroinMove.IsImmune)
        {
            // プレイヤーが無敵状態なら、ダメージ処理を一切行わずに終了
            return;
        }

        // 既に死亡処理が始まっている場合は、重複して実行しない
        if (isDying)
            return;

        // ダメージを受ける前のHPと最大HPを取得
        int hpBeforeDamage = GetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP);
        int playerCurrentMaxHP = playerMaxHP;

        // HPがGutsEffectThresholdの閾値以上あるかどうかの条件を確認
        bool hasGutsEffect =
            (float)hpBeforeDamage / playerCurrentMaxHP >= GameConstants.GUTS_EFFECT_THRESHOLD;

        SEManager.instance?.PlayPlayerActionSE(SE_PlayerAction.Damage1); //ダメージの効果音を鳴らす
        SetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP, hpBeforeDamage - damage); //HPを更新

        // HPを減らした直後に、リアクションを促すイベントを発行する
        OnDamageReaction?.Invoke();

        int hpAfterDamage = hpBeforeDamage - damage;

        // HPが変化したときに呼び出されるイベントを発火
        // 復活の処理の関係から、OnPlayerDiedイベントの前に発火させる
        OnChangeHP?.Invoke(hpAfterDamage);

        if (hpAfterDamage <= 0)
        {
            if (hasGutsEffect)
            {
                // 90%以上あった場合、HPを1にして耐える
                SetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP, 1);
                OnChangeHP?.Invoke(1); // HPが変化したときに呼び出されるイベントを発火
            }
            else
            {
                // 死亡処理フラグを立て、重複実行を防ぐ
                isDying = true;

                // HPを0に確定
                SetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP, 0);

                // 死亡時のSEを再生
                SEManager.instance.PlayPlayerActionSE(SE_PlayerAction.Death1);

                // プレイヤーが死亡したときに呼び出されるイベントを発火
                // 復活処理の関係から、ExecuteDeathFastTravelの前に発火させる
                OnPlayerDied?.Invoke();

                // isEnableSaveの値を取得
                bool isEnableSave = SaveLoadManager.instance?.isEnableSave ?? false;

                // 死亡演出のコルーチンを開始
                StartCoroutine(DeathSequenceCoroutine(isEnableSave));
            }
        }
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
            GameOverUIManager.instance.StartGameOver(); //ゲームオーバーの関数を呼び出す
        }

        // 5. 死亡処理フラグをリセット
        isDying = false;
    }

    public void HealHP(int heal)
    {
        // 0以下の回復量は無意味なので、ここで処理を終了
        if (heal <= 0)
        {
            return;
        }

        int currentHP = GetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP);
        int maxHP = playerMaxHP;
        bool wasDead = currentHP <= 0; // 回復前に死亡していたかを記録

        // すでにHPが満タンで、かつ死んでいない場合は回復不要
        if (currentHP >= maxHP && !wasDead)
        {
            return;
        }

        // 回復後のHPを計算し、0と最大値の間に収める
        int newHP = Mathf.Clamp(currentHP + heal, 0, maxHP);

        // HPに変化がなければ、イベントを発火させる必要もない
        if (newHP == currentHP)
        {
            return;
        }

        // 計算結果を一度だけセット
        SetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP, newHP);

        // 死亡状態から復活した場合のイベントを発火
        if (wasDead && newHP > 0)
        {
            OnPlayerRevived?.Invoke();
        }

        // HPが変化したイベントを発火
        OnChangeHP?.Invoke(newHP);
    }

    public void RestoreFullHP()
    {
        int maxHP = playerMaxHP;
        int HP = GetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP);
        int recoverAmount = maxHP - HP;
        if (recoverAmount > 0)
        {
            HealHP(recoverAmount); // HPを最大値まで回復
        }
    }

    /// <summary>
    /// WP（武器ポイント）消費のバッファを加算し、
    /// 1以上になった場合は整数部分だけWPにダメージを与え、
    /// 余りをバッファとして保存する。
    /// </summary>
    /// <param name="addedBufferValue">加算するWP消費のバッファ値（小数対応）</param>
    public void AddWpConsumptionBuffer(float addedBufferValue)
    {
        // 現在のWP消費バッファ値を取得
        float currentWpConsumptionBuffer = GetPlayerFloatStatus(
            PlayerStatusFloatName.wpConsumptionBuffer
        );

        // 新たなバッファ値を加算
        currentWpConsumptionBuffer += addedBufferValue;

        // 合計値が1以上であれば、整数部分をWPダメージとして反映
        if (currentWpConsumptionBuffer >= 1f)
        {
            int intPart = Mathf.FloorToInt(currentWpConsumptionBuffer); // 整数部分を取得
            currentWpConsumptionBuffer -= intPart; // 小数部分のみを残す
            DamageWP(intPart); // WPにダメージを加える
        }

        // 残った小数部分のバッファを再保存
        SetPlayerFloatStatus(PlayerStatusFloatName.wpConsumptionBuffer, currentWpConsumptionBuffer);
    }

    public void HealWP(int heal)
    {
        int maxWP = playerMaxWP;
        int currentWP = GetPlayerIntStatus(PlayerStatusIntName.playerCurrentWP);

        // すでにWPが満タンなら、何もせず処理を終了（ガード節）
        if (currentWP >= maxWP)
        {
            return;
        }

        // 回復後のWPを計算し、最大値を超えないようにMathf.Minで制限
        int newWP = Mathf.Min(currentWP + heal, maxWP);

        // 計算結果を一度だけセットする
        SetPlayerIntStatus(PlayerStatusIntName.playerCurrentWP, newWP);

        // 正しい最終的な値でイベントを発火させる
        OnChangeWP?.Invoke(newWP);
    }

    public void DamageWP(int damage)
    {
        if (!(GameManager.instance?.savedata?.PlayerStatus?.isChangeWP ?? false))
            return;

        GameManager.instance.savedata.PlayerStatus.playerCurrentWP -= damage;
        int WP = GetPlayerIntStatus(PlayerStatusIntName.playerCurrentWP);
        if (WP < 0)
        {
            SetPlayerIntStatus(PlayerStatusIntName.playerCurrentWP, 0); // WPを0に設定
        }
        OnChangeWP?.Invoke(WP);
    }

    // プレイヤーのWPを設定する関数
    // 主に演出用に使用される
    /// <param name="wp">設定するWPの値</param>
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
        OnChangeWP?.Invoke(wp); // WPが変化したときに呼び出されるイベントを発火
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
        if (!ItemInventory.UseItem(ID, 1)) //アイテムを使用
        {
            return false;
        }

        SEManager.instance?.PlayPlayerActionSE(SE_PlayerAction.HealItem1); //効果音を鳴らす
        HealItemData item = healItemDatabase.GetItemByID(ID); //itemのDataを取得
        if (item != null)
        {
            if (item.hpHealAmount > 0)
                HealHP(item.hpHealAmount);
            if (item.wpHealAmount > 0)
                HealWP(item.wpHealAmount);

            // 特殊効果の適用をPlayerEffectManagerに委任する
            foreach (var effect in item.buffEffects)
            {
                // effect.EffectApply() は PlayerEffectManager のメソッドを呼び出すように実装されている想定
                effect.EffectApply();
            }
        }
        return true; //アイテムの使用に成功
    }

    //即座に使用できるアイテムを入れ替える関数
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
        SEManager.instance?.PlayUISE(SE_UI.Register1); //登録の効果音を鳴らす
        OnQuickSlotAssigned?.Invoke();
    }

    /// <summary>
    /// プレイヤーの現在のワールド座標をVector2で返します。
    /// プレイヤーが見つからない場合は(0, 0)を返します。
    /// </summary>
    /// <returns>プレイヤーの座標 (Vector2)</returns>
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

    #region Level & Experience
    /// <summary>
    /// 外部システム（PlayerLevelManagerなど）から最大HPを更新し、イベントを発行します。
    /// </summary>
    /// <param name="newMaxHP">新しい最大HP</param>
    public void SetMaxHP(int newMaxHP)
    {
        // 値に変化がなければ何もしない
        if (playerMaxHP == newMaxHP)
            return;

        playerMaxHP = newMaxHP;
        OnChangeMaxHP?.Invoke(playerMaxHP); // 最大HPが変化したときに呼び出されるイベントを発火
    }

    /// <summary>
    /// 外部システム（PlayerLevelManagerなど）から最大WPを更新し、イベントを発行します。
    /// </summary>
    /// <param name="newMaxWP">新しい最大WP</param>
    public void SetMaxWP(int newMaxWP)
    {
        // 値に変化がなければ何もしない
        if (playerMaxWP == newMaxWP)
            return;

        playerMaxWP = newMaxWP;
        OnChangeMaxWP?.Invoke(playerMaxWP); // 最大WPが変化したときに呼び出されるイベントを発火
    }
    #endregion

    #region Inventory Management
    // アイテムの正しい並び順を高速に検索するための辞書
    private Dictionary<int, int> itemSortOrderMap;

    /// <summary>
    /// HealItemDatabaseからアイテムの正しい表示順を読み込み、辞書としてキャッシュします。
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

    #region Control Lock
    // 強制移動などの開始
    public void LockControl()
    {
        isControlLocked = true;
    }

    // 強制移動などの終了
    public void UnlockControl()
    {
        isControlLocked = false;
    }
    #endregion
}

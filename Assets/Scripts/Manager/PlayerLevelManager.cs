using System;
using UnityEngine;

/// <summary>
/// プレイヤーのレベルと経験値を専門に管理するクラス。
/// 経験値の増減、レベルアップ判定、レベルアップに伴うステータス更新を行います。
/// </summary>
public class PlayerLevelManager : MonoBehaviour
{
    // シングルトンインスタンス
    public static PlayerLevelManager instance { get; private set; }

    // プレイヤーのコアステータスを管理するPlayerManagerへの参照
    private PlayerManager playerManager;

    // --- プレイヤーのレベル関連ステータス ---
    public int playerLv { get; private set; } = 1; // プレイヤーのレベル
    public float attackLvActualDeltaValue { get; private set; } = 0; // レベルによる攻撃力の変化値
    public int defenseLvActualDeltaValue { get; private set; } = 0; // レベルによる防御力の変化値
    public event Action<int> OnLeveledUp; //レベルアップ時に発行されるイベント

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
    }

    public void Start()
    {
        // セーブデータから経験値を取得し、現在のレベルを算出する
        InitializeLevelFromSaveData();
    }

    /// <summary>
    /// セーブデータに基づいてプレイヤーのレベルを初期化します。
    /// </summary>
    private void InitializeLevelFromSaveData()
    {
        var PlayerStatus = GameManager.instance.savedata.PlayerStatus;
        if (PlayerStatus == null)
        {
            Debug.LogError("PlayerStatusDataがnullです");
            return;
        }

        int playerExp = PlayerStatus.playerExp;
        playerLv = GetLevelFromExp(playerExp);

        // 算出したレベルに基づいてステータスを更新
        UpdateLevelBasedStats(false);
    }

    #region Level & Experience
    /// <summary>
    /// 経験値を追加し、必要ならレベルアップを行う
    /// </summary>
    public void AddExperience(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("経験値の追加量は正の値でなければなりません");
            return;
        }

        var PlayerStatus = GameManager.instance.savedata.PlayerStatus;
        if (PlayerStatus == null)
        {
            Debug.LogError("PlayerStatusDataがnullです");
            return;
        }

        int oldLevel = playerLv;
        PlayerStatus.playerExp += amount; // 経験値を追加

        while (CanLevelUp())
        {
            LevelUp();
        }

        int newLevel = playerLv;
        int levelIncreased = newLevel - oldLevel;

        if (levelIncreased > 0)
        {
            GameUIManager.instance?.ShowLevelUpUI(newLevel); // レベルアップのメッセージを表示
            OnLeveledUp?.Invoke(levelIncreased); //レベルアップしたことを通知する
        }
    }

    /// <summary>
    /// 現在の経験値が次のレベルに達しているかを判定
    /// </summary>
    private bool CanLevelUp()
    {
        var PlayerStatus = GameManager.instance.savedata.PlayerStatus;
        if (PlayerStatus == null)
        {
            Debug.LogError("PlayerStatusDataがnullです");
            return false;
        }

        int nextLevel = playerLv + 1;
        if (nextLevel > GameConstants.PLAYER_MAX_LEVEL)
            return false;
        if (!GameConstants.LevelExpRequirements.ContainsKey(nextLevel))
            return false;

        return PlayerStatus.playerExp >= GameConstants.LevelExpRequirements[nextLevel];
    }

    /// <summary>
    /// レベルアップ処理
    /// </summary>
    private void LevelUp()
    {
        if (GameManager.instance.savedata.PlayerStatus == null)
        {
            Debug.LogError("PlayerStatusDataがnullです");
            return;
        }
        playerLv++;
        UpdateLevelBasedStats(true); // レベルに応じた能力の変化値を更新し、HPをリセット

        SEManager.instance?.PlaySystemEventSE(SE_SystemEvent.LevelUp); // レベルアップSEを再生
    }

    /// <summary>
    /// 次のレベルに必要な経験値までの残り
    /// </summary>
    public int GetExpToNextLevel()
    {
        var PlayerStatus = GameManager.instance.savedata.PlayerStatus;
        if (PlayerStatus == null)
        {
            Debug.LogError("PlayerStatusDataがnullです");
            return 0;
        }

        int nextLevel = playerLv + 1;
        if (!GameConstants.LevelExpRequirements.ContainsKey(nextLevel))
            return 0;

        return GameConstants.LevelExpRequirements[nextLevel] - PlayerStatus.playerExp;
    }

    // <summary>
    /// 指定された経験値がどのレベルに相当するかを計算して返します。
    /// (GameConstants.LevelExpRequirementsがレベル昇順にソートされている前提)
    /// </summary>
    /// <param name="experience">計算対象の経験値</param>
    /// <returns>対応するレベル (最低レベル1)</returns>
    public static int GetLevelFromExp(int experience)
    {
        int determinedLevel = 1; // 経験値0でもレベル1

        // 条件を満たす限り、レベルを上げていく
        // (GameConstants.LevelExpRequirements は Key=レベル, Value=必要経験値 の辞書orリスト)
        foreach (var pair in GameConstants.LevelExpRequirements)
        {
            // 所持経験値(experience)が、そのレベル(pair.Key)に必要な経験値(pair.Value)以上か
            if (experience >= pair.Value)
            {
                // 満たしている場合、レベルを更新
                determinedLevel = pair.Key;
            }
            else
            {
                // 必要な経験値を満たさなくなったら、それ以降のレベルはチェック不要
                break;
            }
        }

        // 最大レベルを超えないように丸める (CanLevelUpの処理に合わせる)
        if (determinedLevel > GameConstants.PLAYER_MAX_LEVEL)
        {
            determinedLevel = GameConstants.PLAYER_MAX_LEVEL;
        }

        return determinedLevel;
    }

    /// <summary>
    /// レベルに応じたステータスを更新し、PlayerManagerに反映させる
    /// </summary>
    private void UpdateLevelBasedStats(bool isResettingHP = false)
    {
        if (GameManager.instance.savedata.PlayerStatus == null)
        {
            Debug.LogError("PlayerStatusDataがnullです");
            return;
        }

        //レベルに応じた実際の攻撃力と防御力の変化値を計算し、自身のプロパティを更新
        attackLvActualDeltaValue = playerLv * GameConstants.LEVEL_ATTACK_BONUS;
        defenseLvActualDeltaValue = GameConstants.GetDefense(playerLv);

        // --- ここからPlayerManagerへの反映処理 ---
        if (playerManager == null)
            return;

        //レベルに応じた最大HPを設定
        int playerMaxHP = GameConstants.GetMaxHP(playerLv);
        playerManager.SetMaxHP(playerMaxHP);
        //レベルに応じた最大WPを設定
        int playerMaxWP = GameConstants.GetMaxWP(playerLv);
        playerManager.SetMaxWP(playerMaxWP);

        if (isResettingHP)
        {
            // プレイヤーのHPを最大HPに設定
            int playerCurrentHP = playerManager.GetPlayerIntStatus(
                PlayerStatusIntName.playerCurrentHP
            );
            int healthDelta = playerMaxHP - playerCurrentHP;
            playerManager.HealHP(healthDelta);
        }
    }
    #endregion
    private void OnEnable()
    {
        // SaveLoadManagerのロード状態変化イベントを購読
        SaveLoadManager.OnLoadingStateChanged += HandleLoadingStateChanged;
    }

    private void OnDisable()
    {
        // イベント購読を解除（メモリーリーク防止）
        SaveLoadManager.OnLoadingStateChanged -= HandleLoadingStateChanged;
    }

    /// <summary>
    /// ロード状態が変化した時に呼ばれる処理
    /// </summary>
    private void HandleLoadingStateChanged(bool isLoading)
    {
        // ロードが完了した(falseになった)タイミングで、データが最新になっているため初期化を実行
        if (!isLoading)
        {
            InitializeLevelFromSaveData();
        }
    }
}

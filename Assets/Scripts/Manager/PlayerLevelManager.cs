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
        int determinedLevel = 1;

        // 条件を満たす限り、レベルを上げていく
        foreach (var pair in GameConstants.LevelExpRequirements)
        {
            if (playerExp >= pair.Value)
                determinedLevel = pair.Key;
            else
                break;
        }
        playerLv = determinedLevel;

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
        if (nextLevel > GameConstants.PlayerMaxLevel)
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

        var seManager = SEManager.instance;
        if (seManager != null)
        {
            //もし、レベルアップのSEが再生中でなければ、再生する
            if (!seManager.IsPlayingSystemEventSE(SE_SystemEvent.LevelUp))
            {
                seManager.PlaySystemEventSE(SE_SystemEvent.LevelUp);
            }
        }
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
        attackLvActualDeltaValue = playerLv * GameConstants.levelAttackBonus;
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
}

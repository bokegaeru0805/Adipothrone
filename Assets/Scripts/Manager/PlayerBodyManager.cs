using System;
using UnityEngine;

/// <summary>
/// プレイヤーの体形と、WP（Willpower）に基づくステータス倍率を専門に管理するクラス。
/// </summary>
public class PlayerBodyManager : MonoBehaviour
{
    // シングルトンインスタンス
    public static PlayerBodyManager instance { get; private set; }

    // プレイヤーのコアステータスを管理するPlayerManagerへの参照
    private PlayerManager playerManager;

    // --- プレイヤーの体形関連ステータス ---
    public int BodyState { get; private set; } = GameConstants.BodyState_Normal; // プレイヤーの体形状態
    public int AnimBodyState { get; private set; } = GameConstants.AnimBodyState_Normal; // プレイヤーのアニメーション体形状態
    public float attackWpScale { get; private set; } = 0; // 攻撃力のWP倍率
    public float defenseWpScale { get; private set; } = 0; // 防御力のWP倍率
    public float speedWpScale { get; private set; } = 0; // スピードのWP倍率
    public event Action OnChangeBodyState; // 体形状態が変更されたときに発行されるイベント

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

        playerManager = GetComponent<PlayerManager>();
        if (playerManager == null)
        {
            Debug.LogError("PlayerManagerが同じGameObjectにアタッチされていません！");
        }
    }

    private void Start()
    {
        // PlayerManagerのWPが変更されたら、こちらも自動的に更新するようにイベントを購読
        if (playerManager != null)
        {
            playerManager.OnChangeWP += UpdateBodyStatus;
        }
        else
        {
            Debug.LogError(
                "PlayerManagerが見つかりません。PlayerBodyManagerはPlayerManagerに依存しています。"
            );
        }

        // ゲーム開始時に一度、現在のWPに基づいて状態を初期化
        UpdateBodyStatus(playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerCurrentWP));
    }

    private void OnDisable()
    {
        // オブジェクトが無効になる際に、イベントの購読を解除
        if (playerManager != null)
        {
            playerManager.OnChangeWP -= UpdateBodyStatus;
        }
    }

    /// <summary>
    /// 現在のWPに基づいて、体形状態とステータス倍率を更新します。
    /// </summary>
    private void UpdateBodyStatus(int playerCurrentWP)
    {
        // WPに応じて体形状態を設定
        SetBodyStates(playerCurrentWP);
        // WPに応じてステータス倍率を設定
        SetWpScales(playerCurrentWP);
        // 体形状態が変更された場合、イベントを発行
        OnChangeBodyState?.Invoke();
    }

    #region Body & WP Management
    // プレイヤーの体形を段階的に変更する関数
    // number: 変更する数値、isplus: trueなら増加、falseなら減少
    public void StepBodyState(int number, bool isplus)
    {
        int playerCurrentWP = playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerCurrentWP);
        int currentBodyStateIndex = GetBodyStateFromWP(playerCurrentWP);

        if (isplus)
        {
            for (int i = 0; i < number; i++)
            {
                if (currentBodyStateIndex == GameConstants.WpThresholds.Length - 1)
                    currentBodyStateIndex = 0;
                else
                    currentBodyStateIndex += 1;
            }
        }
        else
        {
            for (int i = 0; i < number; i++)
            {
                if (currentBodyStateIndex == 0)
                    currentBodyStateIndex = GameConstants.WpThresholds.Length - 1;
                else
                    currentBodyStateIndex -= 1;
            }
        }

        // PlayerManagerにWPの変更とイベントの発行を依頼する
        playerManager.SetWP(GameConstants.WpThresholds[currentBodyStateIndex]);
        UpdateBodyStatus(GameConstants.WpThresholds[currentBodyStateIndex]); // 状態を更新するために再度呼び出す
    }

    private void SetBodyStates(int playerCurrentWP)
    {
        // WPに応じて体形状態を設定
        if (playerCurrentWP >= GameConstants.WpThreshold_Armed3)
        {
            BodyState = GameConstants.BodyState_Armed3;
            AnimBodyState = GameConstants.AnimBodyState_Armed3;
        }
        else if (playerCurrentWP >= GameConstants.WpThreshold_Armed2)
        {
            BodyState = GameConstants.BodyState_Armed2;
            AnimBodyState = GameConstants.AnimBodyState_Armed2;
        }
        else if (playerCurrentWP >= GameConstants.WpThreshold_Armed1)
        {
            BodyState = GameConstants.BodyState_Armed1;
            AnimBodyState = GameConstants.AnimBodyState_Armed1;
        }
        else
        {
            BodyState = GameConstants.BodyState_Normal;
            AnimBodyState = GameConstants.AnimBodyState_Normal;
        }
    }

    private void SetWpScales(int playerCurrentWP)
    {
        if (playerCurrentWP < 0)
        {
            playerCurrentWP = 0; // WPがマイナスにならないようにする
        }
        // WPに応じてスケールを設定
        attackWpScale = 1 + playerCurrentWP * GameConstants.PlayerAttackWpMultiplier;
        defenseWpScale = 1 + playerCurrentWP * GameConstants.PlayerDefenseWpMultiplier;
        speedWpScale = 1 + playerCurrentWP * GameConstants.PlayerMoveWpMultiplier;
    }

    private int GetBodyStateFromWP(int currentWP)
    {
        int level = 0;
        // しきい値を上回る限り、状態を進める
        for (int i = 0; i < GameConstants.WpThresholds.Length; i++)
        {
            if (currentWP >= GameConstants.WpThresholds[i])
                level = i;
            else
                break;
        }
        return level;
    }

    /// <summary>
    /// 現在の体形状態をBodyStateEnumとして取得します。
    /// </summary>
    /// <returns>現在の体形に対応するBodyStateEnum</returns>
    public GameConstants.BodyStateEnum GetCurrentBodyStateEnum()
    {
        // 現在のWPを取得
        int playerCurrentWP = playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerCurrentWP);

        // WPの値に基づいて、どの体形状態に該当するかを判定
        if (playerCurrentWP >= GameConstants.WpThreshold_Immobile)
        {
            return GameConstants.BodyStateEnum.BodyState_Immobile;
        }
        else if (playerCurrentWP >= GameConstants.WpThreshold_Armed3)
        {
            return GameConstants.BodyStateEnum.BodyState_Armed3;
        }
        else if (playerCurrentWP >= GameConstants.WpThreshold_Armed2)
        {
            return GameConstants.BodyStateEnum.BodyState_Armed2;
        }
        else if (playerCurrentWP >= GameConstants.WpThreshold_Armed1)
        {
            return GameConstants.BodyStateEnum.BodyState_Armed1;
        }
        else
        {
            return GameConstants.BodyStateEnum.BodyState_Normal;
        }
    }

    /// <summary>
    /// 体の状態(BodyState)に応じてWPの値を設定し、関連するステータスを更新します。
    /// </summary>
    /// <param name="bodyState">設定の基準となる体の状態。</param>
    public void SetWPFromBodyState(GameConstants.BodyStateEnum bodyState)
    {
        int newWP = 0;
        switch (bodyState)
        {
            case GameConstants.BodyStateEnum.BodyState_Normal:
                newWP = 0;
                break;
            case GameConstants.BodyStateEnum.BodyState_Armed1:
                newWP = GameConstants.WpThreshold_Armed1;
                break;
            case GameConstants.BodyStateEnum.BodyState_Armed2:
                newWP = GameConstants.WpThreshold_Armed2;
                break;
            case GameConstants.BodyStateEnum.BodyState_Armed3:
                newWP = GameConstants.WpThreshold_Armed3;
                break;
            case GameConstants.BodyStateEnum.BodyState_Immobile:
                newWP = GameConstants.WpThreshold_Immobile;
                break;
        }

        // PlayerManagerにWPの変更を依頼し、イベントを発行する
        playerManager.SetWP(newWP);
        UpdateBodyStatus(newWP); // 状態を更新するために再度呼び出す
    }
    #endregion
}

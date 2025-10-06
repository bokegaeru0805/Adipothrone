using UnityEngine;

/// <summary>
/// プレイヤーのレベルアップを検知して、アタッチされたパーティクルエフェクトを再生する。
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class PlayerLevelUpEffect : MonoBehaviour
{
    // --- 内部キャッシュ ---
    private PlayerLevelManager playerLevelManager;
    private ParticleSystem levelUpParticleSystem;

    private void Awake()
    {
        // 自身にアタッチされているParticleSystemコンポーネントを取得
        levelUpParticleSystem = GetComponent<ParticleSystem>();
    }

    private void Start()
    {
        // PlayerLevelManagerのインスタンスをキャッシュ
        playerLevelManager = PlayerLevelManager.instance;
        if (playerLevelManager == null)
        {
            Debug.LogError("PlayerLevelManagerが見つかりません。このコンポーネントは機能しません。", this);
            this.enabled = false; // エラー時はスクリプトを無効化
            return;
        }
        // イベントの購読（イベント登録）
        playerLevelManager.OnLeveledUp += PlayLevelUpEffect;
    }

    private void OnEnable()
    {
        // Startより後に有効化された場合も考慮して、OnEnableでも購読
        if (playerLevelManager != null)
        {
            playerLevelManager.OnLeveledUp += PlayLevelUpEffect;
        }
    }

    private void OnDisable()
    {
        // オブジェクトが無効化・破棄される際にイベントの購読を解除（メモリリーク防止）
        if (playerLevelManager != null)
        {
            playerLevelManager.OnLeveledUp -= PlayLevelUpEffect;
        }
    }

    /// <summary>
    /// PlayerLevelManagerからレベルアップイベントを受け取ったときに呼ばれるメソッド
    /// </summary>
    private void PlayLevelUpEffect(int _newLevel)
    {
        // エフェクトを再生
        levelUpParticleSystem.Play();
    }
}
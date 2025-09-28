using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーのバフ状態に応じて、アタッチされたパーティクルエフェクトの色と再生を制御する。
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class PlayerBuffEffect : MonoBehaviour
{
    [System.Serializable]
    public class BuffVisualMapping
    {
        public StatusEffectType effectType;
        public Color effectColor = Color.white;
    }

    private List<BuffVisualMapping> buffVisuals = new List<BuffVisualMapping>
    {
        new BuffVisualMapping { effectType = StatusEffectType.Attack, effectColor = Color.red },
        new BuffVisualMapping
        {
            effectType = StatusEffectType.Defense,
            effectColor = new Color(193f / 255f, 192f / 255f, 190f / 255f),
        },
        new BuffVisualMapping { effectType = StatusEffectType.Speed, effectColor = Color.blue },
        new BuffVisualMapping { effectType = StatusEffectType.Luck, effectColor = Color.yellow },
    };

    // --- 内部キャッシュ ---
    private PlayerEffectManager playerEffectManager;
    private ParticleSystem buffParticleSystem;
    private Dictionary<StatusEffectType, Color> buffColorDictionary;

    private void Awake()
    {
        // 自身のParticleSystemコンポーネントを取得
        buffParticleSystem = GetComponent<ParticleSystem>();

        // Inspectorで設定されたリストを、高速に検索できる辞書(Dictionary)に変換
        buffColorDictionary = new Dictionary<StatusEffectType, Color>();
        foreach (var mapping in buffVisuals)
        {
            buffColorDictionary[mapping.effectType] = mapping.effectColor;
        }
    }

    private void Start()
    {
        // PlayerEffectManagerのインスタンスをキャッシュ
        playerEffectManager = PlayerEffectManager.instance;
        if (playerEffectManager == null)
        {
            Debug.LogError(
                "PlayerEffectManagerが見つかりません。このコンポーネントは機能しません。",
                this
            );
            this.enabled = false; // エラー時はスクリプトを無効化
            return;
        }
        playerEffectManager.OnBuffApplied += OnBuffApplied;
    }

    private void OnEnable()
    {
        // Managerのバフイベントを購読（イベント登録）
        if (playerEffectManager != null)
        {
            playerEffectManager.OnBuffApplied += OnBuffApplied;
        }
    }

    private void OnDisable()
    {
        // イベントの購読を解除（メモリリーク防止）
        if (playerEffectManager != null)
        {
            playerEffectManager.OnBuffApplied -= OnBuffApplied;
        }
    }

    /// <summary>
    /// PlayerEffectManagerからバフ適用イベントを受け取ったときに呼ばれるメソッド
    /// </summary>
    private void OnBuffApplied(StatusEffectType effectType)
    {
        // 辞書から、指定されたバフタイプに対応する色を検索
        if (buffColorDictionary.TryGetValue(effectType, out Color newColor))
        {
            // ParticleSystemのメインモジュールを取得
            var mainModule = buffParticleSystem.main;

            // エフェクトの色を設定
            mainModule.startColor = newColor;

            // エフェクトを再生
            buffParticleSystem.Play();
        }
        else
        {
            Debug.LogWarning(
                $"このバフタイプに対応する色の設定が見つかりません: {effectType}",
                this
            );
        }
    }
}

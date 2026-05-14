using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// エフェクトグループ（Core/Particle）のスケール、再生、停止を一括管理するクラス。
/// Animator等によるオブジェクトのアクティブ状態の切り替えに連動した自動再生に対応します。
/// </summary>
public class BurstEffect_Master : MonoBehaviour
{
    [Header("再生設定")]
    [
        SerializeField,
        Tooltip("オブジェクトが有効（Active）になった瞬間に自動でエフェクトを再生するかどうか")
    ]
    private bool playOnEnable = false;

    [Header("エフェクトグループの参照")]
    [SerializeField, Tooltip("中心となるエフェクトグループのTransform")]
    private Transform coreEffectsGroup;

    [SerializeField, Tooltip("周囲に飛び散るパーティクルグループのTransform")]
    private Transform particleEffectsGroup;

    [Header("詳細設定")]
    [
        SerializeField,
        Tooltip("速度変更（SetParticleSpeedMultiplier）を適用しない例外エフェクトのリスト")
    ]
    private List<ParticleSystem> speedChangeExceptions;

    [Header("エディタ用プレビュー設定")]
    [
        SerializeField,
        Tooltip("Coreグループの表示スケール"),
        OnValueChanged(nameof(UpdateScaleInEditor))
    ]
    private float coreScale = 1.0f;

    [
        SerializeField,
        Tooltip("Particleグループの速度倍率"),
        OnValueChanged(nameof(UpdateScaleInEditor))
    ]
    private float particleScale = 1.0f;

    /// <summary>
    /// 管理対象の全パーティクルシステムのキャッシュリスト
    /// </summary>
    private List<ParticleSystem> allParticleSystems;

    /// <summary>
    /// 各パーティクルシステムの初期速度（Min, Max）を保持する辞書
    /// </summary>
    private Dictionary<ParticleSystem, Vector2> particleSystemOriginalSpeeds;

    /// <summary>
    /// 初期化が完了しているかどうかのフラグ
    /// </summary>
    private bool isInitialized = false;

    private void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// オブジェクトが有効になった際のコールバック
    /// playOnEnableが有効な場合、エフェクトを再生します
    /// </summary>
    private void OnEnable()
    {
        if (playOnEnable)
        {
            PlayEffect();
        }
    }

    /// <summary>
    /// 子階層から全てのParticleSystemを取得し、初期状態をキャッシュします
    /// </summary>
    public void Initialize()
    {
        if (isInitialized)
            return;

        allParticleSystems = new List<ParticleSystem>();
        particleSystemOriginalSpeeds = new Dictionary<ParticleSystem, Vector2>();

        // CoreEffectsGroup配下の取得
        if (coreEffectsGroup != null)
        {
            var coreTemp = new List<ParticleSystem>();
            coreEffectsGroup.GetComponentsInChildren(true, coreTemp);
            allParticleSystems.AddRange(coreTemp);
        }
        else
        {
            Debug.LogWarning("CoreEffectsGroup が未設定です。", this);
        }

        // ParticleEffectsGroup配下の取得と速度データの保存
        if (particleEffectsGroup != null)
        {
            var particleTemp = new List<ParticleSystem>();
            particleEffectsGroup.GetComponentsInChildren(true, particleTemp);

            foreach (var ps in particleTemp)
            {
                allParticleSystems.Add(ps);

                var main = ps.main;
                if (!particleSystemOriginalSpeeds.ContainsKey(ps))
                {
                    particleSystemOriginalSpeeds.Add(
                        ps,
                        new Vector2(main.startSpeed.constantMin, main.startSpeed.constantMax)
                    );
                }
            }
        }
        else
        {
            Debug.LogWarning("ParticleEffectsGroup が未設定です。", this);
        }

        isInitialized = true;
    }

    /// <summary>
    /// 全てのエフェクトを最初から再生します
    /// </summary>
    public void PlayEffect()
    {
        if (!isInitialized)
            Initialize();

        foreach (var ps in allParticleSystems)
        {
            if (ps == null)
                continue;

            // 既存の粒子を消去してから再生を開始
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play();
        }
    }

    /// <summary>
    /// 全てのエフェクトを停止します
    /// </summary>
    public void StopEffect()
    {
        if (!isInitialized)
            return;

        foreach (var ps in allParticleSystems)
        {
            if (ps == null)
                continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    /// <summary>
    /// Coreグループ全体のローカルスケールを設定します
    /// </summary>
    /// <param name="scale">設定するスケール値</param>
    public void SetCoreScale(float scale)
    {
        if (coreEffectsGroup != null)
        {
            coreEffectsGroup.localScale = Vector3.one * scale;
        }
    }

    /// <summary>
    /// Particleグループ内のエフェクトの初速（またはサイズ/バースト数）に倍率を適用します
    /// </summary>
    /// <param name="multiplier">適用する倍率</param>
    public void SetParticleSpeedMultiplier(float multiplier)
    {
        if (!isInitialized)
            Initialize();

        foreach (var pair in particleSystemOriginalSpeeds)
        {
            ParticleSystem ps = pair.Key;
            Vector2 originalSpeeds = pair.Value;

            if (ps == null)
                continue;

            // 例外リストに含まれるかどうかで処理を分岐
            if (speedChangeExceptions != null && speedChangeExceptions.Contains(ps))
            {
                // バースト数の調整
                var emission = ps.emission;
                if (emission.burstCount > 0)
                {
                    var burst = emission.GetBurst(0);
                    burst.count = 60 * multiplier;
                    emission.SetBurst(0, burst);
                }

                // サイズの調整
                var main = ps.main;
                var size = main.startSize;
                size.mode = ParticleSystemCurveMode.TwoConstants;
                size.constantMin = 0.04f * multiplier;
                size.constantMax = 0.06f * multiplier;
                main.startSize = size;
            }
            else
            {
                // 通常の速度調整
                var main = ps.main;
                var speed = main.startSpeed;
                speed.mode = ParticleSystemCurveMode.TwoConstants;
                speed.constantMin = originalSpeeds.x * multiplier;
                speed.constantMax = originalSpeeds.y * multiplier;
                main.startSpeed = speed;
            }
        }
    }

    /// <summary>
    /// インスペクター上の値が変更された際、非再生モードでもプレビューを更新します
    /// </summary>
    private void UpdateScaleInEditor()
    {
        SetCoreScale(coreScale);
        SetParticleSpeedMultiplier(particleScale);
    }
}

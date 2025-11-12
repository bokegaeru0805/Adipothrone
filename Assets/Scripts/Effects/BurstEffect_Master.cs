using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// BurstEffect_Master オブジェクトにアタッチします。
/// 2つのエフェクトグループ（CoreEffects, ParticleEffects）の
/// 大きさ、再生/停止をまとめて制御します。
/// </summary>
public class BurstEffect_Master : MonoBehaviour
{
    [Header("エフェクトグループのTransform")]
    [SerializeField]
    [Tooltip("CoreEffects オブジェクトのTransformをここにドラッグ＆ドロップ")]
    private Transform coreEffectsGroup;

    [SerializeField]
    [Tooltip("ParticleEffects オブジェクトのTransformをここにドラッグ＆ドロップ")]
    private Transform particleEffectsGroup;

    [Header("Particleエフェクト速度設定")]
    [SerializeField]
    [Tooltip("速度変更（SetParticleSpeedMultiplier）を「適用しない」例外エフェクト")]
    private List<ParticleSystem> speedChangeExceptions;

    [Header("スケール設定（編集モード用）")]
    [Space]
    [SerializeField, Tooltip("Coreグループのスケール"), OnValueChanged(nameof(UpdateScaleInEditor))]
    private float coreScale = 1.0f;

    [
        SerializeField,
        Tooltip("Particleグループのスケール"),
        OnValueChanged(nameof(UpdateScaleInEditor))
    ]
    private float particleScale = 1.0f;

    // キャッシュ（保存）した全てのパーティクルシステム
    private List<ParticleSystem> allParticleSystems;

    /// <summary>
    /// particleEffectsGroup内のPSと、その元の速度(Min, Max)を保存する辞書
    /// </summary>
    private Dictionary<ParticleSystem, Vector2> particleSystemOriginalSpeeds;

    // 初期化が完了したかどうかのフラグ
    private bool isInitialized = false;

    void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// 初期化処理。子階層から全てのParticleSystemを取得しキャッシュします。
    /// </summary>
    private void Initialize()
    {
        // 既に初期化済みの場合は何もしない
        if (isInitialized)
        {
            return;
        }

        // 1. メインのリストと辞書を「空」にする
        allParticleSystems = new List<ParticleSystem>();
        particleSystemOriginalSpeeds = new Dictionary<ParticleSystem, Vector2>();

        // 2. CoreEffectsGroup の処理
        if (coreEffectsGroup != null)
        {
            List<ParticleSystem> coreTempList = new List<ParticleSystem>();
            coreEffectsGroup.GetComponentsInChildren<ParticleSystem>(true, coreTempList);
            foreach (var ps in coreTempList)
            {
                allParticleSystems.Add(ps);
            }
        }
        else
        {
            Debug.LogError("CoreEffectsGroup が設定されていません。", this);
        }

        // 3. ParticleEffectsGroup の処理
        if (particleEffectsGroup != null)
        {
            List<ParticleSystem> particleTempList = new List<ParticleSystem>();
            particleEffectsGroup.GetComponentsInChildren<ParticleSystem>(true, particleTempList);

            foreach (var ps in particleTempList)
            {
                // (A) 全体リストに追加
                allParticleSystems.Add(ps);

                var main = ps.main;
                // 元のMin/Max速度を取得
                float minSpeed = main.startSpeed.constantMin;
                float maxSpeed = main.startSpeed.constantMax;

                // 辞書にPS本体と、Vector2(min, max)の形で元の速度を保存
                if (!particleSystemOriginalSpeeds.ContainsKey(ps))
                {
                    particleSystemOriginalSpeeds.Add(ps, new Vector2(minSpeed, maxSpeed));
                }
            }
        }
        else
        {
            Debug.LogError("ParticleEffectsGroup が設定されていません。", this);
        }

        // 4. 最終結果の確認
        if (allParticleSystems.Count == 0)
        {
            Debug.LogWarning("制御対象のParticleSystemが一つも見つかりませんでした。", this);
        }

        isInitialized = true;
    }

    // --- 外部から実行する Public メソッド ---

    public void PlayEffect()
    {
        if (!isInitialized)
        {
            Initialize();
        }
        foreach (var ps in allParticleSystems)
        {
            if (ps == null)
                continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play();
        }
    }

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

    // --- 大きさを制御するメソッド ---

    public void SetCoreScale(float scale)
    {
        if (coreEffectsGroup != null)
        {
            coreEffectsGroup.localScale = new Vector3(scale, scale, scale);
        }
    }

    /// <summary>
    /// ParticleEffects グループ内のエフェクトの初速（Start Speed）に倍率をかけます。
    /// </summary>
    /// <param name="multiplier">元の速度にかける倍率 (例: 1.0 = 100%, 2.5 = 250%)</param>
    public void SetParticleSpeedMultiplier(float multiplier)
    {
        if (!isInitialized)
        {
            Debug.LogWarning(
                "Initialize() が完了する前に SetParticleSpeedMultiplier が呼ばれました。"
            );
            Initialize(); // 安全のため初期化
        }

        // 保存しておいた「particleEffectsGroup」のエフェクトだけをループ
        foreach (var pair in particleSystemOriginalSpeeds)
        {
            ParticleSystem ps = pair.Key;
            Vector2 originalSpeeds = pair.Value; // (originalMin, originalMax)

            if (ps == null)
                continue;

            // このPSが例外リストに含まれているかチェック
            if (speedChangeExceptions != null && speedChangeExceptions.Contains(ps))
            {
                // 例外リストに含まれている場合は、バースト数を調整する例外処理
                ParticleSystem.Burst burst = ps.emission.GetBurst(0);
                burst.count = 60 * multiplier;
                ps.emission.SetBurst(0, burst);

                // サイズを設定
                var main = ps.main;
                var sizeCurve = main.startSize;

                sizeCurve.mode = ParticleSystemCurveMode.TwoConstants;

                // 計算された倍率(effectiveMultiplier)でサイズを設定
                sizeCurve.constantMin = 0.04f * multiplier;
                sizeCurve.constantMax = 0.06f * multiplier;

                main.startSize = sizeCurve;
            }
            else
            {
                // 速度を設定
                var main = ps.main;
                var speedCurve = main.startSpeed;

                speedCurve.mode = ParticleSystemCurveMode.TwoConstants;

                // 計算された倍率(effectiveMultiplier)で速度を設定
                speedCurve.constantMin = originalSpeeds.x * multiplier;
                speedCurve.constantMax = originalSpeeds.y * multiplier;

                main.startSpeed = speedCurve;
            }
        }
    }

    /// <summary>
    /// エディタの非再生中（編集モード）でスケールを更新します。
    /// coreScale と particleScale の値が変更されたときに呼び出されます。
    /// </summary>
    private void UpdateScaleInEditor()
    {
        SetCoreScale(coreScale);
        SetParticleSpeedMultiplier(particleScale);
    }
}

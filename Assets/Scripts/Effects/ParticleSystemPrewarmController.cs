using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameObjectが有効になるたびに、配下のParticleSystemを指定秒数まで事前シミュレーションします。
/// 雪、雨、霧など、表示直後から十分に粒子が行き渡っていてほしい環境エフェクトに使用します。
/// </summary>
public class ParticleSystemPrewarmController : MonoBehaviour
{
    #region Fields

    [Header("事前シミュレーション設定")]
    [SerializeField, Min(0f), Tooltip("有効化時にParticleSystemを何秒後の状態まで進めるか。")]
    private float prewarmTime = 5f;

    [
        SerializeField,
        Tooltip("有効にすると固定刻みで高精度に計算します。環境エフェクトでは通常オフを推奨します。")
    ]
    private bool isUsingFixedTimeStep = false;

    // 子ParticleSystemの二重シミュレーションを避けるため、最上位のものだけを保持します。
    private readonly List<ParticleSystem> rootParticleSystems = new List<ParticleSystem>();

    #endregion

    #region Unity Lifecycle Methods

    private void Awake()
    {
        CacheRootParticleSystems();
    }

    private void OnEnable()
    {
        PrewarmAndPlay();
    }

    private void OnValidate()
    {
        prewarmTime = Mathf.Max(0f, prewarmTime);
    }

    #endregion

    #region Prewarm Logic

    /// <summary>
    /// 配下のParticleSystemを初期状態から指定時間まで進め、その状態から通常再生を開始します。
    /// </summary>
    private void PrewarmAndPlay()
    {
        if (rootParticleSystems.Count == 0)
        {
            CacheRootParticleSystems();
        }

        foreach (ParticleSystem particleSystem in rootParticleSystems)
        {
            if (particleSystem == null)
                continue;

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            if (prewarmTime > 0f)
            {
                particleSystem.Simulate(prewarmTime, true, true, isUsingFixedTimeStep);
            }

            particleSystem.Play(true);
        }
    }

    /// <summary>
    /// 自身を含む子階層から、別のParticleSystemを親に持たない最上位のParticleSystemを取得します。
    /// </summary>
    private void CacheRootParticleSystems()
    {
        rootParticleSystems.Clear();

        ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem != null && !HasParticleSystemParent(particleSystem.transform))
            {
                rootParticleSystems.Add(particleSystem);
            }
        }

        if (rootParticleSystems.Count == 0)
        {
            Debug.LogWarning("配下にParticleSystemが見つかりません。", this);
        }
    }

    /// <summary>
    /// このコンポーネントのTransform配下に、対象ParticleSystemの親ParticleSystemがあるか確認します。
    /// </summary>
    private bool HasParticleSystemParent(Transform particleTransform)
    {
        Transform parent = particleTransform.parent;
        while (parent != null && parent.IsChildOf(transform))
        {
            if (parent.TryGetComponent(out ParticleSystem _))
            {
                return true;
            }

            parent = parent.parent;
        }

        return false;
    }

    #endregion
}

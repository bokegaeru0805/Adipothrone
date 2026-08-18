using System.Collections.Generic;
using DG.Tweening; // DOTweenを使用するために追加
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// 魔法陣エフェクトの各子オブジェクトのスケールと、全体の透明度を動的に制御するクラス
/// </summary>
public class MagicCircleController : MonoBehaviour
{
    #region フィールド

    [Header("対象オブジェクト")]
    [
        SerializeField,
        Tooltip("変化の対象となる子オブジェクトたちのTransform（未設定時はAwakeで自動取得）")
    ]
    private List<Transform> targetChildren = new List<Transform>();

    [Header("デフォルト設定")]
    [SerializeField, Tooltip("変化のデフォルトのイージング（インスペクター上で調整可能）")]
    private Ease defaultEase = Ease.Linear;

    [Header("エディタ用プレビュー設定")]
    [
        SerializeField,
        Tooltip("プレビュー用のスケール（Z軸には影響しません）"),
        OnValueChanged(nameof(UpdateScaleInEditor))
    ]
    private Vector2 previewScale = Vector2.one;

    private ParticleSystem[] particleSystems;
    private Tween alphaTween;

    #endregion

    #region 初期化・エディタ更新

    private void Awake()
    {
        InitializeParticleSystems();
    }

    /// <summary>
    /// 子オブジェクトに含まれるすべてのParticleSystemとTransformを取得・キャッシュします
    /// </summary>
    private void InitializeParticleSystems()
    {
        // 子のTransformをそれぞれ取得・キャッシュ
        targetChildren.Clear();
        foreach (Transform child in transform)
        {
            targetChildren.Add(child);
        }

        // 全ての子オブジェクトからParticleSystemコンポーネントを取得
        particleSystems = GetComponentsInChildren<ParticleSystem>();

        if (particleSystems == null || particleSystems.Length == 0)
        {
            Debug.LogWarning("子オブジェクトに ParticleSystem が見つかりません。", this);
        }
    }

    /// <summary>
    /// インスペクター上の値が変更された際、非再生モードでもプレビューを更新します
    /// </summary>
    private void UpdateScaleInEditor()
    {
        // 親オブジェクト（本体）ではなく、すべての子オブジェクトのスケールを変更します
        foreach (Transform child in transform)
        {
            if (child != null)
            {
                // Z軸のスケールは現在の値を維持
                child.localScale = new Vector3(previewScale.x, previewScale.y, child.localScale.z);
            }
        }
    }

    #endregion

    #region スケール制御

    /// <summary>
    /// すべての子オブジェクトのX軸とY軸のスケールを同時に変更します
    /// </summary>
    /// <param name="endScale">最終的なスケール値</param>
    /// <param name="duration">変化にかかる時間（秒）</param>
    /// <param name="startScale">初期スケール値（省略時は現在の値）</param>
    /// <param name="ease">イージングの設定（省略時はデフォルト設定を使用）</param>
    public void ChangeScaleXY(
        Vector2 endScale,
        float duration,
        Vector2? startScale = null,
        Ease? ease = null
    )
    {
        EnsureTargetChildrenLines();

        Ease activeEase = ease ?? defaultEase;

        foreach (var child in targetChildren)
        {
            if (child == null)
                continue;

            // 初期値の指定があれば即座に適用
            if (startScale.HasValue)
            {
                child.localScale = new Vector3(
                    startScale.Value.x,
                    startScale.Value.y,
                    child.localScale.z
                );
            }

            // 前の処理をキャンセルして新しい処理で上書き
            child.DOKill();

            // 即時反映（duration が 0）の場合の安全処理
            if (duration <= 0)
            {
                child.localScale = new Vector3(endScale.x, endScale.y, child.localScale.z);
            }
            else
            {
                child
                    .DOScale(new Vector3(endScale.x, endScale.y, child.localScale.z), duration)
                    .SetEase(activeEase);
            }
        }
    }

    /// <summary>
    /// すべての子オブジェクトのX軸のスケールのみを変更します（Y軸のアニメーションを邪魔しません）
    /// </summary>
    /// <param name="endScaleX">最終的なX軸のスケール値</param>
    /// <param name="duration">変化にかかる時間（秒）</param>
    /// <param name="startScaleX">初期のX軸のスケール値（省略時は現在の値）</param>
    /// <param name="ease">イージングの設定（省略時はデフォルト設定を使用）</param>
    public void ChangeScaleX(
        float endScaleX,
        float duration,
        float? startScaleX = null,
        Ease? ease = null
    )
    {
        EnsureTargetChildrenLines();

        Ease activeEase = ease ?? defaultEase;

        foreach (var child in targetChildren)
        {
            if (child == null)
                continue;

            // X軸の初期値の指定があれば即座に適用
            if (startScaleX.HasValue)
            {
                child.localScale = new Vector3(
                    startScaleX.Value,
                    child.localScale.y,
                    child.localScale.z
                );
            }

            // X軸に関する古いTweenerのみをピンポイントで削除して上書き
            string tweenId = string.Concat(child.GetInstanceID(), "_scaleX");
            DOTween.Kill(tweenId);

            if (duration <= 0)
            {
                child.localScale = new Vector3(endScaleX, child.localScale.y, child.localScale.z);
            }
            else
            {
                // DOScaleX を使用し、IDを付与して単一軸のみを制御
                child.DOScaleX(endScaleX, duration).SetEase(activeEase).SetId(tweenId);
            }
        }
    }

    /// <summary>
    /// すべての子オブジェクトのY軸のスケールのみを変更します（X軸のアニメーションを邪魔しません）
    /// </summary>
    /// <param name="endScaleY">最終的なY軸のスケール値</param>
    /// <param name="duration">変化にかかる時間（秒）</param>
    /// <param name="startScaleY">初期のY軸のスケール値（省略時は現在の値）</param>
    /// <param name="ease">イージングの設定（省略時はデフォルト設定を使用）</param>
    public void ChangeScaleY(
        float endScaleY,
        float duration,
        float? startScaleY = null,
        Ease? ease = null
    )
    {
        EnsureTargetChildrenLines();

        Ease activeEase = ease ?? defaultEase;

        foreach (var child in targetChildren)
        {
            if (child == null)
                continue;

            // Y軸の初期値の指定があれば即座に適用
            if (startScaleY.HasValue)
            {
                child.localScale = new Vector3(
                    child.localScale.x,
                    startScaleY.Value,
                    child.localScale.z
                );
            }

            // Y軸に関する古いTweenerのみをピンポイントで削除して上書き
            string tweenId = string.Concat(child.GetInstanceID(), "_scaleY");
            DOTween.Kill(tweenId);

            if (duration <= 0)
            {
                child.localScale = new Vector3(child.localScale.x, endScaleY, child.localScale.z);
            }
            else
            {
                // DOScaleY を使用し、IDを付与して単一軸のみを制御
                child.DOScaleY(endScaleY, duration).SetEase(activeEase).SetId(tweenId);
            }
        }
    }

    /// <summary>
    /// 外部スクリプトからの動的呼び出し時に、リストが空だった場合の安全用ランタイム初期化
    /// </summary>
    private void EnsureTargetChildrenLines()
    {
        if (targetChildren == null || targetChildren.Count == 0)
        {
            InitializeParticleSystems();
        }
    }

    #endregion

    #region 透明度制御

    /// <summary>
    /// 魔法陣（すべてのParticleSystem）の透明度を変更します
    /// </summary>
    /// <param name="endAlpha">最終的な透明度（ 0.0 ~ 1.0 ）</param>
    /// <param name="duration">変化にかかる時間（秒）</param>
    /// <param name="startAlpha">初期の透明度（省略時は現在の値）</param>
    /// <param name="ease">イージングの設定（省略時はデフォルト設定を使用）</param>
    public void ChangeAlpha(
        float endAlpha,
        float duration,
        float? startAlpha = null,
        Ease? ease = null
    )
    {
        float start = startAlpha ?? GetCurrentAlpha();

        // 前の処理をキャンセルして新しい処理で上書き
        alphaTween?.Kill();

        Ease activeEase = ease ?? defaultEase;

        // DOVirtualを使用して指定した時間かけて値を補間し、毎フレーム UpdateParticleAlpha を呼び出す
        alphaTween = DOVirtual
            .Float(start, endAlpha, duration, UpdateParticleAlpha)
            .SetEase(activeEase);
    }

    /// <summary>
    /// すべてのParticleSystemの透明度を更新します
    /// 発生済みのパーティクルと、これから発生するパーティクルの両方に適用します
    /// </summary>
    /// <param name="alpha">設定する透明度</param>
    private void UpdateParticleAlpha(float alpha)
    {
        if (particleSystems == null || particleSystems.Length == 0)
        {
            InitializeParticleSystems();
            if (particleSystems == null)
                return;
        }

        foreach (var ps in particleSystems)
        {
            if (ps == null)
                continue;

            // 1. これから発生するパーティクルの初期色を変更
            var main = ps.main;
            Color startColor = main.startColor.color;
            startColor.a = alpha;
            main.startColor = startColor;

            // 2. すでに発生している（生存中の）パーティクルの色を変更
            ParticleSystem.Particle[] aliveParticles = new ParticleSystem.Particle[
                main.maxParticles
            ];
            int count = ps.GetParticles(aliveParticles);

            for (int i = 0; i < count; i++)
            {
                Color32 currentColor = aliveParticles[i].startColor;
                // 0.0 ~ 1.0 の値を 0 ~ 255 に変換して適用
                currentColor.a = (byte)(alpha * 255f);
                aliveParticles[i].startColor = currentColor;
            }

            ps.SetParticles(aliveParticles, count);
        }
    }

    /// <summary>
    /// 現在の透明度を取得します（最初のParticleSystemのアルファ値を基準とします）
    /// </summary>
    /// <returns>現在の透明度</returns>
    private float GetCurrentAlpha()
    {
        if (particleSystems != null && particleSystems.Length > 0 && particleSystems[0] != null)
        {
            return particleSystems[0].main.startColor.color.a;
        }
        return 1.0f;
    }

    #endregion
}

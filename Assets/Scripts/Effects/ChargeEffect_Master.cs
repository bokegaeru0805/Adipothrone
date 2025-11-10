using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// ChargeEffect_Master オブジェクトにアタッチします。
/// 2つのエフェクトグループ（CoreEffects, SlashEffects）の
/// 大きさ、再生時間、再生/停止をまとめて制御します。
/// </summary>
public class ChargeEffect_Master : MonoBehaviour
{
    [Header("エフェクトグループのTransform")]
    [SerializeField]
    [Tooltip("CoreEffects オブジェクトのTransformをここにドラッグ＆ドロップ")]
    private Transform coreEffectsGroup;

    [SerializeField]
    [Tooltip("SlashEffects オブジェクトのTransformをここにドラッグ＆ドロップ")]
    private Transform slashEffectsGroup;

    [Header("スケール設定（編集モード用）")]
    [Space]
    [SerializeField, Tooltip("Coreグループのスケール"), OnValueChanged(nameof(UpdateScaleInEditor))]
    private float coreScale = 1.0f;

    [
        SerializeField,
        Tooltip("Slashグループのスケール"),
        OnValueChanged(nameof(UpdateScaleInEditor))
    ]
    private float slashScale = 1.0f;

    // キャッシュ（保存）した全てのパーティクルシステム
    private List<ParticleSystem> allParticleSystems;

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

        // 1. メインのリストを「空」にする
        allParticleSystems = new List<ParticleSystem>();

        // 2. CoreEffectsGroup の処理
        if (coreEffectsGroup != null)
        {
            // (A) まず「一時的なリスト」にCoreの子孫をすべて取得する
            List<ParticleSystem> coreTempList = new List<ParticleSystem>();
            coreEffectsGroup.GetComponentsInChildren<ParticleSystem>(true, coreTempList);

            // (B) foreach でメインリストに追加する
            foreach (var ps in coreTempList)
            {
                allParticleSystems.Add(ps);
            }
        }
        else
        {
            Debug.LogError("CoreEffectsGroup が設定されていません。", this);
        }

        // 3. SlashEffectsGroup の処理
        if (slashEffectsGroup != null)
        {
            // (A) また別の一時リストにSlashの子孫をすべて取得する
            List<ParticleSystem> slashTempList = new List<ParticleSystem>();
            slashEffectsGroup.GetComponentsInChildren<ParticleSystem>(true, slashTempList);

            // (B) foreach でメインリストの「末尾に」追加する
            // (Coreの分は消えない)
            foreach (var ps in slashTempList)
            {
                allParticleSystems.Add(ps);
            }

        }
        else
        {
            Debug.LogError("SlashEffectsGroup が設定されていません。", this);
        }

        // 4. 最終結果の確認
        if (allParticleSystems.Count == 0)
        {
            Debug.LogWarning("制御対象のParticleSystemが一つも見つかりませんでした。", this);
        }

        isInitialized = true;
    }

    // --- 外部から実行する Public メソッド ---

    /// <summary>
    /// 全てのエフェクトを再生します。
    /// </summary>
    public void PlayEffect()
    {
        // Awakeが呼ばれていない場合（非アクティブからいきなりPlayなど）に備えて初期化チェック
        if (!isInitialized)
        {
            Initialize();
        }

        foreach (var ps in allParticleSystems)
        {
            if (ps == null)
                continue; // 参照が切れた場合

            // 再生する前に、もし再生中なら一度停止してリセットする
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play();
        }
    }

    /// <summary>
    /// 全てのエフェクトを停止します（新しいパーティクルの発生を止めます）。
    /// </summary>
    public void StopEffect()
    {
        if (!isInitialized)
            return;

        foreach (var ps in allParticleSystems)
        {
            if (ps == null)
                continue; // 参照が切れた場合

            // StopEmitting = 新たなパーティクルの発生を止める
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    // --- 大きさを制御するメソッド ---

    /// <summary>
    /// CoreEffects グループの全体の大きさを設定します。
    /// </summary>
    /// <param name="scale">設定するスケール値 (例: 1.0, 2.5)</param>
    public void SetCoreScale(float scale)
    {
        if (coreEffectsGroup != null)
        {
            coreEffectsGroup.localScale = new Vector3(scale, scale, scale);
        }
    }

    /// <summary>
    /// SlashEffects グループの全体の大きさを設定します。
    /// </summary>
    /// <param name="scale">設定するスケール値 (例: 1.0, 2.5)</param>
    public void SetSlashScale(float scale)
    {
        if (slashEffectsGroup != null)
        {
            slashEffectsGroup.localScale = new Vector3(scale, scale, scale);
        }
    }

    // --- 存在時間を制御するメソッド ---

    /// <summary>
    /// 全てのエフェクトの共通の存在時間（Duration と StartLifetime）を設定します。
    /// </summary>
    /// <param name="duration">設定する時間（秒）</param>
    public void SetDuration(float duration)
    {
        if (!isInitialized)
        {
            Initialize();
        }

        if (duration <= 0)
        {
            Debug.LogWarning("エフェクトの時間は0以下に設定できません。", this);
            return;
        }

        foreach (var ps in allParticleSystems)
        {
            if (ps == null)
                continue; // 参照が切れた場合

            // ParticleSystem の main モジュールを取得
            var main = ps.main;

            //システム自体の再生時間を設定
            main.duration = duration - main.startLifetime.constantMax;
        }
    }

    /// <summary>
    /// エディタの非再生中（編集モード）でスケールを更新します。
    /// coreScale と slashScale の値が変更されたときに呼び出されます。
    /// </summary>
    private void UpdateScaleInEditor()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning(
                "このボタンはエディタの非再生中（編集モード）でのみ使用してください。"
            );
            return;
        }

        SetCoreScale(coreScale);
        SetSlashScale(slashScale);
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// シーン内のGlobal Volumeを一元管理するクラス。
/// プロファイルの変更をクロスフェード（滑らかな遷移）で行う機能を提供します。
/// </summary>
public class GlobalVolumeManager : MonoBehaviour
{
    #region Singleton

    public static GlobalVolumeManager instance { get; private set; }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            InitializeVolumes();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region Private Fields
    private Volume mainVolume;
    private Volume blendVolume;
    private Coroutine transitionCoroutine;
    private float currentMasterWeight = 1.0f; // 外部から設定される基本ウェイト
    #endregion

    #region Events

    /// <summary>
    /// プロファイルが変更された（遷移が完了した）時に発行されるイベント
    /// </summary>
    public event System.Action<VolumeProfile> OnProfileChanged;

    #endregion

    #region Public Properties

    /// <summary>
    /// 現在適用されている（または遷移の目標となっている）プロファイル
    /// </summary>
    public VolumeProfile CurrentProfile => mainVolume != null ? mainVolume.profile : null;

    /// <summary>
    /// Volume全体の適用度（0.0f ~ 1.0f）。
    /// ピンチエフェクトなどで全体をON/OFFする際に使用します。
    /// </summary>
    public float Weight
    {
        get => currentMasterWeight;
        set
        {
            currentMasterWeight = Mathf.Clamp01(value);
            // 現在のボリューム状態に即座に反映
            UpdateVolumeWeights(mainVolume.weight, blendVolume != null ? blendVolume.weight : 0f);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 指定した時間をかけて、現在のプロファイルから新しいプロファイルへ滑らかに遷移します。
    /// </summary>
    /// <param name="newProfile">新しいプロファイル</param>
    /// <param name="duration">遷移にかける時間（秒）</param>
    public void ChangeProfile(VolumeProfile newProfile, float duration = 0f)
    {
        if (mainVolume == null || newProfile == null)
            return;

        // 既に同じプロファイルなら何もしない
        if (mainVolume.profile == newProfile)
            return;

        // 遷移時間がほぼ0、またはブレンド用ボリュームがない場合は即時切り替え
        if (duration <= 0.01f || blendVolume == null)
        {
            ChangeProfileImmediate(newProfile);
            return;
        }

        // 既存の遷移処理があれば停止
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        // 新しい遷移を開始
        transitionCoroutine = StartCoroutine(TransitionRoutine(newProfile, duration));
    }

    /// <summary>
    /// プロファイルを即座に変更します。
    /// </summary>
    public void ChangeProfileImmediate(VolumeProfile newProfile)
    {
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        mainVolume.profile = newProfile;
        mainVolume.weight = 1.0f * currentMasterWeight;

        if (blendVolume != null)
        {
            blendVolume.weight = 0f;
            blendVolume.profile = null;
        }

        OnProfileChanged?.Invoke(newProfile);
    }

    /// <summary>
    /// 現在のプロファイルから指定した型（Vignetteなど）のコンポーネントを取得します。
    /// </summary>
    public bool TryGetProfileComponent<T>(out T component)
        where T : VolumeComponent
    {
        component = null;
        if (mainVolume == null || mainVolume.profile == null)
            return false;

        return mainVolume.profile.TryGet<T>(out component);
    }

    #endregion

    #region Internal Logic & Coroutines

    /// <summary>
    /// 初期化処理。ブレンド用のVolumeがなければ生成します。
    /// </summary>
    private void InitializeVolumes()
    {
        // mainVolumeが未設定ならタグで検索
        if (mainVolume == null)
        {
            GameObject volumeObj = GameObject.FindGameObjectWithTag(
                GameConstants.MAIN_GLOBAL_VOLUME_TAG_NAME
            );
            if (volumeObj != null)
            {
                mainVolume = volumeObj.GetComponent<Volume>();
            }

            if (mainVolume == null)
            {
                Debug.LogError("GlobalVolumeManager: 管理対象のVolumeが見つかりません。", this);
                return;
            }
        }

        // blendVolumeが未設定なら、mainVolumeを複製して作成（同じPriority設定などを引き継ぐため）
        if (blendVolume == null)
        {
            GameObject blendObj = Instantiate(mainVolume.gameObject, mainVolume.transform.parent);
            blendObj.name = $"{mainVolume.gameObject.name}_Blend";

            // 不要なコンポーネント（もしあれば）の整理や初期化
            blendVolume = blendObj.GetComponent<Volume>();

            // Manager自身についている場合などの無限増殖防止策として、コンポーネントのみ追加の方が安全な場合もあるが、
            // 設定引き継ぎのため複製を採用。ただしManagerと同じオブジェクトなら子は作らずコンポーネント追加を推奨。
            if (mainVolume.gameObject == this.gameObject)
            {
                // Managerと同じオブジェクトにVolumeがある場合は複製だとManagerも増えるのでDestroyしてAddし直す
                Destroy(blendObj);
                blendVolume = gameObject.AddComponent<Volume>();
                blendVolume.isGlobal = true;
                blendVolume.priority = mainVolume.priority; // 同じ優先度
            }
        }

        // 初期状態設定
        mainVolume.weight = 1f * currentMasterWeight;
        blendVolume.weight = 0f;
        blendVolume.profile = null;
    }

    /// <summary>
    /// クロスフェード遷移を行うコルーチン
    /// </summary>
    private IEnumerator TransitionRoutine(VolumeProfile targetProfile, float duration)
    {
        // 1. ブレンド用ボリュームにターゲットプロファイルを設定
        blendVolume.profile = targetProfile;

        // 開始時のウェイト状態
        float timer = 0f;

        // クロスフェード処理
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);

            // 滑らかなカーブを適用（EaseInOut）
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // Main（元）をフェードアウト、Blend（新）をフェードイン
            // ※MasterWeightを考慮して乗算する
            float mainWeight = (1f - smoothT);
            float blendWeight = smoothT;

            UpdateVolumeWeights(mainWeight, blendWeight);

            yield return null;
        }

        // 2. 完了処理：メインのボリュームを新しいプロファイルに差し替える
        mainVolume.profile = targetProfile;

        // 3. ウェイトをリセット（メインを1、ブレンドを0）
        UpdateVolumeWeights(1f, 0f);
        blendVolume.profile = null;

        transitionCoroutine = null;

        // 4. 変更通知
        OnProfileChanged?.Invoke(targetProfile);
    }

    /// <summary>
    /// MasterWeightを考慮して各ボリュームのウェイトを適用するヘルパー
    /// </summary>
    private void UpdateVolumeWeights(float mainBaseWeight, float blendBaseWeight)
    {
        if (mainVolume != null)
            mainVolume.weight = mainBaseWeight * currentMasterWeight;

        if (blendVolume != null)
            blendVolume.weight = blendBaseWeight * currentMasterWeight;
    }

    #endregion
}

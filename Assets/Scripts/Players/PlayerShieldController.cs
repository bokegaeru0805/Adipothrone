using System;
using UnityEngine;

/// <summary>
/// プレイヤーのシールド耐久値、状態、エフェクト（パーティクル）、およびジャストガードを総合的に管理するクラス。
/// </summary>
public class PlayerShieldController : MonoBehaviour
{
    #region Inspector Settings

    [Header("シールド設定")]
    [SerializeField, Tooltip("シールドのエフェクト用ParticleSystem（子オブジェクト）")]
    private ParticleSystem shieldParticle;

    [SerializeField, Tooltip("通常時の1秒あたりの耐久値回復割合")]
    private float normalRecoveryRate = 0.2f;

    [SerializeField, Tooltip("破壊状態時の1秒あたりの耐久値回復割合")]
    private float brokenRecoveryRate = 0.05f;

    [SerializeField, Tooltip("シールド展開中の1秒あたりの耐久値減少割合")]
    private float depletionRate = 0.1f;

    [Header("ジャストガード設定")]
    [SerializeField, Tooltip("シールド展開後、ジャストガード判定になる秒数")]
    private float justGuardWindow = 0.2f;

    [SerializeField, Tooltip("ジャストガード成功時のダメージ倍率（0.3ならダメージ7割減）")]
    private float justGuardMultiplier = 0.3f;

    #endregion

    #region Public Properties & Events

    /// <summary>現在の耐久値（0.0 ～ 1.0）</summary>
    public float CurrentDurability { get; private set; } = 1.0f;

    /// <summary>シールド展開中かどうかのフラグ</summary>
    public bool isShieldActive { get; private set; } = false;

    /// <summary>破壊状態（耐久値0未満でペナルティ中）かどうかのフラグ</summary>
    public bool isBroken { get; private set; } = false;

    /// <summary>耐久値が変化した際にUIなどに通知するイベント</summary>
    public event Action<float> OnDurabilityChanged;

    /// <summary>シールドの展開状態が変化した際にUIへ通知するイベント</summary>
    public event Action<bool> OnShieldActiveChanged;

    /// <summary>破壊状態が変化した際にUIへ通知するイベント</summary>
    public event Action<bool> OnBrokenStateChanged;

    #endregion

    #region Internal Variables

    // --- 内部参照 ---
    private InputManager inputManager;
    private PlayerManager playerManager;
    private ParticleSystem.MainModule particleMain;

    // --- 状態管理用 ---
    private Color baseColor; // パーティクルの基準色
    private bool canUseShield = true; // システム的にシールドが使用可能かどうかのフラグ
    private float shieldActivationTime = -1f; // シールドが展開された時間を記録する変数（ジャストガード判定用）
    #endregion

    #region Unity Lifecycle Methods

    /// <summary>
    /// 必要なコンポーネントの取得、変数の初期化、およびイベントの購読を行います。
    /// </summary>
    private void Start()
    {
        inputManager = InputManager.instance;
        playerManager = PlayerManager.instance;

        // エフェクトの初期設定
        if (shieldParticle != null)
        {
            particleMain = shieldParticle.main;
            baseColor = particleMain.startColor.color;
        }
        else
        {
            Debug.LogWarning("シールド用のParticleSystemが設定されていません。");
        }

        // 状態の初期化
        CurrentDurability = 1.0f;

        // PlayerManagerのステータス変更イベントを購読
        if (playerManager != null)
        {
            canUseShield = playerManager.GetPlayerBoolStatus(PlayerStatusBoolName.isCanUseShield);
            playerManager.OnBoolStatusChanged += OnAnyBoolStatusChanged;
        }
    }

    /// <summary>
    /// 毎フレームの入力受付、耐久値の更新（減少・回復）、およびエフェクトへの反映を行います。
    /// </summary>
    private void Update()
    {
        HandleInput();
        HandleRecovery();
        HandleDepletion();
        UpdateParticleAppearance();
    }

    /// <summary>
    /// オブジェクト破棄時にイベント購読を解除し、メモリリークを防ぎます。
    /// </summary>
    private void OnDestroy()
    {
        if (playerManager != null)
        {
            playerManager.OnBoolStatusChanged -= OnAnyBoolStatusChanged;
        }
    }

    #endregion

    #region Input & State Control

    /// <summary>
    /// シールド展開のキー入力処理と、それに伴う状態の切り替えを行います。
    /// </summary>
    private void HandleInput()
    {
        if (inputManager == null)
            return;

        bool isHoldingShield = inputManager.GetPlayerShieldHold();

        // 破壊されておらず、キーが押されており、かつシールドが使用可能な状態の場合のみ展開を許可
        if (isHoldingShield && !isBroken && canUseShield)
        {
            if (!isShieldActive)
            {
                isShieldActive = true;
                shieldActivationTime = Time.time; // ジャストガード用に展開した時間を記録
                shieldParticle.gameObject.SetActive(true); // エフェクトを有効化
                shieldParticle?.Play();
                OnShieldActiveChanged?.Invoke(true); // UIへ展開開始を通知
            }
        }
        else
        {
            // キーを離した、破壊された、または使用不可状態になった場合は解除
            if (isShieldActive)
            {
                DeactivateShield();
            }
        }
    }

    /// <summary>
    /// シールドの非表示と状態リセットを行う共通処理です。
    /// </summary>
    private void DeactivateShield()
    {
        isShieldActive = false;
        shieldParticle?.Stop(); // Clear()にすると即座にエフェクトが消えるためStop()を使用
        shieldParticle.gameObject.SetActive(false); // エフェクトオブジェクト自体を非表示
        OnShieldActiveChanged?.Invoke(false); // UIへ非表示を通知
    }

    /// <summary>
    /// PlayerManagerからのステータス変更通知を受け取り、シールドの使用可否を更新します。
    /// </summary>
    private void OnAnyBoolStatusChanged(PlayerStatusBoolName flag, bool isEnabled)
    {
        if (flag == PlayerStatusBoolName.isCanUseShield)
        {
            canUseShield = isEnabled;

            // もしシールド展開中に使用不可制限がかけられた場合は、即座にシールドを解除する
            if (!canUseShield && isShieldActive)
            {
                DeactivateShield();
                Debug.Log("シールドの使用が制限されたため、展開を強制解除しました。");
            }
        }
    }

    #endregion

    #region Durability & Damage Logic

    /// <summary>
    /// PlayerManagerから呼ばれる、シールドへのダメージ適用処理です。
    /// ジャストガード判定もここで行います。
    /// </summary>
    /// <param name="damageRatio">最大HPに対するダメージ量の割合</param>
    public void TakeShieldDamage(float damageRatio)
    {
        // 1. ジャストガード判定
        // 現在の時間から展開した時間を引き、判定秒数(Window)以内かどうかをチェック
        if (Time.time - shieldActivationTime <= justGuardWindow)
        {
            damageRatio *= justGuardMultiplier; // ダメージ量を軽減
            Debug.Log("ジャストガード成功！ダメージを大幅に軽減しました。");

            // ※ここにジャストガード成功用の専用SEやフラッシュ演出を追加すると手触りが良くなります
        }

        // 2. 耐久値の減少処理
        CurrentDurability -= damageRatio;

        // 3. 破壊判定
        // 0を下回った場合は破壊状態へ強制移行する
        if (CurrentDurability <= 0f)
        {
            CurrentDurability = 0f;
            isBroken = true;
            isShieldActive = false;
            shieldParticle?.Stop();
            shieldParticle.gameObject.SetActive(false);

            // UIへ破壊および非展開を通知
            OnShieldActiveChanged?.Invoke(false);
            OnBrokenStateChanged?.Invoke(true);
            Debug.Log("シールドが破壊されました！");
        }

        // 耐久値の変更をUIへ通知
        OnDurabilityChanged?.Invoke(CurrentDurability);
    }

    /// <summary>
    /// シールド展開中の耐久値の自然減少処理です。
    /// </summary>
    private void HandleDepletion()
    {
        // 展開中のみ減少処理を行う
        if (!isShieldActive)
            return;

        CurrentDurability -= depletionRate * Time.deltaTime;

        // 展開し続けた結果、耐久値が0を下回った場合は破壊状態へ移行
        if (CurrentDurability <= 0f)
        {
            CurrentDurability = 0f;
            isBroken = true;
            isShieldActive = false;
            shieldParticle?.Stop();
            shieldParticle.gameObject.SetActive(false);

            // UIへ破壊および非展開を通知
            OnShieldActiveChanged?.Invoke(false);
            OnBrokenStateChanged?.Invoke(true);
            Debug.Log("シールドが限界に達し、自然破壊されました！");
        }

        OnDurabilityChanged?.Invoke(CurrentDurability);
    }

    /// <summary>
    /// シールド未展開時の耐久値の自然回復処理です。
    /// </summary>
    private void HandleRecovery()
    {
        // 展開中は回復しない
        if (isShieldActive)
            return;

        // 耐久値が最大未満の場合のみ回復処理を行う
        if (CurrentDurability < 1.0f)
        {
            // 破壊状態かどうかで回復速度(Rate)を切り替える
            float rate = isBroken ? brokenRecoveryRate : normalRecoveryRate;
            CurrentDurability += rate * Time.deltaTime;

            // 最大値に達した場合の処理（全回復）
            if (CurrentDurability >= 1.0f)
            {
                CurrentDurability = 1.0f;
                isBroken = false; // 破壊状態から復帰
                OnBrokenStateChanged?.Invoke(false); // UIへ破壊状態の解除を通知
                Debug.Log("シールドの耐久値が全回復し、再使用可能になりました。");
            }

            OnDurabilityChanged?.Invoke(CurrentDurability);
        }
    }

    #endregion

    #region Visual & Effect Control

    /// <summary>
    /// 耐久値に応じてParticleSystemのアルファ値（透明度）を更新し、
    /// 視覚的に残り耐久値を表現します。
    /// </summary>
    private void UpdateParticleAppearance()
    {
        if (isShieldActive && shieldParticle != null)
        {
            Color currentColor = baseColor;
            // 基準のアルファ値に、現在の耐久値の割合(0.0～1.0)を掛け合わせる
            currentColor.a = baseColor.a * CurrentDurability;
            particleMain.startColor = currentColor;
        }
    }

    #endregion
}

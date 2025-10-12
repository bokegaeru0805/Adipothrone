using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// プレイヤーのHPが危険な状態になった際に、Global Volumeを利用した画面効果を制御するクラス
/// </summary>
public class PinchEffectManager : MonoBehaviour
{
    [Header("エフェクト設定")]
    [SerializeField, Tooltip("制御対象のGlobal Volumeコンポーネント")]
    private Volume globalVolume;

    [SerializeField, Range(0.01f, 1f), Tooltip("このHP割合以下になったらエフェクトを開始します")]
    private float healthThreshold = 0.10f; // HP割合のしきい値 (例: 10%)

    [Header("ビネットの脈動（Pulsation）設定")]
    [SerializeField, Range(0f, 1f), Tooltip("ビネットのSmoothnessの下限値")]
    private float minSmoothness = 0.5f;

    [SerializeField, Range(0f, 1f), Tooltip("ビネットのSmoothnessの上限値")]
    private float maxSmoothness = 1.0f;

    [SerializeField, Tooltip("脈動の速さ")]
    private float pulsationSpeed = 2.0f;


    // --- 内部で管理する変数 ---
    private PlayerManager playerManager;
    private Vignette vignette; // Volume内のVignetteプロファイルをキャッシュ
    private Coroutine pulsationCoroutine; // 実行中のコルーチンを保持
    private bool isEffectActive = false; // エフェクトが現在アクティブかどうかのフラグ

    private void Start()
    {
        // PlayerManagerのインスタンスを取得
        playerManager = PlayerManager.instance;
        if (playerManager == null)
        {
            Debug.LogError("PlayerManagerのインスタンスが見つかりません。このスクリプトは機能しません。");
            this.enabled = false; // スクリプトを無効化
            return;
        }

        // VolumeとVignetteのセットアップ
        if (globalVolume == null)
        {
            Debug.LogError("Global Volumeが設定されていません。");
            this.enabled = false;
            return;
        }

        // VolumeプロファイルからVignetteの設定を取得
        if (!globalVolume.profile.TryGet<Vignette>(out vignette))
        {
            Debug.LogError("Global VolumeのプロファイルにVignetteが見つかりません。");
            this.enabled = false;
            return;
        }

        // 初期状態ではエフェクトを非表示にする
        globalVolume.weight = 0f;
        isEffectActive = false;

        // PlayerManagerのHP変更イベントを購読
        playerManager.OnChangeHP += HandleHPChange;

        // 初期HPで一度チェックを実行
        HandleHPChange(playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP));
    }

    private void OnDestroy()
    {
        // オブジェクトが破棄される際に、イベントの購読を解除（メモリリーク防止）
        if (playerManager != null)
        {
            playerManager.OnChangeHP -= HandleHPChange;
        }
    }

    /// <summary>
    /// PlayerManagerからのHP変更通知を受け取り、エフェクトを制御する
    /// </summary>
    /// <param name="currentHP">現在のHP</param>
    private void HandleHPChange(int currentHP)
    {
        // 最大HPが0の場合はゼロ除算を避ける
        if (playerManager.playerMaxHP <= 0) return;

        // 現在のHP割合を計算
        float healthRatio = (float)currentHP / playerManager.playerMaxHP;

        // HPがしきい値を下回り、かつエフェクトが非アクティブな場合
        if (healthRatio <= healthThreshold && !isEffectActive)
        {
            StartPinchEffect();
        }
        // HPがしきい値を上回り、かつエフェクトがアクティブな場合
        else if (healthRatio > healthThreshold && isEffectActive)
        {
            StopPinchEffect();
        }
    }

    /// <summary>
    /// ピンチエフェクトを開始する
    /// </summary>
    private void StartPinchEffect()
    {
        isEffectActive = true;
        globalVolume.weight = 1f; // Volumeを有効化

        // 既にコルーチンが動いている可能性を考慮して、一度停止してから開始する
        if (pulsationCoroutine != null)
        {
            StopCoroutine(pulsationCoroutine);
        }
        pulsationCoroutine = StartCoroutine(PulsateVignette());
    }

    /// <summary>
    /// ピンチエフェクトを停止する
    /// </summary>
    private void StopPinchEffect()
    {
        isEffectActive = false;
        globalVolume.weight = 0f; // Volumeを無効化

        // 実行中のコルーチンを停止
        if (pulsationCoroutine != null)
        {
            StopCoroutine(pulsationCoroutine);
            pulsationCoroutine = null;
        }
    }

    /// <summary>
    /// VignetteのSmoothnessを脈動させるコルーチン
    /// </summary>
    private IEnumerator PulsateVignette()
    {
        while (true)
        {
            // Mathf.Sinを使って-1から1の範囲で滑らかに変動する値を作成
            float sinValue = Mathf.Sin(Time.time * pulsationSpeed);

            // 値の範囲を0から1に変換
            float normalizedValue = (sinValue + 1f) / 2f;

            // minとmaxの範囲に合わせてSmoothnessの値を計算
            float targetSmoothness = Mathf.Lerp(minSmoothness, maxSmoothness, normalizedValue);

            // VignetteのSmoothnessに適用
            vignette.smoothness.value = targetSmoothness;

            // 次のフレームまで待機
            yield return null;
        }
    }
}
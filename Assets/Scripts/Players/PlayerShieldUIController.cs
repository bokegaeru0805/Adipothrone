using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// プレイヤーのシールド耐久値のUIバーを制御します。
/// </summary>
public class PlayerShieldUIController : MonoBehaviour
{
    [Header("UIコンポーネント")]
    [SerializeField]
    [Tooltip("シールド耐久値の量を表すImageコンポーネント（Fill Amount用）")]
    private Image fillImage;

    [SerializeField]
    [Tooltip("シールドUI全体のルートオブジェクト（表示/非表示の切り替え用）")]
    private GameObject shieldBarRootObject;

    [SerializeField]
    [Tooltip("監視する対象のPlayerShieldController")]
    private PlayerShieldController shieldController;

    [Header("色設定")]
    [SerializeField, Tooltip("通常時のシールドバーの色")]
    private Color normalColor = Color.cyan;

    [SerializeField, Tooltip("破壊時のシールドバーの色")]
    private Color brokenColor = Color.red;

    private float fillTweenDuration = 0.2f; // バーが変化する際のアニメーション時間（秒）

    // 実行中のDOTweenアニメーションの参照
    private Tween fillTween;

    private void OnEnable()
    {
        if (shieldController != null)
        {
            // 耐久値変動イベントを購読
            shieldController.OnDurabilityChanged += HandleDurabilityChanged;

            // 展開状態の変動イベントを購読
            shieldController.OnShieldActiveChanged += HandleShieldActiveChanged;

            // 破壊状態の変動イベントを購読
            shieldController.OnBrokenStateChanged += HandleBrokenStateChanged;

            // 表示を現在の耐久値で初期化（アニメーションなし）
            UpdateFillAmount(shieldController.CurrentDurability, 0f);

            // 初期状態の表示設定（展開中、または破壊状態なら表示する）
            if (shieldBarRootObject != null)
            {
                shieldBarRootObject.SetActive(
                    shieldController.isShieldActive || shieldController.isBroken
                );
            }

            // 初期状態の色設定
            if (fillImage != null)
            {
                fillImage.color = shieldController.isBroken ? brokenColor : normalColor;
            }
        }
        else
        {
            Debug.LogError("監視対象の PlayerShieldController が設定されていません。", this);
        }
    }

    private void OnDisable()
    {
        if (shieldController != null)
        {
            // イベント購読を解除
            shieldController.OnDurabilityChanged -= HandleDurabilityChanged;

            // 展開状態の変動イベント購読を解除
            shieldController.OnShieldActiveChanged -= HandleShieldActiveChanged;

            // 破壊状態の変動イベント購読を解除
            shieldController.OnBrokenStateChanged -= HandleBrokenStateChanged;
        }

        // アニメーションが残っていれば停止
        if (fillTween != null && fillTween.IsActive())
        {
            fillTween.Kill();
        }

        // 非アクティブ時はUIも非表示にする
        if (shieldBarRootObject != null)
        {
            shieldBarRootObject.SetActive(false);
        }
    }

    /// <summary>
    /// PlayerShieldController.OnDurabilityChanged から呼び出されるメソッド
    /// </summary>
    /// <param name="newDurability">現在の耐久値の割合（0.0 ～ 1.0）</param>
    private void HandleDurabilityChanged(float newDurability)
    {
        UpdateFillAmount(newDurability, fillTweenDuration);
    }

    /// <summary>
    /// UIバーの FillAmount をDOTweenで更新します
    /// </summary>
    private void UpdateFillAmount(float targetAmount, float duration)
    {
        if (fillImage != null)
        {
            if (fillTween != null && fillTween.IsActive())
            {
                fillTween.Kill();
            }

            fillTween = fillImage.DOFillAmount(targetAmount, duration).SetEase(Ease.OutQuart);
        }
    }

    /// <summary>
    /// PlayerShieldController.OnShieldActiveChanged から呼び出されるメソッド
    /// </summary>
    /// <param name="isActive">シールドが展開中かどうか</param>
    private void HandleShieldActiveChanged(bool isActive)
    {
        if (shieldBarRootObject != null)
        {
            // 展開中、または破壊状態であれば表示を維持する
            shieldBarRootObject.SetActive(isActive || shieldController.isBroken);
        }
    }

    /// <summary>
    /// PlayerShieldController.OnBrokenStateChanged から呼び出されるメソッド
    /// </summary>
    /// <param name="isBroken">破壊状態かどうか</param>
    private void HandleBrokenStateChanged(bool isBroken)
    {
        if (fillImage != null)
        {
            // 状態に応じて色を変更
            fillImage.color = isBroken ? brokenColor : normalColor;
        }

        if (shieldBarRootObject != null)
        {
            // 破壊されたら表示、回復（破壊解除）してかつ展開中でなければ非表示にする
            shieldBarRootObject.SetActive(shieldController.isShieldActive || isBroken);
        }
    }
}

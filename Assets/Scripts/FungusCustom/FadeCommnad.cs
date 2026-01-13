using System.Collections;
using Fungus;
using UnityEngine;

/// <summary>
/// FungusからFadeCanvasを制御するコマンド。
/// TimelineSkipManagerの早送り（Zキー）と全スキップ（Tキー）に対応。
/// </summary>
[CommandInfo(
    "Camera",
    "Control Fade Canvas",
    "カスタムFadeCanvasを制御します。TimelineSkipManagerの早送り・全スキップに対応しています。"
)]
[AddComponentMenu("")]
public class FadeCommand : Command
{
    [Tooltip("フェードの色タイプ (黒/白)")]
    [SerializeField]
    protected FadeClip.FadeColorType colorType = FadeClip.FadeColorType.Black;

    [Tooltip("目標のアルファ値 (0=透明, 1=完全に見えない)")]
    [Range(0f, 1f)]
    [SerializeField]
    protected float targetAlpha = 1.0f;

    [Tooltip("変化にかかる時間（秒）")]
    [SerializeField]
    protected float duration = 1.0f;

    [Tooltip("完了するまで次のコマンドに進まないか")]
    [SerializeField]
    protected bool waitUntilFinished = true;

    // TimelineSkipManagerで設定されている早送り倍率と同じ値（または参照）にしてください
    private const float FAST_FORWARD_MULTIPLIER = 3.0f;

    public override void OnEnter()
    {
        if (FadeCanvas.instance == null)
        {
            Debug.LogError("FadeCanvas instance is not found.");
            Continue();
            return;
        }

        StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        // 1. 現在のアルファ値を取得
        float startAlpha = 0f;
        if (colorType == FadeClip.FadeColorType.Black)
            startAlpha = FadeCanvas.instance.CurrentAlpha;
        else
            startAlpha = FadeCanvas.instance.CurrentFlashAlpha;

        // 2. もし時間がほぼ0なら即座に適用して終了
        if (duration <= Mathf.Epsilon)
        {
            ApplyAlpha(targetAlpha);
            if (waitUntilFinished)
                Continue();
            yield break;
        }

        float timer = 0f;

        // 3. フェードループ
        while (timer < duration)
        {
            // 経過時間の計算
            float dt = Time.deltaTime;

            // Zキー早送り中なら、時間を倍速で進める
            // (Tキーの全スキップ時はTime.timeScaleが変わるため、dtが自動的に大きくなり対応不要)
            if (
                TimelineSkipManager.instance != null
                && TimelineSkipManager.instance.IsFastForwarding
            )
            {
                dt *= FAST_FORWARD_MULTIPLIER;
            }

            timer += dt;

            // 進捗率 (0.0 ～ 1.0)
            float progress = Mathf.Clamp01(timer / duration);

            // 現在値を計算して適用
            float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            ApplyAlpha(newAlpha);

            yield return null;
        }

        // 4. 最終値を確実に適用
        ApplyAlpha(targetAlpha);

        if (waitUntilFinished)
        {
            Continue();
        }
    }

    /// <summary>
    /// 指定されたアルファ値をCanvasに適用するヘルパー
    /// </summary>
    private void ApplyAlpha(float alpha)
    {
        if (colorType == FadeClip.FadeColorType.Black)
        {
            FadeCanvas.instance.SetAlpha(alpha);
            //Debug.Log($"FadeCommand SetAlpha: {alpha}", this);
        }
        else
        {
            FadeCanvas.instance.SetFlashAlpha(alpha);
            //Debug.Log($"FadeCommand SetFlashAlpha: {alpha}", this);
        }
    }

    // 「待機しない」設定の場合、即座に次へ進む
    public override void Execute()
    {
        base.Execute();
        if (!waitUntilFinished)
        {
            Continue();
        }
    }

    public override string GetSummary()
    {
        string colorName = (colorType == FadeClip.FadeColorType.Black) ? "Black" : "White";
        return $"{colorName} -> {targetAlpha} ({duration}s)";
    }

    public override Color GetButtonColor()
    {
        // 視認しやすい薄い緑色
        return new Color32(216, 228, 170, 255);
    }
}

using System.Collections;
using Fungus;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[CommandInfo(
    "Light 2D",
    "Set Light 2D Property",
    "Light 2DのIntensity、Color、Enabledを変更します。IntensityとColorはTweenに対応しています。"
)]
[AddComponentMenu("")]
public class Light2DPropertyCommand : Command
{
    public enum Light2DPropertyMode
    {
        Intensity = 0,
        Color = 10,
        Enabled = 20,
    }

    [BoxGroup("Target Settings")]
    [Tooltip("変更対象のLight 2D")]
    [SerializeField]
    protected Light2D targetLight;

    [BoxGroup("Target Settings")]
    [SerializeField]
    protected Light2DPropertyMode propertyMode = Light2DPropertyMode.Intensity;

    [BoxGroup("Value Settings")]
    [AllowNesting]
    [ShowIf("IsIntensityMode")]
    [Tooltip("目標のIntensity")]
    [SerializeField]
    protected FloatData targetIntensity = new FloatData(1f);

    [BoxGroup("Value Settings")]
    [AllowNesting]
    [ShowIf("IsColorMode")]
    [Tooltip("目標の色")]
    [SerializeField]
    protected ColorData targetColor = new ColorData(Color.white);

    [BoxGroup("Value Settings")]
    [AllowNesting]
    [ShowIf("IsEnabledMode")]
    [Tooltip("Light 2Dコンポーネントを有効にするか")]
    [SerializeField]
    protected BooleanData isEnabled = new BooleanData(true);

    [BoxGroup("Tween Settings")]
    [AllowNesting]
    [ShowIf("SupportsTween")]
    [Tooltip("変化にかける時間（秒）。0以下の場合は即座に変更されます。")]
    [SerializeField]
    protected FloatData duration = new FloatData(0f);

    [BoxGroup("Tween Settings")]
    [AllowNesting]
    [ShowIf("IsTweening")]
    [Tooltip("変化が完了するまで次のコマンドを待機するか")]
    [SerializeField]
    protected bool waitUntilFinished = false;

    [BoxGroup("Tween Settings")]
    [AllowNesting]
    [ShowIf("IsTweening")]
    [Tooltip("Time Scaleの影響を受けない時間で変化させるか")]
    [SerializeField]
    protected bool useUnscaledTime = false;

    [BoxGroup("Tween Settings")]
    [AllowNesting]
    [ShowIf("IsTweening")]
    [Tooltip("変化のカーブ")]
    [SerializeField]
    protected AnimationCurve easeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private bool IsIntensityMode() => propertyMode == Light2DPropertyMode.Intensity;

    private bool IsColorMode() => propertyMode == Light2DPropertyMode.Color;

    private bool IsEnabledMode() => propertyMode == Light2DPropertyMode.Enabled;

    private bool SupportsTween() => IsIntensityMode() || IsColorMode();

    private bool IsTweening() => SupportsTween() && duration.Value > 0f;

    public override void OnEnter()
    {
        if (targetLight == null)
        {
            Debug.LogWarning("Light 2D Property Command: Target Lightが設定されていません。", this);
            Continue();
            return;
        }

        if (IsEnabledMode())
        {
            targetLight.enabled = isEnabled.Value;
            Continue();
            return;
        }

        float tweenDuration = duration.Value;
        if (tweenDuration <= 0f)
        {
            ApplyTargetValue();
            Continue();
            return;
        }

        StartCoroutine(TweenRoutine(tweenDuration));

        if (!waitUntilFinished)
        {
            Continue();
        }
    }

    private IEnumerator TweenRoutine(float tweenDuration)
    {
        float startIntensity = targetLight.intensity;
        Color startColor = targetLight.color;
        float elapsedTime = 0f;

        while (elapsedTime < tweenDuration)
        {
            if (targetLight == null)
            {
                if (waitUntilFinished)
                {
                    Continue();
                }

                yield break;
            }

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (
                TimelineSkipManager.instance != null
                && TimelineSkipManager.instance.IsFastForwarding
            )
            {
                deltaTime *= TimelineSkipManager.instance.FastForwardSpeed;
            }
            elapsedTime += deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / tweenDuration);
            float curveValue = easeCurve.Evaluate(normalizedTime);

            if (IsIntensityMode())
            {
                targetLight.intensity = Mathf.Lerp(
                    startIntensity,
                    targetIntensity.Value,
                    curveValue
                );
            }
            else
            {
                targetLight.color = UnityEngine.Color.Lerp(
                    startColor,
                    targetColor.Value,
                    curveValue
                );
            }

            yield return null;
        }

        if (targetLight != null)
        {
            ApplyTargetValue();
        }

        if (waitUntilFinished)
        {
            Continue();
        }
    }

    private void ApplyTargetValue()
    {
        switch (propertyMode)
        {
            case Light2DPropertyMode.Intensity:
                targetLight.intensity = targetIntensity.Value;
                break;
            case Light2DPropertyMode.Color:
                targetLight.color = targetColor.Value;
                break;
            case Light2DPropertyMode.Enabled:
                targetLight.enabled = isEnabled.Value;
                break;
        }
    }

    public override void OnStopExecuting()
    {
        StopAllCoroutines();
        base.OnStopExecuting();
    }

    public override string GetSummary()
    {
        if (targetLight == null)
            return "Error: No Target Light 2D";

        switch (propertyMode)
        {
            case Light2DPropertyMode.Intensity:
                return $"{targetLight.name} : Intensity -> {targetIntensity.Value}{GetTweenSummary()}";
            case Light2DPropertyMode.Color:
                return $"{targetLight.name} : Color -> {targetColor.Value}{GetTweenSummary()}";
            case Light2DPropertyMode.Enabled:
                return $"{targetLight.name} : Enabled -> {isEnabled.Value}";
            default:
                return targetLight.name;
        }
    }

    private string GetTweenSummary()
    {
        return duration.Value > 0f ? $" over {duration.Value}s" : "";
    }

    public override Color GetButtonColor()
    {
        return new Color32(255, 220, 120, 255);
    }
}

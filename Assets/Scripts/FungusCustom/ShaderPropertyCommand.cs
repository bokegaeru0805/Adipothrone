using System.Collections;
using System.Collections.Generic;
using Fungus;
using NaughtyAttributes;
using UnityEngine;

[CommandInfo(
    "Shader",
    "Set Shader Property",
    "SpriteLitFlash.shaderのFloatプロパティを変更します。Tweenと子SpriteRendererの一括変更に対応しています。"
)]
[AddComponentMenu("")]
public class ShaderPropertyCommand : Command
{
    public enum SpriteLitFlashFloatProperty
    {
        FlashAmount = 0,
        OverlayGlow = 10,
        OverlayBlend = 20,
        OverlayTextureScrollXSpeed = 30,
        OverlayTextureScrollYSpeed = 40,
        HologramStripesAmount = 50,
        HologramUnmodAmount = 60,
        HologramStripesSpeed = 70,
        HologramMinAlpha = 80,
        HologramMaxAlpha = 90,
        HologramBlend = 100,
    }

    private const string ShaderName = "MyShaders/2D/SpriteLitFlash";
    private const string OverlayKeyword = "_OVERLAY_ON";
    private const string HologramKeyword = "_HOLOGRAM_ON";

    [BoxGroup("Target Settings")]
    [Tooltip("変更対象のSpriteRendererを持つGameObject")]
    [SerializeField]
    protected GameObjectData targetGameObject;

    [BoxGroup("Target Settings")]
    [Tooltip("子オブジェクトのSpriteRendererも含めて変更するか")]
    [SerializeField]
    protected bool applyRecursively = false;

    [BoxGroup("Property Settings")]
    [SerializeField]
    protected SpriteLitFlashFloatProperty propertyMode = SpriteLitFlashFloatProperty.HologramBlend;

    [BoxGroup("Property Settings")]
    [Tooltip("変更後の値。Shaderで定義された範囲内に制限されます。")]
    [SerializeField]
    protected FloatData targetValue = new FloatData(0f);

    [BoxGroup("Tween Settings")]
    [Tooltip("変化にかける時間（秒）。0以下の場合は即座に変更されます。")]
    [SerializeField]
    protected FloatData duration = new FloatData(0f);

    [BoxGroup("Tween Settings")]
    [AllowNesting]
    [ShowIf("IsTweening")]
    [Tooltip("変化が完了するまで次のコマンドを待機するか")]
    [SerializeField]
    protected bool waitUntilFinished = true;

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

    private bool IsTweening() => duration.Value > 0f;

    public override void OnEnter()
    {
        if (targetGameObject.Value == null)
        {
            Debug.LogWarning("Sprite Lit Flash Property Command: Target GameObjectが設定されていません。", this);
            Continue();
            return;
        }

        SpriteRenderer[] spriteRenderers = GetTargetSpriteRenderers();
        int propertyId = Shader.PropertyToID(GetPropertyName());
        List<Material> materials = GetTargetMaterials(spriteRenderers, propertyId);

        if (materials.Count == 0)
        {
            Debug.LogWarning(
                "Sprite Lit Flash Property Command: 変更可能なSpriteLitFlash Materialが見つかりませんでした。",
                this
            );
            Continue();
            return;
        }

        float endValue = ClampValue(targetValue.Value);
        EnableRequiredKeyword(materials, endValue);

        float tweenDuration = duration.Value;
        if (tweenDuration <= 0f)
        {
            ApplyValue(materials, propertyId, endValue);
            Continue();
            return;
        }

        StartCoroutine(TweenRoutine(materials, propertyId, endValue, tweenDuration));

        if (!waitUntilFinished)
        {
            Continue();
        }
    }

    private SpriteRenderer[] GetTargetSpriteRenderers()
    {
        if (applyRecursively)
        {
            return targetGameObject.Value.GetComponentsInChildren<SpriteRenderer>();
        }

        SpriteRenderer spriteRenderer = targetGameObject.Value.GetComponent<SpriteRenderer>();
        return spriteRenderer != null ? new[] { spriteRenderer } : new SpriteRenderer[0];
    }

    private List<Material> GetTargetMaterials(SpriteRenderer[] spriteRenderers, int propertyId)
    {
        List<Material> materials = new List<Material>();

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null || spriteRenderer.sharedMaterial == null)
                continue;

            Material sharedMaterial = spriteRenderer.sharedMaterial;
            if (sharedMaterial.shader == null || sharedMaterial.shader.name != ShaderName)
                continue;

            Material material = spriteRenderer.material;
            if (!material.HasProperty(propertyId))
                continue;

            materials.Add(material);
        }

        return materials;
    }

    private IEnumerator TweenRoutine(
        List<Material> materials,
        int propertyId,
        float endValue,
        float tweenDuration
    )
    {
        float[] startValues = new float[materials.Count];
        for (int i = 0; i < materials.Count; i++)
        {
            startValues[i] = materials[i] != null ? materials[i].GetFloat(propertyId) : 0f;
        }

        float elapsedTime = 0f;
        while (elapsedTime < tweenDuration)
        {
            elapsedTime += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / tweenDuration);
            float curveValue = easeCurve.Evaluate(normalizedTime);

            for (int i = 0; i < materials.Count; i++)
            {
                if (materials[i] != null)
                {
                    float currentValue = Mathf.LerpUnclamped(startValues[i], endValue, curveValue);
                    materials[i].SetFloat(propertyId, ClampValue(currentValue));
                }
            }

            yield return null;
        }

        ApplyValue(materials, propertyId, endValue);

        if (waitUntilFinished)
        {
            Continue();
        }
    }

    private void EnableRequiredKeyword(List<Material> materials, float endValue)
    {
        if (endValue <= 0f)
            return;

        string keyword;
        if (propertyMode == SpriteLitFlashFloatProperty.HologramBlend)
        {
            keyword = HologramKeyword;
        }
        else if (propertyMode == SpriteLitFlashFloatProperty.OverlayBlend)
        {
            keyword = OverlayKeyword;
        }
        else
        {
            return;
        }

        foreach (Material material in materials)
        {
            if (material != null)
            {
                material.EnableKeyword(keyword);
            }
        }
    }

    private static void ApplyValue(List<Material> materials, int propertyId, float value)
    {
        foreach (Material material in materials)
        {
            if (material != null)
            {
                material.SetFloat(propertyId, value);
            }
        }
    }

    private string GetPropertyName()
    {
        switch (propertyMode)
        {
            case SpriteLitFlashFloatProperty.FlashAmount:
                return "_FlashAmount";
            case SpriteLitFlashFloatProperty.OverlayGlow:
                return "_OverlayGlow";
            case SpriteLitFlashFloatProperty.OverlayBlend:
                return "_OverlayBlend";
            case SpriteLitFlashFloatProperty.OverlayTextureScrollXSpeed:
                return "_OverlayTextureScrollXSpeed";
            case SpriteLitFlashFloatProperty.OverlayTextureScrollYSpeed:
                return "_OverlayTextureScrollYSpeed";
            case SpriteLitFlashFloatProperty.HologramStripesAmount:
                return "_HologramStripesAmount";
            case SpriteLitFlashFloatProperty.HologramUnmodAmount:
                return "_HologramUnmodAmount";
            case SpriteLitFlashFloatProperty.HologramStripesSpeed:
                return "_HologramStripesSpeed";
            case SpriteLitFlashFloatProperty.HologramMinAlpha:
                return "_HologramMinAlpha";
            case SpriteLitFlashFloatProperty.HologramMaxAlpha:
                return "_HologramMaxAlpha";
            case SpriteLitFlashFloatProperty.HologramBlend:
                return "_HologramBlend";
            default:
                return "_HologramBlend";
        }
    }

    private float ClampValue(float value)
    {
        switch (propertyMode)
        {
            case SpriteLitFlashFloatProperty.OverlayGlow:
                return Mathf.Clamp(value, 0f, 25f);
            case SpriteLitFlashFloatProperty.OverlayTextureScrollXSpeed:
            case SpriteLitFlashFloatProperty.OverlayTextureScrollYSpeed:
                return Mathf.Clamp(value, -5f, 5f);
            case SpriteLitFlashFloatProperty.HologramStripesSpeed:
                return Mathf.Clamp(value, -20f, 20f);
            case SpriteLitFlashFloatProperty.HologramMaxAlpha:
                return Mathf.Clamp(value, 0f, 100f);
            default:
                return Mathf.Clamp01(value);
        }
    }

    public override void OnStopExecuting()
    {
        StopAllCoroutines();
        base.OnStopExecuting();
    }

    public override string GetSummary()
    {
        if (targetGameObject.Value == null)
            return "Error: No Target GameObject";

        string recursiveText = applyRecursively ? " (Recursive)" : "";
        string tweenText = duration.Value > 0f ? $" over {duration.Value}s" : "";
        return $"{targetGameObject.Value.name}{recursiveText} : {propertyMode} -> {ClampValue(targetValue.Value)}{tweenText}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(100, 220, 240, 255);
    }
}

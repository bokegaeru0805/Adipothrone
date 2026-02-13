using UnityEngine;
using System.Collections;
using Fungus;
using NaughtyAttributes;

[CommandInfo("Sprite", "Set Sprite Property", "SpriteRendererの色(Tween対応)、反転、描画順、画像を変更します。")]
[AddComponentMenu("")]
public class SpriteRendererPropertyCommand : Command
{
    public enum SpritePropertyMode
    {
        Color,          // 色・透明度 (Tween対応)
        Flip,           // 上下・左右反転
        SortingOrder,   // 描画順 (Sorting Order)
        ChangeSprite    // スプライト画像の差し替え
    }

    public enum OperationMode
    {
        Set, // 指定した値にする
        Add  // 現在の値に加算する（Sorting Order用）
    }

    // --- ターゲット設定 ---
    [BoxGroup("Target Settings")]
    [Tooltip("変更対象のSpriteRendererを持つGameObject")]
    [SerializeField] protected GameObjectData targetGameObject;

    [BoxGroup("Target Settings")]
    [SerializeField] protected SpritePropertyMode propertyMode = SpritePropertyMode.Color;

    // --- Color 設定 ---
    [BoxGroup("Color Settings")]
    [AllowNesting]
    [ShowIf("IsColorMode")]
    [Tooltip("目標の色 (Alphaを変えればフェードになります)")]
    [SerializeField] protected ColorData targetColor = new ColorData(Color.white);

    // --- Flip 設定 ---
    [BoxGroup("Flip Settings")]
    [AllowNesting]
    [ShowIf("IsFlipMode")]
    [Tooltip("X方向(左右)の反転を変更するか")]
    [SerializeField] protected bool modifyFlipX = true;

    [BoxGroup("Flip Settings")]
    [AllowNesting]
    [ShowIf("IsFlipModeAndModifyX")]
    [Tooltip("左右反転の状態")]
    [SerializeField] protected BooleanData flipX;

    [BoxGroup("Flip Settings")]
    [AllowNesting]
    [ShowIf("IsFlipMode")]
    [Tooltip("Y方向(上下)の反転を変更するか")]
    [SerializeField] protected bool modifyFlipY = false;

    [BoxGroup("Flip Settings")]
    [AllowNesting]
    [ShowIf("IsFlipModeAndModifyY")]
    [Tooltip("上下反転の状態")]
    [SerializeField] protected BooleanData flipY;

    // --- Sorting Order 設定 ---
    [BoxGroup("Sorting Order Settings")]
    [AllowNesting]
    [ShowIf("IsSortingOrderMode")]
    [SerializeField] protected OperationMode orderOperation = OperationMode.Set;

    [BoxGroup("Sorting Order Settings")]
    [AllowNesting]
    [ShowIf("IsSortingOrderMode")]
    [Tooltip("描画順序の値")]
    [SerializeField] protected IntegerData sortingOrder;

    // --- Sprite 画像設定 ---
    [BoxGroup("Sprite Settings")]
    [AllowNesting]
    [ShowIf("IsChangeSpriteMode")]
    [Tooltip("差し替えるスプライト画像")]
    [SerializeField] protected Sprite targetSprite;

    // --- アニメーション (Tween) ---
    // ※ Colorモードのときのみ表示
    [BoxGroup("Tween Settings")]
    [AllowNesting]
    [ShowIf("IsColorMode")]
    [Tooltip("変化にかける時間(秒)。0の場合は即座に変更されます。")]
    [SerializeField] protected FloatData duration = new FloatData(0f);

    [BoxGroup("Tween Settings")]
    [AllowNesting]
    [ShowIf("IsTweening")]
    [Tooltip("待機するかどうか")]
    [SerializeField] protected bool waitUntilFinished = true;

    [BoxGroup("Tween Settings")]
    [AllowNesting]
    [ShowIf("IsTweening")]
    [Tooltip("変化のカーブ")]
    [SerializeField] protected AnimationCurve easeCurve = AnimationCurve.Linear(0, 0, 1, 1);

    // --- NaughtyAttributes用のバリデーション ---
    private bool IsColorMode() => propertyMode == SpritePropertyMode.Color;
    private bool IsFlipMode() => propertyMode == SpritePropertyMode.Flip;
    private bool IsFlipModeAndModifyX() => IsFlipMode() && modifyFlipX;
    private bool IsFlipModeAndModifyY() => IsFlipMode() && modifyFlipY;
    private bool IsSortingOrderMode() => propertyMode == SpritePropertyMode.SortingOrder;
    private bool IsChangeSpriteMode() => propertyMode == SpritePropertyMode.ChangeSprite;
    private bool IsTweening() => IsColorMode() && duration.Value > 0f;

    public override void OnEnter()
    {
        if (targetGameObject.Value == null)
        {
            Continue();
            return;
        }

        SpriteRenderer sr = targetGameObject.Value.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            Debug.LogWarning($"Target GameObject '{targetGameObject.Value.name}' does not have a SpriteRenderer component.");
            Continue();
            return;
        }

        switch (propertyMode)
        {
            case SpritePropertyMode.Color:
                HandleColorChange(sr);
                break;

            case SpritePropertyMode.Flip:
                HandleFlipChange(sr);
                Continue();
                break;

            case SpritePropertyMode.SortingOrder:
                HandleSortingOrderChange(sr);
                Continue();
                break;

            case SpritePropertyMode.ChangeSprite:
                HandleSpriteChange(sr);
                Continue();
                break;
        }
    }

    private void HandleColorChange(SpriteRenderer sr)
    {
        Color startColor = sr.color;
        Color endColor = targetColor.Value;

        if (duration.Value <= 0f)
        {
            sr.color = endColor;
            Continue();
        }
        else
        {
            StartCoroutine(TweenColorRoutine(sr, startColor, endColor));
        }
    }

    private IEnumerator TweenColorRoutine(SpriteRenderer sr, Color start, Color end)
    {
        float timer = 0f;
        float time = duration.Value;

        while (timer < time)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / time);
            float curveValue = easeCurve.Evaluate(t);

            if (sr != null)
            {
                sr.color = Color.Lerp(start, end, curveValue);
            }

            yield return null;
        }

        if (sr != null)
        {
            sr.color = end;
        }

        if (waitUntilFinished)
        {
            Continue();
        }
    }

    private void HandleFlipChange(SpriteRenderer sr)
    {
        if (modifyFlipX)
        {
            sr.flipX = flipX.Value;
        }
        if (modifyFlipY)
        {
            sr.flipY = flipY.Value;
        }
    }

    private void HandleSortingOrderChange(SpriteRenderer sr)
    {
        if (orderOperation == OperationMode.Set)
        {
            sr.sortingOrder = sortingOrder.Value;
        }
        else // Add
        {
            sr.sortingOrder += sortingOrder.Value;
        }
    }

    private void HandleSpriteChange(SpriteRenderer sr)
    {
        if (targetSprite != null)
        {
            sr.sprite = targetSprite;
        }
    }

    public override void OnStopExecuting()
    {
        StopAllCoroutines();
        base.OnStopExecuting();
    }

    public override string GetSummary()
    {
        if (targetGameObject.Value == null) return "Error: No Target GameObject";

        string modeInfo = "";
        switch (propertyMode)
        {
            case SpritePropertyMode.Color:
                string tweenStr = duration.Value > 0 ? $" over {duration.Value}s" : "";
                modeInfo = $"Color -> {targetColor.Value}{tweenStr}";
                break;
            case SpritePropertyMode.Flip:
                string xStr = modifyFlipX ? $"FlipX:{flipX.Value} " : "";
                string yStr = modifyFlipY ? $"FlipY:{flipY.Value}" : "";
                modeInfo = $"{xStr}{yStr}";
                break;
            case SpritePropertyMode.SortingOrder:
                string opStr = orderOperation == OperationMode.Add ? "+=" : "=";
                modeInfo = $"Order {opStr} {sortingOrder.Value}";
                break;
            case SpritePropertyMode.ChangeSprite:
                modeInfo = $"Sprite -> {(targetSprite != null ? targetSprite.name : "None")}";
                break;
        }

        return $"{targetGameObject.Value.name} : {modeInfo}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(184, 210, 235, 255); // 薄い青系
    }
}
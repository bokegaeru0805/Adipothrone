using Fungus;
using NaughtyAttributes;
using UnityEngine;

[CommandInfo(
    "Animator",
    "Set Animator Property",
    "Animatorのパラメータ(Trigger, Bool, Float, Int)や再生状態(Play, CrossFade)を変更します。"
)]
[AddComponentMenu("")]
public class AnimatorPropertyCommand : Command
{
    public enum AnimatorOperationMode
    {
        SetTrigger,
        ResetTrigger,
        SetBool,
        SetFloat,
        SetInteger,
        PlayState,
    }

    // --- ターゲット設定 ---
    [BoxGroup("Target Settings")]
    [Tooltip("変更対象のAnimatorを持つGameObject")]
    [SerializeField]
    protected GameObjectData targetGameObject;

    [BoxGroup("Target Settings")]
    [Tooltip("子オブジェクトのAnimatorも含めて変更するか")]
    [SerializeField]
    protected bool applyRecursively = false;

    [BoxGroup("Target Settings")]
    [SerializeField]
    protected AnimatorOperationMode operationMode = AnimatorOperationMode.SetTrigger;

    // --- パラメータ設定 ---
    [BoxGroup("Parameter Settings")]
    [AllowNesting]
    [ShowIf("RequiresParameterName")]
    [Tooltip("Animatorに設定されたパラメータ名")]
    [SerializeField]
    protected StringData parameterName;

    [BoxGroup("Value Settings")]
    [AllowNesting]
    [ShowIf("IsBoolMode")]
    [Tooltip("設定するBool値")]
    [SerializeField]
    protected BooleanData boolValue;

    [BoxGroup("Value Settings")]
    [AllowNesting]
    [ShowIf("IsFloatMode")]
    [Tooltip("設定するFloat値")]
    [SerializeField]
    protected FloatData floatValue;

    [BoxGroup("Value Settings")]
    [AllowNesting]
    [ShowIf("IsIntegerMode")]
    [Tooltip("設定するInteger値")]
    [SerializeField]
    protected IntegerData intValue;

    // --- ステート設定 ---
    [BoxGroup("State Settings")]
    [AllowNesting]
    [ShowIf("IsPlayStateMode")]
    [Tooltip("再生するステート名")]
    [SerializeField]
    protected StringData stateName;

    [BoxGroup("State Settings")]
    [AllowNesting]
    [ShowIf("IsPlayStateMode")]
    [Tooltip("クロスフェードにかける時間(秒)。0の場合は即座に切り替わります。")]
    [SerializeField]
    protected FloatData transitionDuration = new FloatData(0f);

    // --- NaughtyAttributes用のバリデーション ---
    private bool RequiresParameterName() => operationMode != AnimatorOperationMode.PlayState;

    private bool IsBoolMode() => operationMode == AnimatorOperationMode.SetBool;

    private bool IsFloatMode() => operationMode == AnimatorOperationMode.SetFloat;

    private bool IsIntegerMode() => operationMode == AnimatorOperationMode.SetInteger;

    private bool IsPlayStateMode() => operationMode == AnimatorOperationMode.PlayState;

    public override void OnEnter()
    {
        if (targetGameObject.Value == null)
        {
            Continue();
            return;
        }

        Animator[] animators;
        if (applyRecursively)
        {
            // 子オブジェクトも含めて全て取得
            animators = targetGameObject.Value.GetComponentsInChildren<Animator>();
        }
        else
        {
            // 対象オブジェクトのみ（配列にラップする）
            var anim = targetGameObject.Value.GetComponent<Animator>();
            animators = anim != null ? new Animator[] { anim } : new Animator[0];
        }

        if (animators.Length == 0)
        {
            Debug.LogWarning(
                $"AnimatorPropertyCommand: {targetGameObject.Value.name} にAnimatorが見つかりません。"
            );
        }
        else
        {
            foreach (var anim in animators)
            {
                ApplyToAnimator(anim);
            }
        }

        Continue();
    }

    private void ApplyToAnimator(Animator anim)
    {
        switch (operationMode)
        {
            case AnimatorOperationMode.SetTrigger:
                if (!string.IsNullOrEmpty(parameterName.Value))
                    anim.SetTrigger(parameterName.Value);
                break;

            case AnimatorOperationMode.ResetTrigger:
                if (!string.IsNullOrEmpty(parameterName.Value))
                    anim.ResetTrigger(parameterName.Value);
                break;

            case AnimatorOperationMode.SetBool:
                if (!string.IsNullOrEmpty(parameterName.Value))
                    anim.SetBool(parameterName.Value, boolValue.Value);
                break;

            case AnimatorOperationMode.SetFloat:
                if (!string.IsNullOrEmpty(parameterName.Value))
                    anim.SetFloat(parameterName.Value, floatValue.Value);
                break;

            case AnimatorOperationMode.SetInteger:
                if (!string.IsNullOrEmpty(parameterName.Value))
                    anim.SetInteger(parameterName.Value, intValue.Value);
                break;

            case AnimatorOperationMode.PlayState:
                if (!string.IsNullOrEmpty(stateName.Value))
                {
                    if (transitionDuration.Value > 0f)
                    {
                        // クロスフェードによる滑らかな状態遷移
                        anim.CrossFade(stateName.Value, transitionDuration.Value);
                    }
                    else
                    {
                        // 即時再生
                        anim.Play(stateName.Value);
                    }
                }
                break;
        }
    }

    public override string GetSummary()
    {
        if (targetGameObject.Value == null)
            return "Error: No Target GameObject";

        string modeInfo = "";
        switch (operationMode)
        {
            case AnimatorOperationMode.SetTrigger:
                modeInfo = $"Trigger -> {parameterName.Value}";
                break;
            case AnimatorOperationMode.ResetTrigger:
                modeInfo = $"ResetTrigger -> {parameterName.Value}";
                break;
            case AnimatorOperationMode.SetBool:
                modeInfo = $"Bool {parameterName.Value} -> {boolValue.Value}";
                break;
            case AnimatorOperationMode.SetFloat:
                modeInfo = $"Float {parameterName.Value} -> {floatValue.Value}";
                break;
            case AnimatorOperationMode.SetInteger:
                modeInfo = $"Int {parameterName.Value} -> {intValue.Value}";
                break;
            case AnimatorOperationMode.PlayState:
                string fadeStr =
                    transitionDuration.Value > 0 ? $" (Fade {transitionDuration.Value}s)" : "";
                modeInfo = $"Play -> {stateName.Value}{fadeStr}";
                break;
        }

        string recursiveStr = applyRecursively ? " (Recursive)" : "";
        return $"{targetGameObject.Value.name}{recursiveStr} : {modeInfo}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(160, 235, 160, 255);
    }
}

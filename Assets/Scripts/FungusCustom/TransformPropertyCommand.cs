using System.Collections;
using Fungus;
using NaughtyAttributes;
using UnityEngine;

[CommandInfo(
    "Transform",
    "Set Transform Property",
    "GameObjectのPosition, Rotation, Scaleを変更します。Tweenアニメーションにも対応しています。"
)]
[AddComponentMenu("")]
public class TransformPropertyCommand : Command
{
    public enum TransformMode
    {
        Position,
        Rotation,
        Scale,
    }

    public enum SpaceMode
    {
        World,
        Local,
    }

    public enum OperationMode
    {
        Set, // 指定した値にする
        Add // 現在の値に加算する
        ,
    }

    // --- 設定 ---
    [BoxGroup("Target Settings")]
    [Tooltip("変更対象のTransform")]
    [SerializeField]
    protected TransformData targetTransform;

    [BoxGroup("Target Settings")]
    [SerializeField]
    protected TransformMode transformMode = TransformMode.Position;

    // ScaleのときはWorld座標の概念が特殊なのでLocal固定に見せるなどの制御
    [BoxGroup("Target Settings")]
    [HideIf("IsScaleMode")]
    [SerializeField]
    protected SpaceMode spaceMode = SpaceMode.World;

    [BoxGroup("Target Settings")]
    [SerializeField]
    protected OperationMode operationMode = OperationMode.Set;

    // --- 値 ---
    [BoxGroup("Value Settings")]
    [Tooltip("目標値、または加算する値")]
    [SerializeField]
    protected Vector3Data targetValue;

    // --- アニメーション ---
    [BoxGroup("Tween Settings")]
    [Tooltip("変化にかける時間(秒)。0の場合は即座に変更されます。")]
    [SerializeField]
    protected FloatData duration = new FloatData(0f);

    [BoxGroup("Tween Settings")]
    [ShowIf("IsTweening")]
    [Tooltip("待機するかどうか")]
    [SerializeField]
    protected bool waitUntilFinished = false;

    [BoxGroup("Tween Settings")]
    [ShowIf("IsTweening")]
    [Tooltip("変化のカーブ")]
    [SerializeField]
    protected AnimationCurve easeCurve = AnimationCurve.Linear(0, 0, 1, 1);

    // 物理演算の競合防止用変数
    private Rigidbody2D activeRb2d = null;
    private RigidbodyType2D originalBodyType = RigidbodyType2D.Dynamic;

    // --- NaughtyAttributes用のバリデーション ---
    private bool IsScaleMode() => transformMode == TransformMode.Scale;

    private bool IsTweening() => duration.Value > 0f;

    public override void OnEnter()
    {
        if (targetTransform.Value == null)
        {
            Continue();
            return;
        }

        //  Rigidbody2Dの干渉（落下や床抜け）を防ぐ処理
        activeRb2d = targetTransform.Value.GetComponent<Rigidbody2D>();
        if (activeRb2d != null)
        {
            originalBodyType = activeRb2d.bodyType;
            activeRb2d.velocity = Vector2.zero; // 蓄積した落下速度をリセット
            activeRb2d.bodyType = RigidbodyType2D.Kinematic; // 物理演算を一時無効化してTweenに専念させる
        }

        // ターゲットの値（Vector3）を計算
        Vector3 startVal = GetCurrentValue();
        Vector3 endVal = CalculateEndValue(startVal);

        // 即時反映かTweenか
        if (duration.Value <= 0f)
        {
            ApplyValue(endVal);

            // 物理演算の状態を元に戻す
            if (activeRb2d != null)
            {
                activeRb2d.velocity = Vector2.zero; // 念押しでリセット
                activeRb2d.bodyType = originalBodyType;
                activeRb2d = null;
            }

            Continue();
        }
        else
        {
            StartCoroutine(TweenRoutine(startVal, endVal));
            if (!waitUntilFinished)
                Continue();
        }
    }

    private IEnumerator TweenRoutine(Vector3 start, Vector3 end)
    {
        float timer = 0f;
        float time = duration.Value;

        while (timer < time)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / time);
            float curveValue = easeCurve.Evaluate(t);

            // 回転の場合はQuaternionでLerpする（ジンバルロック防止・最短経路補間）
            if (transformMode == TransformMode.Rotation)
            {
                Quaternion startQ = Quaternion.Euler(start);
                Quaternion endQ = Quaternion.Euler(end);
                Quaternion currentQ = Quaternion.Lerp(startQ, endQ, curveValue);
                ApplyRotation(currentQ);
            }
            else
            {
                // 位置・スケールはVector3でLerp
                Vector3 currentPos = Vector3.Lerp(start, end, curveValue);
                ApplyValue(currentPos);
            }

            yield return null;
        }

        // 最終値を確実に適用
        if (transformMode == TransformMode.Rotation)
            ApplyRotation(Quaternion.Euler(end));
        else
            ApplyValue(end);

        // Tween完了時に物理演算の状態を元に戻す
        if (activeRb2d != null)
        {
            activeRb2d.velocity = Vector2.zero;
            activeRb2d.bodyType = originalBodyType;
            activeRb2d = null;
        }


        // Tween完了後、waitUntilFinishedがtrueならContinue()で次のコマンドへ
        // falseの時に二重にContinueが呼ばれ、フローが壊れるバグを修正
        if (waitUntilFinished)
        {
            Continue();
        }
    }

    private void ApplyRotation(Quaternion rotation)
    {
        Transform t = targetTransform.Value;
        if (spaceMode == SpaceMode.World)
            t.rotation = rotation;
        else
            t.localRotation = rotation;
    }

    // 終了時にも呼ばれるため、waitUntilFinishedがfalseの場合、即座に次のコマンドへ
    // ただし、コルーチンが回っている場合はそちらでContinue制御を行う
    public override void OnStopExecuting()
    {
        // 強制停止時の復帰処理
        if (activeRb2d != null)
        {
            activeRb2d.velocity = Vector2.zero;
            activeRb2d.bodyType = originalBodyType;
            activeRb2d = null;
        }

        StopAllCoroutines();
        base.OnStopExecuting();
    }

    // --- ヘルパーメソッド ---

    private Vector3 GetCurrentValue()
    {
        Transform t = targetTransform.Value;
        switch (transformMode)
        {
            case TransformMode.Position:
                return (spaceMode == SpaceMode.World) ? t.position : t.localPosition;
            case TransformMode.Rotation:
                return (spaceMode == SpaceMode.World) ? t.eulerAngles : t.localEulerAngles;
            case TransformMode.Scale:
                return t.localScale; // Scaleは基本Localのみ
        }
        return Vector3.zero;
    }

    private Vector3 CalculateEndValue(Vector3 start)
    {
        Vector3 input = targetValue.Value;

        if (operationMode == OperationMode.Set)
        {
            return input;
        }
        else // Add
        {
            return start + input;
        }
    }

    private void ApplyValue(Vector3 value)
    {
        Transform t = targetTransform.Value;
        switch (transformMode)
        {
            case TransformMode.Position:
                if (spaceMode == SpaceMode.World)
                    t.position = value;
                else
                    t.localPosition = value;
                break;
            case TransformMode.Rotation:
                if (spaceMode == SpaceMode.World)
                    t.eulerAngles = value;
                else
                    t.localEulerAngles = value;
                break;
            case TransformMode.Scale:
                t.localScale = value;
                break;
        }
    }

    public override string GetSummary()
    {
        if (targetTransform.Value == null)
            return "Error: No Target Transform";

        string modeStr = transformMode.ToString();
        string opStr = operationMode == OperationMode.Add ? "+=" : "=";
        string valStr = targetValue.Value.ToString();
        string tweenStr = duration.Value > 0 ? $" ({duration.Value}s)" : "";

        return $"{targetTransform.Value.name} : {modeStr} {opStr} {valStr}{tweenStr}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(150, 210, 200, 255);
    }
}

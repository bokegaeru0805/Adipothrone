using NaughtyAttributes;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// エディタ上でBladeAttackActionDataの動きをシミュレーションするためのコンポーネント
/// </summary>
[ExecuteInEditMode]
public class BladeAttackPreviewer : MonoBehaviour
{
    [Header("プレビュー設定")]
    [Tooltip("再生したい攻撃データ")]
    public BladeAttackActionData actionData;

    [Tooltip("剣のオブジェクト（回転させる対象）")]
    public Transform bladeTransform;

    [Tooltip("1ステップあたりの所要時間（Robot_blade_moveの設定値などを想定）")]
    public float durationPerStep = 0.3f;

    [Tooltip("ロボットが右を向いているか")]
    public bool rightFlag = false;

    [Header("デバッグ操作")]
    [Range(0f, 1f)]
    public float seekPosition = 0f; // シークバー (0% ～ 100%)

    // 内部変数
    private Vector3 initialRobotPos;

    private void OnEnable()
    {
        // 基準位置を保存（プレビューで動いた後に戻すため）
        initialRobotPos = transform.localPosition;
    }

    /// <summary>
    /// 指定された進行度(0.0~1.0)に基づいて、ロボットと剣の姿勢を更新する
    /// </summary>
    public void UpdatePreview(float normalizedTime)
    {
        if (actionData == null || bladeTransform == null || actionData.attackSteps.Count == 0)
            return;

        var steps = actionData.attackSteps;

        // 1. 全体の合計時間を計算
        float totalDuration = 0f;
        foreach (var s in steps)
            totalDuration += s.attackTime;

        if (totalDuration <= 0f)
            return;

        // 2. 現在の「絶対時間（秒）」を算出
        float currentTotalTime = normalizedTime * totalDuration;

        // 3. 現在の時間がいったい「どのステップ」の「何秒目」なのかを探す
        int targetStepIndex = 0;
        float accumulatedTime = 0f; // 累積時間
        float timeInCurrentStep = 0f; // そのステップ内での経過時間

        for (int i = 0; i < steps.Count; i++)
        {
            float stepDuration = steps[i].attackTime;

            // 現在時刻が、このステップの終了時刻より前なら、このステップの中にいる
            if (currentTotalTime <= accumulatedTime + stepDuration)
            {
                targetStepIndex = i;
                timeInCurrentStep = currentTotalTime - accumulatedTime;
                break;
            }

            accumulatedTime += stepDuration;

            // ループの最後（シークバー100%時などの誤差対策）
            if (i == steps.Count - 1)
            {
                targetStepIndex = i;
                timeInCurrentStep = stepDuration; // 最後まで完了した状態にする
            }
        }

        // --- 以下、特定したステップ情報を使って姿勢を計算 ---
        var currentStep = steps[targetStepIndex];

        // そのステップ内での進捗率 (0.0 ~ 1.0)
        float stepProgress = 0f;
        if (currentStep.attackTime > 0)
        {
            stepProgress = Mathf.Clamp01(timeInCurrentStep / currentStep.attackTime);
        }

        // 1. 緩急カーブの適用
        float easedT = actionData.bladeEaseCurve.Evaluate(stepProgress);

        // 2. データの準備 (反転処理含む)
        float startAngle = currentStep.startAngle;
        float endAngle = currentStep.endAngle;
        bool isClockwiseRot = currentStep.isClockwiseRotation;

        Vector2 stepStartPoint = currentStep.startPoint;
        Vector2 stepEndPoint = currentStep.endPoint;
        Vector2 stepCenter = currentStep.center;
        float stepMoveStartAngle = currentStep.moveStartAngle;
        float stepMoveEndAngle = currentStep.moveEndAngle;
        bool isClockwiseMove = currentStep.isClockwiseMovement;

        // 右向きの場合の反転処理
        if (rightFlag)
        {
            // 角度系は180度基準で反転
            startAngle = 180f - startAngle;
            endAngle = 180f - endAngle;
            isClockwiseRot = !isClockwiseRot;

            // X座標反転
            stepStartPoint.x *= -1;
            stepEndPoint.x *= -1;
            stepCenter.x *= -1;

            stepMoveStartAngle = 180f - stepMoveStartAngle;
            stepMoveEndAngle = 180f - stepMoveEndAngle;
            isClockwiseMove = !isClockwiseMove;
        }

        // 3. 剣の回転と位置
        float currentAngle = isClockwiseRot
            ? LerpAngleClockwise(startAngle, endAngle, easedT)
            : LerpAngleCounterClockwise(startAngle, endAngle, easedT);

        // 剣に回転適用
        bladeTransform.rotation = Quaternion.Euler(0f, 0f, currentAngle);

        // 剣のオフセット計算
        float radians = currentAngle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
        float offsetT = Mathf.Sin(Mathf.PI * easedT);
        Vector2 bladeOffset = direction * actionData.bladeSwingOffsetRadius * offsetT;

        bladeTransform.localPosition = bladeOffset;

        // 4. ロボット本体の移動
        if (currentStep.movementType != BladeAttackActionData.MovementType.None)
        {
            Vector2 robotMovementPos = transform.localPosition; // fallback

            switch (currentStep.movementType)
            {
                case BladeAttackActionData.MovementType.Linear:
                    robotMovementPos = Vector2.Lerp(stepStartPoint, stepEndPoint, easedT);
                    break;

                case BladeAttackActionData.MovementType.Circular:
                    float moveAngle = isClockwiseMove
                        ? LerpAngleClockwise(stepMoveStartAngle, stepMoveEndAngle, easedT)
                        : LerpAngleCounterClockwise(stepMoveStartAngle, stepMoveEndAngle, easedT);

                    float moveRadians = moveAngle * Mathf.Deg2Rad;
                    Vector2 localDirection = new Vector2(
                        Mathf.Cos(moveRadians),
                        Mathf.Sin(moveRadians)
                    );
                    robotMovementPos = stepCenter + localDirection * currentStep.radius;
                    break;
            }
            // プレビュー用にローカル座標を上書き
            // 注: 実際のゲームでは物理挙動などが絡む場合がありますが、ここでは軌道確認を優先
            transform.localPosition = robotMovementPos;
        }
        else
        {
            // 移動なしの場合は初期位置に戻す（あるいは前のステップの終了位置などを考慮する必要があるが、簡易的に初期位置）
            transform.localPosition = initialRobotPos;
        }
    }

    // --- プレビュー終了時に位置をリセットする ---
    public void ResetPreview()
    {
        transform.localPosition = initialRobotPos;
        bladeTransform.localPosition = Vector3.zero;
        bladeTransform.localRotation = Quaternion.identity;
    }

    // --- Math Helpers ---
    // 反時計回り（CCW: Counter Clockwise） = 角度が増える方向（Unityの標準）
    // 遠回りだろうと近道だろうと、必ず「プラス方向」に回す
    private float LerpAngleCounterClockwise(float from, float to, float t)
    {
        float delta = (to - from + 360f) % 360f;
        return from + delta * t;
    }

    // 時計回り（CW: Clockwise） = 角度が減る方向
    // 遠回りだろうと近道だろうと、必ず「マイナス方向」に回す
    private float LerpAngleClockwise(float from, float to, float t)
    {
        float delta = (from - to + 360f) % 360f;
        return from - delta * t;
    }

    // ギズモ表示（軌跡の描画）
    private void OnDrawGizmosSelected()
    {
        if (actionData == null)
            return;

        // ロボットの移動軌跡を描画
        Gizmos.color = Color.yellow;
        foreach (var step in actionData.attackSteps)
        {
            // 簡易的に始点と終点を結ぶ線を描画（実際は反転などを考慮する必要があります）
            if (step.movementType == BladeAttackActionData.MovementType.Linear)
            {
                // 親の座標系を考慮していない簡易表示です
                // 厳密にやるならUpdatePreviewのロジックを使って点をプロットするのがベスト
            }
        }
    }
}

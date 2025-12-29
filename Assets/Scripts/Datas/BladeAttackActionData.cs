using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// 剣の攻撃モーションデータを管理するScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "NewBladeAttackData", menuName = "Weapons/Blade Attack Data")]
public class BladeAttackActionData : ScriptableObject
{
    [Header("全体設定")]
    [Tooltip("剣攻撃後の待機時間")]
    public float afterBladeSec = 0.4f;

    [Tooltip("剣の連続攻撃の入力受付時間")]
    public float inputWindowTime = 0.5f;

    [Tooltip("剣の振り子半径")]
    public float bladeSwingOffsetRadius = 1.5f;

    [Tooltip("攻撃アニメーションの緩急カーブ")]
    public AnimationCurve bladeEaseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("攻撃ステップ設定")]
    [Tooltip("各攻撃ごとの動きリスト。このリストの要素数が最大攻撃回数になります。")]
    public List<BladeAttackStep> attackSteps = new List<BladeAttackStep>();

    /// <summary>
    /// 攻撃時の移動タイプを定義します
    /// </summary>
    public enum MovementType
    {
        None, // 移動しない
        Linear, // 直線移動
        Circular // 円周上を移動
        ,
    }

    /// <summary>
    /// 1回の攻撃ステップにおける詳細設定
    /// </summary>
    [System.Serializable]
    public class BladeAttackStep
    {
        [Header("時間設定")]
        [Tooltip("この攻撃モーションにかかる時間（秒）")]
        public float attackTime = 1.0f;

        [Header("回転設定")]
        [Tooltip("開始角度（右向き時基準）")]
        public float startAngle;

        [Tooltip("終了角度（右向き時基準）")]
        public float endAngle;

        [Tooltip("時計回りに回転するか")]
        public bool isClockwiseRotation = false;

        [Header("移動設定")]
        public MovementType movementType = MovementType.None;

        [Header("--- 直線移動用 ---")]
        [ShowIf("movementType", MovementType.Linear)]
        [AllowNesting]
        public Vector2 startPoint;

        [ShowIf("movementType", MovementType.Linear)]
        [AllowNesting]
        public Vector2 endPoint;

        [Header("--- 円周移動用 ---")]
        [ShowIf("movementType", MovementType.Circular)]
        [AllowNesting]
        public Vector2 center;

        [ShowIf("movementType", MovementType.Circular)]
        [AllowNesting]
        public float radius = 1.0f;

        [ShowIf("movementType", MovementType.Circular)]
        [AllowNesting]
        public float moveStartAngle;

        [ShowIf("movementType", MovementType.Circular)]
        [AllowNesting]
        public float moveEndAngle;

        [Tooltip("移動軌道が時計回りか")]
        [ShowIf("movementType", MovementType.Circular)]
        [AllowNesting]
        public bool isClockwiseMovement = false;
    }

    #region Helper Properties (取り回し計算機能)

    /// <summary>
    /// コンボ全段の合計時間（攻撃モーションのみ）を返します。
    /// 数値が小さいほど「速い武器」と言えます。
    /// </summary>
    public float TotalMotionDuration
    {
        get
        {
            float total = 0f;
            foreach (var step in attackSteps)
            {
                total += step.attackTime;
            }
            return total;
        }
    }

    /// <summary>
    /// コンボを完走して硬直が解けるまでの総時間（モーション合計 + 最後の硬直）を返します。
    /// </summary>
    public float FullComboDuration
    {
        get { return TotalMotionDuration + afterBladeSec; }
    }

    /// <summary>
    /// 「取り回しの良さ（軽快さ）」をスコア化して返します。
    /// 例：1秒間に平均何発振れるか (攻撃回数 ÷ 合計時間)
    /// 数値が高いほど「取り回しが良い（連撃が速い）」武器になります。
    /// </summary>
    public float HandlingScore
    {
        get
        {
            float duration = TotalMotionDuration;
            if (duration <= 0.001f) return 0f; // ゼロ除算対策
            return (float)attackSteps.Count / duration;
        }
    }

    /// <summary>
    /// 1撃あたりの平均所要時間を返します。
    /// </summary>
    public float AverageStepTime
    {
        get
        {
            if (attackSteps.Count == 0) return 0f;
            return TotalMotionDuration / attackSteps.Count;
        }
    }

    #endregion
}

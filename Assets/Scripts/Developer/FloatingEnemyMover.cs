#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 楕円軌道をベースにした敵の浮遊移動を制御するクラス
/// </summary>
public class FloatingEnemyMover : MonoBehaviour
{
    // 動きのパターンの定義
    public enum MovementPattern
    {
        Standard, // 基本の楕円
        Figure8, // 8の字
        TripleWave, // 大波
        Bounce, // バウンド
        SharpEdge, // 鋭角ターン
        Flutter, // 揺らぎ
        UShape, // U字
        Teardrop, // 涙型
        EasedHover, // イージング
        Zigzag, // ジグザグ
        Astroid, // 星型（アストロイド）
        Rectangular, // 長方形（ボックス）
        Crescent, // 三日月
        InvertedBounce, // 天井バウンド
        XWobble // 横揺れドリフト
        ,
    }

    [Header("Movement Settings")]
    [Tooltip("動きのパターンを選択")]
    [SerializeField]
    private MovementPattern pattern = MovementPattern.Standard;

    [Tooltip("横幅の半径 (a)")]
    [SerializeField]
    private float widthRadius = 3.0f;

    [Tooltip("縦幅の半径 (b)")]
    [SerializeField]
    private float heightRadius = 1.5f;

    [Tooltip("1周にかかる時間 (秒)")]
    [SerializeField]
    private float cyclePeriod = 4.0f;

    [Tooltip("揺らぎパターン用のノイズ強度")]
    [SerializeField]
    private float noiseMagnitude = 0.5f;

    [Header("Control Settings")]
    [Tooltip("このキーを押すと一時停止/再開します")]
    [SerializeField]
    private KeyCode stopKey = KeyCode.Space;

    [Tooltip("これにチェックが入っている間は動きが止まります")]
    public bool isStopped = false;

    // 内部変数
    private Vector3 initialPosition; // 基準となる開始位置
    private float currentTime; // 経過時間

    void Start()
    {
        // 開始時の座標を基準点として保存
        initialPosition = transform.position;
    }

    void Update()
    {
        // 停止キーが押されたら停止状態を切り替え
        if (Input.GetKeyDown(stopKey))
        {
            isStopped = !isStopped;
        }
    }

    void FixedUpdate()
    {
        // 停止中 (true) なら、時間を進めずにここで処理を終える
        // 座標計算が行われないため、オブジェクトはその場で止まる
        if (isStopped)
            return;

        // 周期が設定されていない場合は動かない
        if (cyclePeriod <= 0f)
            return;

        // 時間を経過させる
        currentTime += Time.deltaTime;

        // シータ（角度）を計算: 0 から 2π の範囲で変化
        // theta = (2π * 経過時間) / 周期
        float theta = (2.0f * Mathf.PI * currentTime) / cyclePeriod;

        // パターンに応じたオフセット（ズレ）を計算
        Vector2 offset = CalculateOffset(theta);

        // 座標を更新 (基準点 + オフセット)
        transform.position = initialPosition + new Vector3(offset.x, offset.y, 0f);
    }

    /// <summary>
    /// 現在の角度(theta)に基づいて位置オフセットを計算する
    /// </summary>
    private Vector2 CalculateOffset(float theta)
    {
        float x = 0f;
        float y = 0f;

        // 基本のX座標計算 (x = a * cosθ)
        // 特殊なパターン以外はこれが適用される
        float basicX = widthRadius * Mathf.Cos(theta);

        switch (pattern)
        {
            case MovementPattern.Standard:
                x = basicX;
                y = heightRadius * Mathf.Sin(theta);
                break;

            case MovementPattern.Figure8:
                x = basicX;
                // y = b * sin(2θ)
                y = heightRadius * Mathf.Sin(2.0f * theta);
                break;

            case MovementPattern.TripleWave:
                x = basicX;
                // y = b * sin(3θ)
                y = heightRadius * Mathf.Sin(3.0f * theta);
                break;

            case MovementPattern.Bounce:
                x = basicX;
                // y = b * |sinθ| (絶対値で跳ねる動き)
                y = heightRadius * Mathf.Abs(Mathf.Sin(theta));
                break;

            case MovementPattern.SharpEdge:
                // x = a * cos^3θ (端で減速する動き)
                x = widthRadius * Mathf.Pow(Mathf.Cos(theta), 3.0f);
                y = heightRadius * Mathf.Sin(theta);
                break;

            case MovementPattern.Flutter:
                x = basicX;
                // y = b * sinθ + noise * sin(10θ)
                y = (heightRadius * Mathf.Sin(theta)) + (noiseMagnitude * Mathf.Sin(10.0f * theta));
                break;

            case MovementPattern.UShape:
                x = basicX;
                // y = b * (cos(2θ) - 1)
                y = heightRadius * (Mathf.Cos(2.0f * theta) - 1.0f);
                break;

            case MovementPattern.Teardrop:
                x = basicX;
                // y = b * sinθ + (b/2) * sin(2θ)
                y =
                    heightRadius * Mathf.Sin(theta)
                    + (heightRadius * 0.5f * Mathf.Sin(2.0f * theta));
                break;

            case MovementPattern.EasedHover:
                x = basicX;
                // y = b * sin^3θ
                y = heightRadius * Mathf.Pow(Mathf.Sin(theta), 3.0f);
                break;

            case MovementPattern.Zigzag:
                x = basicX;
                // y = b * sin(5θ)
                y = heightRadius * Mathf.Sin(5.0f * theta);
                break;

            case MovementPattern.Astroid:
                // x = a * cos^3θ, y = b * sin^3θ
                x = widthRadius * Mathf.Pow(Mathf.Cos(theta), 3.0f);
                y = heightRadius * Mathf.Pow(Mathf.Sin(theta), 3.0f);
                break;

            case MovementPattern.Rectangular:
                // スーパー楕円: x = a * sgn(cos)|cos|^0.3
                // 指数が小さいほど四角形に近づく
                float p = 0.3f;
                float cosT = Mathf.Cos(theta);
                float sinT = Mathf.Sin(theta);
                x = widthRadius * Mathf.Sign(cosT) * Mathf.Pow(Mathf.Abs(cosT), p);
                y = heightRadius * Mathf.Sign(sinT) * Mathf.Pow(Mathf.Abs(sinT), p);
                break;

            case MovementPattern.Crescent:
                x = basicX;
                // y = b * sinθ + (b/2) * sin^2θ
                y =
                    heightRadius * Mathf.Sin(theta)
                    + (heightRadius * 0.5f * Mathf.Pow(Mathf.Sin(theta), 2.0f));
                break;

            case MovementPattern.InvertedBounce:
                x = basicX;
                // y = -b * |sinθ| (天井側でのバウンド)
                y = -heightRadius * Mathf.Abs(Mathf.Sin(theta));
                break;

            case MovementPattern.XWobble:
                // X軸にノイズを加える: x = a * cosθ + (0.2a) * sin(6θ)
                x = basicX + (widthRadius * 0.2f * Mathf.Sin(6.0f * theta));
                y = heightRadius * Mathf.Sin(theta);
                break;
            default:
                x = basicX;
                y = heightRadius * Mathf.Sin(theta);
                break;
        }

        return new Vector2(x, y);
    }

    // エディタ上で軌道を可視化する機能 (Debug用)
    private void OnDrawGizmosSelected()
    {
        // 実行中でなければ現在の位置を基準にする
        Vector3 center = Application.isPlaying ? initialPosition : transform.position;

        Gizmos.color = Color.cyan;
        float step = 0.1f; // 描画の細かさ

        Vector3 prevPos = center + (Vector3)CalculateOffset(0);

        // 0 から 2π まで線を描画して軌跡を表示
        for (float t = step; t <= 2.0f * Mathf.PI + step; t += step)
        {
            Vector3 nextPos = center + (Vector3)CalculateOffset(t);
            Gizmos.DrawLine(prevPos, nextPos);
            prevPos = nextPos;
        }
    }
}
#endif

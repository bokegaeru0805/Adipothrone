using UnityEngine;

/// <summary>
/// プレイヤーが侵入した際に、移動速度や重力、風などの環境効果を与えるエリア。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class EnvironmentArea : MonoBehaviour
{
    [Header("基本環境設定")]
    [Tooltip(
        "エリア内での全体的な移動速度倍率 (1.0 = 通常, 0.5 = 半減)\n泥沼や水中などで使用します。"
    )]
    [Range(0.1f, 2.0f)]
    public float GlobalSpeedMultiplier = 1.0f;

    [Tooltip("エリア内での重力倍率 (1.0 = 通常, 0.5 = 低重力)\n宇宙空間や水中などで使用します。")]
    [Range(0.0f, 5.0f)]
    public float GravityMultiplier = 1.0f;

    [Header("風・外力設定")]
    [Tooltip("風の強さと方向。\n(X=0, Y=0)なら無風。\n(X=-1, Y=0)なら左向きの風（抵抗）。")]
    public Vector2 WindVelocity = Vector2.zero;

    [Tooltip("風に向かって歩く際の抵抗係数。\n1.0に近いほど、向かい風で強く減速します。")]
    [Range(0.0f, 1.0f)]
    public float WindResistanceFactor = 0.5f;

    [Header("落下制限")]
    [Tooltip(
        "このエリア内での最大落下速度（絶対値）。\n0の場合は制限なし。\n例: 2.0 にすると、秒速2.0以上で落下しなくなります（ゆっくり落下）。"
    )]
    public float MaxFallSpeed = 0f;

    [Header("氷の床（滑る環境）設定")]
    [Tooltip(
        "床での滑りにくさ（加速度）。\n0の場合は滑りません（通常の即時移動）。\n値が小さいほど滑りやすく（加速・減速に時間がかかる）なります。"
    )]
    public float SlipAcceleration = 0f;

    /// <summary>
    /// プレイヤーがエリアに入ったときの処理
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // プレイヤーかどうか判定
        if (collision.CompareTag(GameConstants.PLAYER_TAG_NAME))
        {
            var player = collision.GetComponent<Heroin_move>();
            if (player != null)
            {
                // 自身(this)をプレイヤーの環境リストに登録
                player.EnterEnvironmentArea(this);
            }
        }
    }

    /// <summary>
    /// プレイヤーがエリアから出たときの処理
    /// </summary>
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag(GameConstants.PLAYER_TAG_NAME))
        {
            var player = collision.GetComponent<Heroin_move>();
            if (player != null)
            {
                // 自身(this)をプレイヤーの環境リストから解除
                player.ExitEnvironmentArea(this);
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// エディタ上で風向きを可視化するためのギズモ描画
    /// </summary>
    private void OnDrawGizmos()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
            return;

        // エリアの色（半透明のオレンジ）
        Color fillColor = new Color(1f, 0.64f, 0f, 0.2f);
        Gizmos.color = fillColor;
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);

        // 枠線（不透明のオレンジ）
        Color borderColor = new Color(1f, 0.64f, 0f, 1f);
        Gizmos.color = borderColor;
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);

        // 風向きの矢印
        if (WindVelocity.sqrMagnitude > 0.1f)
        {
            Gizmos.color = Color.yellow;
            Vector3 center = col.bounds.center;
            Vector3 direction = (Vector3)WindVelocity.normalized;
            float arrowLength = 1.5f;

            // 矢印の胴体
            Gizmos.DrawLine(center, center + direction * arrowLength);

            // 矢印の先端
            Vector3 right = Quaternion.Euler(0, 0, 160) * direction;
            Vector3 left = Quaternion.Euler(0, 0, -160) * direction;
            Gizmos.DrawLine(
                center + direction * arrowLength,
                center + direction * arrowLength + right * 0.5f
            );
            Gizmos.DrawLine(
                center + direction * arrowLength,
                center + direction * arrowLength + left * 0.5f
            );
        }
    }
#endif
}

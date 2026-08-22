using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// プレイヤーが侵入した際に、移動速度や重力、風などの環境効果を与えるエリア。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(BoxCollider2D))]
public class EnvironmentArea : MonoBehaviour
{
    [Header("連携設定")]
    [SerializeField]
    [Tooltip("このEnvironmentAreaのコライダーを同期させるCameraMoveArea")]
    private CameraMoveArea targetCameraArea;

    [Tooltip("上端のオフセット（正の値で上へ、負の値で下へ移動）")]
    [SerializeField, ShowIf(nameof(HasTargetCameraArea))]
    private float offsetTop = 0f;

    [Tooltip("下端のオフセット（正の値で下へ、負の値で上へ移動）")]
    [SerializeField, ShowIf(nameof(HasTargetCameraArea))]
    private float offsetBottom = 0f;

    [Tooltip("左端のオフセット（正の値で右へ、負の値で左へ移動）")]
    [SerializeField, ShowIf(nameof(HasTargetCameraArea))]
    private float offsetLeft = 0f;

    [Tooltip("右端のオフセット（正の値で左へ、負の値で右へ移動）")]
    [SerializeField, ShowIf(nameof(HasTargetCameraArea))]
    private float offsetRight = 0f;

    private bool HasTargetCameraArea()
    {
        return targetCameraArea != null;
    }

    [Header("基本環境設定")]
    [Tooltip(
        "エリア内での全体的な移動速度倍率 (1.0 = 通常, 0.5 = 半減)\n泥沼や水中などで使用します。"
    )]
    [Range(0.1f, 2.0f)]
    public float GlobalSpeedMultiplier = 1.0f;

    [Tooltip("エリア内での重力倍率 (1.0 = 通常, 0.5 = 低重力)\n宇宙空間や水中などで使用します。")]
    [Range(0.0f, 5.0f)]
    public float GravityMultiplier = 1.0f;

    [Tooltip("エリア内でのジャンプ到達高度倍率 (1.0 = 通常, 1.5 = 1.5倍の高さ)")]
    [Min(0.0f)]
    public float JumpHeightMultiplier = 1.0f;

    [Header("風・外力設定")]
    [Tooltip("風の強さと方向。\n(X=0, Y=0)なら無風。\n(X=-1, Y=0)なら左向きの風（抵抗）。")]
    public Vector2 WindVelocity = Vector2.zero;

    [Tooltip("風に向かって歩く際の抵抗係数。\n1.0に近いほど、向かい風で強く減速します。")]
    [Range(0.0f, 1.0f)]
    public float WindResistanceFactor = 0.0f;

    [Tooltip("エリア退出後、風と向かい風抵抗を徐々に減衰させるかどうか")]
    [SerializeField]
    private bool isWindFadeOutEnabled = false;

    [Tooltip("風と向かい風抵抗が完全になくなるまでの秒数")]
    [SerializeField, ShowIf(nameof(isWindFadeOutEnabled)), Min(0f)]
    private float windFadeOutDuration = 1f;

    /// <summary>
    /// エリア退出後に風を徐々に減衰させるかどうか。
    /// </summary>
    public bool IsWindFadeOutEnabled => isWindFadeOutEnabled;

    /// <summary>
    /// エリア退出後に風が完全になくなるまでの秒数。
    /// </summary>
    public float WindFadeOutDuration => windFadeOutDuration;

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

    private void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            SyncCollider();
        }
#endif
    }

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
    /// 対象のCameraMoveAreaのBoxCollider2Dに自身のコライダーを同期させます。
    /// </summary>
    private void SyncCollider()
    {
        if (targetCameraArea == null)
            return;

        BoxCollider2D environmentBox = GetComponent<BoxCollider2D>();
        BoxCollider2D targetBox = targetCameraArea.GetComponent<BoxCollider2D>();
        if (environmentBox == null || targetBox == null)
            return;

        Vector3 targetWorldCenter = targetCameraArea.transform.TransformPoint(
            (Vector3)targetBox.offset
        );
        Vector2 targetWorldSize = new Vector2(
            targetBox.size.x * Mathf.Abs(targetCameraArea.transform.lossyScale.x),
            targetBox.size.y * Mathf.Abs(targetCameraArea.transform.lossyScale.y)
        );

        float left = targetWorldCenter.x - targetWorldSize.x / 2f + offsetLeft;
        float right = targetWorldCenter.x + targetWorldSize.x / 2f - offsetRight;
        float bottom = targetWorldCenter.y - targetWorldSize.y / 2f + offsetBottom;
        float top = targetWorldCenter.y + targetWorldSize.y / 2f - offsetTop;

        targetWorldCenter.x = (left + right) / 2f;
        targetWorldCenter.y = (bottom + top) / 2f;
        targetWorldSize.x = Mathf.Max(0.0001f, right - left);
        targetWorldSize.y = Mathf.Max(0.0001f, top - bottom);

        Vector3 localOffset = transform.InverseTransformPoint(targetWorldCenter);
        Vector2 newOffset = new Vector2(localOffset.x, localOffset.y);

        float scaleX = Mathf.Abs(transform.lossyScale.x);
        float scaleY = Mathf.Abs(transform.lossyScale.y);
        Vector2 newSize = new Vector2(
            targetWorldSize.x / (scaleX != 0f ? scaleX : 1f),
            targetWorldSize.y / (scaleY != 0f ? scaleY : 1f)
        );

        if (environmentBox.offset == newOffset && environmentBox.size == newSize)
            return;

        UnityEditor.Undo.RecordObject(environmentBox, "Sync Collider with CameraMoveArea");
        environmentBox.offset = newOffset;
        environmentBox.size = newSize;
        UnityEditor.EditorUtility.SetDirty(environmentBox);
    }

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

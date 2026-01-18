using UnityEngine;

/// <summary>
/// 始点と終点の間を往復する2Dプラットフォーム。
/// </summary>
public class MovingPlatform : BaseMovingPlatform
{
    [Header("移動設定")]
    [Tooltip("移動の始点となるローカル座標")]
    [SerializeField]
    private Vector2 startLocalPosition;

    [Tooltip("移動の終点となるローカル座標")]
    [SerializeField]
    private Vector2 endLocalPosition;

    [Tooltip("リフトの移動速度")]
    [SerializeField]
    private float speed = 2.0f;

    [Tooltip("折り返し待機時間")]
    [SerializeField]
    private float waitTimeAtEnds = 1.0f;

    private Vector2 targetWorldPosition;
    private bool movingToEnd = true;
    private float waitTimer = 0.0f;
    private bool isWaiting = false;

    protected override void Awake()
    {
        base.Awake(); // 基底クラスの初期化（RB設定など）を実行

        if (startLocalPosition == Vector2.zero && endLocalPosition == Vector2.zero)
        {
            Debug.LogError($"{this.name}の始点・終点が未設定です。");
        }
    }

    private void Start()
    {
        transform.localPosition = startLocalPosition;
        targetWorldPosition = GetWorldPosition(endLocalPosition);
    }

    private void FixedUpdate()
    {
        if (isWaiting)
        {
            HandleWait();
            return;
        }

        MoveToTarget();
    }

    /// <summary>
    /// 折り返し時の待機処理
    /// </summary>
    private void HandleWait()
    {
        waitTimer += Time.fixedDeltaTime;
        if (waitTimer >= waitTimeAtEnds)
        {
            isWaiting = false;
            waitTimer = 0.0f;
            movingToEnd = !movingToEnd;
            targetWorldPosition = GetWorldPosition(
                movingToEnd ? endLocalPosition : startLocalPosition
            );

            PlayMovingSound(); // 移動開始時に音を鳴らす
        }
        else
        {
            StopMovingSound(); // 待機中は音を止める
        }
    }

    /// <summary>
    /// ターゲットに向かって移動する
    /// </summary>
    private void MoveToTarget()
    {
        float distance = Vector2.Distance(transform.position, targetWorldPosition);
        if (distance <= 0.01f)
        {
            transform.position = targetWorldPosition;
            isWaiting = true;
            StopMovingSound(); // 到着したら音を止める
            return;
        }

        Vector2 direction = (targetWorldPosition - (Vector2)transform.position).normalized;
        Vector2 moveStep = direction * speed * Time.fixedDeltaTime;
        rb.MovePosition((Vector2)transform.position + moveStep);

        // 再生チェック（ループ再生が途切れないように）
        PlayMovingSound();
    }

    /// <summary>
    /// ローカル座標をワールド座標に変換して返す
    /// </summary>
    /// <param name="localPosition"> ローカル座標 </param>
    /// <returns> ワールド座標 </returns>
    private Vector2 GetWorldPosition(Vector2 localPosition)
    {
        return transform.parent != null
            ? (Vector2)transform.parent.TransformPoint(localPosition)
            : localPosition;
    }

    private void OnBecameVisible()
    {
        if (!isWaiting)
            PlayMovingSound();
    }

    private void OnBecameInvisible()
    {
        StopMovingSound();
    }

    #region Gizmos
    private void OnDrawGizmos()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
            return;
        Vector3 size = (Vector3)box.size;
        size.z = Mathf.Max(size.z, 0.1f);

        Vector3 startWorldPos =
            transform.parent != null
                ? transform.parent.TransformPoint(startLocalPosition)
                : (Vector3)startLocalPosition;
        Vector3 endWorldPos =
            transform.parent != null
                ? transform.parent.TransformPoint(endLocalPosition)
                : (Vector3)endLocalPosition;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(startWorldPos, size);
        Gizmos.DrawWireCube(endWorldPos, size);
        Gizmos.DrawLine(startWorldPos, endWorldPos);

        Vector3 center = (startWorldPos + endWorldPos) * 0.5f;
        Vector3 trajSize = new Vector3(
            Mathf.Abs(startWorldPos.x - endWorldPos.x) + size.x,
            Mathf.Abs(startWorldPos.y - endWorldPos.y) + size.y,
            size.z
        );
        Gizmos.color = new Color(0.5f, 0f, 1f, 0.2f);
        Gizmos.DrawCube(center, trajSize);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, size * 1.05f);
        }
    }
    #endregion
}

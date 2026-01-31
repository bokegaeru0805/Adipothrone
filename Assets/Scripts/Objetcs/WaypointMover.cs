using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// 指定されたポイント間を物理的に移動するコンポーネント。
/// 移動ロジックのみを担当し、乗客の運搬（親子付け）は PassengerCarrier に、
/// 音の再生は MovingPlatformAudio に委譲します。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class WaypointMover : MonoBehaviour
{
    // --- データ定義 ---
    [System.Serializable]
    public class WaypointData
    {
        [Tooltip("このポイントのローカル座標")]
        public Vector2 localPosition;

        [Tooltip("このポイントに到達した際の待機時間（秒）")]
        [Min(0f)]
        public float waitTime = 1.0f;
    }

    // --- 設定項目 ---
    [Header("経路設定")]
    [Tooltip("経由する点の設定リスト。Index 0 が始点となります。")]
    [ReorderableList]
    [SerializeField]
    private List<WaypointData> waypoints = new List<WaypointData>();

    [Header("移動設定")]
    [Tooltip("移動速度")]
    [SerializeField]
    private float speed = 2.0f;

    [Tooltip("trueの場合、最後のポイントに到達したら始点に戻り、周回し続けます")]
    [SerializeField]
    private bool isLoop = false;

    [Tooltip("trueの場合、プレイヤーが接触するまで待機し、接触した瞬間に動き出します")]
    [SerializeField]
    private bool activateOnPlayerEnter = false;

    [Header("導線表示設定")]
    [Tooltip("ゲーム中に経路の線を表示するか")]
    [SerializeField]
    private bool showPathLine = false;

    [Tooltip("線を描画するためのLineRendererコンポーネント（任意）")]
    [SerializeField, ShowIf(nameof(showPathLine))]
    private LineRenderer pathLineRenderer;

    // --- 内部参照 ---
    private Rigidbody2D rb;
    private MovingPlatformAudio platformAudio; // 音声コンポーネント（アタッチされていれば使う）

    // --- 内部状態 ---
    private int currentTargetIndex = 0; // 現在目指しているポイントのインデックス
    private int moveDirection = 1; // 1: 順方向, -1: 逆方向
    private bool isWaiting = false; // ポイントでの待機中フラグ
    private float waitTimer = 0.0f; // 待機タイマー
    private bool hasStarted = false; // 起動済みかどうか

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // リフトは重力や外部からの力の影響を受けないように Kinematic にする
        rb.bodyType = RigidbodyType2D.Kinematic;

        // 移動中の衝突検知精度を上げて、すり抜けを防ぐ（推奨設定）
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // 必要に応じてZ回転を固定（Kinematicなら基本回らないが、念のため）
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // 音声コンポーネントを取得（なくてもエラーにはしない）
        platformAudio = GetComponent<MovingPlatformAudio>();

        // データ検証
        if (waypoints == null || waypoints.Count < 2)
        {
            Debug.LogError(
                $"{this.name}: [WaypointMover] ウェイポイントは最低2つ（始点と終点）必要です。",
                this
            );
            this.enabled = false;
            return;
        }
    }

    private void Start()
    {
        // 初期位置をIndex 0に設定
        transform.localPosition = waypoints[0].localPosition;

        // 次の目標をIndex 1に設定
        currentTargetIndex = 1;
        moveDirection = 1;

        // 起動設定の確認
        if (!activateOnPlayerEnter)
        {
            // 自動起動
            StartMoving();
        }
        else
        {
            // プレイヤー接触待ち
            hasStarted = false;
            if (platformAudio != null)
                platformAudio.StopMoveSound();
        }

        // 経路線の初期描画処理
        if (showPathLine && pathLineRenderer != null)
        {
            DrawPathLine();
        }
        else if (pathLineRenderer != null)
        {
            pathLineRenderer.enabled = false;
        }
    }

    private void FixedUpdate()
    {
        // まだ起動していない場合は動かない
        if (!hasStarted)
            return;

        // 待機中処理
        if (isWaiting)
        {
            HandleWait();
            return;
        }

        // 移動処理
        MoveAlongPath();
    }

    // --- 公開メソッド ---

    /// <summary>
    /// 外部から明示的に動きを開始させるメソッド
    /// </summary>
    public void StartMoving()
    {
        if (!hasStarted)
        {
            hasStarted = true;
            if (platformAudio != null)
                platformAudio.PlayMoveSound();
        }
    }

    /// <summary>
    /// 外部から明示的に動きを停止させるメソッド
    /// </summary>
    public void StopMoving()
    {
        hasStarted = false;
        if (platformAudio != null)
            platformAudio.StopMoveSound();
    }

    // --- 内部ロジック ---

    /// <summary>
    /// ポイント到達時の待機時間を管理する
    /// </summary>
    private void HandleWait()
    {
        waitTimer += Time.fixedDeltaTime;

        // 到達したポイント（currentTargetIndex）の待機時間を参照したいが、
        // 処理の都合上 currentTargetIndex は「次の目標」を指しているため注意が必要。
        // ここでは簡易的に「現在目指している（到達した）ポイント」のデータを使う設計としています。
        float requiredWaitTime = waypoints[currentTargetIndex].waitTime;

        if (waitTimer >= requiredWaitTime)
        {
            // 待機終了。次の目的地を決定して移動再開
            isWaiting = false;
            waitTimer = 0.0f;

            DetermineNextWaypoint();

            if (platformAudio != null)
                platformAudio.PlayMoveSound();
        }
        else
        {
            // 待機中は音を止める
            if (platformAudio != null)
                platformAudio.StopMoveSound();
        }
    }

    /// <summary>
    /// 目的地に向かって移動させる（Rigidbody使用）
    /// </summary>
    private void MoveAlongPath()
    {
        // 目標のワールド座標を取得
        Vector2 targetWorldPos = GetWorldPosition(waypoints[currentTargetIndex].localPosition);

        float distance = Vector2.Distance(transform.position, targetWorldPos);
        float step = speed * Time.fixedDeltaTime;

        // 到達判定（1ステップで到達できる距離なら到達とみなす）
        if (distance <= step)
        {
            // 位置を正確に補正して停止
            rb.MovePosition(targetWorldPos);

            // 待機モードへ移行
            isWaiting = true;
            waitTimer = 0.0f;

            if (platformAudio != null)
                platformAudio.StopMoveSound();
        }
        else
        {
            // 目標へ向かって移動
            Vector2 direction = (targetWorldPos - (Vector2)transform.position).normalized;
            rb.MovePosition((Vector2)transform.position + direction * step);

            // 音再生（ループ再生管理はAudioコンポーネント側に任せる）
            if (platformAudio != null)
                platformAudio.PlayMoveSound();
        }
    }

    /// <summary>
    /// 次のウェイポイントのインデックスを計算する（往復ロジック）
    /// </summary>
    private void DetermineNextWaypoint()
    {
        if (isLoop)
        {
            // ループモード：常に次の番号へ進み、末尾を超えたら0に戻る
            // (往復ではなく一方通行で周回する挙動になります)
            int nextIndex = currentTargetIndex + 1;

            if (nextIndex >= waypoints.Count)
            {
                nextIndex = 0;
            }

            currentTargetIndex = nextIndex;
            // ループ時は方向変数をリセットしておく（念のため）
            moveDirection = 1;
        }
        else
        {
            // 既存の往復（Ping-Pong）ロジック
            int nextIndex = currentTargetIndex + moveDirection;

            // リストの末尾を超えた場合 -> 折り返し
            if (nextIndex >= waypoints.Count)
            {
                moveDirection = -1; // 逆方向へ
                nextIndex = waypoints.Count - 2; // 末尾の1つ手前を目指す
            }
            // リストの先頭より前になった場合 -> 折り返し
            else if (nextIndex < 0)
            {
                moveDirection = 1; // 順方向へ
                nextIndex = 1; // 先頭の次を目指す
            }

            currentTargetIndex = nextIndex;
        }
    }

    /// <summary>
    /// ローカル座標をワールド座標に変換するヘルパー
    /// </summary>
    private Vector2 GetWorldPosition(Vector2 localPosition)
    {
        // 親がいる場合は親基準の座標として変換、いなければそのままワールド座標として扱う
        return transform.parent != null
            ? (Vector2)transform.parent.TransformPoint(localPosition)
            : localPosition;
    }

    // --- イベント制御 ---

    /// <summary>
    /// プレイヤー接触による起動トリガー（乗せる処理はPassengerCarrierが行う）
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // まだ動いておらず、かつ接触起動モードの場合のみ
        if (activateOnPlayerEnter && !hasStarted)
        {
            if (other.CompareTag(GameConstants.PLAYER_TAG_NAME))
            {
                StartMoving();
            }
        }
    }

    // 画面外に出た時の音制御
    private void OnBecameVisible()
    {
        if (hasStarted && !isWaiting && platformAudio != null)
        {
            platformAudio.PlayMoveSound();
        }
    }

    private void OnBecameInvisible()
    {
        if (platformAudio != null)
        {
            platformAudio.StopMoveSound();
        }
    }

    // --- 描画・可視化関連 ---

    /// <summary>
    /// LineRendererを使って経路を描画する
    /// </summary>
    private void DrawPathLine()
    {
        pathLineRenderer.enabled = true;
        pathLineRenderer.positionCount = waypoints.Count;

        // ループ設定をLineRendererに反映（始点と終点を自動で結ぶ）
        pathLineRenderer.loop = isLoop;

        bool useWorldSpace = pathLineRenderer.useWorldSpace;

        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 pos;
            if (useWorldSpace)
            {
                pos = GetWorldPosition(waypoints[i].localPosition);
            }
            else
            {
                // LineRendererがローカル設定の場合、リフトの親座標系などに合わせる必要がある
                // 簡易実装として、親がいる場合はそのローカル座標を使用
                pos =
                    transform.parent != null
                        ? (Vector3)waypoints[i].localPosition
                        : (Vector3)waypoints[i].localPosition - transform.position;
            }
            pathLineRenderer.SetPosition(i, pos);
        }
    }

    /// <summary>
    /// エディタ上でのギズモ表示（デバッグ用）
    /// </summary>
    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Count == 0)
            return;

        // リフトのサイズを取得（コライダーがあればそれを使う）
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        Vector3 size = box != null ? (Vector3)box.size : Vector3.one;
        size.z = Mathf.Max(size.z, 0.1f); // Z軸がつぶれないように

        // ウェイポイントの表示色
        Gizmos.color = Color.cyan;

        Vector3? prevPos = null;
        Vector3 firstPos = Vector3.zero;

        for (int i = 0; i < waypoints.Count; i++)
        {
            // ポイントのワールド位置計算
            Vector3 worldPos =
                transform.parent != null
                    ? transform.parent.TransformPoint(waypoints[i].localPosition)
                    : (Vector3)waypoints[i].localPosition;
            if (i == 0)
                firstPos = worldPos;

            // ポイント位置に枠線を表示
            Gizmos.DrawWireCube(worldPos, size);

            // 経路を線で結ぶ
            if (prevPos.HasValue)
            {
                Gizmos.DrawLine(prevPos.Value, worldPos);
            }

            // ラベル表示（待機時間など）
#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                worldPos + Vector3.up * 0.5f,
                $"P{i} ({waypoints[i].waitTime}s)"
            );
#endif
            prevPos = worldPos;
        }

        // ループモードなら終点(prevPos)と始点(firstPos)を結ぶ
        if (isLoop && waypoints.Count > 1 && prevPos.HasValue)
        {
            Gizmos.DrawLine(prevPos.Value, firstPos);
        }

        // 現在位置の表示（黄色）
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, size * 1.05f);
        }
    }
}

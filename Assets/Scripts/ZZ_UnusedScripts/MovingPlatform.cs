// using System.Collections.Generic;
// using NaughtyAttributes;
// using UnityEngine;

// /// <summary>
// /// 複数の経由点を往復移動する2Dプラットフォーム。
// /// プレイヤーが乗った時の起動や、各地点での待機時間を管理します。
// /// </summary>
// public class MovingPlatform : BaseMovingPlatform
// {
//     [System.Serializable]
//     public class WaypointData
//     {
//         [Tooltip("このポイントのローカル座標")]
//         public Vector2 localPosition;

//         [Tooltip("このポイントに到達した際の待機時間（秒）")]
//         [Min(0f)]
//         public float waitTime = 1.0f;
//     }

//     [Header("経路設定")]
//     [Tooltip("経由する点の設定リスト。Index 0 が始点となります。")]
//     [ReorderableList] // NaughtyAttributesがない場合は無視されます
//     [SerializeField]
//     private List<WaypointData> waypoints = new List<WaypointData>();

//     [Header("移動設定")]
//     [Tooltip("リフトの移動速度")]
//     [SerializeField]
//     private float speed = 2.0f;

//     [Tooltip("trueの場合、プレイヤーが乗るまで待機し、乗った瞬間に動き出します")]
//     [SerializeField]
//     private bool activateOnPlayerEnter = false;

//     [Header("導線表示設定")]
//     [Tooltip("ゲーム中に経路の線を表示するか")]
//     [SerializeField]
//     private bool showPathLine = false;

//     [Tooltip("線を描画するためのLineRendererコンポーネント（プレハブ内の子オブジェクト等を指定）")]
//     [SerializeField]
//     private LineRenderer pathLineRenderer;

//     // --- 内部状態 ---
//     private int currentTargetIndex = 0; // 現在目指しているポイントのインデックス
//     private int moveDirection = 1; // 1: 順方向, -1: 逆方向
//     private bool isWaiting = false; // ポイントでの待機中フラグ
//     private float waitTimer = 0.0f; // 待機タイマー
//     private bool hasStarted = false; // 起動済みかどうか

//     protected override void Awake()
//     {
//         base.Awake();

//         // データ検証
//         if (waypoints == null || waypoints.Count < 2)
//         {
//             Debug.LogError($"{this.name}: ウェイポイントは最低2つ（始点と終点）必要です。");
//             this.enabled = false;
//             return;
//         }
//     }

//     private void Start()
//     {
//         // 初期位置をIndex 0に設定
//         transform.localPosition = waypoints[0].localPosition;

//         // 次の目標をIndex 1に設定
//         currentTargetIndex = 1;
//         moveDirection = 1;

//         // プレイヤー接触起動が無効なら、最初から動く
//         if (!activateOnPlayerEnter)
//         {
//             hasStarted = true;
//             PlayMovingSound();
//         }
//         else
//         {
//             hasStarted = false; // プレイヤー待ち
//             StopMovingSound();
//         }

//         // 経路線の初期描画
//         if (showPathLine && pathLineRenderer != null)
//         {
//             DrawPathLine();
//         }
//         else if (pathLineRenderer != null)
//         {
//             pathLineRenderer.enabled = false; // 設定でOFFなら非表示に
//         }
//     }

//     private void FixedUpdate()
//     {
//         // まだ起動していない、または待機中の場合は動かない
//         if (!hasStarted)
//             return;

//         if (isWaiting)
//         {
//             HandleWait();
//             return;
//         }

//         MoveAlongPath();
//     }

//     /// <summary>
//     /// ポイント到達時の待機処理
//     /// </summary>
//     private void HandleWait()
//     {
//         waitTimer += Time.fixedDeltaTime;

//         // 現在到達しているポイント（＝前回目指していたポイント）の待機時間を参照
//         // 到達直後にインデックス更新を行わず、待機後に更新する設計にするため
//         // ここでは「到達したはずのポイント」の情報を取得する必要がありますが、
//         // 簡易的に「待機時間が終わったら次の目的地を決める」ロジックにします。

//         // 到達したポイントのデータを取得（currentTargetIndexは「次の目標」を指しているため、逆算が必要）
//         // ただし、MoveAlongPath内で到達時にインデックス更新を保留する設計に変えます。

//         // ここでは「現在のターゲット（＝到達した場所）」の待機時間を消費中とする
//         float requiredWaitTime = waypoints[currentTargetIndex].waitTime;

//         if (waitTimer >= requiredWaitTime)
//         {
//             // 待機終了。次の目的地を決定する
//             isWaiting = false;
//             waitTimer = 0.0f;

//             DetermineNextWaypoint();
//             PlayMovingSound();
//         }
//         else
//         {
//             StopMovingSound();
//         }
//     }

//     /// <summary>
//     /// 次の目的地へ移動する処理
//     /// </summary>
//     private void MoveAlongPath()
//     {
//         // 目標のワールド座標を取得
//         Vector2 targetWorldPos = GetWorldPosition(waypoints[currentTargetIndex].localPosition);

//         float distance = Vector2.Distance(transform.position, targetWorldPos);
//         float step = speed * Time.fixedDeltaTime;

//         if (distance <= step) // 到達判定
//         {
//             // 位置を補正
//             rb.MovePosition(targetWorldPos);

//             // 待機開始
//             isWaiting = true;
//             waitTimer = 0.0f;
//             StopMovingSound();
//         }
//         else
//         {
//             // 移動
//             Vector2 direction = (targetWorldPos - (Vector2)transform.position).normalized;
//             rb.MovePosition((Vector2)transform.position + direction * step);
//             PlayMovingSound();
//         }
//     }

//     /// <summary>
//     /// 次のウェイポイントのインデックスを決定する（往復ロジック）
//     /// </summary>
//     private void DetermineNextWaypoint()
//     {
//         int nextIndex = currentTargetIndex + moveDirection;

//         // リストの末尾を超えた場合 -> 折り返し
//         if (nextIndex >= waypoints.Count)
//         {
//             moveDirection = -1; // 逆方向へ
//             nextIndex = waypoints.Count - 2; // 末尾の1つ手前を目指す
//         }
//         // リストの先頭より前になった場合 -> 折り返し
//         else if (nextIndex < 0)
//         {
//             moveDirection = 1; // 順方向へ
//             nextIndex = 1; // 先頭の次を目指す
//         }

//         currentTargetIndex = nextIndex;
//     }

//     /// <summary>
//     /// ローカル座標をワールド座標に変換
//     /// </summary>
//     private Vector2 GetWorldPosition(Vector2 localPosition)
//     {
//         return transform.parent != null
//             ? (Vector2)transform.parent.TransformPoint(localPosition)
//             : localPosition;
//     }

//     #region 経路線表示
//     /// <summary>
//     /// LineRendererを使って経路を描画する
//     /// </summary>
//     private void DrawPathLine()
//     {
//         // LineRendererを有効化
//         pathLineRenderer.enabled = true;

//         // ポイントの数を設定
//         pathLineRenderer.positionCount = waypoints.Count;

//         // 線をワールド座標系で描くかローカルで描くか
//         // LineRendererの "Use World Space" が ON の場合、ワールド座標を渡す必要があります
//         // ここでは親の動きに追従できるように、Use World Space = false (ローカル) 推奨ですが、
//         // スクリプトで座標をセットする際は柔軟に対応します。

//         bool useWorldSpace = pathLineRenderer.useWorldSpace;

//         for (int i = 0; i < waypoints.Count; i++)
//         {
//             Vector3 pos;

//             if (useWorldSpace)
//             {
//                 // ワールド座標に変換してセット
//                 pos = GetWorldPosition(waypoints[i].localPosition);
//             }
//             else
//             {
//                 // ローカル座標のままセット（LineRendererが親の子にある場合など）
//                 // ただし、リフト自体が動くとLineRendererも動いてしまうため、
//                 // LineRendererは「リフトの親」または「動かない別のオブジェクト」に置くのがベストです。
//                 // 簡易実装として、ここでは「親の座標系」に合わせます。

//                 pos =
//                     transform.parent != null
//                         ? (Vector3)waypoints[i].localPosition // 親がいるならそのままローカル座標
//                         : (Vector3)waypoints[i].localPosition - transform.position; // 親がいないなら相対計算など工夫が必要
//             }

//             pathLineRenderer.SetPosition(i, pos);
//         }
//     }
//     #endregion

//     #region 接触管理
//     protected override void OnTriggerEnter2D(Collider2D other)
//     {
//         base.OnTriggerEnter2D(other); // 親子付けなどの基本処理

//         // プレイヤー接触起動が有効で、まだ動いていない場合
//         if (activateOnPlayerEnter && !hasStarted)
//         {
//             if (other.CompareTag(GameConstants.PLAYER_TAG_NAME))
//             {
//                 hasStarted = true;
//                 PlayMovingSound();
//             }
//         }
//     }
//     #endregion
//     private void OnBecameVisible()
//     {
//         if (hasStarted && !isWaiting)
//             PlayMovingSound();
//     }

//     private void OnBecameInvisible()
//     {
//         StopMovingSound();
//     }

//     #region Gizmos表示
//     private void OnDrawGizmos()
//     {
//         if (waypoints == null || waypoints.Count == 0)
//             return;

//         // BoxCollider2Dからサイズを取得（表示用）
//         BoxCollider2D box = GetComponent<BoxCollider2D>();
//         Vector3 size = box != null ? (Vector3)box.size : Vector3.one;
//         size.z = Mathf.Max(size.z, 0.1f);

//         Gizmos.color = Color.cyan;

//         Vector3? prevPos = null;

//         for (int i = 0; i < waypoints.Count; i++)
//         {
//             // ワールド座標計算
//             Vector3 worldPos =
//                 transform.parent != null
//                     ? transform.parent.TransformPoint(waypoints[i].localPosition)
//                     : (Vector3)waypoints[i].localPosition;

//             // ポイント位置にリフトの枠を表示
//             Gizmos.DrawWireCube(worldPos, size);

//             // 経路を線で結ぶ
//             if (prevPos.HasValue)
//             {
//                 Gizmos.DrawLine(prevPos.Value, worldPos);
//             }

//             // ラベル表示（インデックスと待機時間）
// #if UNITY_EDITOR
//             UnityEditor.Handles.Label(
//                 worldPos + Vector3.up * 0.5f,
//                 $"P{i} ({waypoints[i].waitTime}s)"
//             );
// #endif
//             prevPos = worldPos;
//         }

//         // 現在位置
//         if (Application.isPlaying)
//         {
//             Gizmos.color = Color.yellow;
//             Gizmos.DrawWireCube(transform.position, size * 1.05f);
//         }
//     }
//     #endregion
// }

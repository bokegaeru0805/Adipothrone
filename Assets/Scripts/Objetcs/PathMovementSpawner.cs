using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ObjectPoolerからオブジェクトを生成し、指定したパス（経由点）に沿って移動させるスパウナー。
/// </summary>
public class PathMovementSpawner : MonoBehaviour
{
    [Header("プール設定")]
    [Tooltip("生成するオブジェクトのタグ")]
    [SerializeField]
    private string poolTag;

    [Tooltip("使用するプールの種類")]
    [SerializeField]
    private PoolType poolType = PoolType.Scene;

    [Header("生成設定")]
    [Tooltip("生成間隔（秒）")]
    [SerializeField]
    private FloatRange spawnInterval;

    [Tooltip("移動速度")]
    [SerializeField]
    private float moveSpeed = 3.0f;

    [Header("経路設定")]
    [Tooltip(
        "経由する点（ワールド座標）。\n始点(Spawnerの現在位置)は自動で含まれるため、ここには「次の目的地」以降を設定してください。"
    )]
    [SerializeField]
    private List<Vector3> waypoints = new List<Vector3>();

    [Header("初期配置設定")]
    [Tooltip(
        "Trueの場合、開始時に生成間隔と速度に基づいて経路上にオブジェクトを配置します（Prewarm機能）"
    )]
    [SerializeField]
    private bool prewarm = false;

    [Header("デバッグ表示")]
    [Tooltip("Gizmosで生成間隔などのテキスト情報を表示するか")]
    [SerializeField]
    private bool showDebugInfo = true;

    private Coroutine spawnCoroutine;

    private void OnEnable()
    {
        if (spawnCoroutine != null)
            StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(SpawnRoutine());
    }

    private void OnDisable()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    private IEnumerator SpawnRoutine()
    {
        // ObjectPoolerの初期化を待つため、1フレーム待機
        yield return null;

        // 初期化後にPrewarmを実行
        if (prewarm)
        {
            PrewarmObjects();
            //Debug.Log($"[PathMovementSpawner] Prewarmed objects along the path.", this);
        }

        // 最初のインターバルを取得
        float interval = Mathf.Max(0.1f, spawnInterval.Value);
        float nextSpawnTime = Time.time + interval;

        // 初回生成までの待機
        yield return new WaitForSeconds(interval);

        while (true)
        {
            SpawnObject();

            // 次のインターバルを取得（ランダム幅がある場合に備えて毎回取得）
            interval = Mathf.Max(0.1f, spawnInterval.Value);
            nextSpawnTime += interval;

            // 現在時刻との差分を待機することで、処理落ち等によるズレを補正する
            float waitTime = nextSpawnTime - Time.time;

            // 処理落ちで時間が過ぎてしまっている場合
            if (waitTime < 0)
            {
                // 遅れを取り戻すために即時実行するか、遅れを許容して時間をリセットするか
                // ここでは極端なバースト生成を防ぐため、あまりに遅れている場合はスケジュールを引き直す
                if (waitTime < -0.5f)
                {
                    nextSpawnTime = Time.time;
                    waitTime = 0;
                }
                else
                {
                    waitTime = 0; // 即時実行
                }
            }

            yield return new WaitForSeconds(waitTime);
        }
    }

    /// <summary>
    /// 開始時に経路を埋めるようにオブジェクトを配置する
    /// </summary>
    private void PrewarmObjects()
    {
        // 経路全体の座標リストを取得
        List<Vector3> fullPath = GetWorldPath();
        if (fullPath.Count < 2)
            return;

        float totalLength = GetPathLength(fullPath);
        float interval = spawnInterval.Value; // 初期配置は代表値を使用
        float spacing = moveSpeed * interval; // オブジェクト間の距離

        // 距離0はこれからSpawnRoutineで生成されるので、spacing分進んだ位置から配置開始
        float currentDist = spacing;

        while (currentDist < totalLength)
        {
            // 指定距離の位置から始まるパスを生成してSpawn
            SpawnAtDistance(fullPath, currentDist);
            float nextInterval = Mathf.Max(0.1f, spawnInterval.Value); // 次のオブジェクトまでの距離を都度計算（ランダム間隔に対応）
            currentDist += moveSpeed * nextInterval;
        }
    }

    /// <summary>
    /// 経路上の指定距離の位置にオブジェクトを生成する
    /// </summary>
    private void SpawnAtDistance(List<Vector3> fullPath, float distance)
    {
        float distAccum = 0f;
        for (int i = 0; i < fullPath.Count - 1; i++)
        {
            float segLen = Vector3.Distance(fullPath[i], fullPath[i + 1]);

            // このセグメント内に配置位置があるか判定
            if (distAccum + segLen >= distance)
            {
                float remain = distance - distAccum;
                Vector3 spawnPos = Vector3.MoveTowards(fullPath[i], fullPath[i + 1], remain);

                // このオブジェクト専用のパスリストを作成
                // [計算した現在地, 次の経由点, その次の経由点...]
                List<Vector3> objectPath = new List<Vector3>();
                objectPath.Add(spawnPos);
                for (int j = i + 1; j < fullPath.Count; j++)
                {
                    objectPath.Add(fullPath[j]);
                }

                // 生成実行
                SpawnInternal(objectPath, spawnPos);
                return;
            }
            distAccum += segLen;
        }
    }

    /// <summary>
    /// 経路全体の長さを計算
    /// </summary>
    private float GetPathLength(List<Vector3> path)
    {
        float length = 0f;
        for (int i = 0; i < path.Count - 1; i++)
        {
            length += Vector3.Distance(path[i], path[i + 1]);
        }
        return length;
    }

    /// <summary>
    /// 現在の設定に基づいて経路全体の座標リストを返す
    /// </summary>
    private List<Vector3> GetWorldPath()
    {
        List<Vector3> worldPath = new List<Vector3>();
        if (waypoints != null && waypoints.Count > 0)
        {
            worldPath.AddRange(waypoints);
        }
        else
        {
            worldPath.Add(transform.position);
        }
        return worldPath;
    }

    /// <summary>
    /// オブジェクトを生成して移動を開始する
    /// </summary>
    private void SpawnObject()
    {
        List<Vector3> worldPath = GetWorldPath();
        Vector3 spawnPos = worldPath.Count > 0 ? worldPath[0] : transform.position;

        SpawnInternal(worldPath, spawnPos);
    }

    /// <summary>
    /// パスと生成位置を指定して生成を行う内部メソッド
    /// </summary>
    private void SpawnInternal(List<Vector3> path, Vector3 spawnPos)
    {
        // 2. プールから取得
        GameObject obj = null;
        if (poolType == PoolType.Scene)
        {
            if (ObjectPooler.SceneInstance != null)
                obj = ObjectPooler.SceneInstance.SpawnFromPool(
                    poolTag,
                    spawnPos,
                    Quaternion.identity
                );
        }
        else
        {
            if (ObjectPooler.PersistentInstance != null)
                obj = ObjectPooler.PersistentInstance.SpawnFromPool(
                    poolTag,
                    spawnPos,
                    Quaternion.identity
                );
        }

        // 3. 移動コンポーネントにパス情報を渡して起動
        if (obj != null)
        {
            var mover = obj.GetComponent<PathMover>();
            if (mover != null)
            {
                mover.Initialize(path, moveSpeed, poolTag, poolType);
            }
            else
            {
                Debug.LogWarning(
                    $"生成されたオブジェクト '{obj.name}' に 'PathMover' がついていません。",
                    this
                );
            }
        }
    }

    // --- Gizmos ---
    private void OnDrawGizmos()
    {
        // 設定に応じた始点を決定
        Vector3 startPos;
        // Waypoints[0]を始点とする。なければSpawner位置
        if (waypoints != null && waypoints.Count > 0)
            startPos = waypoints[0];
        else
            startPos = transform.position;

        // 始点（Start）の表示
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(startPos, 0.3f);

        if (waypoints == null || waypoints.Count == 0)
            return;

        Gizmos.color = Color.yellow;
        Vector3 prevPos = startPos;

        // 経路の描画

        for (int i = 1; i < waypoints.Count; i++)
        {
            Vector3 currentPos = waypoints[i];

            // 線を引く
            Gizmos.DrawLine(prevPos, currentPos);
            // 点を描く
            Gizmos.DrawWireSphere(currentPos, 0.2f);

            // 進行方向の矢印
            Vector3 dir = (currentPos - prevPos).normalized;
            if (dir != Vector3.zero)
            {
                Gizmos.DrawLine(
                    currentPos,
                    currentPos - dir * 0.4f + Vector3.Cross(dir, Vector3.forward) * 0.2f
                );
                Gizmos.DrawLine(
                    currentPos,
                    currentPos - dir * 0.4f - Vector3.Cross(dir, Vector3.forward) * 0.2f
                );
            }

            prevPos = currentPos;
        }

        // ラベル表示（showDebugInfoがtrueの時のみ表示）
#if UNITY_EDITOR
        if (showDebugInfo)
        {
            Handles.Label(
                startPos + Vector3.up * 0.5f,
                $"Start\nInt:{spawnInterval.minValue:F1}-{spawnInterval.maxValue:F1}s"
            );

            for (int i = 0; i < waypoints.Count; i++)
            {
                // spawnAtSpawnerPositionがFalseの場合、waypoints[0]はStartなのでラベル重複を避ける
                if (i == 0)
                    continue;

                Vector3 pos = waypoints[i];
                Handles.Label(pos + Vector3.up * 0.3f, $"P{i + 1}");
            }
        }
#endif
    }
}

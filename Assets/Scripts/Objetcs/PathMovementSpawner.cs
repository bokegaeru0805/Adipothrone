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
        "経由する点（ローカル座標）。\n始点(0,0)は自動で含まれるため、ここには「次の目的地」以降を設定してください。"
    )]
    [SerializeField]
    private List<Vector3> waypoints = new List<Vector3>();

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
        // 最初のウェイト
        yield return new WaitForSeconds(spawnInterval.Value);

        while (true)
        {
            SpawnObject();

            // 次の生成まで待機
            float waitTime = Mathf.Max(0.1f, spawnInterval.Value);
            yield return new WaitForSeconds(waitTime);
        }
    }

    private void SpawnObject()
    {
        // 1. パス座標をワールド座標に変換してリスト化
        // 始点（Spawnerの位置）を最初に加える
        List<Vector3> worldPath = new List<Vector3>();
        worldPath.Add(transform.position);

        foreach (var point in waypoints)
        {
            // ローカル座標をワールド座標に変換
            worldPath.Add(transform.TransformPoint(point));
        }

        // 2. プールから取得
        GameObject obj = null;
        if (poolType == PoolType.Scene)
        {
            if (ObjectPooler.SceneInstance != null)
                obj = ObjectPooler.SceneInstance.SpawnFromPool(
                    poolTag,
                    transform.position,
                    Quaternion.identity
                );
        }
        else
        {
            if (ObjectPooler.PersistentInstance != null)
                obj = ObjectPooler.PersistentInstance.SpawnFromPool(
                    poolTag,
                    transform.position,
                    Quaternion.identity
                );
        }

        // 3. 移動コンポーネントにパス情報を渡して起動
        if (obj != null)
        {
            var mover = obj.GetComponent<PathMover>();
            if (mover != null)
            {
                mover.Initialize(worldPath, moveSpeed, poolTag, poolType);
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
        // 始点（本体）の表示
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.3f);

        if (waypoints == null || waypoints.Count == 0)
            return;

        Gizmos.color = Color.yellow;
        Vector3 prevPos = transform.position;

        // 経路の描画
        for (int i = 0; i < waypoints.Count; i++)
        {
            // ローカル -> ワールド変換（親の回転やスケールも考慮）
            Vector3 currentPos = transform.TransformPoint(waypoints[i]);

            // 線を引く
            Gizmos.DrawLine(prevPos, currentPos);
            // 点を描く
            Gizmos.DrawWireSphere(currentPos, 0.2f);

            // 進行方向の矢印（簡易）
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

        // ラベル表示
#if UNITY_EDITOR
        Handles.Label(
            transform.position + Vector3.up * 0.5f,
            $"Start\nInt:{spawnInterval.minValue:F1}-{spawnInterval.maxValue:F1}s"
        );
        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 pos = transform.TransformPoint(waypoints[i]);
            Handles.Label(pos + Vector3.up * 0.3f, $"P{i + 1}");
        }
#endif
    }
}

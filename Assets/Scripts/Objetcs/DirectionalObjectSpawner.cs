using System.Collections;
using NaughtyAttributes;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 数値の設定（固定値 or ランダム範囲）を管理するクラス
/// </summary>
[System.Serializable]
public class FloatRange
{
    [Tooltip("ランダムな範囲を使用するかどうか")]
    public bool useRandom = false;

    [Tooltip("固定値")]
    [AllowNesting]
    [HideIf(nameof(useRandom))]
    public float fixedValue = 1.0f;

    [Tooltip("最小値")]
    [AllowNesting]
    [ShowIf(nameof(useRandom))]
    public float minValue = 0.0f;

    [Tooltip("最大値")]
    [AllowNesting]
    [ShowIf(nameof(useRandom))]
    public float maxValue = 1.0f;

    /// <summary>
    /// 設定に基づいた値を返します
    /// </summary>
    public float Value => useRandom ? Random.Range(minValue, maxValue) : fixedValue;
}

/// <summary>
/// ObjectPoolerを使用してオブジェクトを生成し、指定した方向・速度で発射するクラス。
/// 継承することで生成ロジックを拡張可能です。
/// </summary>
public class DirectionalObjectSpawner : MonoBehaviour, IEnemyResettable
{
    [Header("プール設定")]
    [Tooltip("ObjectPoolerに登録されているタグ")]
    [SerializeField]
    protected string poolTag;

    [Tooltip("使用するプールの種類")]
    [SerializeField]
    protected PoolType poolType = PoolType.Scene;

    [Tooltip("生成間隔（秒）")]
    [SerializeField]
    protected FloatRange spawnInterval;

    [Tooltip("trueの場合、開始時に待機時間を待たずに即座に生成を行います")]
    [SerializeField]
    protected bool spawnImmediately = false;

    [Header("動きの設定")]
    [Tooltip("発射速度")]
    [SerializeField]
    protected FloatRange speed;

    [Tooltip("発射角度（度数法）。X軸正方向（右）を0度とし、反時計回りを正とします。")]
    [SerializeField]
    protected FloatRange angle;

    [Tooltip("生成されたオブジェクト自体の回転を、進行方向に合わせるか")]
    [SerializeField]
    protected bool rotateObjectToDirection = true;

    // 生成制御用コルーチン
    protected Coroutine spawnCoroutine;

    protected virtual void OnEnable()
    {
        StartSpawning();
    }

    protected virtual void OnDisable()
    {
        StopSpawning();
    }

    /// <summary>
    /// 生成プロセスを開始します。
    /// </summary>
    public void StartSpawning()
    {
        if (spawnCoroutine == null)
        {
            spawnCoroutine = StartCoroutine(SpawnRoutine());
        }
    }

    /// <summary>
    /// 生成プロセスを停止します。
    /// </summary>
    public void StopSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    /// <summary>
    /// 生成ループを行うコルーチン
    /// </summary>
    protected virtual IEnumerator SpawnRoutine()
    {
        // フラグが有効なら、最初の待機時間の前に一度生成を実行する
        if (spawnImmediately)
        {
            Spawn();
        }

        while (true)
        {
            // 次の生成までの待機時間を取得
            float waitTime = Mathf.Max(0.01f, spawnInterval.Value);
            yield return new WaitForSeconds(waitTime);

            Spawn();
        }
    }

    /// <summary>
    /// オブジェクトを1つ生成し、初期化を行うメソッド
    /// </summary>
    public virtual void Spawn()
    {
        if (string.IsNullOrEmpty(poolTag))
            return;

        // 1. 生成パラメータの決定
        float currentSpeed = speed.Value;
        float currentAngle = angle.Value;

        // 速度ベクトルの計算
        Vector2 velocity = CalculateVelocity(currentSpeed, currentAngle);

        // 生成時の回転（オプション：進行方向に向ける）
        Quaternion rotation = Quaternion.identity;
        if (rotateObjectToDirection)
        {
            rotation = Quaternion.Euler(0, 0, currentAngle);
        }

        // 2. ObjectPoolerから取得
        GameObject obj = null;
        if (poolType == PoolType.Scene)
        {
            if (ObjectPooler.SceneInstance != null)
                obj = ObjectPooler.SceneInstance.SpawnFromPool(
                    poolTag,
                    transform.position,
                    rotation
                );
        }
        else
        {
            if (ObjectPooler.PersistentInstance != null)
                obj = ObjectPooler.PersistentInstance.SpawnFromPool(
                    poolTag,
                    transform.position,
                    rotation
                );
        }

        if (obj != null)
        {
            // 3. 物理挙動の適用
            ApplyPhysics(obj, velocity);

            // 4. その他のカスタム設定（継承先で拡張可能）
            OnObjectSpawned(obj);
        }
    }

    /// <summary>
    /// 速度と角度からベクトルを計算します。
    /// </summary>
    protected virtual Vector2 CalculateVelocity(float speed, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        // X軸正方向(0度)基準で計算
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed;
    }

    /// <summary>
    /// 生成されたオブジェクトに物理力を適用します。
    /// </summary>
    protected virtual void ApplyPhysics(GameObject obj, Vector2 velocity)
    {
        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = velocity;
        }
        else
        {
            Debug.LogWarning(
                $"[{name}] 生成されたオブジェクト '{obj.name}' にRigidbody2Dがありません。速度を適用できませんでした。",
                this
            );
        }
    }

    /// <summary>
    /// オブジェクト生成後の追加処理用フック（継承先で利用）
    /// </summary>
    protected virtual void OnObjectSpawned(GameObject spawnedObject)
    {
        // 必要に応じてオーバーライドして処理を追加
    }

    /// <summary>
    /// IEnemyResettableの実装
    /// </summary>
    public void ResetState()
    {
        //何もしない
    }

    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        // 描画色設定（シアン色で見やすく）
        Gizmos.color = Color.cyan;
        Vector3 center = transform.position;

        // 1. 本体の位置を表示（透明でも選択時に分かるように）
        Gizmos.DrawWireSphere(center, 0.3f);

        // 描画用の長さ（速度）を決定
        // ランダム速度の場合は、最大値を使って「最大の届く範囲」として表示
        float drawLength = speed.useRandom ? speed.maxValue : speed.fixedValue;
        // 速度0だと見えないので、最低限の長さを保証
        drawLength = Mathf.Max(drawLength, 1.0f);

        if (angle.useRandom)
        {
            // --- 範囲指定（扇形）の場合 ---

            // 最小角度と最大角度のベクトルを計算
            Vector3 minDir = CalculateVelocityGizmo(angle.minValue, drawLength);
            Vector3 maxDir = CalculateVelocityGizmo(angle.maxValue, drawLength);

            // 扇の両端を描画
            Gizmos.DrawLine(center, center + minDir);
            Gizmos.DrawLine(center, center + maxDir);

            // 扇の弧（円周部分）を描画
            int segments = 20; // 分割数
            Vector3 prevPoint = center + minDir;

            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float currentAng = Mathf.Lerp(angle.minValue, angle.maxValue, t);
                Vector3 currentDir = CalculateVelocityGizmo(currentAng, drawLength);
                Vector3 nextPoint = center + currentDir;

                Gizmos.DrawLine(prevPoint, nextPoint);
                prevPoint = nextPoint;
            }

            // 範囲内であることを示すために少し透明な色で塗りつぶし風のラインを引く（オプション）
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            Gizmos.DrawLine(
                center,
                center + CalculateVelocityGizmo((angle.minValue + angle.maxValue) / 2f, drawLength)
            );
        }
        else
        {
            // --- 固定角度（矢印）の場合 ---

            Vector3 dir = CalculateVelocityGizmo(angle.fixedValue, drawLength);
            Vector3 endPos = center + dir;

            // 線を描画
            Gizmos.DrawLine(center, endPos);

            // 矢印の先端を描画
            float arrowSize = 0.3f;
            Vector3 arrowRight = CalculateVelocityGizmo(angle.fixedValue + 150, arrowSize);
            Vector3 arrowLeft = CalculateVelocityGizmo(angle.fixedValue - 150, arrowSize);

            Gizmos.DrawLine(endPos, endPos + arrowRight);
            Gizmos.DrawLine(endPos, endPos + arrowLeft);
        }

        // ラベル表示 (UnityEditor名前空間が必要)
#if UNITY_EDITOR
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 10;

        string label =
            $"Spd:{(speed.useRandom ? $"{speed.minValue:F1}~{speed.maxValue:F1}" : $"{speed.fixedValue:F1}")}\n"
            + $"Int:{(spawnInterval.useRandom ? $"{spawnInterval.minValue:F2}~{spawnInterval.maxValue:F2}" : $"{spawnInterval.fixedValue:F2}")}s";

        Handles.Label(center + Vector3.up * 0.5f, label, style);
#endif
    }

    /// <summary>
    /// Gizmo描画用のベクトル計算ヘルパー
    /// </summary>
    private Vector3 CalculateVelocityGizmo(float angleDeg, float length)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * length;
    }
    #endregion
}

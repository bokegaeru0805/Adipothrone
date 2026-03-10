using System.Collections;
using NaughtyAttributes;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#region Helper Classes

/// <summary>
/// 数値の設定（固定値 or ランダム範囲）をインスペクター上で柔軟に管理するクラス
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
    /// インスペクターの設定に基づいた値（固定値、または指定範囲内のランダム値）を返します。
    /// </summary>
    public float Value => useRandom ? Random.Range(minValue, maxValue) : fixedValue;
}

#endregion

/// <summary>
/// ObjectPoolerを使用してオブジェクトを生成し、指定した方向・速度で発射するクラス。
/// 継承することで生成時や物理挙動のロジックを拡張可能です。
/// </summary>
public class DirectionalObjectSpawner : MonoBehaviour, IEnemyResettable
{
    #region Inspector Settings

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

    [Header("トリガー制御設定")]
    [Tooltip(
        "trueの場合、自身にアタッチされたCollider2D(IsTrigger)内にプレイヤーがいる時のみ生成します。"
    )]
    [SerializeField]
    protected bool useTriggerControl = false;

    #endregion

    #region Internal State
    protected bool isPlayerInTrigger = false; // プレイヤーがトリガー範囲内にいるかどうかのフラグ

    protected Coroutine spawnCoroutine; // 現在実行中の生成コルーチンを保持する変数
    #endregion

    #region Unity Lifecycle Methods

    protected virtual void Awake()
    {
        // 視認性向上：トリガー制御を有効にしたのにColliderの設定が漏れている場合の警告
        if (useTriggerControl)
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col == null || !col.isTrigger)
            {
                Debug.LogWarning(
                    $"[{name}] トリガー制御が有効ですが、Collider2D(IsTrigger)がアタッチされていないか、IsTriggerがチェックされていません。生成が開始されません。",
                    this
                );
            }
        }
    }

    protected virtual void OnEnable()
    {
        // トリガー制御を使わない場合のみ、オブジェクトが有効になった時点で自動的に生成を開始する
        if (!useTriggerControl)
        {
            StartSpawning();
        }
    }

    protected virtual void OnDisable()
    {
        // オブジェクトが無効になったら生成を停止し、状態をリセットする
        StopSpawning();
        isPlayerInTrigger = false;
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (!useTriggerControl)
            return;

        // 指定したタグ（プレイヤー）が範囲に入ったら生成を開始
        if (other.CompareTag(GameConstants.PLAYER_TAG_NAME))
        {
            isPlayerInTrigger = true;
            StartSpawning();
        }
    }

    protected virtual void OnTriggerExit2D(Collider2D other)
    {
        if (!useTriggerControl)
            return;

        // プレイヤーが範囲から出たら生成を停止
        if (other.CompareTag(GameConstants.PLAYER_TAG_NAME))
        {
            isPlayerInTrigger = false;
            StopSpawning();
        }
    }

    #endregion

    #region Spawning Logic

    /// <summary>
    /// オブジェクトの定期生成プロセスを開始します。
    /// </summary>
    public void StartSpawning()
    {
        // 既に実行中の場合は重複を防ぐために一度停止
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }
        spawnCoroutine = StartCoroutine(SpawnRoutine());
    }

    /// <summary>
    /// オブジェクトの定期生成プロセスを停止します。
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
    /// 設定された間隔で生成を繰り返すコルーチン
    /// </summary>
    protected virtual IEnumerator SpawnRoutine()
    {
        // フラグが有効なら、最初の待機時間を待たずに即座に1回生成を実行する
        if (spawnImmediately)
        {
            Spawn();
        }

        while (true)
        {
            // 次の生成までの待機時間を取得（安全のため最低0.01秒は待機させる）
            float waitTime = Mathf.Max(0.01f, spawnInterval.Value);
            yield return new WaitForSeconds(waitTime);

            Spawn();
        }
    }

    /// <summary>
    /// オブジェクトを1つプールから取得・生成し、初期化を行うメイン処理
    /// </summary>
    public virtual void Spawn()
    {
        if (string.IsNullOrEmpty(poolTag))
            return;

        // 1. 生成パラメータ（速度・角度）の決定
        float currentSpeed = speed.Value;
        float currentAngle = angle.Value;

        // 速度ベクトルの計算
        Vector2 velocity = CalculateVelocity(currentSpeed, currentAngle);

        // 生成時のオブジェクトの向きの決定
        Quaternion rotation = Quaternion.identity;
        if (rotateObjectToDirection)
        {
            rotation = Quaternion.Euler(0, 0, currentAngle);
        }

        // 2. ObjectPoolerからオブジェクトを取得
        GameObject obj = null;
        if (poolType == PoolType.Scene && ObjectPooler.SceneInstance != null)
        {
            obj = ObjectPooler.SceneInstance.SpawnFromPool(poolTag, transform.position, rotation);
        }
        else if (poolType == PoolType.Persistent && ObjectPooler.PersistentInstance != null)
        {
            obj = ObjectPooler.PersistentInstance.SpawnFromPool(
                poolTag,
                transform.position,
                rotation
            );
        }

        // 3. 取得したオブジェクトに対する物理挙動の適用とカスタム処理
        if (obj != null)
        {
            ApplyPhysics(obj, velocity);
            OnObjectSpawned(obj);
        }
    }

    #endregion

    #region Physics & Math Helpers

    /// <summary>
    /// 速度と角度から、実際の進行方向ベクトル（Velocity）を計算します。
    /// </summary>
    protected virtual Vector2 CalculateVelocity(float speed, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        // X軸正方向(右: 0度)を基準にして三角関数でベクトルを算出
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed;
    }

    /// <summary>
    /// 生成されたオブジェクトのRigidbody2Dに計算した物理力（速度）を適用します。
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
    /// オブジェクト生成後の追加処理用フック。
    /// 継承先のクラスでオーバーライドすることで、生成直後のエフェクト追加などの独自処理を記述できます。
    /// </summary>
    protected virtual void OnObjectSpawned(GameObject spawnedObject)
    {
        // Baseクラスでは何もしない
    }

    #endregion

    #region Interface Implementations

    /// <summary>
    /// IEnemyResettableの実装。
    /// リトライ時やエリア再読み込み時の状態リセット処理を記述します。
    /// </summary>
    public void ResetState()
    {
        // 現状はリセットが必要な状態変数がないため何もしない
    }

    #endregion

    #region Editor & Gizmos

    private void OnDrawGizmosSelected()
    {
        // 描画色設定（トリガー制御中かつプレイヤーがいない時はグレーにして「休止中」を表現）
        Color gizmoColor = Color.cyan;
        if (Application.isPlaying && useTriggerControl && !isPlayerInTrigger)
        {
            gizmoColor = Color.gray;
        }
        Gizmos.color = gizmoColor;
        Vector3 center = transform.position;

        // 1. 本体の位置を表示（スプライトが透明でも選択時に座標が分かるように）
        Gizmos.DrawWireSphere(center, 0.3f);

        // 2. 描画用の長さ（速度）を決定
        // ランダム速度の場合は、最大値を使って「最大到達範囲」として表示
        float drawLength = speed.useRandom ? speed.maxValue : speed.fixedValue;
        drawLength = Mathf.Max(drawLength, 1.0f); // 速度0だと見えないため最低限の長さを保証

        // 3. 角度の範囲または矢印の描画
        if (angle.useRandom)
        {
            // --- 範囲指定（扇形）の場合 ---
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

            // 範囲内であることを示すために少し透明な色で中心線を引く
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

        // 4. 数値ラベルの表示 (Unityエディタ上のみ)
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
    /// Gizmo描画用のベクトル計算ヘルパー。
    /// （Z軸を0に固定したVector3を返します）
    /// </summary>
    private Vector3 CalculateVelocityGizmo(float angleDeg, float length)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * length;
    }

    #endregion
}

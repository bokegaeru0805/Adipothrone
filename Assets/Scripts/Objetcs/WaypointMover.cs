using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 指定されたポイント間を物理的に移動するコンポーネント。
/// 移動ロジックのみを担当し、乗客の運搬（親子付け）は PassengerCarrier に、
/// 音の再生は MovingPlatformAudio に委譲します。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class WaypointMover : MonoBehaviour, IEnemyResettable
{
    #region 定義・列挙型

    /// <summary>
    /// ウェイポイントの座標を解釈するモード
    /// </summary>
    public enum CoordinateMode
    {
        /// <summary>指定した絶対座標（または親からの相対座標）を基準にする</summary>
        Absolute,

        /// <summary>ゲーム開始時の自身の位置を(0, 0)とした相対座標を基準にする</summary>
        Relative
    }

    /// <summary>
    /// 各ウェイポイントの個別データ
    /// </summary>
    [System.Serializable]
    public class WaypointData
    {
        [Tooltip("このポイントのローカル座標")]
        public Vector2 localPosition;

        [Tooltip("このポイントに到達した際の待機時間（秒）")]
        [Min(0f)]
        public float waitTime = 1.0f;
    }

    #endregion

   #region フィールド (インスペクター設定)

    [Header("座標設定")]
    [Tooltip("ウェイポイントの座標をどのように解釈するか")]
    [SerializeField]
    private CoordinateMode _coordinateMode = CoordinateMode.Absolute;

    [Header("経路設定")]
    [Tooltip("経由する点の設定リスト。Index 0 が始点となります。")]
    [ReorderableList]
    [FormerlySerializedAs("waypoints")] // 旧変数名からのデータ引き継ぎ
    [SerializeField]
    private List<WaypointData> _waypoints = new List<WaypointData>();

    [Header("移動設定")]
    [Tooltip("移動速度")]
    [FormerlySerializedAs("speed")]
    [SerializeField]
    private float _speed = 2.0f;

    [Tooltip("trueの場合、最後のポイントに到達したら始点に戻り、周回し続けます")]
    [FormerlySerializedAs("isLoop")]
    [SerializeField]
    private bool _isLoop = false;

    [Tooltip("trueの場合、プレイヤーが接触するまで待機し、接触した瞬間に動き出します")]
    [FormerlySerializedAs("activateOnPlayerEnter")]
    [SerializeField]
    private bool _activateOnPlayerEnter = false;

    [Header("開始時間設定")]
    [Tooltip("trueの場合、指定した秒数だけ経過した状態（途中位置）から開始します。\nランダム開始設定より優先されます。")]
    [FormerlySerializedAs("startWithTimeOffset")]
    [SerializeField]
    private bool _startWithTimeOffset = false;

    [Tooltip("開始時の経過時間オフセット（秒）")]
    [FormerlySerializedAs("startTimeOffset")]
    [SerializeField, ShowIf(nameof(_startWithTimeOffset))]
    [Min(0f)]
    private float _startTimeOffset = 0f;

    [Tooltip("trueの場合、ゲーム開始時にランダムなウェイポイントから開始します")]
    [FormerlySerializedAs("randomizeStartIndex")]
    [SerializeField]
    private bool _randomizeStartIndex = false;

    [Header("導線表示設定")]
    [Tooltip("ゲーム中に経路の線を表示するか")]
    [FormerlySerializedAs("showPathLine")]
    [SerializeField]
    private bool _showPathLine = false;

    [Tooltip("線を描画するためのLineRendererコンポーネント（任意）")]
    [FormerlySerializedAs("pathLineRenderer")]
    [SerializeField, ShowIf(nameof(_showPathLine))]
    private LineRenderer _pathLineRenderer;

    #endregion
    #region フィールド (内部変数)

    // 内部参照
    private Rigidbody2D _rb;
    private MovingPlatformAudio _platformAudio; // 音声コンポーネント（アタッチされていれば使う）

    // 内部状態
    private int _currentTargetIndex = 0; // 現在目指しているポイントのインデックス
    private int _moveDirection = 1; // 1: 順方向, -1: 逆方向
    private bool _isWaiting = false; // ポイントでの待機中フラグ
    private float _waitTimer = 0.0f; // 待機タイマー
    private bool _hasStarted = false; // 起動済みかどうか
    private Vector2 _startPosition; // Relativeモード用の初期位置キャッシュ

    #endregion

    #region Unity ライフサイクルイベント

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

        // Relativeモードの基準点は、再有効化されても変わらないよう初回のみ保存する
        _startPosition = transform.localPosition;

        // リフトは重力や外部からの力の影響を受けないように Kinematic にする
        _rb.bodyType = RigidbodyType2D.Kinematic;

        // 移動中の衝突検知精度を上げて、すり抜けを防ぐ（推奨設定）
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // 必要に応じてZ回転を固定（Kinematicなら基本回らないが、念のため）
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // 音声コンポーネントを取得（なくてもエラーにはしない）
        _platformAudio = GetComponent<MovingPlatformAudio>();

        // データ検証
        if (_waypoints == null || _waypoints.Count < 2)
        {
            Debug.LogError($"{name}: [WaypointMover] ウェイポイントは最低2つ（始点と終点）必要です。", this);
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        if (_startWithTimeOffset)
        {
            // 時間指定スタートの場合：0秒地点からシミュレーションして位置を決定
            ApplyStartOffset();
        }
        else
        {
            int startIdx = 0;

            // ランダム開始設定が有効ならランダムなインデックスを選択
            if (_randomizeStartIndex && _waypoints.Count > 0)
            {
                startIdx = Random.Range(0, _waypoints.Count);
            }

            // 初期位置を選択したウェイポイントに設定
            transform.localPosition = GetTargetLocalPosition(_waypoints[startIdx].localPosition);

            // 次の目標（currentTargetIndex）と移動方向（moveDirection）を決定
            if (_isLoop)
            {
                // ループモードなら単純に次のインデックスへ（末尾なら0に戻る）
                _currentTargetIndex = (startIdx + 1) % _waypoints.Count;
                _moveDirection = 1;
            }
            else
            {
                // 往復モードの場合
                if (startIdx >= _waypoints.Count - 1)
                {
                    // 末尾スタートなら逆方向（戻る）へ
                    _moveDirection = -1;
                    _currentTargetIndex = startIdx - 1;
                }
                else
                {
                    // それ以外（先頭含む）なら順方向（進む）へ
                    _moveDirection = 1;
                    _currentTargetIndex = startIdx + 1;
                }
            }
        }

        // 起動設定の確認
        if (!_activateOnPlayerEnter)
        {
            // 自動起動
            StartMoving();
        }
        else
        {
            // プレイヤー接触待ち
            _hasStarted = false;
            if (_platformAudio != null)
                _platformAudio.StopMoveSound();
        }

        // 経路線の初期描画処理
        if (_showPathLine && _pathLineRenderer != null)
        {
            DrawPathLine();
        }
        else if (_pathLineRenderer != null)
        {
            _pathLineRenderer.enabled = false;
        }
    }

    private void FixedUpdate()
    {
        if (TimeManager.instance != null && TimeManager.instance.isEnemyMovePaused)
        {
            // ポーズ中は音を停止して処理を中断（移動しない）
            if (_platformAudio != null)
                _platformAudio.StopMoveSound();
            return;
        }
        else
        {
            // ポーズ解除中、起動済みで待機中でなければ音を再生（移動音の復帰）
            if (_hasStarted && !_isWaiting && _platformAudio != null)
                _platformAudio.PlayMoveSound();
        }

        // まだ起動していない場合は動かない
        if (!_hasStarted)
            return;

        // 待機中処理
        if (_isWaiting)
        {
            HandleWait();
            return;
        }

        // 移動処理
        MoveAlongPath();
    }

    #endregion

    #region 公開メソッド

    /// <summary>
    /// 外部から明示的に動きを開始させるメソッド
    /// </summary>
    public void StartMoving()
    {
        if (!_hasStarted)
        {
            _hasStarted = true;
            if (_platformAudio != null)
                _platformAudio.PlayMoveSound();
        }
    }

    /// <summary>
    /// 外部から明示的に動きを停止させるメソッド
    /// </summary>
    public void StopMoving()
    {
        _hasStarted = false;
        if (_platformAudio != null)
            _platformAudio.StopMoveSound();
    }

    /// <summary>
    /// 移動状態を初期化し、ウェイポイント0へ戻します。
    /// </summary>
    public void ResetState()
    {
        if (_waypoints == null || _waypoints.Count < 2)
            return;

        _isWaiting = false;
        _waitTimer = 0f;
        _moveDirection = 1;
        _currentTargetIndex = 1;

        Vector2 initialWorldPosition = GetWorldPosition(_waypoints[0].localPosition);
        _rb.position = initialWorldPosition;
        // Debug.Log($"{name}: WaypointMoverの状態をリセットしました。初期位置: {initialWorldPosition}", this);
    }

    #endregion

    #region 内部ロジック

    /// <summary>
    /// ポイント到達時の待機時間を管理する
    /// </summary>
    private void HandleWait()
    {
        _waitTimer += Time.fixedDeltaTime;

        // 到達したポイント（currentTargetIndex）の待機時間を参照したいが、
        // 処理の都合上 currentTargetIndex は「次の目標」を指しているため注意が必要。
        // ここでは簡易的に「現在目指している（到達した）ポイント」のデータを使う設計としています。
        float requiredWaitTime = _waypoints[_currentTargetIndex].waitTime;

        if (_waitTimer >= requiredWaitTime)
        {
            // 待機終了。次の目的地を決定して移動再開
            _isWaiting = false;
            _waitTimer = 0.0f;

            DetermineNextWaypoint();

            if (_platformAudio != null)
                _platformAudio.PlayMoveSound();
        }
        else
        {
            // 待機中は音を止める
            if (_platformAudio != null)
                _platformAudio.StopMoveSound();
        }
    }

    /// <summary>
    /// 目的地に向かって移動させる（Rigidbody使用）
    /// </summary>
    private void MoveAlongPath()
    {
        // 目標のワールド座標を取得
        Vector2 targetWorldPos = GetWorldPosition(_waypoints[_currentTargetIndex].localPosition);

        float distance = Vector2.Distance(transform.position, targetWorldPos);
        float step = _speed * Time.fixedDeltaTime;

        // 到達判定（1ステップで到達できる距離なら到達とみなす）
        if (distance <= step)
        {
            // 位置を正確に補正して停止
            _rb.MovePosition(targetWorldPos);

            // 待機モードへ移行
            _isWaiting = true;
            _waitTimer = 0.0f;

            if (_platformAudio != null)
                _platformAudio.StopMoveSound();
        }
        else
        {
            // 目標へ向かって移動
            Vector2 direction = (targetWorldPos - (Vector2)transform.position).normalized;
            _rb.MovePosition((Vector2)transform.position + direction * step);

            // 音再生（ループ再生管理はAudioコンポーネント側に任せる）
            if (_platformAudio != null)
                _platformAudio.PlayMoveSound();
        }
    }

    /// <summary>
    /// 次のウェイポイントのインデックスを計算する（往復ロジック）
    /// </summary>
    private void DetermineNextWaypoint()
    {
        if (_isLoop)
        {
            // ループモード：常に次の番号へ進み、末尾を超えたら0に戻る
            // (往復ではなく一方通行で周回する挙動になります)
            int nextIndex = _currentTargetIndex + 1;

            if (nextIndex >= _waypoints.Count)
            {
                nextIndex = 0;
            }

            _currentTargetIndex = nextIndex;
            // ループ時は方向変数をリセットしておく（念のため）
            _moveDirection = 1;
        }
        else
        {
            // 既存の往復（Ping-Pong）ロジック
            int nextIndex = _currentTargetIndex + _moveDirection;

            // リストの末尾を超えた場合 -> 折り返し
            if (nextIndex >= _waypoints.Count)
            {
                _moveDirection = -1; // 逆方向へ
                nextIndex = _waypoints.Count - 2; // 末尾の1つ手前を目指す
            }
            // リストの先頭より前になった場合 -> 折り返し
            else if (nextIndex < 0)
            {
                _moveDirection = 1; // 順方向へ
                nextIndex = 1; // 先頭の次を目指す
            }

            _currentTargetIndex = nextIndex;
        }
    }

    /// <summary>
    /// 座標モード設定を加味した、目標のローカル座標を計算して取得する
    /// </summary>
    private Vector2 GetTargetLocalPosition(Vector2 waypointLocalPos)
    {
        // Relativeモードの場合はゲーム起動時の初期位置をオフセットの基準点とする
        if (_coordinateMode == CoordinateMode.Relative)
        {
            Vector2 basePos = Application.isPlaying ? _startPosition : (Vector2)transform.localPosition;
            return basePos + waypointLocalPos;
        }
        return waypointLocalPos;
    }

    /// <summary>
    /// ローカル座標をワールド座標に変換するヘルパー
    /// </summary>
    private Vector2 GetWorldPosition(Vector2 localPosition)
    {
        Vector2 finalLocalPos = GetTargetLocalPosition(localPosition);

        // 親がいる場合は親基準の座標として変換、いなければそのままワールド座標として扱う
        return transform.parent != null
            ? (Vector2)transform.parent.TransformPoint(finalLocalPos)
            : finalLocalPos;
    }

    /// <summary>
    /// 指定された startTimeOffset 分だけ時間を進めた状態を計算し、初期位置を設定する
    /// </summary>
    private void ApplyStartOffset()
    {
        if (_waypoints.Count < 2 || _speed <= 0f)
            return;

        // シミュレーション用の初期状態（Index 0 から開始）
        int tempCurrentIdx = 0; // 今「出発した」または「待機している」ポイント
        int tempNextIdx = 1; // 次に向かうポイント
        int tempDirection = 1; // 進行方向
        float remainingTime = _startTimeOffset;

        // 座標系ヘルパー（モードに対応したローカル座標を使用）
        Vector2 currentPos = GetTargetLocalPosition(_waypoints[0].localPosition);

        // 無限ループ防止用の安全装置
        int safetyCount = 1000;

        while (remainingTime > 0 && safetyCount > 0)
        {
            safetyCount--;

            // A. 現在のポイントでの待機処理チェック
            // （論理上、ポイントに到着した直後に待機が発生する）
            float waitT = _waypoints[tempNextIdx].waitTime;

            // 1. まず移動にかかる時間を計算
            Vector2 nextPointPos = GetTargetLocalPosition(_waypoints[tempNextIdx].localPosition);
            float dist = Vector2.Distance(currentPos, nextPointPos);
            float travelTime = dist / _speed;

            if (remainingTime >= travelTime)
            {
                // 移動完了：時間を消費して座標を更新
                remainingTime -= travelTime;
                currentPos = nextPointPos;

                // 到着したので、インデックスを更新（ここが「現在の場所」になる）
                tempCurrentIdx = tempNextIdx;

                // 次のターゲットを決定するロジック（DetermineNextWaypoint相当）
                if (_isLoop)
                {
                    tempNextIdx = tempCurrentIdx + 1;
                    if (tempNextIdx >= _waypoints.Count)
                        tempNextIdx = 0;
                    tempDirection = 1;
                }
                else
                {
                    tempNextIdx = tempCurrentIdx + tempDirection;
                    if (tempNextIdx >= _waypoints.Count)
                    {
                        tempDirection = -1;
                        tempNextIdx = _waypoints.Count - 2;
                    }
                    else if (tempNextIdx < 0)
                    {
                        tempDirection = 1;
                        tempNextIdx = 1;
                    }
                }

                // 2. 到着後の待機時間を処理
                // 到着したポイント(tempCurrentIdx)の待機時間
                float wait = _waypoints[tempCurrentIdx].waitTime;

                if (remainingTime >= wait)
                {
                    // 待機完了
                    remainingTime -= wait;
                }
                else
                {
                    // 待機中にタイムアップ（現在時刻）
                    transform.localPosition = currentPos;
                    _currentTargetIndex = tempNextIdx; // 次に向かうべき場所
                    _moveDirection = tempDirection;

                    // 待機中状態にする
                    _isWaiting = true;
                    _waitTimer = remainingTime; // 経過した待機時間
                    return; // 完了
                }
            }
            else
            {
                // 移動中にタイムアップ
                float t = remainingTime / travelTime;
                transform.localPosition = Vector2.Lerp(currentPos, nextPointPos, t);

                _currentTargetIndex = tempNextIdx;
                _moveDirection = tempDirection;
                _isWaiting = false;
                _waitTimer = 0f;
                return; // 完了
            }
        }

        // ループを抜けた場合（時間がぴったり一致など）、その位置を設定
        transform.localPosition = currentPos;
        _currentTargetIndex = tempNextIdx;
        _moveDirection = tempDirection;
        _isWaiting = false;
        _waitTimer = 0f;
    }

    #endregion

    #region イベント制御

    /// <summary>
    /// プレイヤー接触による起動トリガー（乗せる処理はPassengerCarrierが行う）
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // まだ動いておらず、かつ接触起動モードの場合のみ
        if (_activateOnPlayerEnter && !_hasStarted)
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
        if (_hasStarted && !_isWaiting && _platformAudio != null)
        {
            _platformAudio.PlayMoveSound();
        }
    }

    private void OnBecameInvisible()
    {
        if (_platformAudio != null)
        {
            _platformAudio.StopMoveSound();
        }
    }

    #endregion

    #region 描画・可視化関連

    /// <summary>
    /// LineRendererを使って経路を描画する
    /// </summary>
    private void DrawPathLine()
    {
        _pathLineRenderer.enabled = true;
        _pathLineRenderer.positionCount = _waypoints.Count;

        // ループ設定をLineRendererに反映（始点と終点を自動で結ぶ）
        _pathLineRenderer.loop = _isLoop;

        bool useWorldSpace = _pathLineRenderer.useWorldSpace;

        for (int i = 0; i < _waypoints.Count; i++)
        {
            Vector3 pos;
            if (useWorldSpace)
            {
                pos = GetWorldPosition(_waypoints[i].localPosition);
            }
            else
            {
                // LineRendererがローカル設定の場合、リフトの親座標系などに合わせる必要がある
                // 親がいる場合はそのローカル座標を使用
                Vector2 localPos = GetTargetLocalPosition(_waypoints[i].localPosition);
                pos = transform.parent != null
                    ? (Vector3)localPos
                    : (Vector3)localPos - transform.position;
            }
            _pathLineRenderer.SetPosition(i, pos);
        }
    }

    /// <summary>
    /// エディタ上でのギズモ表示（デバッグ用）
    /// </summary>
    private void OnDrawGizmos()
    {
        if (_waypoints == null || _waypoints.Count == 0)
            return;

        // コライダー情報の取得
        BoxCollider2D box = GetComponent<BoxCollider2D>();

        // コライダーのローカルサイズとオフセットを取得
        Vector3 localSize = box != null ? (Vector3)box.size : Vector3.one;
        Vector3 localOffset = box != null ? (Vector3)box.offset : Vector3.zero;
        localSize.z = Mathf.Max(localSize.z, 0.1f); // Z軸がつぶれないように

        // ウェイポイントの表示色
        Gizmos.color = Color.cyan;

        Vector3? prevPos = null;
        Vector3 firstPos = Vector3.zero;

        // 現在のオブジェクトの回転とスケールをキャッシュ（移動中も姿勢は変わらない前提）
        Quaternion currentRot = transform.rotation;
        Vector3 currentScale = transform.lossyScale;

        // 元のマトリックスを保存
        Matrix4x4 originalMatrix = Gizmos.matrix;

        for (int i = 0; i < _waypoints.Count; i++)
        {
            // ポイントのワールド位置計算（ここが各ポイントでの「Transform.position」になる）
            Vector2 targetLocalPos = GetTargetLocalPosition(_waypoints[i].localPosition);
            Vector3 worldPos = transform.parent != null
                ? transform.parent.TransformPoint(targetLocalPos)
                : (Vector3)targetLocalPos;

            if (i == 0)
                firstPos = worldPos;

            // その地点にオブジェクトがあるかのように描画するため、TRS行列を作成
            // これにより、ScaleやRotation、Auto Tilingによるサイズ変化が正確に反映されます
            Gizmos.matrix = Matrix4x4.TRS(worldPos, currentRot, currentScale);

            // ローカル座標系で描画（オフセットを加味）
            Gizmos.DrawWireCube(localOffset, localSize);

            // 線を描くためにマトリックスをワールド座標系に戻す
            Gizmos.matrix = originalMatrix;

            // 経路を線で結ぶ
            if (prevPos.HasValue)
            {
                Gizmos.DrawLine(prevPos.Value, worldPos);
            }

            // ラベル表示
#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                worldPos + Vector3.up * 0.5f,
                $"P{i} ({_waypoints[i].waitTime}s)"
            );
#endif
            prevPos = worldPos;
        }

        // ループモードなら終点と始点を結ぶ
        if (_isLoop && _waypoints.Count > 1 && prevPos.HasValue)
        {
            Gizmos.DrawLine(prevPos.Value, firstPos);
        }

        // 現在位置の表示（黄色）
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            // 現在位置でも同様にマトリックスを適用して正確に描画
            Gizmos.matrix = Matrix4x4.TRS(transform.position, currentRot, currentScale);
            Gizmos.DrawWireCube(localOffset, localSize * 1.05f); // 重なって見にくいので少し大きく
            Gizmos.matrix = originalMatrix;
        }
    }

    #endregion
}

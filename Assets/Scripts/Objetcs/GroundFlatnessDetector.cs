using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 地面が平坦であるかを判定・監視し、状態の変化に応じてイベントを発火させるコンポーネント
/// </summary>
public class GroundFlatnessDetector : MonoBehaviour
{
    #region 定義・列挙型

    /// <summary>
    /// 判定基準とする座標のモード
    /// </summary>
    public enum CoordinateMode
    {
        /// <summary>自身の座標（transform.position）を基準にする</summary>
        Relative,

        /// <summary>指定した絶対座標を基準にする</summary>
        Absolute,
    }

    /// <summary>
    /// 横方向の判定の基準点（ピボット）
    /// </summary>
    public enum HorizontalPivot
    {
        /// <summary>基準座標を中心として、左右に均等に判定を広げる</summary>
        Center,

        /// <summary>基準座標を左端として、右方向へ判定を広げる</summary>
        Left,

        /// <summary>基準座標を右端として、左方向へ判定を広げる</summary>
        Right,
    }

    #endregion

    #region フィールド (インスペクター設定)

    [Header("座標設定")]
    [Tooltip("判定の基準をどこにするか")]
    [SerializeField]
    private CoordinateMode _coordinateMode = CoordinateMode.Relative;

    [Tooltip("Absoluteモードの時に基準となる絶対座標")]
    [SerializeField, ShowIf("_coordinateMode", CoordinateMode.Absolute)]
    private Vector2 _absolutePosition;

    [Tooltip("横方向の判定の基準点（ピボット）。基準座標に対して判定エリアをどう配置するか。")]
    [SerializeField]
    private HorizontalPivot _horizontalPivot = HorizontalPivot.Center;

    [Header("判定の設定")]
    [Tooltip("判定する横幅")]
    [SerializeField]
    private float _checkWidth = 2.0f;

    [Tooltip("許容する隙間の量（X軸）。この間隔でレイを飛ばします。")]
    [SerializeField]
    private float _allowedGap = 0.5f;

    [Tooltip("判定基準となるY座標からの誤差許容範囲")]
    [SerializeField]
    private float _yErrorTolerance = 0.1f;

    [Tooltip("レイを発射する高さ（基準Y座標からの上方向のオフセット）")]
    [SerializeField]
    private float _rayStartHeightOffset = 1.0f;

    [Tooltip("レイを飛ばす距離")]
    [SerializeField]
    private float _rayDistance = 2.0f;

    [Header("時間判定の設定")]
    [Tooltip("平らな状態が何秒継続したら「整地された」と確定させるか")]
    [SerializeField]
    private float _timeToBecomeFlat = 2.0f;

    [Tooltip("平らでない状態が何秒継続したら「整地が解除された」と確定させるか")]
    [SerializeField]
    private float _timeToLoseFlatness = 0.0f;

    [Header("イベント")]
    [Tooltip("地面が平らな状態になった時に発火するイベント")]
    [SerializeField]
    private UnityEvent _onBecameFlat;

    [Tooltip("地面が平らではなくなった時に発火するイベント")]
    [SerializeField]
    private UnityEvent _onLostFlatness;

    #endregion

    #region フィールド (内部変数)

    // 現在確定している「地面が平らかどうか」の状態フラグ
    private bool _isFlat = false;

    // 状態変化を遅延させるためのタイマー
    private float _stateChangeTimer = 0f;

    // 地面として判定するレイヤーマスク
    private LayerMask _groundLayerMask;

    #endregion

    #region Unity ライフサイクルイベント

    private void Start()
    {
        // 指定されたレイヤーマスクを取得（タイルやオブジェクトの地面を対象とする）
        _groundLayerMask = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );
    }

    private void FixedUpdate()
    {
        // 毎フレーム判定処理を行い、現在の物理的な状態を取得
        bool currentFlatness = CheckFlatness();

        // スクリプトが認識している状態と実際の状態が異なる場合、タイマーを進める
        if (currentFlatness != _isFlat)
        {
            _stateChangeTimer += Time.fixedDeltaTime;

            // 現在の状態に応じて、確定に必要な目標秒数を取得
            float requiredTime = currentFlatness ? _timeToBecomeFlat : _timeToLoseFlatness;

            // タイマーが指定秒数に到達した場合、状態を更新してイベントを発火
            if (_stateChangeTimer >= requiredTime)
            {
                _isFlat = currentFlatness;
                _stateChangeTimer = 0f; // タイマーをリセット

                if (_isFlat)
                {
                    _onBecameFlat?.Invoke();
                    Debug.Log("地面が平らな状態になりました");
                }
                else
                {
                    _onLostFlatness?.Invoke();
                    Debug.Log("地面が平らではなくなりました");
                }
            }
        }
        else
        {
            // 状態が一致している（変化が完了した、または元に戻った）間はタイマーをリセット
            _stateChangeTimer = 0f;
        }
    }

    #endregion

    #region メインロジック

    /// <summary>
    /// 指定された条件に基づいて、地面が平らかどうかを判定する
    /// </summary>
    /// <returns>平らであればtrue、そうでなければfalse</returns>
    private bool CheckFlatness()
    {
        // 隙間設定が0以下の場合は無限ループやエラーを防ぐためにfalseを返す
        if (_allowedGap <= 0f)
            return false;

        // モードに応じて基準となる中心座標を決定する
        Vector2 centerPosition = GetCenterPosition();

        // ピボットの設定に応じて、レイを飛ばし始めるX座標（左端）を計算する
        float startX = GetStartX(centerPosition.x);

        // 許容される隙間の間隔に基づいてレイの本数と実際の間隔を計算
        int rayCount = Mathf.CeilToInt(_checkWidth / _allowedGap) + 1;
        float spacing = _checkWidth / (rayCount - 1);

        float baselineY = centerPosition.y;
        float originY = baselineY + _rayStartHeightOffset;

        for (int i = 0; i < rayCount; i++)
        {
            float currentX = startX + (spacing * i);
            Vector2 origin = new Vector2(currentX, originY);

            // 下方向にRaycastを飛ばして判定
            RaycastHit2D hit = Physics2D.Raycast(
                origin,
                Vector2.down,
                _rayDistance,
                _groundLayerMask
            );

            if (hit.collider == null)
            {
                // 何もヒットしない場合は、許容範囲以上の隙間（穴）が空いているとみなす
                // Debug.Log($"Raycast at X={currentX} did not hit any ground. This indicates a gap.");
                return false;
            }

            float hitY = hit.point.y;
            float differenceY = hitY - baselineY;

            if (differenceY > _yErrorTolerance)
            {
                // 基準より上側に物体が存在している（物が防いでいる状況）
                // Debug.Log($"Raycast at X={currentX} hit an object above the baseline. Hit Y: {hitY}, Baseline Y: {baselineY}, Difference: {differenceY}");
                return false;
            }

            if (differenceY < -_yErrorTolerance)
            {
                // 地面が基準よりも低すぎる（許容誤差外）
                // Debug.Log($"Raycast at X={currentX} hit a ground point below the baseline. Hit Y: {hitY}, Baseline Y: {baselineY}, Difference: {differenceY}");
                return false;
            }
        }

        // すべてのレイが条件を満たした場合
        return true;
    }

    /// <summary>
    /// 設定された座標モードに基づき、判定の中心となる座標を取得する
    /// </summary>
    /// <returns>基準となる2D座標</returns>
    private Vector2 GetCenterPosition()
    {
        return _coordinateMode == CoordinateMode.Relative
            ? (Vector2)transform.position
            : _absolutePosition;
    }

    /// <summary>
    /// ピボット設定に基づき、判定を開始する一番左端のX座標を計算する
    /// </summary>
    private float GetStartX(float baseCenterX)
    {
        switch (_horizontalPivot)
        {
            case HorizontalPivot.Left:
                return baseCenterX; // 基準座標がそのまま左端になる
            case HorizontalPivot.Right:
                return baseCenterX - _checkWidth; // 基準座標が右端になるよう左へずらす
            case HorizontalPivot.Center:
            default:
                return baseCenterX - (_checkWidth / 2f); // 基準座標を中心に左右へずらす
        }
    }

    #endregion

    #region デバッグ・エディタ機能

    private void OnDrawGizmosSelected()
    {
        // モードに応じて基準となる中心座標を決定
        Vector2 centerPosition = GetCenterPosition();

        // ギズモとして描画するボックスの中心X座標をピボットから計算
        float gizmoCenterX = centerPosition.x;
        switch (_horizontalPivot)
        {
            case HorizontalPivot.Left:
                gizmoCenterX = centerPosition.x + (_checkWidth / 2f);
                break;
            case HorizontalPivot.Right:
                gizmoCenterX = centerPosition.x - (_checkWidth / 2f);
                break;
            case HorizontalPivot.Center:
                gizmoCenterX = centerPosition.x;
                break;
        }

        float baselineY = centerPosition.y;
        Vector3 gizmoCenter = new Vector3(gizmoCenterX, baselineY, transform.position.z);

        // Yの誤差許容範囲（上限と下限）を高さとするボックス
        Vector3 size = new Vector3(_checkWidth, _yErrorTolerance * 2.0f, 0f);

        // プレイ中は判定結果に応じて色を変更（平ら＝緑、不可＝赤）
        Gizmos.color = Application.isPlaying && _isFlat ? Color.green : Color.red;
        Gizmos.DrawWireCube(gizmoCenter, size);

        // レイの視覚化
        if (_allowedGap > 0f)
        {
            Gizmos.color = new Color(1.0f, 1.0f, 0.0f, 0.5f); // 薄い黄色
            int rayCount = Mathf.CeilToInt(_checkWidth / _allowedGap) + 1;
            float spacing = _checkWidth / (rayCount - 1);
            float startX = GetStartX(centerPosition.x);
            float originY = baselineY + _rayStartHeightOffset;

            for (int i = 0; i < rayCount; i++)
            {
                float currentX = startX + (spacing * i);
                Vector3 startPos = new Vector3(currentX, originY, transform.position.z);
                Vector3 endPos = startPos + Vector3.down * _rayDistance;
                Gizmos.DrawLine(startPos, endPos);
            }
        }
    }

    #endregion
}

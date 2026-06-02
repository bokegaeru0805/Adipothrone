using DG.Tweening;
using UnityEngine;

/// <summary>
/// プレイヤーに横から押されると、1マスずつ正確にスライド移動するブロックのコントローラー。
/// IEnemyResettableを実装し、状態のリセットおよび地面へのスナップ機能を提供します。
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class GridPushableBlock : MonoBehaviour, IEnemyResettable
{
    #region インスペクター設定

    [Header("移動設定")]
    [SerializeField]
    [Tooltip("1マスの移動にかかる時間（秒）")]
    private float _moveDuration = 0.4f;

    [Header("リセット設定")]
    [SerializeField]
    [Tooltip("リセット機能（初期位置への復帰）を有効にするかどうか")]
    private bool _isResetEnabled = true;

    #endregion

    #region 内部変数・コンポーネント参照

    // --- 内部状態変数 ---
    private Vector3 _initialPosition; // 配置時の初期座標
    private bool _isMoving = false; // 移動中かどうかのフラグ
    private LayerMask _groundLayerMask; // 接地判定用のレイヤーマスク

    // --- コンポーネント参照 ---
    private Rigidbody2D _rigidbody;
    private Collider2D _collider;

    #endregion

    #region Unityイベント関数

    private void Awake()
    {
        InitializeComponents();
        InitializeSettings();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // 移動中は新たな入力を受け付けない
        if (_isMoving)
            return;

        HandlePlayerPushInput(collision);
    }

    #endregion

    #region 初期化処理

    /// <summary>
    /// 必要なコンポーネントとレイヤーマスクの取得を行います。
    /// </summary>
    private void InitializeComponents()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();

        // 地面とみなすレイヤーを自動取得
        _groundLayerMask = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );
    }

    /// <summary>
    /// 初期座標の保存と、物理演算の基本拘束設定を行います。
    /// </summary>
    private void InitializeSettings()
    {
        _initialPosition = transform.position;

        // 基本状態はZ軸の回転に加え、X軸の移動も物理演算から固定する
        // これにより、プレイヤーがただ接触した際の意図しないズレを完全に防ぐ
        _rigidbody.constraints =
            RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;
    }

    #endregion

    #region リセット機能 (IEnemyResettable 実装)

    /// <summary>
    /// 外部からリセット機能の有効/無効を切り替えるプロパティ
    /// </summary>
    public bool IsResetEnabled
    {
        get => _isResetEnabled;
        set => _isResetEnabled = value;
    }

    /// <summary>
    /// オブジェクトの状態を初期化し、元の座標へ戻します。
    /// </summary>
    public void ResetState()
    {
        if (!_isResetEnabled)
            return;

        // 実行中のアニメーション（DOTween）があれば強制終了
        transform.DOKill();
        _isMoving = false;

        // 物理挙動のリセット
        _rigidbody.velocity = Vector2.zero;
        _rigidbody.constraints =
            RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;

        // 初期座標に戻しつつ、地面に合わせて補正する
        SnapToGround();
    }

    /// <summary>
    /// 初期座標のX軸を維持しつつ、真下の地面を探してコライダーの底辺をぴったり合わせます。
    /// </summary>
    private void SnapToGround()
    {
        Vector3 targetPos = _initialPosition;

        // 初期座標の少し上空から真下へ向かってレイ（光線）を飛ばし、地面を探す
        Vector2 rayOrigin = new Vector2(_initialPosition.x, _initialPosition.y + 2.0f);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, 10.0f, _groundLayerMask);

        if (hit.collider != null)
        {
            // コライダーの底辺（Bounds.min.y）と、オブジェクトの原点（transform.position.y）の差分を計算
            float pivotOffset = transform.position.y - _collider.bounds.min.y;

            // 地面の衝突ポイント(hit.point.y)に差分を足すことで、正確なY座標を算出
            targetPos.y = hit.point.y + pivotOffset;
        }

        transform.position = targetPos;
    }

    #endregion

    #region 移動制御ロジック

    /// <summary>
    /// プレイヤーからの接触を検知し、適切な条件下で移動処理を呼び出します。
    /// </summary>
    private void HandlePlayerPushInput(Collision2D collision)
    {
        Heroin_move player = collision.gameObject.GetComponent<Heroin_move>();
        if (player == null)
            return;

        ContactPoint2D contact = collision.GetContact(0);

        // 法線のY成分の絶対値が小さい場合、真横からの衝突と判定する
        if (Mathf.Abs(contact.normal.y) < 0.5f)
        {
            // プレイヤーとブロックのX座標を比較して相対位置を判定
            bool isPlayerOnLeft = player.transform.position.x < transform.position.x;
            bool isPlayerOnRight = player.transform.position.x > transform.position.x;

            // 左から右へ押している場合
            if (isPlayerOnLeft && player.rightFlag)
            {
                ExecuteGridMovement(1f);
            }
            // 右から左へ押している場合
            else if (isPlayerOnRight && !player.rightFlag)
            {
                ExecuteGridMovement(-1f);
            }
        }
    }

    /// <summary>
    /// 指定された方向へブロックを1マス分スライド移動させます。
    /// </summary>
    /// <param name="directionX">移動方向（右なら1、左なら-1）</param>
    private void ExecuteGridMovement(float directionX)
    {
        _isMoving = true;

        // 現在のX座標から最も近い「整数 + 0.5」の基準座標を算出
        float baseX = Mathf.Round(transform.position.x - 0.5f) + 0.5f;
        float targetX = baseX + directionX;

        // 移動中はX軸の固定を解除し、代わりにY軸の落下を防ぐ
        _rigidbody.constraints =
            RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionY;
        _rigidbody.velocity = Vector2.zero;

        // DOTweenによる滑らかな移動
        transform
            .DOMoveX(targetX, _moveDuration)
            .SetEase(Ease.InOutQuad)
            .OnComplete(() =>
            {
                // 誤差補正
                transform.position = new Vector3(
                    targetX,
                    transform.position.y,
                    transform.position.z
                );

                // 移動終了後、再びX軸を固定し、Y軸は重力で落下できるように戻す
                _rigidbody.constraints =
                    RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;

                _isMoving = false;
            });
    }

    #endregion
}

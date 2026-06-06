using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// プレイヤーに横から押されると、1マスずつ正確にスライド移動するブロックのコントローラー。
/// IEnemyResettableを実装し、状態のリセットおよび地面へのスナップ機能を提供します。
/// 複数のブロックが隣接している場合、連鎖して一緒に押し出すことが可能です。
/// Collider2D.Castを使用し、L字などの複雑な形状（PolygonCollider2D等）にも対応しています。
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

    [SerializeField]
    [Tooltip("リセット時に地面へスナップさせるかどうか")]
    private bool _isSnapToGroundEnabled = true;

    #endregion

    #region 内部変数・コンポーネント参照

    // --- 内部状態変数 ---
    private Vector3 _initialPosition; // 配置時の初期座標
    private bool _isMoving = false; // 移動中かどうかのフラグ
    private LayerMask _groundLayerMask; // 接地判定用のレイヤーマスク
    private ContactFilter2D _contactFilter; // Cast用のフィルター
    private RaycastHit2D[] _hitBuffer = new RaycastHit2D[16]; // Castの判定結果を格納するバッファ（メモリ確保用）

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
        // 自身が移動中の場合は新たな入力を受け付けない
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
    /// 初期座標の保存と、物理演算の基本拘束、判定フィルターの設定を行います。
    /// </summary>
    private void InitializeSettings()
    {
        _initialPosition = transform.position;

        // 基本状態はZ軸の回転に加え、X軸の移動も物理演算から固定する
        _rigidbody.constraints =
            RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;

        // 障害物検知用のフィルター設定（トリガーは無視する）
        _contactFilter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask =
                false // 全レイヤーを対象とし、コード内で個別に弾く
            ,
        };
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

        // スナップ設定が有効な場合は地面に合わせて補正し、無効な場合は純粋な初期座標へ戻す
        if (_isSnapToGroundEnabled)
        {
            SnapToGround();
        }
        else
        {
            transform.position = _initialPosition;
        }
    }

    /// <summary>
    /// 初期座標を基準に、ブロックの形状をそのまま下へ滑らせて正確な地面の高さを割り出し、スナップします。
    /// </summary>
    private void SnapToGround()
    {
        // 念のため初期座標から少し浮かせた位置を起点とし、エディタでのわずかな「めり込み」を解消する
        Vector3 startPos = _initialPosition + Vector3.up * 0.5f;
        transform.position = startPos;

        // 下方向へ最大10マス分まで地面を探す
        float searchDistance = 10.0f;
        int hitCount = _collider.Cast(Vector2.down, _contactFilter, _hitBuffer, searchDistance);

        bool foundGround = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = _hitBuffer[i];

            // 自分自身やトリガーは無視
            if (hit.collider == null || hit.collider == _collider || hit.collider.isTrigger)
                continue;

            // プレイヤーも無視
            if (hit.collider.GetComponent<Heroin_move>() != null)
                continue;

            // Y軸の落下判定（着地）においては、すり抜け床（PlatformEffector2D）も上に乗るため無視せずに着地点とする

            // 最初に見つかった有効な床に対して、ぶつかるまでの距離（hit.distance）分だけ下げる
            transform.position = startPos + Vector3.down * hit.distance;
            foundGround = true;
            break;
        }

        // もし下に何も見つからなかった場合は、純粋な初期座標にリセットしておく
        if (!foundGround)
        {
            transform.position = _initialPosition;
        }
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
            bool isPlayerOnLeft = player.transform.position.x < transform.position.x;
            bool isPlayerOnRight = player.transform.position.x > transform.position.x;

            if (isPlayerOnLeft && player.rightFlag)
            {
                TryPushChain(1f);
            }
            else if (isPlayerOnRight && !player.rightFlag)
            {
                TryPushChain(-1f);
            }
        }
    }

    /// <summary>
    /// 進行方向にあるすべてのブロックの移動可否をチェックし、可能であれば一斉に動かします。
    /// </summary>
    private void TryPushChain(float directionX)
    {
        List<GridPushableBlock> pushChain = new List<GridPushableBlock>();

        // 自身を起点に前方の空間を連鎖的にチェックする
        if (BuildMoveChain(directionX, pushChain))
        {
            foreach (var block in pushChain)
            {
                block.ExecuteGridMovement(directionX);
            }
        }
    }

    /// <summary>
    /// 前方に障害物がないか自身の形状を用いて確認し、ブロックがあれば再帰的にリストへ追加します。
    /// </summary>
    private bool BuildMoveChain(float directionX, List<GridPushableBlock> chain)
    {
        // 既に移動中の場合は連鎖不可
        if (_isMoving)
            return false;

        chain.Add(this);

        // 自身のコライダーの形状をそのまま進行方向へ1マス分キャストする
        Vector2 castDirection = new Vector2(directionX, 0f);
        float distance = 1.0f;
        int hitCount = _collider.Cast(castDirection, _contactFilter, _hitBuffer, distance);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = _hitBuffer[i];

            // 自分自身やトリガーは無視する
            if (hit.collider == _collider || hit.collider.isTrigger)
                continue;

            // プレイヤー自身も無視する
            if (hit.collider.GetComponent<Heroin_move>() != null)
                continue;

            // 横移動時は、PlatformEffector2Dを持つ足場（すり抜け床）を障害物として扱わず無視する
            if (
                hit.collider.usedByEffector
                && hit.collider.GetComponent<PlatformEffector2D>() != null
            )
                continue;

            // 横へスライドした際に床や天井の面と擦れたことによる誤検知を防ぐ
            // (法線が上下を向いている＝床か天井に乗っている/接しているだけ)
            if (Mathf.Abs(hit.normal.y) > 0.5f)
                continue;

            GridPushableBlock nextBlock = hit.collider.GetComponent<GridPushableBlock>();
            if (nextBlock != null)
            {
                // 次のブロックが既にリストにある場合は無視（無限ループ防止）
                if (chain.Contains(nextBlock))
                    continue;

                // 次のブロックに対しても「さらに前へ進めるか」を再帰的にチェックする
                bool nextCanMove = nextBlock.BuildMoveChain(directionX, chain);

                if (!nextCanMove)
                    return false;
            }
            else
            {
                // 別のブロックでもプレイヤーでもなく、床でもない障害物（敵、壁など）に当たった
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 連鎖移動の判定を通過したブロックに対し、実際にDOTweenを用いたスライド移動を実行します。
    /// </summary>
    public void ExecuteGridMovement(float directionX)
    {
        _isMoving = true;

        float baseX = Mathf.Round(transform.position.x - 0.5f) + 0.5f;
        float targetX = baseX + directionX;

        _rigidbody.constraints =
            RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionY;
        _rigidbody.velocity = Vector2.zero;

        transform
            .DOMoveX(targetX, _moveDuration)
            .SetEase(Ease.InOutQuad)
            .OnComplete(() =>
            {
                transform.position = new Vector3(
                    targetX,
                    transform.position.y,
                    transform.position.z
                );

                _rigidbody.constraints =
                    RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;

                _isMoving = false;
            });
    }

    #endregion
}

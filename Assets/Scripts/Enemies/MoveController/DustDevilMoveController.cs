using System.Collections;
using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class DustDevilMoveController : MonoBehaviour, IEnemyResettable
{
    private const string WIND_POOLTAG = "DustDevilWind";

    [Header("設定項目")]
    [SerializeField]
    private Transform playerTransform = null;

    [SerializeField]
    private EnemyActivator activator = null; // 親のEnemyActivatorコンポーネント

    [Header("攻撃の設定")]
    [SerializeField, Tooltip("この敵がプレイヤーに与えるダメージ")]
    private int damage = 0;

    [SerializeField]
    private float attackRange = 1.5f;

    [Header("待機・移動時間の設定")]
    [Tooltip("攻撃前の待機時間（秒）")]
    [SerializeField]
    private float attackStartupTime = 0.5f;

    [Tooltip("攻撃後の待機時間の最小値（秒）")]
    [SerializeField]
    private float minAfterAttackTime = 1.0f;

    [Tooltip("攻撃後の待機時間の最大値（秒）")]
    [SerializeField]
    private float maxAfterAttackTime = 3.0f;

    [Header("位置の設定")]
    [Tooltip("地面から浮かせたい高さ（Y座標のオフセット）")]
    [SerializeField]
    private float targetHeightFromGround = 0.0f;

    [Header("浮遊アニメーション設定")]
    [Tooltip("上下に揺れる幅")]
    [SerializeField]
    private float floatAmount = 0.5f;

    [Tooltip("1回の揺れ（片道）にかかる時間")]
    [SerializeField]
    private float floatDuration = 1.0f;

    [Header("初期位置の設定")]
    [Tooltip("手動で初期位置を設定するかどうか")]
    [SerializeField]
    private bool isUseManualInitialPosition = false;

    private float maxCheckDistance = 20.0f; //地面を探す最大距離
    private LayerMask groundLayer;
    private Transform _transform;
    private Animator animator;
    private EnemyHealth enemyHP;
    private CriWare.Assets.CriAtomSePlayer sePlayer;
    private Tween floatingTween;

    private enum DustDevilState
    {
        Idle,
        Attacking,
    }

    private DustDevilState currentState = DustDevilState.Idle;

    private void Awake()
    {
        groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );

        if (activator == null)
        {
            activator = GetComponentInParent<EnemyActivator>();
        }

        _transform = this.transform;
        sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
        animator = GetComponent<Animator>();

        enemyHP = this.GetComponent<EnemyHealth>();
        if (enemyHP == null)
        {
            Debug.LogError($"{this.gameObject.name}にEnemyHealthコンポーネントがありません。");
            return;
        }
    }

    public void ResetState()
    {
        if (playerTransform == null)
        {
            playerTransform = GameObject
                .FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME)
                ?.transform;
            if (playerTransform == null)
            {
                Debug.LogError($"{this.name}はPlayerTransformを見つけられませんでした");
                return;
            }
        }

        if (enemyHP != null)
        {
            // 自分のHPをリセット
            enemyHP.ResetState();
        }
        else
        {
            Debug.LogError($"{this.gameObject.name}にenemy_HPコンポーネントがありません。");
            return;
        }

        if (activator != null)
        {
            if (!isUseManualInitialPosition) // 自動設定モードの場合
            {
                if (activator != null)
                {
                    // activatorが持つCollider2Dの境界を取得する
                    var activatorCollider = activator.GetComponent<Collider2D>();
                    if (activatorCollider != null)
                    {
                        // Colliderのワールド空間での左端と右端を取得
                        float activatorLeftBound = activatorCollider.bounds.min.x;
                        float activatorRightBound = activatorCollider.bounds.max.x;

                        // アクティベーターの検出範囲内でランダムな中心位置を決定
                        float randomCenter = Random.Range(activatorLeftBound, activatorRightBound);

                        //初期位置を決定
                        transform.position = new Vector2(randomCenter, transform.position.y);
                    }
                }
                else // activaterが見つからない場合
                {
                    Debug.LogWarning(
                        $"{this.name}の親にEnemyActivatorが見つかりませんでした。移動範囲の自動設定は行いません。"
                    );
                }
            }
        }

        AdjustHeight(); // 高さを調整
        tag = GameConstants.IMMUNE_ENEMY_TAG_NAME; // タグをリセット
        currentState = DustDevilState.Idle; // 初期状態をIdleに設定

        animator.SetTrigger("IdleTrigger"); // アニメーションをIdleに設定
        sePlayer.Play(SE_Field.WindGust_weak); // 環境音：風の音再生

        //古いTweenがあれば破棄（二重再生防止）
        if (floatingTween != null)
        {
            floatingTween.Kill();
        }

        // DoTweenでふわふわさせる
        // Relative(true)にすると「現在位置から+Y」に移動してしまうので、
        // ここでは基準位置からのオフセット移動として記述する

        // 動き出しをランダムにして、複数の敵が完全に同期しないようにする（オプション）
        // transform.position += Vector3.up * Random.Range(0f, floatAmount);

        floatingTween = transform
            .DOLocalMoveY(transform.localPosition.y + floatAmount, floatDuration) // 基準位置 + 幅 まで移動
            .SetEase(Ease.InOutSine) // つむじ風らしい、柔らかい動き
            .SetLoops(-1, LoopType.Yoyo); // 無限に往復する
    }

    /// <summary>
    /// レイキャストを使って地面を検出し、Y座標を調整する処理
    /// </summary>
    public void AdjustHeight()
    {
        // レイの開始位置を決定（現在のX座標、現在のY座標）
        Vector2 origin = new Vector2(transform.position.x, transform.position.y);

        // 下方向にレイを飛ばして地面を探す
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, maxCheckDistance, groundLayer);

        // 地面が見つかった場合
        if (hit.collider != null)
        {
            // 現在の座標を取得
            Vector3 newPos = transform.position;

            // Y座標を「地面のY座標 + 指定した高さ」に上書き
            newPos.y = hit.point.y + targetHeightFromGround;

            // 座標を適用
            transform.position = newPos;
        }
        else
        {
            // デバッグ用（地面が見つからなかった場合）
            // Debug.LogWarning($"{this.gameObject.name}: 指定されたレイヤーの地面が見つかりませんでした。", this);
        }
    }

    private void FixedUpdate()
    {
        if (playerTransform == null)
            return;

        //敵の動きがポーズされているかどうかを確認
        // もしポーズされていれば何もしない
        if (TimeManager.instance.isEnemyMovePaused)
        {
            return;
        }

        switch (currentState)
        {
            case DustDevilState.Idle:
                // Idle中はプレイヤーとの距離チェックを行い、攻撃範囲に入ったら即座に攻撃へ移行
                if (IsPlayerInAttackRange())
                {
                    StartCoroutine(Attack());
                }
                break;
        }
    }

    private IEnumerator Attack()
    {
        currentState = DustDevilState.Attacking;

        float timer = 0f;
        animator.SetTrigger("ChargeTrigger");
        while (timer < attackStartupTime)
        {
            yield return null; // 1フレーム待機
            timer += Time.deltaTime; // 時間経過を更新
        }

        animator.SetTrigger("AttackTrigger");
        GameObject windObject = ObjectPooler.SceneInstance.SpawnFromPool(
            WIND_POOLTAG,
            _transform.position,
            Quaternion.identity
        );

        if (windObject == null)
        {
            currentState = DustDevilState.Idle;
            animator.SetTrigger("IdleTrigger");
            yield break;
        }

        ContactDamageController stateController =
            windObject.GetComponent<ContactDamageController>();
        if (stateController == null)
        {
            Debug.LogError($"{windObject.name}にEnemyStateControllerが見つかりません。");
        }
        else
        {
            stateController.SetNormalDamage(damage); // ボールのダメージ量を設定
        }

        sePlayer.Play(SE_EnemyAction.Attack_wind1); // つむじ風攻撃音再生

        //windObjectの生成後の管理はAutoPoolReturnスクリプトに委ねる

        float afterAttackTime = Random.Range(minAfterAttackTime, maxAfterAttackTime);
        yield return new WaitForSeconds(afterAttackTime);
        animator.SetTrigger("IdleTrigger");
        currentState = DustDevilState.Idle;
    }

    /// <summary>
    /// 自身の位置からプレイヤーへのベクトルを取得します
    /// </summary>
    private Vector2 GetVectorToPlayer()
    {
        if (playerTransform != null)
        {
            return (Vector2)playerTransform.position - (Vector2)_transform.position;
        }
        return Vector2.zero;
    }

    /// <summary>
    /// プレイヤーが攻撃範囲内にいるか判定する
    /// </summary>
    private bool IsPlayerInAttackRange()
    {
        Vector2 dir = GetVectorToPlayer();
        return Mathf.Abs(dir.x) <= attackRange;
    }

    private void OnDisable()
    {
        // オブジェクトが非アクティブ化されたらTweenを止める
        if (floatingTween != null)
        {
            floatingTween.Kill();
            floatingTween = null;
        }

        sePlayer.Stop();
    }

    private void OnDrawGizmos()
    {
        // 索敵範囲のGizmosを表示
        Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
        Vector3 size = new Vector3(attackRange * 2, 2f, 0.1f);
        Gizmos.DrawCube(this.transform.position, size);
    }
}

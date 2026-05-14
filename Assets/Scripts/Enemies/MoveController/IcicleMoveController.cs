using System.Collections;
using DG.Tweening; // DOTweenを使用
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class IcicleMoveController : MonoBehaviour, IEnemyResettable
{
    #region 列挙型

    private enum EnemyVariant
    {
        None = 0,
        Icicle = 1,
    }

    private enum IcicleState
    {
        Sleep, // 待機・プレイヤー検知
        Shake, // 落下前の予備動作（微振動）
        Fall, // 落下中
        Fade, // 地面衝突後、フェードアウトして消える
        Regenerate // 指定時間後に初期位置から生えてくる
        ,
    }

    private enum TagState
    {
        Immune, // 無敵状態
        Damageable, // ダメージを受ける・与える状態
        Untagged // タグが設定されていない状態
        ,
    }

    #endregion

    #region インスペクター設定

    [Header("敵のタイプ")]
    [SerializeField]
    private EnemyVariant _variantType = EnemyVariant.Icicle;

    [Header("参照設定")]
    [
        SerializeField,
        Tooltip(
            "見た目と接触ダメージを担当する子オブジェクト（DrawMode=Tiled, Pivot=TopCenter, BoxCollider2D, ContactDamageControllerをアタッチ）"
        )
    ]
    private GameObject _visualAndDamageObject = null;

    [
        SerializeField,
        Tooltip("地面衝突時のエフェクト用オブジェクト（SpriteRenderer, Animatorをアタッチ）")
    ]
    private GameObject _hitEffectObject = null;

    [Header("検知設定")]
    [SerializeField, Tooltip("プレイヤーを検知する左右の範囲")]
    private float _detectRangeX = 2.0f;

    [Header("予備動作設定")]
    [SerializeField, Tooltip("落下前の振動時間（秒）")]
    private float _shakeDuration = 0.5f;

    [SerializeField, Tooltip("振動の強さ")]
    private float _shakeStrength = 0.2f;

    [Header("落下設定")]
    [SerializeField, Tooltip("落下時の加速度（重力）")]
    private float _fallGravity = 20.0f;

    [SerializeField, Tooltip("落下の最大速度")]
    private float _maxFallSpeed = 30.0f;

    [SerializeField, Tooltip("地面に突き刺さる深さ")]
    private float _pierceDepth = 0.5f;

    [Header("衝突・消失・復帰設定")]
    [SerializeField, Tooltip("地面に衝突してから完全に消えるまでの時間")]
    private float _fadeDuration = 0.5f;

    [SerializeField, Tooltip("消滅後、再び生え始めるまでの待機時間")]
    private float _respawnDelay = 2.0f;

    [SerializeField, Tooltip("生え終わるまでにかかる時間")]
    private float _regenerateDuration = 1.0f;

    #endregion

    #region 内部変数

    // キャッシュ
    private Rigidbody2D _rbody;
    private BoxCollider2D _boxCollider;
    private Transform _playerTransform;
    private EnemyHealth _enemyHP;
    private LayerMask _groundLayer;

    // Visual・Damageコンポーネントキャッシュ
    private SpriteRenderer _visualSpriteRenderer;
    private BoxCollider2D _visualCollider;
    private ContactDamageController _contactDamageController;

    // HitEffectコンポーネントキャッシュ
    private Transform _hitEffectTransform;
    private SpriteRenderer _hitEffectSpriteRenderer;
    private Animator _hitEffectAnimator;

    // 状態管理
    private int _damage = 20;
    private IcicleState _currentState = IcicleState.Sleep;
    private Vector3 _initialPosition;
    private Vector2 _originalSpriteSize;

    // 落下計算用
    private float _currentFallSpeed = 0f;

    // 時間停止対応のためのDOTweenキャッシュ
    private Tween _currentTween;

    #endregion

    #region Unityイベント関数

    private void Awake()
    {
        // バリアントに基づくダメージ設定
        switch (_variantType)
        {
            case EnemyVariant.Icicle:
                _damage = 30;
                break;
            default:
                Debug.LogError($"{this.name}のEnemyVariantが設定されていません。");
                break;
        }

        _rbody = GetComponent<Rigidbody2D>();
        _boxCollider = GetComponent<BoxCollider2D>();
        _enemyHP = GetComponent<EnemyHealth>();

        if (_rbody != null)
        {
            // Static設定によるvelocity代入エラーを防止するため、強制的にDynamicにする
            _rbody.bodyType = RigidbodyType2D.Dynamic;
            _rbody.gravityScale = 0f; // 重力は手動で計算するため0
            _rbody.constraints =
                RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;
        }

        // VisualとDamageを兼ね備えたオブジェクトの初期設定
        if (_visualAndDamageObject != null)
        {
            _visualSpriteRenderer = _visualAndDamageObject.GetComponent<SpriteRenderer>();
            _visualCollider = _visualAndDamageObject.GetComponent<BoxCollider2D>();
            _contactDamageController =
                _visualAndDamageObject.GetComponent<ContactDamageController>();

            if (_visualSpriteRenderer != null)
            {
                _originalSpriteSize = _visualSpriteRenderer.size;
            }
            else
            {
                Debug.LogError(
                    $"{this.name}の子オブジェクトにSpriteRendererがアタッチされていません。"
                );
            }

            if (_contactDamageController != null)
            {
                _contactDamageController.SetNormalDamage(_damage);
            }
            else
            {
                Debug.LogError(
                    $"{this.name}の子オブジェクトにContactDamageControllerがアタッチされていません。"
                );
            }
        }

        // 地面のレイヤーを取得
        _groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );

        // エフェクトオブジェクトの初期設定
        if (_hitEffectObject != null)
        {
            _hitEffectTransform = _hitEffectObject.transform;
            _hitEffectSpriteRenderer = _hitEffectObject.GetComponent<SpriteRenderer>();
            _hitEffectAnimator = _hitEffectObject.GetComponent<Animator>();

            _hitEffectObject.SetActive(false); // 初期状態は非表示
        }
    }

    private void Start()
    {
        _initialPosition = transform.position;
        ResetState();
    }

    private void FixedUpdate()
    {
        if (_playerTransform == null)
            return;

        // 時間停止処理の再現
        if (TimeManager.instance.isEnemyMovePaused)
        {
            if (_rbody.simulated)
                _rbody.simulated = false;
            if (_hitEffectAnimator != null && _hitEffectAnimator.enabled)
                _hitEffectAnimator.enabled = false;
            _currentTween?.Pause();
            return;
        }
        else
        {
            if (!_rbody.simulated)
                _rbody.simulated = true;
            if (_hitEffectAnimator != null && !_hitEffectAnimator.enabled)
                _hitEffectAnimator.enabled = true;
            _currentTween?.Play();
        }

        switch (_currentState)
        {
            case IcicleState.Sleep:
                UpdateSleepState();
                break;

            case IcicleState.Fall:
                UpdateFallState();
                break;

            case IcicleState.Shake:
            case IcicleState.Fade:
            case IcicleState.Regenerate:
                // これらの状態はDOTweenやコルーチンで進行するため、物理的な移動は行わない
                _rbody.velocity = Vector2.zero;
                break;
        }
    }

    private void OnDestroy()
    {
        // オブジェクト破棄時にTweenをキルしてメモリリークを防ぐ
        _currentTween?.Kill();
    }

    #endregion

    #region 初期化・リセット処理

    /// <summary>
    /// 敵の状態を初期化・リセットします。
    /// </summary>
    public void ResetState()
    {
        // 特異的なコードの再現: PlayerManagerからの取得
        if (_playerTransform == null)
        {
            if (PlayerManager.instance != null && PlayerManager.instance.PlayerGameObject != null)
            {
                _playerTransform = PlayerManager.instance.PlayerGameObject.transform;
            }
            else
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag(
                    GameConstants.PLAYER_TAG_NAME
                );
                if (playerObj != null)
                    _playerTransform = playerObj.transform;
            }
        }

        if (_enemyHP != null)
        {
            _enemyHP.ResetState();
        }

        // 現在実行中のTweenがあればキャンセル
        _currentTween?.Kill();

        // 状態のリセット
        transform.position = _initialPosition;
        _rbody.velocity = Vector2.zero;
        _currentFallSpeed = 0f;

        // 見た目とコライダーのリセット
        if (_visualSpriteRenderer != null)
        {
            _visualSpriteRenderer.size = _originalSpriteSize;
            Color color = _visualSpriteRenderer.color;
            color.a = 1f;
            _visualSpriteRenderer.color = color;
        }
        if (_visualCollider != null)
        {
            _visualCollider.size = new Vector2(_visualCollider.size.x, _originalSpriteSize.y);
            _visualCollider.offset = new Vector2(
                _visualCollider.offset.x,
                -_originalSpriteSize.y / 2f
            );
        }

        if (_hitEffectObject != null)
        {
            _hitEffectObject.SetActive(false);
        }

        ChangeState(IcicleState.Sleep);
    }

    #endregion

    #region ステート更新処理

    /// <summary>
    /// 待機状態の処理を行います。プレイヤーが指定範囲内に来たら予備動作へ移行します。
    /// </summary>
    private void UpdateSleepState()
    {
        Vector3 myPos = transform.position;
        Vector3 playerPos = _playerTransform.position;

        // プレイヤーのX座標が指定範囲内か判定
        float deltaX = Mathf.Abs(playerPos.x - myPos.x);
        if (deltaX > _detectRangeX)
            return;

        // 自身の位置から下方向にレイを飛ばして地面までの距離を動的に取得
        RaycastHit2D hit = Physics2D.BoxCast(
            _boxCollider.bounds.center,
            _boxCollider.size,
            0f,
            Vector2.down,
            100f, // 適当な最大検知距離
            _groundLayer
        );

        if (hit.collider != null)
        {
            // プレイヤーが自分より下、かつレイが当たった地面より上にいるかを判定
            if (playerPos.y < myPos.y && playerPos.y > hit.point.y)
            {
                ChangeState(IcicleState.Shake);
            }
        }
    }

    /// <summary>
    /// 落下状態の処理を行います。徐々に加速しながら落下し、地面との衝突を検知します。
    /// </summary>
    private void UpdateFallState()
    {
        // 加速処理
        _currentFallSpeed += _fallGravity * Time.fixedDeltaTime;
        if (_currentFallSpeed > _maxFallSpeed)
        {
            _currentFallSpeed = _maxFallSpeed;
        }

        // 速度を適用
        _rbody.velocity = new Vector2(0f, -_currentFallSpeed);

        // すり抜け防止のためのBoxCastによる地面事前検知
        Vector2 boxSize = _boxCollider.size;
        float castDistance = (_currentFallSpeed * Time.fixedDeltaTime) + 0.1f;

        RaycastHit2D hit = Physics2D.BoxCast(
            _boxCollider.bounds.center,
            boxSize,
            0f,
            Vector2.down,
            castDistance,
            _groundLayer
        );

        if (hit.collider != null)
        {
            // 衝突地点に合わせて少しめり込みを補正し、地面に突き刺さったようにする
            Vector3 newPos = transform.position;
            newPos.y = hit.point.y + (boxSize.y / 2f) - _pierceDepth;
            transform.position = newPos;

            // 突き刺さった瞬間に動きを止める
            _rbody.velocity = Vector2.zero;

            // エフェクトの位置を衝突地点に合わせる
            if (_hitEffectTransform != null)
            {
                _hitEffectTransform.position = hit.point;
            }

            ChangeState(IcicleState.Fade);
        }
    }

    #endregion

    #region 状態管理とアニメーション・Tween処理

    /// <summary>
    /// 現在の行動ステートを変更し、必要な初期化やTweenの実行を行います。
    /// </summary>
    /// <param name="newState">移行する新しいステート</param>
    private void ChangeState(IcicleState newState)
    {
        _currentState = newState;

        switch (newState)
        {
            case IcicleState.Sleep:
                // 待機状態は無敵
                SetTagState(TagState.Immune);
                break;

            case IcicleState.Shake:
                // 落下開始の合図としてダメージ可能タグに変更
                SetTagState(TagState.Damageable);
                // X軸方向にのみ微振動させる
                _currentTween = transform
                    .DOShakePosition(
                        _shakeDuration,
                        new Vector3(_shakeStrength, 0f, 0f),
                        20,
                        90f,
                        false,
                        true
                    )
                    .OnComplete(() => ChangeState(IcicleState.Fall));
                break;

            case IcicleState.Fall:
                // 落下開始時は初速0
                _currentFallSpeed = 0f;
                break;

            case IcicleState.Fade:
                // 地面に接触してから生え終わるまではUntaggedにする
                SetTagState(TagState.Untagged);

                // エフェクトをアクティブ化して自動再生
                if (_hitEffectObject != null)
                {
                    _hitEffectObject.SetActive(true);
                    if (_hitEffectSpriteRenderer != null)
                    {
                        Color effColor = _hitEffectSpriteRenderer.color;
                        effColor.a = 1f;
                        _hitEffectSpriteRenderer.color = effColor;
                    }
                }

                // 本体とエフェクトを同時にフェードアウトさせる
                Sequence fadeSeq = DOTween.Sequence();
                if (_visualSpriteRenderer != null)
                {
                    fadeSeq.Join(_visualSpriteRenderer.DOFade(0f, _fadeDuration));
                }
                if (_hitEffectSpriteRenderer != null)
                {
                    fadeSeq.Join(_hitEffectSpriteRenderer.DOFade(0f, _fadeDuration));
                }

                // フェード完了後、指定時間待機してからRegenerateへ移行
                fadeSeq.OnComplete(() =>
                {
                    if (_hitEffectObject != null)
                    {
                        _hitEffectObject.SetActive(false); // 再生終了後に非表示に戻す
                    }
                    _currentTween = DOVirtual.DelayedCall(
                        _respawnDelay,
                        () => ChangeState(IcicleState.Regenerate)
                    );
                });
                _currentTween = fadeSeq;
                break;

            case IcicleState.Regenerate:
                // 初期位置に戻す
                transform.position = _initialPosition;
                _rbody.velocity = Vector2.zero;

                // 本体を透明から不透明に戻す
                if (_visualSpriteRenderer != null)
                {
                    Color visColor = _visualSpriteRenderer.color;
                    visColor.a = 1f;
                    _visualSpriteRenderer.color = visColor;

                    // TiledとPivot TopCenterを活かして、高さを元のサイズまでアニメーションさせて「生える」表現
                    // 合わせてコライダーのサイズとOffsetも同期させる
                    _currentTween = DOTween
                        .To(
                            () => 0f,
                            y =>
                            {
                                _visualSpriteRenderer.size = new Vector2(_originalSpriteSize.x, y);
                                if (_visualCollider != null)
                                {
                                    _visualCollider.size = new Vector2(_visualCollider.size.x, y);
                                    _visualCollider.offset = new Vector2(
                                        _visualCollider.offset.x,
                                        -y / 2f
                                    );
                                }
                            },
                            _originalSpriteSize.y,
                            _regenerateDuration
                        )
                        .SetEase(Ease.OutQuad)
                        .OnComplete(() => ChangeState(IcicleState.Sleep));
                }
                else
                {
                    ChangeState(IcicleState.Sleep);
                }
                break;
        }
    }

    /// <summary>
    /// 本体のタグと接触ダメージオブジェクトのタグを切り替えます。
    /// </summary>
    /// <param name="state">変更するタグの状態</param>
    private void SetTagState(TagState state)
    {
        string targetTag = GameConstants.UNTAGGED_TAG_NAME;

        switch (state)
        {
            case TagState.Immune:
                targetTag = GameConstants.IMMUNE_ENEMY_TAG_NAME;
                break;
            case TagState.Damageable:
                targetTag = GameConstants.DAMAGEABLE_ENEMY_TAG_NAME;
                break;
            case TagState.Untagged:
                targetTag = GameConstants.UNTAGGED_TAG_NAME;
                break;
        }

        this.gameObject.tag = targetTag;

        if (_visualAndDamageObject != null)
        {
            _visualAndDamageObject.tag = targetTag;
        }
    }

    #endregion

    #region デバッグ描画

    private void OnDrawGizmosSelected()
    {
        // プレイヤー検知範囲の描画（エディタ確認用）
        // Y軸は動的取得に変更したため、X軸の範囲のみ描画します
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);

        Vector3 p1 = transform.position + new Vector3(-_detectRangeX, 0f, 0f);
        Vector3 p2 = transform.position + new Vector3(_detectRangeX, 0f, 0f);
        Vector3 p3 = p1 + new Vector3(0f, -10f, 0f); // 下方向への目安
        Vector3 p4 = p2 + new Vector3(0f, -10f, 0f);

        Gizmos.DrawLine(p1, p3);
        Gizmos.DrawLine(p2, p4);
        Gizmos.DrawLine(p1, p2);
    }

    #endregion
}

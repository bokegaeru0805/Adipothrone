using System;
using System.Collections;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// レア敵（妖精）の移動コントローラー。
/// 通常時はパーリンノイズとランダムな目的地を用いて気まぐれに飛び、
/// 被弾すると激しく飛び回った後、上空へ逃走します。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
[RequireComponent(typeof(SpriteRenderer))]
public class RareFairyMoveController : MonoBehaviour, IEnemyResettable
{
    #region 列挙型

    private enum EnemyVariant
    {
        None = 0,
        Tower = 1,
    }

    private enum FairyState
    {
        NormalFly, // 通常の飛行
        Fleeing, // 被弾後の激しい飛行
        Escaping, // 画面上空への逃走
        Hidden // 画面外へ消えた後の待機状態
        ,
    }

    #endregion

    #region インスペクター設定

    [Header("敵のタイプ")]
    [SerializeField]
    private EnemyVariant _variantType = EnemyVariant.Tower;

    [Header("基本設定")]
    [SerializeField]
    private EnemyActivator _activator = null;

    [Tooltip("表示時のエフェクト（レア敵用）")]
    [SerializeField]
    private ParticleSystem _spawnEffect = null;

    [Header("横移動の設定")]
    [Tooltip("通常時の横移動速度")]
    [SerializeField]
    private float _normalSpeedX = 2.0f;

    [Tooltip("被弾して逃げ回る時の横移動速度")]
    [SerializeField]
    private float _fleeingSpeedX = 4.0f;

    [Tooltip("手動で移動範囲を設定するかどうか")]
    [SerializeField]
    private bool _isUseManualBounds = false;

    [SerializeField, ShowIf(nameof(_isUseManualBounds))]
    private float _leftBound;

    [SerializeField, ShowIf(nameof(_isUseManualBounds))]
    private float _rightBound;

    [Header("飛行（高さ）の設定")]
    [Tooltip("地面から維持したい最小の高さ")]
    [SerializeField]
    private float _minHeightFromGround = 2.0f;

    [Tooltip("地面から維持したい最大の高さ")]
    [SerializeField]
    private float _maxHeightFromGround = 4.0f;

    [Tooltip("高さ調整の追従速度（高いほど素早く地形に合わせる）")]
    [SerializeField]
    private float _heightAdjustSpeed = 5.0f;

    [Tooltip("地面を探すレイの長さ")]
    [SerializeField]
    private float _rayLength = 20.0f;

    [Header("ランダム移動（ウェイポイント）の設定")]
    [Tooltip("通常時、次の目的地を決めるまでの時間（秒）")]
    [SerializeField]
    private float _waypointIntervalNormal = 2.0f;

    [Tooltip("被弾時、次の目的地を決めるまでの時間（秒）")]
    [SerializeField]
    private float _waypointIntervalFleeing = 0.5f;

    [Header("パーリンノイズ（揺らぎ）の設定")]
    [Tooltip("通常時の揺らぎの強さ（X方向、Y方向）")]
    [SerializeField]
    private Vector2 _normalNoiseAmplitude = new Vector2(1.5f, 1.0f);

    [Tooltip("被弾時の揺らぎの強さ（X方向、Y方向）")]
    [SerializeField]
    private Vector2 _fleeingNoiseAmplitude = new Vector2(3.0f, 2.5f);

    [Tooltip("揺らぎが変化するスピード（速いほどビクビクした動きになります）")]
    [SerializeField]
    private float _noiseSpeed = 2.0f;

    [Header("逃走の設定")]
    [Tooltip("被弾してから上空へ逃げ去るまでの時間（秒）")]
    [SerializeField]
    private float _fleeDuration = 3.0f;

    [Tooltip("逃走開始時、谷なりに飛ぶために下に沈み込む初速度")]
    [SerializeField]
    private float _escapeDipVelocity = 2.0f;

    [Tooltip("沈み込んでから上昇へ転じるまでの時間（秒）")]
    [SerializeField]
    private float _escapeSwoopDuration = 1.2f;

    [Tooltip("上空へ逃げ去る際の最終的な上昇速度")]
    [SerializeField]
    private float _escapeAscendSpeed = 8.0f;

    #endregion

    #region 内部変数

    // 内部コンポーネントのキャッシュ
    private Animator _animator;
    private Rigidbody2D _rbody;
    private EnemyHealth _enemyHP;
    private SpriteRenderer _spriteRenderer;
    private CriWare.Assets.CriAtomSePlayer _sePlayer;

    private Transform _playerTransform;
    private LayerMask _groundLayer;

    // ステータス管理
    private FairyState _currentState = FairyState.NormalFly;
    private float _fleeTimer = 0f;
    private bool _hasBeenSeenByCamera = false;
    private float _escapeTimer = 0f;
    private float _escapeDirectionX = 1f;

    // ウェイポイントとノイズ管理
    private float _targetX;
    private float _targetHeight;
    private float _waypointTimer;
    private float _noiseOffsetX;
    private float _noiseOffsetY;

    // アニメーター用パラメーター
    private readonly int _isFleeingHash = Animator.StringToHash("IsFleeing");

    #endregion

    #region Unityイベント関数

    private void Awake()
    {
        _groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );

        _animator = GetComponent<Animator>();
        _rbody = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
        _enemyHP = GetComponent<EnemyHealth>();

        if (_activator == null)
        {
            _activator = GetComponentInParent<EnemyActivator>();
        }

        if (_enemyHP == null)
        {
            Debug.LogError($"{gameObject.name}にEnemyHealthコンポーネントがありません。");
        }
        else
        {
            // HP変動イベントにメソッドを登録
            _enemyHP.OnHPChanged += HandleHPChanged;
        }

        if (_rbody != null)
        {
            _rbody.gravityScale = 0f; // 常に浮遊するため重力は無視する
            _rbody.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        tag = GameConstants.IMMUNE_ENEMY_TAG_NAME;
    }

    private void Start()
    {
        ResetState();
    }

    private void FixedUpdate()
    {
        // 時間停止処理の再現
        if (TimeManager.instance.isEnemyMovePaused)
        {
            if (_rbody.simulated)
                _rbody.simulated = false;
            if (_animator.enabled)
                _animator.enabled = false;
            return;
        }
        else
        {
            if (!_rbody.simulated)
                _rbody.simulated = true;
            if (!_animator.enabled)
                _animator.enabled = true;
        }

        switch (_currentState)
        {
            case FairyState.NormalFly:
                UpdateFlight(_normalSpeedX, _normalNoiseAmplitude);
                break;

            case FairyState.Fleeing:
                UpdateFlight(_fleeingSpeedX, _fleeingNoiseAmplitude);

                // 一定時間逃げ回ったら上空へ逃走開始
                _fleeTimer += Time.fixedDeltaTime;
                if (_fleeTimer >= _fleeDuration)
                {
                    ChangeState(FairyState.Escaping);
                }
                break;

            case FairyState.Escaping:
                // --- 以下を書き換えてください ---
                _escapeTimer += Time.fixedDeltaTime;

                // tは0から1の間で変化し、1に達するとそのまま維持
                float t = Mathf.Clamp01(_escapeTimer / _escapeSwoopDuration);

                // X方向：逃げ回っていた時の横移動速度から、わずかな横移動に滑らかに減速させる（斜めに飛び去るため）
                float velocityX = Mathf.Lerp(
                    _escapeDirectionX * _fleeingSpeedX,
                    _escapeDirectionX * 1.5f,
                    t
                );

                // Y方向：マイナス（下降）からプラス（上昇）へと滑らかに遷移させ、谷なりの軌道を作る
                float velocityY = Mathf.Lerp(-_escapeDipVelocity, _escapeAscendSpeed, t);

                _rbody.velocity = new Vector2(velocityX, velocityY);

                // 向きの更新
                if (velocityX > 0.1f)
                    _spriteRenderer.flipX = false;
                else if (velocityX < -0.1f)
                    _spriteRenderer.flipX = true;
                break;

            case FairyState.Hidden:
                // 画面外に消えた後は動かない
                _rbody.velocity = Vector2.zero;
                break;
        }
    }

    private void OnDestroy()
    {
        // メモリリーク防止のためイベントの登録解除
        if (_enemyHP != null)
        {
            _enemyHP.OnHPChanged -= HandleHPChanged;
        }
    }

    /// <summary>
    /// Rendererがカメラに映るようになった時に呼び出されるUnityの標準イベント
    /// </summary>
    private void OnBecameVisible()
    {
        // 初めて画面に入った時のレア敵特有の演出
        if (!_hasBeenSeenByCamera)
        {
            _hasBeenSeenByCamera = true;

            if (_spawnEffect != null)
            {
                _spawnEffect.Play();
            }

            if (_sePlayer != null)
            {
                _sePlayer.Play(SE_EnemyAction.RareEnemyAppear);
            }
        }
    }

    /// <summary>
    /// Rendererがカメラの描画範囲外に出た時に呼び出されるUnityの標準イベント
    /// </summary>
    private void OnBecameInvisible()
    {
        // 逃走状態で画面外に出たら非表示にして行動を終了する
        if (_currentState == FairyState.Escaping)
        {
            ChangeState(FairyState.Hidden);
            gameObject.SetActive(false);
        }
    }

    #endregion

    #region 初期化・リセット処理

    public void ResetState()
    {
        // プレイヤーの取得
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

        _hasBeenSeenByCamera = false;
        _fleeTimer = 0f;
        gameObject.SetActive(true); // Hiddenから復帰した場合に備えてアクティブ化

        // 移動範囲の自動設定
        if (!_isUseManualBounds && _activator != null)
        {
            Collider2D activatorCollider = _activator.GetComponent<Collider2D>();
            if (activatorCollider != null)
            {
                _leftBound = activatorCollider.bounds.min.x;
                _rightBound = activatorCollider.bounds.max.x;
            }
        }

        // パーリンノイズの取得開始位置をランダムにして、毎回違う揺らぎのパターンにする
        _noiseOffsetX = UnityEngine.Random.Range(0f, 1000f);
        _noiseOffsetY = UnityEngine.Random.Range(0f, 1000f);

        // 最初の目的地を決定する
        PickNewWaypoint(FairyState.NormalFly);

        ChangeState(FairyState.NormalFly);
    }

    #endregion

    #region 状態管理・移動処理

    /// <summary>
    /// ステートを変更し、アニメーションなどを設定します。
    /// </summary>
    private void ChangeState(FairyState newState)
    {
        _currentState = newState;

        switch (newState)
        {
            case FairyState.NormalFly:
                _animator.SetBool(_isFleeingHash, false);
                break;
            case FairyState.Fleeing:
                _animator.SetBool(_isFleeingHash, true);
                break;
            case FairyState.Escaping:
                _escapeTimer = 0f;
                // スプライトの向きから現在の進行方向を記録（右向きなら1、左向きなら-1）
                _escapeDirectionX = _spriteRenderer.flipX ? -1f : 1f;
                break;
            case FairyState.Hidden:
                // 逃走中や隠蔽中もFleeingのアニメーションを継続する
                break;
        }
    }

    /// <summary>
    /// HPが変動した際に呼ばれるイベントハンドラー
    /// </summary>
    private void HandleHPChanged(int currentHP)
    {
        // 最大HPより減っており、かつ通常状態であれば逃走状態に移行する
        if (_currentState == FairyState.NormalFly && currentHP < _enemyHP.MaxHP)
        {
            ChangeState(FairyState.Fleeing);
        }
    }

    /// <summary>
    /// ランダムな新しい目的地（X座標と地面からの高さ）を設定します。
    /// </summary>
    private void PickNewWaypoint(FairyState state)
    {
        _targetX = UnityEngine.Random.Range(_leftBound, _rightBound);
        _targetHeight = UnityEngine.Random.Range(_minHeightFromGround, _maxHeightFromGround);
        _waypointTimer =
            (state == FairyState.Fleeing) ? _waypointIntervalFleeing : _waypointIntervalNormal;
    }

    /// <summary>
    /// 指定された速度とパーリンノイズの振幅で、ランダムな目的地へ向かって揺らぎながら飛行します。
    /// </summary>
    private void UpdateFlight(float speedX, Vector2 noiseAmplitude)
    {
        Vector2 currentPos = transform.position;

        // 1. 目的地の更新
        _waypointTimer -= Time.fixedDeltaTime;
        // タイマーが切れるか、X座標が目的地に十分近づいたら新しい目的地を設定
        if (_waypointTimer <= 0f || Mathf.Abs(currentPos.x - _targetX) < 0.5f)
        {
            PickNewWaypoint(_currentState);
        }

        // 2. 目的地へ向かう基本的な速度の計算
        // X方向: 目的地に向かって進む
        float dirX = Mathf.Sign(_targetX - currentPos.x);
        float baseVelocityX = dirX * speedX;

        // Y方向: Raycastで地面の高さを取得し、目的の高さへ向かう
        float baseVelocityY = _rbody.velocity.y;
        RaycastHit2D hit = Physics2D.Raycast(currentPos, Vector2.down, _rayLength, _groundLayer);

        if (hit.collider != null)
        {
            float targetY = hit.point.y + _targetHeight;
            float diffY = targetY - currentPos.y;
            baseVelocityY = diffY * _heightAdjustSpeed;
        }

        // 3. パーリンノイズによる自然な揺らぎ（ふわふわ感）の加算
        // 時間経過でノイズのサンプリング位置を進める
        _noiseOffsetX += Time.fixedDeltaTime * _noiseSpeed;
        _noiseOffsetY += Time.fixedDeltaTime * _noiseSpeed;

        // Mathf.PerlinNoiseは0～1の値を返すため、-1～1になるように変換してから振幅を掛ける
        float noiseX = (Mathf.PerlinNoise(_noiseOffsetX, 0f) - 0.5f) * 2f * noiseAmplitude.x;
        float noiseY = (Mathf.PerlinNoise(0f, _noiseOffsetY) - 0.5f) * 2f * noiseAmplitude.y;

        // 基本の速度にノイズ（揺らぎ）を足し合わせて最終的な速度とする
        float finalVelocityX = baseVelocityX + noiseX;
        float finalVelocityY = baseVelocityY + noiseY;

        _rbody.velocity = new Vector2(finalVelocityX, finalVelocityY);

        // 4. 進行方向に合わせたスプライトの反転
        if (finalVelocityX > 0.1f)
        {
            _spriteRenderer.flipX = false; // 右へ移動中
        }
        else if (finalVelocityX < -0.1f)
        {
            _spriteRenderer.flipX = true; // 左へ移動中
        }
    }

    #endregion

    #region デバッグ描画

    private void OnDrawGizmosSelected()
    {
        // 移動範囲の描画
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        if (_isUseManualBounds || _activator != null)
        {
            float boundsLeft = _isUseManualBounds
                ? _leftBound
                : (
                    _activator
                        ? _activator.GetComponent<Collider2D>().bounds.min.x
                        : transform.position.x
                );
            float boundsRight = _isUseManualBounds
                ? _rightBound
                : (
                    _activator
                        ? _activator.GetComponent<Collider2D>().bounds.max.x
                        : transform.position.x
                );

            Vector3 center = new Vector3(
                (boundsLeft + boundsRight) / 2f,
                transform.position.y,
                transform.position.z
            );
            Vector3 size = new Vector3(
                Mathf.Abs(boundsRight - boundsLeft),
                _maxHeightFromGround,
                0.1f
            );
            Gizmos.DrawWireCube(center, size);
        }

        // Raycastの長さを可視化
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * _rayLength);
    }

    #endregion
}

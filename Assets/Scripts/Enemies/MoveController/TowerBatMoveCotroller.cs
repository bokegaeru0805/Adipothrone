using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class TowerBatMoveController : MonoBehaviour, IEnemyResettable
{
    #region 列挙型

    private enum EnemyVariant
    {
        None = 0,
        Tower = 1,
    }

    private enum BatState
    {
        Sleep,
        Fly,
        Feint, // フェイント状態
        Attack,
        Return,
    }

    #endregion

    #region インスペクター設定

    [Header("敵のタイプ")]
    [SerializeField]
    private EnemyVariant _variantType = EnemyVariant.Tower;

    [Header("設定項目")]
    [SerializeField, Tooltip("接触ダメージを与える子オブジェクト")]
    private GameObject _contactDamageObject = null;

    [Header("検知と移動の基本設定")]
    [SerializeField, Tooltip("プレイヤーを検知する左右の範囲")]
    private float _detectRangeX = 5.0f;

    [SerializeField, Tooltip("Sleep状態に戻ってから再び検知を開始するまでの待機時間（秒）")]
    private float _sleepCooldown = 2.0f;

    [SerializeField, Tooltip("突撃のベース速度")]
    private float _flySpeed = 8.0f;

    [SerializeField, Tooltip("突撃がタイムアウトして戻り始めるまでの最大時間")]
    private float _maxFlyDuration = 4.0f;

    [Header("ハイブリッド予測・ホーミング設定")]
    [SerializeField, Tooltip("突撃開始後、ホーミングを継続する時間（秒）")]
    private float _homingDuration = 1.0f;

    [SerializeField, Tooltip("プレイヤーの何秒先の未来座標を予測するか")]
    private float _predictLeadTime = 0.5f;

    [SerializeField, Tooltip("飛行時の上下の揺れ幅（コウモリらしさ）")]
    private float _flutterAmplitude = 0.5f;

    [SerializeField, Tooltip("飛行時の上下の揺れ速度")]
    private float _flutterSpeed = 15.0f;

    [Header("フェイント設定")]
    [SerializeField, Range(0f, 100f), Tooltip("フェイントを行う確率（％）")]
    private float _feintProbability = 30f;

    [SerializeField, Tooltip("突撃開始からフェイントに移行するまでの時間")]
    private float _feintStartTime = 0.5f;

    [SerializeField, Tooltip("フェイント（宙返り）にかかる時間")]
    private float _feintDuration = 0.8f;

    [Header("攻撃・復帰の設定")]
    [SerializeField, Tooltip("攻撃判定（AttackTrigger）を出すプレイヤーとの距離")]
    private float _attackDistance = 1.2f;

    [SerializeField, Tooltip("攻撃後の待機・アニメーション時間")]
    private float _attackDuration = 0.5f;

    [SerializeField, Tooltip("初期座標へ戻る際の速度")]
    private float _returnSpeed = 4.0f;

    [SerializeField, Tooltip("谷なりに戻る軌道の、下方向への膨らみ具合")]
    private float _returnCurveDepth = 2.0f;

    #endregion

    #region 内部変数

    // 内部コンポーネントキャッシュ
    private Animator _animator;
    private Rigidbody2D _rbody;
    private Transform _playerTransform;
    private EnemyHealth _enemyHP;

    // ステータス・座標管理
    private int _damage = 20;
    private BatState _currentState = BatState.Sleep;
    private Vector3 _initialPosition;

    // アニメーター用パラメータ名
    private string _sleepParam = "Sleep";
    private string _flyParam = "Fly";
    private string _attackTriggerParam = "AttackTrigger";

    // 予測・軌道計算用の一時変数
    private float _stateTimer = 0f;
    private float _flyTimer = 0f; // Fly状態の経過時間のみを管理するタイマー
    private Vector3 _predictedTargetPos;
    private Vector3 _playerPrevPos;
    private Vector3 _playerVelocity;
    private Vector3 _returnStartPos;
    private Vector3 _flyDirection;
    private float _calculatedReturnDuration = 1.0f;

    // フェイント用の一時変数
    private bool _willFeint = false;
    private Vector3 _preFeintDirection;

    #endregion

    #region Unityイベント関数

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _rbody = GetComponent<Rigidbody2D>();
        _enemyHP = GetComponent<EnemyHealth>();
        _initialPosition = transform.position;

        if (_rbody != null)
        {
            _rbody.gravityScale = 0f;
            _rbody.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        if (_contactDamageObject != null)
        {
            ContactDamageController contactDamageController =
                _contactDamageObject.GetComponent<ContactDamageController>();
            if (contactDamageController != null)
            {
                contactDamageController.SetNormalDamage(_damage);
            }
            else
            {
                Debug.LogError(
                    $"{this.name}の子オブジェクトにContactDamageControllerがアタッチされていません。"
                );
            }
        }
    }

    private void Start()
    {
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

        // プレイヤーの擬似的な速度を計算（予測用）
        _playerVelocity = (_playerTransform.position - _playerPrevPos) / Time.fixedDeltaTime;
        _playerPrevPos = _playerTransform.position;

        _stateTimer += Time.fixedDeltaTime;

        switch (_currentState)
        {
            case BatState.Sleep:
                UpdateSleepState();
                break;

            case BatState.Fly:
                UpdateFlyState();
                break;

            case BatState.Feint:
                UpdateFeintState();
                break;

            case BatState.Return:
                UpdateReturnState();
                break;

            case BatState.Attack:
                // Attack中はコルーチンで管理するためFixedUpdateでの移動処理は停止
                _rbody.velocity = Vector2.zero;
                break;
        }
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

        // バリアントに基づくダメージ設定
        switch (_variantType)
        {
            case EnemyVariant.Tower:
                _damage = 20;
                break;
            default:
                Debug.LogError($"{this.name}のEnemyVariantが設定されていません。");
                break;
        }

        if (_enemyHP != null)
        {
            _enemyHP.ResetState();
        }

        transform.position = _initialPosition;
        _rbody.velocity = Vector2.zero;

        SetTagImmune(true);
        ChangeState(BatState.Sleep);

        _stateTimer = _sleepCooldown; // Sleep状態に入る前から待機時間をカウントするようにセット

        if (_playerTransform != null)
        {
            _playerPrevPos = _playerTransform.position;
        }
    }

    #endregion

    #region ステート更新処理

    /// <summary>
    /// 待機状態の処理を行います。プレイヤーの接近を検知します。
    /// </summary>
    private void UpdateSleepState()
    {
        if (_stateTimer < _sleepCooldown)
        {
            return;
        }

        Vector3 myPos = transform.position;
        Vector3 playerPos = _playerTransform.position;

        // プレイヤーが自分より下、かつX座標が指定範囲内か判定
        bool isBelow = playerPos.y < myPos.y;
        bool isWithinX = Mathf.Abs(playerPos.x - myPos.x) <= _detectRangeX;

        if (isBelow && isWithinX)
        {
            ChangeState(BatState.Fly);
        }
    }

    /// <summary>
    /// 突撃状態の処理を行います。プレイヤーの予測座標へ向かって飛行します。
    /// </summary>
    private void UpdateFlyState()
    {
        // タイムアウト時間をカウント（フェイント中はカウントされない）
        _flyTimer += Time.fixedDeltaTime;

        // フェイント判定（指定時間が経過し、かつフラグが立っている場合）
        if (_willFeint && _flyTimer >= _feintStartTime)
        {
            ChangeState(BatState.Feint);
            return;
        }

        // 攻撃範囲内のチェック
        float distanceToPlayer = Vector2.Distance(transform.position, _playerTransform.position);
        if (distanceToPlayer <= _attackDistance)
        {
            StartCoroutine(AttackSequence());
            return;
        }

        // タイムアウトチェック
        if (_flyTimer >= _maxFlyDuration)
        {
            ChangeState(BatState.Return);
            return;
        }

        // ハイブリッド予測の更新（ホーミング期間中は予測座標を更新し続ける）
        if (_flyTimer <= _homingDuration)
        {
            _predictedTargetPos = _playerTransform.position + (_playerVelocity * _predictLeadTime);
            // ホーミング期間中は常に目標へ向かう方向を更新
            _flyDirection = (_predictedTargetPos - transform.position).normalized;
        }

        // サイン波でコウモリのような上下の揺れ（羽ばたき）を加える
        float flutterOffset = Mathf.Sin(_flyTimer * _flutterSpeed) * _flutterAmplitude;

        // Xの向きに合わせてスプライトを反転
        if (Mathf.Abs(_flyDirection.x) > 0.01f)
        {
            transform.localScale = new Vector3(
                Mathf.Sign(_flyDirection.x) * Mathf.Abs(transform.localScale.x),
                transform.localScale.y,
                transform.localScale.z
            );
        }

        _rbody.velocity = new Vector2(
            _flyDirection.x * _flySpeed,
            _flyDirection.y * _flySpeed + flutterOffset
        );
    }

    /// <summary>
    /// フェイント状態の処理を行います。進行方向に対して後方へ宙返りする軌道を描きます。
    /// </summary>
    private void UpdateFeintState()
    {
        float t = Mathf.Clamp01(_stateTimer / _feintDuration);

        // 後ろへ宙返りする回転角度を計算
        // 進行方向が右(X>0)なら反時計回り、左(X<0)なら時計回りにすることで、上方向へ引き上げるような後ろ回転になる
        float angle = 360f * t * Mathf.Sign(_preFeintDirection.x);

        // 元の進行方向ベクトルを回転させて円軌道を作る
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
        Vector3 currentDirection = rotation * _preFeintDirection;

        _rbody.velocity = currentDirection * _flySpeed;

        // 回転中もスプライトの向きを進行方向に合わせる
        if (Mathf.Abs(currentDirection.x) > 0.01f)
        {
            transform.localScale = new Vector3(
                Mathf.Sign(currentDirection.x) * Mathf.Abs(transform.localScale.x),
                transform.localScale.y,
                transform.localScale.z
            );
        }

        // 1回転完了したら元の突撃に戻る
        if (t >= 1f)
        {
            _willFeint = false; // 再度フェイントが発動しないようにフラグを折る
            ChangeState(BatState.Fly);
        }
    }

    /// <summary>
    /// 復帰状態の処理を行います。ベジェ曲線を用いて谷なりに初期座標へ戻ります。
    /// </summary>
    private void UpdateReturnState()
    {
        // 算出された復帰時間を使用して t を計算するように変更
        float t = Mathf.Clamp01(_stateTimer / _calculatedReturnDuration);

        // 2次ベジェ曲線を用いた谷なりの軌道計算
        Vector3 p0 = _returnStartPos;
        Vector3 p2 = _initialPosition;
        // 中間点を求め、そこから下方向へオフセットを加えて制御点(p1)とする
        Vector3 p1 = (p0 + p2) / 2f + Vector3.down * _returnCurveDepth;

        // B(t) = (1-t)^2*P0 + 2t(1-t)*P1 + t^2*P2
        float u = 1f - t;
        Vector3 pos = (u * u * p0) + (2f * u * t * p1) + (t * t * p2);

        _rbody.MovePosition(pos);

        // Xの向きに合わせてスプライトを反転（戻る方向を向く）
        float directionX = p2.x - transform.position.x;
        if (Mathf.Abs(directionX) > 0.1f)
        {
            transform.localScale = new Vector3(
                Mathf.Sign(directionX) * Mathf.Abs(transform.localScale.x),
                transform.localScale.y,
                transform.localScale.z
            );
        }

        if (t >= 1f)
        {
            ChangeState(BatState.Sleep);
        }
    }

    #endregion

    #region 攻撃処理

    /// <summary>
    /// プレイヤーに接近した際の攻撃アニメーションと待機処理を管理するコルーチンです。
    /// </summary>
    private IEnumerator AttackSequence()
    {
        ChangeState(BatState.Attack);
        SetTagImmune(false); // 攻撃中はダメージを受ける・与える状態にする

        _animator.SetTrigger(_attackTriggerParam);

        // 指定した攻撃時間待機
        yield return new WaitForSeconds(_attackDuration);

        SetTagImmune(true); // 攻撃終了で再び無敵状態へ
        ChangeState(BatState.Return);
    }

    #endregion

    #region 状態管理・ユーティリティ

    /// <summary>
    /// 現在の行動ステートを変更し、必要なフラグやタイマーの初期化を行います。
    /// </summary>
    /// <param name="newState">移行する新しいステート</param>
    private void ChangeState(BatState newState)
    {
        // 初期状態からFlyへ移行する際にフラグとタイマーをセット
        if (newState == BatState.Fly && _currentState != BatState.Feint)
        {
            _flyTimer = 0f;
            _willFeint = Random.Range(0f, 100f) <= _feintProbability;
        }

        // Feintへ移行する際、その時点の進行方向を保存
        if (newState == BatState.Feint)
        {
            _preFeintDirection = _flyDirection;
        }

        _currentState = newState;
        _stateTimer = 0f;

        // アニメーション状態の更新
        _animator.SetBool(_sleepParam, newState == BatState.Sleep);
        _animator.SetBool(
            _flyParam,
            newState == BatState.Fly || newState == BatState.Return || newState == BatState.Feint
        );

        if (newState == BatState.Return)
        {
            _returnStartPos = transform.position;
            _rbody.velocity = Vector2.zero; // 物理挙動による移動をリセット

            // 現在位置から初期座標までの直線距離と指定速度から、復帰にかかる時間を計算
            float distance = Vector3.Distance(_returnStartPos, _initialPosition);
            _calculatedReturnDuration = distance / _returnSpeed;

            // 距離が近すぎる場合の0割り防止
            if (_calculatedReturnDuration <= 0f)
            {
                _calculatedReturnDuration = 0.1f;
            }
        }
    }

    /// <summary>
    /// 本体のタグと接触ダメージオブジェクトのタグを切り替えます。
    /// </summary>
    /// <param name="isImmune">trueの場合は無敵状態、falseの場合はダメージを受ける状態</param>
    private void SetTagImmune(bool isImmune)
    {
        string targetTag = isImmune
            ? GameConstants.IMMUNE_ENEMY_TAG_NAME
            : GameConstants.DAMAGEABLE_ENEMY_TAG_NAME;
        this.gameObject.tag = targetTag;

        if (_contactDamageObject != null)
        {
            _contactDamageObject.tag = targetTag;
        }
    }

    #endregion

    #region デバッグ描画

    private void OnDrawGizmosSelected()
    {
        // プレイヤー検知範囲の描画（エディタ確認用）
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireCube(
            transform.position - new Vector3(0, 5f, 0),
            new Vector3(_detectRangeX * 2, 10f, 0)
        );
    }

    #endregion
}

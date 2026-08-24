using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// プレイヤーに追従し、攻撃を同期して行うお供キャラ（Hero）のコントローラー
/// </summary>
[RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
public class HeroController : MonoBehaviour
{
    #region インスペクター設定項目

    [Header("参照設定")]
    [SerializeField, Tooltip("プレイヤーの移動制御スクリプト")]
    private Heroin_move _player;

    [SerializeField, Tooltip("同期するお供ロボットの参照")]
    private Robot_move _robot;

    [Header("武器設定")]
    [SerializeField, Tooltip("Heroが振る剣の当たり判定を管理するコントローラー")]
    private HeroBladeController _bladeController;

    [Header("移動設定")]
    [SerializeField, Tooltip("プレイヤーの前方どれくらい離れた位置を目標にするか")]
    private float _forwardOffset = 2.0f;

    [SerializeField, Tooltip("向きを変える際の移動の緩急（小さいほど速い）")]
    private float _turnSmoothTime = 0.3f;

    [Header("壁・接地判定設定")]
    [SerializeField, Tooltip("壁として判定するレイヤー（めり込み防止用）")]
    private LayerMask _wallLayer;

    [SerializeField, Tooltip("地面を壁と誤認しないための光線の高さオフセット（胸の高さ程度）")]
    private float _raycastHeightOffset = 0.5f;

    [SerializeField, Tooltip("壁の手前で止まるためのキャラクターの幅（厚み）")]
    private float _bodyWidthOffset = 0.3f;

    [SerializeField, Tooltip("足元の接地判定位置（中心からのYオフセット）")]
    private float _groundCheckOffsetY = -0.5f;

    [SerializeField, Tooltip("接地判定を行うボックスのサイズ(幅, 高さ)")]
    private Vector2 _groundCheckSize = new Vector2(0.5f, 0.2f);

    [Header("アニメーション閾値設定")]
    [SerializeField, Tooltip("「移動している」と判定するX軸の速度閾値")]
    private float _moveThreshold = 0.1f;

    [SerializeField, Tooltip("「ジャンプ/落下している」と判定するY軸の速度閾値")]
    private float _verticalThreshold = 0.1f;

    [Header("攻撃時間・コンボ設定")]
    [SerializeField, Tooltip("1段目のアニメーション再生時間（実時間）")]
    private float _attack1Duration = 0.3f;

    [SerializeField, Tooltip("2段目のアニメーション再生時間（実時間）")]
    private float _attack2Duration = 0.4f;

    [SerializeField, Tooltip("1段目終了後、2段目の入力（コンボ）を受け付ける猶予時間")]
    private float _comboInputWindow = 0.4f;

    [SerializeField, Tooltip("コンボ終了、または途切れた後のクールダウン時間")]
    private float _attackCooldown = 1.0f;

    [Header("制御設定")]
    [SerializeField, Tooltip("ゲーム開始時からHeroの制御（追従・攻撃・アニメーション）を有効にするか")]
    private bool _isControlEnabled = false;

    #endregion

    #region 定数と内部変数

    // 定数
    private const float DEFAULT_WALK_SPEED = 8.0f; // アニメーション速度を計算するための基準歩行速度
    private const float BASE_ATTACK_ANIM_LENGTH = 1.0f; // 元の攻撃アニメーションの正規化された長さ（1秒）

    // コンポーネントキャッシュ
    private Animator _animator;
    private LayerMask _groundLayer; // AwakeでGameConstantsから取得する接地判定レイヤー

    // AnimatorのHash値（文字列検索の負荷を避けるためキャッシュ）
    private readonly int _hashSpeed = Animator.StringToHash("Speed");
    private readonly int _hashVerticalSpeed = Animator.StringToHash("VerticalSpeed");
    private readonly int _hashIsGrounded = Animator.StringToHash("IsGrounded");
    private readonly int _hashWalkAnimSpeed = Animator.StringToHash("WalkAnimSpeed");
    private readonly int _hashAttack1 = Animator.StringToHash("Attack1");
    private readonly int _hashAttack2 = Animator.StringToHash("Attack2");
    private readonly int _hashIdleTrigger = Animator.StringToHash("IdleTrigger");
    private readonly int _hashAttack1Speed = Animator.StringToHash("Attack1Speed");
    private readonly int _hashAttack2Speed = Animator.StringToHash("Attack2Speed");
    private readonly int _hashNormalIdleState = Animator.StringToHash(
        "Base Layer.Hero_Normal_Idle"
    );

    // 状態管理フラグ
    private bool _isNormalIdleRestorePending; // 非アクティブ中に要求された通常Idleへの復帰を保留する
    private bool _isGrounded; // 現在接地しているかどうか

    // 攻撃関連の状態管理
    private bool _isAttacking = false; // 攻撃中かどうかのフラグ（アニメーション上書き防止に使用）
    private bool _isWaitingForCombo = false; // 2段目のコンボ入力を待機中かどうか
    private bool _comboTriggered = false; // 待機時間中にコンボ入力が成立したかどうか
    private float _cooldownTimer = 0f; // 攻撃後のクールダウンを計測するタイマー
    private int _currentAttack1Damage = 0; // 外部から設定される1段目の攻撃力
    private int _currentAttack2Damage = 0; // 外部から設定される2段目の攻撃力

    // 移動計算用の内部変数
    private float _currentOffset; // プレイヤーからの現在の相対X座標
    private float _offsetVelocity; // SmoothDamp（滑らかな移動）用の内部速度変数
    private float _previousX; // 前フレームのX座標（速度計算用）
    private float _previousY; // 前フレームのY座標（速度計算用）
    private float _smoothedAnimSpeedX; // アニメーション用の平滑化されたX方向の速度
    #endregion

    #region Unity 標準ライフサイクル

    private void Awake()
    {
        _animator = GetComponent<Animator>();

        // インスペクターで設定せず、定数を用いて接地判定用のレイヤーを取得する
        _groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );
    }

    private void OnEnable()
    {
        // ロボットの攻撃イベントを購読する
        if (_robot != null)
        {
            _robot.OnRobotAttackExecuted += HandleRobotAttack;
        }

        if (_isControlEnabled)
        {
            _isNormalIdleRestorePending = true;
            TryRestoreNormalIdle();
        }
    }

    private void OnDisable()
    {
        // メモリリークを防ぐため、オブジェクト非表示時にイベント購読を解除する
        if (_robot != null)
        {
            _robot.OnRobotAttackExecuted -= HandleRobotAttack;
        }
    }

    private void Start()
    {
        if (_player != null)
        {
            // スタート時にプレイヤーの現在位置へ即座に移動する
            transform.position = _player.transform.position;

            // 初期状態のオフセット（立ち位置）を設定
            _currentOffset = _player.rightFlag ? _forwardOffset : -_forwardOffset;

            // 速度計算が暴れないよう、初期座標を記録しておく
            _previousX = transform.position.x;
            _previousY = transform.position.y;
        }
        else
        {
            Debug.LogWarning(
                "プレイヤーの参照が設定されていないため、初期位置の同期をスキップしました。"
            );
        }

        if (_robot == null)
        {
            Debug.LogWarning(
                "Robot_moveの参照が設定されていないため、攻撃の同期機能が動作しません。"
            );
        }
    }

    private void Update()
    {
        if (!_isControlEnabled)
            return;

        // クールダウンタイマーの更新
        if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.deltaTime;
        }
    }

    private void LateUpdate()
    {
        // 制御が無効、またはプレイヤーが存在しない場合は追従処理を行わない
        if (!_isControlEnabled || _player == null)
        {
            return;
        }

        // 1. 向きの同期（SpriteRenderer.flipXではなく、Y軸を回転させる）
        bool isPlayerRight = _player.rightFlag;
        float targetRotationY = isPlayerRight ? 0f : 180f;
        transform.localRotation = Quaternion.Euler(0f, targetRotationY, 0f);

        // 2. 目標オフセットの計算
        float targetOffset = isPlayerRight ? _forwardOffset : -_forwardOffset;

        // 3. 現在のオフセットから目標のオフセットへ、バネのように緩急をつけて遷移させる
        _currentOffset = Mathf.SmoothDamp(
            _currentOffset,
            targetOffset,
            ref _offsetVelocity,
            _turnSmoothTime
        );

        // SmoothDamp特有の微小な揺れ（ジッター）を完全に止めるためのスナップ処理
        if (Mathf.Abs(_currentOffset - targetOffset) < 0.01f)
        {
            _currentOffset = targetOffset;
        }

        // 4. 実際の目標座標を計算（Y座標はプレイヤーに完全同期）
        float targetX = _player.pos.x + _currentOffset;
        float targetY = _player.pos.y;

        // 5. 壁へののめり込み防止処理 (Raycast)
        // 床を壁として誤認しないよう、Rayの起点を少し上に設定する
        Vector2 origin = new Vector2(_player.pos.x, _player.pos.y + _raycastHeightOffset);
        Vector2 direction = _currentOffset > 0 ? Vector2.right : Vector2.left;
        float distance = Mathf.Abs(_currentOffset);

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, distance, _wallLayer);

        if (hit.collider != null)
        {
            // 壁があった場合、壁の衝突地点からキャラクターの厚み分だけ手前にX座標を書き換える
            targetX =
                hit.point.x + (direction == Vector2.right ? -_bodyWidthOffset : _bodyWidthOffset);
        }

        // 6. 最終的な座標の適用
        transform.position = new Vector2(targetX, targetY);

        // 7. 接地判定とアニメーションの更新
        CheckGroundStatus();
        UpdateAnimations();
    }

    #endregion

    #region 外部公開 API

    /// <summary>
    /// このHeroコントローラーによる制御（追従・攻撃・アニメーション）を有効/無効にする
    /// </summary>
    /// <param name="isEnabled">trueで制御開始、falseで制御停止</param>
    public void SetControlEnabled(bool isEnabled)
    {
        _isControlEnabled = isEnabled;

        if (isEnabled)
        {
            _isNormalIdleRestorePending = true;
            TryRestoreNormalIdle();
        }
        else
        {
            _isNormalIdleRestorePending = false;
        }
    }

    /// <summary>
    /// Animatorが利用可能になった時点で、通常の待機状態への復帰を適用する
    /// </summary>
    private void TryRestoreNormalIdle()
    {
        if (!_isNormalIdleRestorePending || !gameObject.activeInHierarchy)
        {
            return;
        }

        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
        }

        if (_animator == null || !_animator.isActiveAndEnabled)
        {
            return;
        }

        // GlobalSkip中にAnimatorの評価を挟まず設定されたTriggerが、
        // 通常Idleへ戻した直後に再発火しないよう、未消費のTriggerを解除する
        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger)
            {
                _animator.ResetTrigger(parameter.nameHash);
            }
        }

        // 遷移条件に依存せず、特殊な待機状態などから通常の待機状態へ戻す
        _animator.Play(_hashNormalIdleState, 0, 0f);
        _isNormalIdleRestorePending = false;
    }

    /// <summary>
    /// 外部（Robotの武器変更通知など）から呼び出され、コンボ各段の攻撃力を設定する
    /// </summary>
    /// <param name="attack1Damage">1段目の攻撃力</param>
    /// <param name="attack2Damage">2段目の攻撃力</param>
    public void SetupDamage(int attack1Damage, int attack2Damage)
    {
        _currentAttack1Damage = attack1Damage;
        _currentAttack2Damage = attack2Damage;
    }

    #endregion

    #region 攻撃処理

    /// <summary>
    /// Robot_move側で攻撃（剣・弾）が実行された際にイベント経由で呼び出される
    /// </summary>
    private void HandleRobotAttack()
    {
        if (!_isControlEnabled)
            return;

        // クールダウン中なら入力を無視する
        if (_cooldownTimer > 0f)
        {
            return;
        }

        if (!_isAttacking)
        {
            // 攻撃中でない場合は1段目の攻撃を開始
            StartCoroutine(AttackSequence());
        }
        else if (_isWaitingForCombo)
        {
            // すでに攻撃中で、かつ2段目の入力受付時間中ならコンボ成立フラグを立てる
            _comboTriggered = true;
        }
    }

    /// <summary>
    /// 攻撃アニメーションの再生時間と、コンボの受付時間を制御するコルーチン
    /// </summary>
    private IEnumerator AttackSequence()
    {
        _isAttacking = true;
        _comboTriggered = false;

        // --- 1段目の攻撃処理 ---

        // インスペクターで設定した時間でアニメーションを終わらせるための再生速度倍率を計算（0除算防止）
        float speedMulti1 = BASE_ATTACK_ANIM_LENGTH / Mathf.Max(0.01f, _attack1Duration);
        _animator.SetFloat(_hashAttack1Speed, speedMulti1);

        // 剣の判定スクリプトに1段目のダメージを設定
        if (_bladeController != null)
        {
            _bladeController.Setup(_currentAttack1Damage);
        }

        // アニメーションを発動
        _animator.SetTrigger(_hashAttack1);

        // 1段目のアニメーション再生時間分待機
        yield return new WaitForSeconds(_attack1Duration);

        // --- 2段目（コンボ）の入力受付処理 ---

        _isWaitingForCombo = true;
        float timer = 0f;

        // 設定された猶予時間だけ待機し、その間に _comboTriggered が true になるか監視する
        while (timer < _comboInputWindow)
        {
            if (_comboTriggered)
            {
                break; // 入力があれば即座にループを抜けて2段目へ移行
            }
            timer += Time.deltaTime;
            yield return null;
        }

        _isWaitingForCombo = false;

        // --- 2段目の攻撃、または攻撃終了処理 ---

        if (_comboTriggered)
        {
            // 2段目用の再生速度倍率を計算
            float speedMulti2 = BASE_ATTACK_ANIM_LENGTH / Mathf.Max(0.01f, _attack2Duration);
            _animator.SetFloat(_hashAttack2Speed, speedMulti2);

            // 剣の判定スクリプトに2段目のダメージを設定
            if (_bladeController != null)
            {
                _bladeController.Setup(_currentAttack2Damage);
            }

            // アニメーションを発動
            _animator.SetTrigger(_hashAttack2);

            // 2段目のアニメーション再生時間分待機
            yield return new WaitForSeconds(_attack2Duration);

            // 2段目終了時にIdleTriggerを送信して確実に待機状態に戻す
            _animator.SetTrigger(_hashIdleTrigger);
        }
        else
        {
            // 猶予時間内に入力がなくコンボが成立しなかった場合、即座に待機状態に戻す
            _animator.SetTrigger(_hashIdleTrigger);
        }

        // 攻撃フェーズ終了。クールダウンタイマーを開始し、移動アニメーションを許可する
        _isAttacking = false;
        _cooldownTimer = _attackCooldown;
    }

    #endregion

    #region 移動・接地・アニメーション制御

    /// <summary>
    /// 足元にOverlapBoxを発生させ、物理演算による正確な接地判定を行う
    /// </summary>
    private void CheckGroundStatus()
    {
        Vector2 groundCheckPos = new Vector2(
            transform.position.x,
            transform.position.y + _groundCheckOffsetY
        );

        _isGrounded = Physics2D.OverlapBox(groundCheckPos, _groundCheckSize, 0f, _groundLayer);
    }

    /// <summary>
    /// 前フレームとの座標差分から速度を計算し、Animatorのパラメータを更新する
    /// </summary>
    private void UpdateAnimations()
    {
        if (Time.deltaTime <= 0f)
            return;

        // 実際の移動速度(単位/秒)を計算
        float rawVelocityX = (transform.position.x - _previousX) / Time.deltaTime;
        float velocityY = (transform.position.y - _previousY) / Time.deltaTime;

        // 実行タイミングのズレによる速度の乱高下を滑らかにする
        _smoothedAnimSpeedX = Mathf.Lerp(
            _smoothedAnimSpeedX,
            Mathf.Abs(rawVelocityX),
            Time.deltaTime * 15f
        );

        // 攻撃中は歩行・ジャンプなどのアニメーション上書きを行わない
        if (!_isAttacking)
        {
            // X軸のアニメーション判定
            if (_smoothedAnimSpeedX > _moveThreshold)
            {
                _animator.SetFloat(_hashSpeed, _smoothedAnimSpeedX);

                // 移動速度に応じて歩行アニメーションの再生速度を動的に変化させる
                float animSpeedMultiplier = _smoothedAnimSpeedX / DEFAULT_WALK_SPEED;
                _animator.SetFloat(_hashWalkAnimSpeed, animSpeedMultiplier);
            }
            else
            {
                _animator.SetFloat(_hashSpeed, 0f);
                _animator.SetFloat(_hashWalkAnimSpeed, 1.0f);
            }

            // Y軸のアニメーション判定
            _animator.SetBool(_hashIsGrounded, _isGrounded);

            if (Mathf.Abs(velocityY) > _verticalThreshold)
            {
                _animator.SetFloat(_hashVerticalSpeed, velocityY);
            }
            else
            {
                _animator.SetFloat(_hashVerticalSpeed, 0f);
            }
        }

        // 次のフレームのために現在の座標を記録
        _previousX = transform.position.x;
        _previousY = transform.position.y;
    }

    #endregion

    #region Animation Event 用コールバック

    /// <summary>
    /// AnimatorのAnimation Eventから呼び出され、剣の当たり判定をONにする
    /// </summary>
    public void OnEnableBlade()
    {
        if (_bladeController != null)
        {
            _bladeController.EnableBlade();
        }
    }

    /// <summary>
    /// AnimatorのAnimation Eventから呼び出され、剣の当たり判定をOFFにする
    /// </summary>
    public void OnDisableBlade()
    {
        if (_bladeController != null)
        {
            _bladeController.DisableBlade();
        }
    }

    #endregion

    #region デバッグ用表示

    private void OnDrawGizmosSelected()
    {
        // エディタ上で選択した際に、接地判定のボックスを緑色のワイヤーフレームで可視化する
        Gizmos.color = Color.green;
        Vector2 groundCheckPos = new Vector2(
            transform.position.x,
            transform.position.y + _groundCheckOffsetY
        );
        Gizmos.DrawWireCube(groundCheckPos, _groundCheckSize);
    }

    #endregion
}

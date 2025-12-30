using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class DesertTempleBossMoveController : MonoBehaviour
{
    [Header("移動の設定")]
    [Tooltip("移動速度")]
    [SerializeField]
    private float moveSpeedX = 3.0f;

    [Header("移動範囲の設定(必須)")]
    [SerializeField]
    private float leftBound = 0;

    [SerializeField]
    private float rightBound = 0;

    [Space(50)]
    [Header("ゲームオブジェクト設定")]
    [SerializeField]
    private GameObject rightArmObject; // 右腕のオブジェクト

    [SerializeField]
    private GameObject leftArmObject; // 左腕のオブジェクト

    [SerializeField]
    private GameObject haloObject; // 光輪オブジェクト

    [Tooltip("光輪の座標のオフセット(左向き時)")]
    [SerializeField]
    private Vector2 haloOffset = new Vector2(0.25f, -0.2f);

    [Header("浮遊の設定")]
    [Tooltip("浮遊の上下幅")]
    [SerializeField]
    private float floatAmplitude = 0.5f;

    [Tooltip("浮遊の1周期にかかる時間")]
    [SerializeField]
    private float floatDuration = 2.0f;

    // --- 内部変数 ---
    private bool rightFlag = false;
    private float initialY; // 初期のY座標
    private bool isMovingLeft = false; // trueなら左移動、falseなら右移動
    private bool isHorizontalMoveActive = false; // 横移動が有効か
    private Tweener floatTween; // 浮遊アニメーション管理用
    private float haloRotationSpeed = 0f; // 光輪の回転速度
    private float DEFAULT_HALO_ROTATION_SPEED = 10f; // デフォルトの光輪回転速度
    private bool isHaloRotatingClockwise = false; // true: 時計回り(CW), false: 反時計回り(CCW)

    // --- 内部参照 ---
    private SpriteRenderer bodySpriteRenderer;
    private Animator bodyAnimator;

    // --- 外部参照 ---
    private SpriteRenderer rightArmSpriteRenderer;
    private SpriteRenderer leftArmSpriteRenderer;
    private Transform haloTransform;
    private Transform playerTransform;
    private Animator rightArmAnimator;
    private Animator leftArmAnimator;

    private void Awake()
    {
        if (leftArmObject != null)
        {
            leftArmSpriteRenderer = leftArmObject.GetComponent<SpriteRenderer>();
            leftArmAnimator = leftArmObject.GetComponent<Animator>();
        }

        if (rightArmObject != null)
        {
            rightArmSpriteRenderer = rightArmObject.GetComponent<SpriteRenderer>();
            rightArmAnimator = rightArmObject.GetComponent<Animator>();
        }

        if (haloObject != null)
        {
            haloTransform = haloObject.transform;
        }

        bodySpriteRenderer = GetComponent<SpriteRenderer>();
        bodyAnimator = GetComponent<Animator>();
    }

    private void Start()
    {
        ResetState();
    }

    public void ResetState()
    {
        if (playerTransform == null)
        {
            playerTransform = GameObject
                .FindGameObjectWithTag(GameConstants.PlayerTagName)
                ?.transform;
            if (playerTransform == null)
            {
                Debug.LogError($"{this.name}はPlayerTransformを見つけられませんでした");
                return;
            }
        }

        // 初期向きを設定
        rightFlag = IsTargetToRight();
        UpdateFacingDirection(rightFlag);
        isMovingLeft = rightFlag; // 最初はプレイヤーの方向へ向かって移動開始

        // アニメーターを初期化
        leftArmAnimator.SetTrigger("IdleTrigger");
        rightArmAnimator.SetTrigger("IdleTrigger");

        // 変数を初期化
        initialY = transform.position.y;
        haloRotationSpeed = DEFAULT_HALO_ROTATION_SPEED;
        isHaloRotatingClockwise = true;

        // 状態のリセット
        StopFloating();
        SetHaloRotation(DEFAULT_HALO_ROTATION_SPEED, true);
        // 移動と浮遊を開始
        StartFloating();

        // Debug用
        isHorizontalMoveActive = true; // 横移動を有効化
    }

    private void FixedUpdate()
    {
        // 敵の動きがポーズされているかどうかを確認
        if (TimeManager.instance.isEnemyMovePaused)
        {
            // ポーズ中はTweenも一時停止させる
            if (floatTween != null && floatTween.IsPlaying())
                floatTween.Pause();
            return;
        }
        else
        {
            // ポーズ解除中はTweenを再開させる
            if (floatTween != null && !floatTween.IsPlaying())
                floatTween.Play();
        }

        // --- 追横移動の処理 ---
        if (isHorizontalMoveActive)
        {
            UpdateHorizontalMove();
        }

        if (haloTransform != null && haloRotationSpeed > 0f)
        {
            // 時計回り(true)の場合はZ軸マイナス方向、反時計回り(false)の場合はプラス方向
            float directionMultiplier = isHaloRotatingClockwise ? -1f : 1f;

            // 自分が向いている向き(flipX)の影響を受けないよう、Transform.Rotateを使用
            float angle = directionMultiplier * haloRotationSpeed * Time.deltaTime;
            haloTransform.Rotate(0, 0, angle);
        }

        bool isTargetCurrentlyRight = IsTargetToRight();
        if (rightFlag != isTargetCurrentlyRight)
        {
            rightFlag = isTargetCurrentlyRight;
            UpdateFacingDirection(rightFlag); //すべてのパーツの向きを更新
        }
    }

    /// <summary>
    /// x座標を基準とした横移動を行う
    /// </summary>
    private void UpdateHorizontalMove()
    {
        float currentX = transform.position.x;
        float nextX = currentX + (moveSpeedX * (isMovingLeft ? -1 : 1) * Time.deltaTime);

        // 範囲外に出そうになったら方向転換
        if (nextX >= rightBound)
        {
            nextX = rightBound;
            isMovingLeft = true; // 左へ
        }
        else if (nextX <= leftBound)
        {
            nextX = leftBound;
            isMovingLeft = false; // 右へ
        }

        transform.position = new Vector3(nextX, transform.position.y, transform.position.z);
    }

    /// <summary>
    /// 上下の浮遊アニメーションを開始します
    /// </summary>
    private void StartFloating()
    {
        // 既に動いている場合は何もしない、またはリセットする
        if (floatTween != null && floatTween.IsActive())
            return;

        // 現在のY座標から開始（あるいは初期位置基準にするなら initialY を使用）
        // ここでは initialY を基準に浮遊させる
        floatTween = transform
            .DOMoveY(initialY + floatAmplitude, floatDuration)
            .SetEase(Ease.InOutSine) // ふわふわした動き
            .SetLoops(-1, LoopType.Yoyo) // 往復ループ
            .SetLink(gameObject); // オブジェクト削除時にTweenも破棄
    }

    /// <summary>
    /// 上下の浮遊アニメーションを停止します
    /// </summary>
    private void StopFloating()
    {
        if (floatTween != null)
        {
            floatTween.Kill();
            floatTween = null;
        }
    }

    /// <summary>
    /// 光輪の回転を設定します
    /// </summary>
    /// <param name="speed">回転速度 (度/秒)</param>
    /// <param name="isClockwise">true: 時計回り, false: 反時計回り</param>
    public void SetHaloRotation(float speed, bool isClockwise)
    {
        haloRotationSpeed = speed;
        isHaloRotatingClockwise = isClockwise;
    }

    /// <summary>
    /// 自身の位置からプレイヤーへのベクトルを取得します
    /// </summary>
    private Vector2 GetVectorToPlayer()
    {
        if (playerTransform != null)
        {
            return (Vector2)playerTransform.position - (Vector2)transform.position;
        }
        return Vector2.zero;
    }

    /// <summary>
    /// 対象が自分より右側にいるか判定します
    /// </summary>
    /// <param name="dir">対象への方向ベクトル</param>
    /// <returns>右側にいるならtrue、左側ならfalse</returns>
    private bool IsTargetToRight()
    {
        Vector2 dir = GetVectorToPlayer();
        return dir.x > 0;
    }

    /// <summary>
    /// スプライトの向きを更新します
    /// </summary>
    /// <param name="isFacingRight">右を向いているか</param>
    private void UpdateFacingDirection(bool isFacingRight)
    {
        //本体の向きを変更
        if (bodySpriteRenderer != null)
        {
            bodySpriteRenderer.flipX = isFacingRight;
        }

        //腕の向きを変更
        if (leftArmSpriteRenderer != null)
        {
            leftArmSpriteRenderer.flipX = isFacingRight;
        }
        if (rightArmSpriteRenderer != null)
        {
            rightArmSpriteRenderer.flipX = isFacingRight;
        }

        //光輪の位置を調整
        if (haloTransform != null)
        {
            if (isFacingRight)
            {
                haloTransform.localPosition = new Vector2(-haloOffset.x, haloOffset.y);
            }
            else
            {
                haloTransform.localPosition = haloOffset;
            }
        }
    }

    private void OnDestroy()
    {
        // 安全のためTweenを破棄
        if (floatTween != null)
        {
            floatTween.Kill();
        }
    }

    private void OnDrawGizmos()
    {
        // --- 移動範囲 ---
        Gizmos.color = new Color(1f, 0f, 0f, 0.15f); // 移動範囲は半透明の赤
        Vector3 center = new Vector3(
            (leftBound + rightBound) / 2f,
            transform.position.y - 0.13f / 2f,
            transform.position.z
        );
        Vector3 size = new Vector3(rightBound - leftBound, 13.0f, 0.1f);
        Gizmos.DrawCube(center, size);
    }
}

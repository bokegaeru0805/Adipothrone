using System.Collections.Generic;
using DG.Tweening;
using NaughtyAttributes;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(ContactDamageController))]
public class DustDevilActiveMoveController : MonoBehaviour, IEnemyResettable
{
    private const float MAX_GROUND_HEIGHT = 12f; // 移動が許可される地面からの最大高度

    [Header("敵のタイプ")]
    [SerializeField]
    private EnemyVariant variantType = EnemyVariant.None;

    [Header("設定項目")]
    [SerializeField]
    private EnemyActivator activator = null; // 親のEnemyActivatorコンポーネント

    [Header("移動設定")]
    [Tooltip("1回の移動距離")]
    [SerializeField]
    private float moveDistance = 8.0f;

    [Header("待機・移動時間の設定")]
    [Tooltip("移動にかかる時間")]
    [SerializeField]
    private float moveDuration = 1.0f;

    [Tooltip("移動前の待機時間")]
    [SerializeField]
    private float waitBeforeMove = 1.5f;

    [Tooltip("移動後の待機時間")]
    [SerializeField]
    private float waitInterval = 2.0f;

    [Header("浮遊アニメーション設定")]
    [Tooltip("上下に揺れる幅")]
    [SerializeField]
    private float floatAmount = 0.5f;

    [Tooltip("1回の揺れ（片道）にかかる時間")]
    [SerializeField]
    private float floatDuration = 1.0f;

    [Header("移動範囲の設定")]
    [Tooltip("手動で横移動範囲を設定するかどうか")]
    [SerializeField]
    private bool isUseManualBounds = false;

    [SerializeField, ShowIf(nameof(isUseManualBounds))]
    private float leftBound;

    [SerializeField, ShowIf(nameof(isUseManualBounds))]
    private float rightBound;

    // 敵の種類を定義
    private enum EnemyVariant
    {
        None = 0,
        Desert = 1,
    }

    private float stateChangeTimer = 0f; // 状態遷移用のタイマー
    private int damage = 0; // 攻撃力
    private LayerMask groundLayer;
    private Animator animator;
    private EnemyHealth enemyHP;
    private ContactDamageController contactDamageController;
    private CriWare.Assets.CriAtomSePlayer sePlayer;

    private Tween floatingTween = null;
    private Tween moveTween = null;

    private enum DustDevilActiveState
    {
        Idle,
        PreparingToAttack,
        Attacking,
    }

    private DustDevilActiveState currentState = DustDevilActiveState.Idle;

    // 8方向のベクトル定義
    private readonly Vector2[] directions = new Vector2[]
    {
        Vector2.up,
        Vector2.down,
        Vector2.left,
        Vector2.right,
        new Vector2(1, 1).normalized, // 右上
        new Vector2(1, -1).normalized, // 右下
        new Vector2(-1, 1).normalized, // 左上
        new Vector2(
            -1,
            -1
        ).normalized // 左下
        ,
    };

    private void Awake()
    {
        groundLayer = LayerMask.GetMask(GameConstants.PHYSICS_LAYER_NAME_GROUND); // Groundレイヤーを取得

        switch (variantType)
        {
            case EnemyVariant.Desert:
                //TODO:攻撃力を設定
                // damage = 23;
                break;
            default:
                Debug.LogError($"{this.name}のEnemyVariantが設定されていません。");
                break;
        }

        if (activator == null)
        {
            activator = GetComponentInParent<EnemyActivator>();
            if (activator == null && !isUseManualBounds)
            {
                Debug.LogError(
                    $"{this.name}の親にEnemyActivatorが見つかりませんでした。移動範囲の設定を手動で行ってください。"
                );
            }
        }

        sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
        animator = GetComponent<Animator>();
        enemyHP = this.GetComponent<EnemyHealth>();
        contactDamageController = GetComponent<ContactDamageController>();
    }

    private void Start()
    {
        sePlayer.player.AttachFader();
        ResetState();
    }

    public void ResetState()
    {
        enemyHP.ResetState(); // 自分のHPをリセット
        contactDamageController?.SetNormalDamage(damage); // 攻撃力をリセット
        stateChangeTimer = 0f; // タイマーをリセット
        tag = GameConstants.IMMUNE_ENEMY_TAG_NAME; // タグをリセット
        currentState = DustDevilActiveState.Idle; // 初期状態をIdleに設定

        animator.SetTrigger("IdleTrigger"); // アニメーションをIdleに設定
        sePlayer.Play(SE_Field.WindGust_weak); //弱い風の音を再生

        if (!isUseManualBounds) // 自動設定モードの場合
        {
            if (activator == null)
            {
                Debug.LogError(
                    $"{this.name}の親にEnemyActivatorが見つかりませんでした。移動範囲の自動設定は行えません。"
                );
                return;
            }

            // activatorが持つCollider2Dの境界を取得する
            var activatorCollider = activator.GetComponent<Collider2D>();
            if (activatorCollider != null)
            {
                // Colliderのワールド空間での左端と右端を取得
                float activatorLeftBound = activatorCollider.bounds.min.x;
                float activatorRightBound = activatorCollider.bounds.max.x;

                // 移動範囲を設定
                leftBound = activatorLeftBound;
                rightBound = activatorRightBound;
            }
        }

        // 初期位置を移動範囲内のランダムな位置に設定
        Vector2 startPos = transform.position;
        transform.position = new Vector2(
            Random.Range(leftBound, rightBound),
            startPos.y + (moveDistance - GetDistanceToGround())
        );

        // 浮遊アニメーションを開始
        SetFloating(true);
    }

    /// <summary>
    /// 待機中の「ふわふわ浮く」アニメーションを制御します。
    /// </summary>
    /// <param name="isFloating">true: 浮遊開始, false: 浮遊停止（元の高さに戻す）</param>
    private void SetFloating(bool isFloating)
    {
        // 重複実行を防ぐため、既に動いている浮遊Tweenがあれば破棄する
        if (floatingTween != null)
        {
            floatingTween.Kill();
            floatingTween = null;
        }

        if (isFloating)
        {
            // 現在位置から少し上へ移動するTweenを作成
            floatingTween = transform
                .DOLocalMoveY(this.transform.position.y + floatAmount, floatDuration)
                .SetEase(Ease.InOutSine) // ふわっとした動き
                .SetLoops(-1, LoopType.Yoyo) // 行って戻ってを無限ループ
                .SetLink(gameObject); // 安全対策
        }
        else
        {
            // 浮遊を停止する際は、パッと止めるのではなく、少し時間をかけて元の位置へ戻す
            // これによりアニメーションの切り替わりが滑らかになる
            transform.DOMoveY(this.transform.position.y, 0.2f).SetEase(Ease.OutSine);
        }
    }

    /// <summary>
    /// 現在位置から真下に向かってRayを飛ばし、地面までの距離を計測します。
    /// </summary>
    /// <returns>地面までの距離 (検出できない場合は探索最大距離)</returns>
    private float GetDistanceToGround()
    {
        Vector2 origin = transform.position;

        // moveDistanceの2倍の長さまで下方向を探索
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, moveDistance * 2, groundLayer);

        if (hit.collider != null)
        {
            // 現在位置Y - 地面位置Y = 地面までの距離
            return origin.y - hit.point.y;
        }
        else
        {
            // 地面が見つからない＝空中にいると判断し、最大値を返す
            return moveDistance * 2;
        }
    }

    private void FixedUpdate()
    {
        // --- 1. ポーズ機能の制御 ---
        // TimeManagerなどが管理するポーズフラグを確認
        if (TimeManager.instance.isEnemyMovePaused)
        {
            // 動いている最中なら一時停止する
            if (moveTween.IsActive() && moveTween.IsPlaying())
            {
                moveTween.Pause();
            }
            return; // ポーズ中はこれ以降の処理をしない
        }
        else
        {
            // ポーズ解除時、停止していたTweenがあれば再開する
            if (moveTween.IsActive() && !moveTween.IsPlaying())
            {
                moveTween.Play();
            }
        }

        switch (currentState)
        {
            case DustDevilActiveState.Idle:
                stateChangeTimer += Time.fixedDeltaTime;
                if (stateChangeTimer >= waitInterval)
                {
                    stateChangeTimer = 0f;
                    SetFloating(false); // 移動開始時に浮遊アニメーションを停止
                    TryMove(); // 移動処理を開始
                }
                break;
            // 以下の状態はDOTweenのSequenceが自動制御するため、ここでは特に処理しない
            case DustDevilActiveState.PreparingToAttack:
            case DustDevilActiveState.Attacking:
                // 安全策：もし何らかの理由でTweenが消滅して動きが止まった場合、Idleに戻す処理を入れても良い
                if (!moveTween.IsActive())
                {
                    // 異常系からの復帰が必要な場合はここに記述
                    // currentState = DustDevilActiveState.Idle;
                }
                break;
        }
    }

    /// <summary>
    /// 次の移動先を決定し、回転・待機・移動の一連の動作（Sequence）を作成して実行します。
    /// </summary>
    private void TryMove()
    {
        // --- 1. 移動先の決定ロジック ---
        Vector2 currentPos = transform.position;
        List<Vector2> validTargets = new List<Vector2>();

        float height = GetDistanceToGround();

        // 高度が高すぎる場合は、強制的に「下」へ降りる動きのみを候補にする
        if (height >= moveDistance * 2 || height >= MAX_GROUND_HEIGHT)
        {
            validTargets.Add(currentPos + (Vector2.down * moveDistance));
        }
        else
        {
            // 通常時：8方向すべてについて、移動先が範囲内かチェック
            foreach (Vector2 dir in directions)
            {
                Vector2 targetPos = currentPos + (dir * moveDistance);
                if (IsWithinBounds(targetPos))
                {
                    validTargets.Add(targetPos);
                }
            }
        }

        // --- 2. 移動アニメーションの構築 ---
        if (validTargets.Count > 0)
        {
            // 候補の中からランダムに1つ選ぶ
            int randomIndex = Random.Range(0, validTargets.Count);
            Vector2 chosenTarget = validTargets[randomIndex];

            // 現在位置からターゲットへの角度を計算
            float angle =
                Mathf.Atan2(chosenTarget.y - currentPos.y, chosenTarget.x - currentPos.x)
                * Mathf.Rad2Deg;

            // DOTweenのSequenceを作成（一連のアニメーションの入れ物）
            Sequence moveSequence = DOTween.Sequence();

            // 予備動作開始
            moveSequence.AppendCallback(() =>
            {
                animator.SetTrigger("ChargeTrigger"); // チャージアニメ再生
                currentState = DustDevilActiveState.PreparingToAttack; // ステート変更
            });

            // 回転（waitBeforeMove の時間を使って回転する）
            moveSequence.Append(
                transform.DORotate(new Vector3(0, 0, angle), waitBeforeMove).SetEase(Ease.InOutCirc)
            );

            // 攻撃開始直前の処理
            moveSequence.AppendCallback(() =>
            {
                this.tag = GameConstants.DAMAGEABLE_ENEMY_TAG_NAME; // 攻撃を受けるタグに変更
                animator.SetTrigger("AttackTrigger"); // 突進アニメ再生
                currentState = DustDevilActiveState.Attacking; // ステート変更
                sePlayer.player.SetFadeOutTime((int)(moveDuration * 1000)); // ミリ秒単位でフェードアウト時間を設定
                sePlayer.Play(SE_Field.WindGust_strong); // 強い風の音を再生
            });

            // 実際の移動
            moveSequence.Append(transform.DOMove(chosenTarget, moveDuration).SetEase(Ease.OutQuad));

            // 終了処理（移動完了時）
            moveSequence.OnComplete(() =>
            {
                this.tag = GameConstants.IMMUNE_ENEMY_TAG_NAME; // 無敵タグに戻す
                animator.SetTrigger("IdleTrigger"); // 待機アニメへ
                currentState = DustDevilActiveState.Idle; // ステートをIdleへ
                sePlayer.player.ResetFaderParameters(); // フェーダーパラメータをリセット
                sePlayer.player.SetFadeInTime(100); //ミリ秒単位でフェードイン時間を設定
                sePlayer.Play(SE_Field.WindGust_weak); // 弱い風の音を再生
                SetFloating(true); // ふわふわ浮遊を再開
            });

            // 作成したシーケンスを保存（ポーズ機能などで参照するため）
            moveTween = moveSequence;
        }
        else
        {
            // 移動先が見つからなかった場合（角にハマった等）
            Debug.LogWarning("移動可能な方向がありません。");

            // ステートがロックされないよう、即座にIdleに戻して浮遊を再開する
            currentState = DustDevilActiveState.Idle;
            SetFloating(true);
        }
    }

    /// <summary>
    /// 指定した位置が移動可能範囲内かどうかを判定します。
    /// </summary>
    /// <param name="pos">判定する位置</param>
    /// <returns>true: 範囲内, false: 範囲外</returns>
    private bool IsWithinBounds(Vector2 pos)
    {
        float minY = this.transform.position.y - GetDistanceToGround();
        float maxY = minY + MAX_GROUND_HEIGHT;
        return pos.x >= leftBound && pos.x <= rightBound && pos.y >= minY && pos.y <= maxY;
    }

    private void OnDisable()
    {
        // オブジェクトが非アクティブ化されたらTweenを止める
        if (floatingTween != null)
        {
            floatingTween.Kill();
            floatingTween = null;
        }

        if (moveTween != null)
        {
            moveTween.Kill();
            moveTween = null;
        }

        sePlayer.Stop();
    }

    private void OnDrawGizmos()
    {
        // 一回の移動範囲を球で描画
        Gizmos.color = Color.cyan;
        Vector3 center = this.transform.position;
        Gizmos.DrawWireSphere(center, moveDistance);

        // 横の移動限界範囲のGizmosを表示
        Gizmos.color = new Color(1f, 0f, 0f, 0.15f); // 半透明の赤色
        center = new Vector3(
            (leftBound + rightBound) / 2f,
            transform.position.y,
            transform.position.z
        );
        Vector3 size = new Vector3(rightBound - leftBound, 4f, 0.1f);
        Gizmos.DrawCube(center, size);
    }
}

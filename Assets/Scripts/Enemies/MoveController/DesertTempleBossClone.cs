using System.Collections;
using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))] // SEが必要なら
public class DesertTempleBossClone : MonoBehaviour
{
    //TODO: 消滅エフェクトの攻撃力
    private const string CLONE_ATTACK_BULLET_POOLTAG = "DesertTempleGolemShoot";
    private const string DESPAWN_EFFECT_POOLTAG = "DesertTempleBossCloneDespawnEffect"; //FFF86C(透明度90)

    [Header("パーツ参照")]
    [SerializeField]
    private SpriteRenderer bodySpriteRenderer;

    [SerializeField]
    private SpriteRenderer leftArmSpriteRenderer;

    [SerializeField]
    private SpriteRenderer rightArmSpriteRenderer;

    [SerializeField]
    private SpriteRenderer haloSpriteRenderer;

    [SerializeField]
    private Transform haloTransform;

    [SerializeField]
    private Animator leftArmAnimator;

    [Header("設定")]
    [SerializeField]
    private Vector2 haloOffset = new Vector2(0.25f, -0.2f);

    [SerializeField]
    private float haloRotationSpeed = 10f; // 基本速度

    [SerializeField]
    private float floatAmplitude = 1f;

    [SerializeField]
    private float floatDuration = 2.0f;

    [SerializeField]
    private float initialY; // 生成時のY座標基準

    // 攻撃パラメータ（本体から受け取る）
    private float bulletSpeed;
    private float cloneAttackChargeTime;
    private Vector2 bulletOffset;
    private float bulletHeight;
    private float groundY;

    // --- 内部変数 ---
    private bool isFacingLocked = false; // 向きの更新をロックするフラグ
    private Transform playerTransform;
    private Tweener floatTween;

    /// <summary>
    /// 初期化（生成時に本体から呼ばれる）
    /// </summary>
    public void Setup(
        Transform target,
        float initialYPos,
        float _attackChargeTime,
        float _bulletSpeed,
        Vector2 _bulletOffset,
        float _groundY
    )
    {
        // 状態リセット
        if (floatTween != null)
            floatTween.Kill();

        // 初期状態設定: 透明にしてアクティブ化
        SetAlpha(0f);
        gameObject.SetActive(true);

        this.playerTransform = target;
        this.initialY = initialYPos;
        this.cloneAttackChargeTime = _attackChargeTime;
        this.bulletSpeed = _bulletSpeed;
        this.bulletOffset = _bulletOffset;
        this.groundY = _groundY;

        // 浮遊開始
        StartFloating();

        // フェードイン演出
        FadeTo(1f, 0.5f);
        this.tag = GameConstants.IMMUNE_ENEMY_TAG_NAME; // Outline用にタグを戻す
        //TODO: 出現SE再生
    }

    private void Update()
    {
        // 1. 向きの更新
        if (!isFacingLocked)
        {
            UpdateFacing();
        }

        // 2. 光輪を反時計回りに回す
        if (haloTransform != null)
        {
            // 反時計回り = Z軸プラス回転
            haloTransform.Rotate(0, 0, haloRotationSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// 向きの更新
    /// </summary>
    private void UpdateFacing()
    {
        if (playerTransform == null)
            return;

        bool isRight = IsTargetToRight();

        // Sprite反転
        if (bodySpriteRenderer)
            bodySpriteRenderer.flipX = isRight;
        if (leftArmSpriteRenderer)
            leftArmSpriteRenderer.flipX = isRight;
        if (rightArmSpriteRenderer)
            rightArmSpriteRenderer.flipX = isRight;

        // 光輪の位置調整
        if (haloTransform)
        {
            float targetX = isRight ? -haloOffset.x : haloOffset.x;
            haloTransform.localPosition = new Vector3(
                targetX,
                haloOffset.y,
                haloTransform.localPosition.z
            );
        }
    }

    /// <summary>
    /// 攻撃リクエスト（コルーチンとして実行）
    /// </summary>
    public IEnumerator AttackSequence(float delay)
    {
        // ランダムな待機
        yield return StartCoroutine(WaitForTime(delay));

        // --- チャージ前に向きを確定してロックする ---
        if (playerTransform != null)
        {
            UpdateFacing(); // 最新の向きに更新
        }
        isFacingLocked = true; // Updateでの向き変更を禁止

        // 現在の向きを取得（右向きならtrue）
        bool isFacingRight = bodySpriteRenderer.flipX;

        // チャージ
        if (leftArmAnimator != null)
        {
            // アニメーション速度調整
            leftArmAnimator.SetFloat(
                "ArmUpSpeed",
                DesertTempleBossMoveController.LEFTARM_ARMUP_ANIMATION_DURATION
                    / cloneAttackChargeTime
            );
            leftArmAnimator.SetTrigger("ArmUpTrigger");
        }

        yield return StartCoroutine(WaitForTime(cloneAttackChargeTime));
        yield return StartCoroutine(WaitForTime(0.5f)); // アニメーション完了待ち

        // --- ロックした向きと現在のプレイヤー位置で不発判定 ---
        bool shouldFire = false;

        if (playerTransform != null)
        {
            // ボス(分身)から見たプレイヤーのX方向の相対距離
            float diffX = playerTransform.position.x - transform.position.x;

            // 腕の長さ（オフセットのX絶対値）を閾値とする
            float armReachThreshold = Mathf.Abs(bulletOffset.x);

            if (isFacingRight)
            {
                // 右を向いている場合:
                // プレイヤーが右側にいて、かつ 腕の長さより遠いなら発射
                if (diffX >= armReachThreshold)
                    shouldFire = true;
            }
            else
            {
                // 左を向いている場合:
                // プレイヤーが左側にいて、かつ 腕の長さより遠いなら発射
                if (diffX <= -armReachThreshold)
                    shouldFire = true;
            }
        }

        // 条件を満たしている場合のみ発射
        if (shouldFire)
        {
            FireBullet();
            if (leftArmAnimator != null)
                leftArmAnimator.SetTrigger("AttackTrigger");

            // 攻撃アニメーション待ち
            yield return StartCoroutine(
                WaitForTime(DesertTempleBossMoveController.LEFTARM_ATTACK_ANIMATION_DURATION)
            );
            yield return StartCoroutine(WaitForTime(0.2f)); // 攻撃後の小休止
        }

        // --- ロック解除 ---
        isFacingLocked = false;

        if (leftArmAnimator != null)
            leftArmAnimator.SetTrigger("IdleTrigger");
    }

    /// <summary>
    /// 弾丸発射
    /// </summary>
    private void FireBullet()
    {
        GameObject bullet = ObjectPooler.SceneInstance.SpawnFromPool(
            CLONE_ATTACK_BULLET_POOLTAG,
            Vector3.zero,
            Quaternion.identity
        );

        if (bullet != null)
        {
            bool isRight = bodySpriteRenderer.flipX;

            // 1. 生成位置を計算
            float spawnY = transform.position.y - 1.5f + bulletHeight; // 簡易高さ計算
            float offsetX = isRight ? -bulletOffset.x : bulletOffset.x;
            Vector3 spawnPos = new Vector3(transform.position.x + offsetX, spawnY, 0f);

            bullet.transform.position = spawnPos;

            // 2. プレイヤーへの方向ベクトルを計算 (変更点)
            Vector2 direction;
            if (playerTransform != null)
            {
                // (ターゲット位置 - 発射位置) の正規化ベクトル
                direction = ((Vector2)playerTransform.position - (Vector2)spawnPos).normalized;
            }
            else
            {
                // ターゲットがいない場合は従来の水平発射
                direction = isRight ? Vector2.right : Vector2.left;
            }

            // 3. 速度を適用
            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = direction * bulletSpeed;
            }

            // 4. エフェクト生成
            ObjectPooler.SceneInstance.SpawnFromPool(
                DesertTempleBossMoveController.RIGHT_ARM_BULLET_SPAWN_EFFECT_POOLTAG,
                spawnPos,
                Quaternion.identity
            );
            //TODO: SE再生
        }
    }

    /// <summary>
    /// 全パーツの透明度を一括変更するヘルパー
    /// </summary>
    private Sequence FadeTo(float targetAlpha, float duration)
    {
        Sequence seq = DOTween.Sequence();
        if (bodySpriteRenderer)
            seq.Join(bodySpriteRenderer.DOFade(targetAlpha, duration));
        if (leftArmSpriteRenderer)
            seq.Join(leftArmSpriteRenderer.DOFade(targetAlpha, duration));
        if (rightArmSpriteRenderer)
            seq.Join(rightArmSpriteRenderer.DOFade(targetAlpha, duration));
        if (haloSpriteRenderer)
            seq.Join(haloSpriteRenderer.DOFade(targetAlpha, duration));
        return seq;
    }

    /// <summary>
    /// 透明度を即座にセットする
    /// </summary>
    private void SetAlpha(float alpha)
    {
        if (bodySpriteRenderer)
            SetSpriteAlpha(bodySpriteRenderer, alpha);
        if (leftArmSpriteRenderer)
            SetSpriteAlpha(leftArmSpriteRenderer, alpha);
        if (rightArmSpriteRenderer)
            SetSpriteAlpha(rightArmSpriteRenderer, alpha);
        if (haloSpriteRenderer)
            SetSpriteAlpha(haloSpriteRenderer, alpha);
    }

    /// <summary>
    /// 個別スプライトの透明度をセットするヘルパー
    /// </summary>
    private void SetSpriteAlpha(SpriteRenderer sr, float alpha)
    {
        Color c = sr.color;
        c.a = alpha;
        sr.color = c;
    }

    /// <summary>
    /// 浮遊アニメーション
    /// </summary>
    private void StartFloating()
    {
        transform.position = new Vector3(transform.position.x, initialY, transform.position.z);
        floatTween = transform
            .DOMoveY(initialY + floatAmplitude, floatDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject);
    }

    /// <summary>
    /// 退場処理 (Destroyせずに非表示にする)
    /// </summary>
    public void Despawn()
    {
        if (floatTween != null)
            floatTween.Kill();

        // フェードアウト -> 完了したら非アクティブ化
        this.tag = GameConstants.UNTAGGED_TAG_NAME; // Outlineを消すためにタグを外す
        ObjectPooler.SceneInstance.SpawnFromPool(
            DESPAWN_EFFECT_POOLTAG,
            transform.position,
            Quaternion.identity
        );
        //TODO: 消失SE再生
        Sequence seq = FadeTo(0f, 0.5f);
        seq.OnComplete(() =>
        {
            gameObject.SetActive(false);
        });
    }

    /// <summary>
    /// ポーズを考慮した待機処理
    /// </summary>
    private IEnumerator WaitForTime(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            // ポーズ中は時間を進めずに待機
            if (TimeManager.instance.isEnemyMovePaused)
            {
                yield return null;
                continue;
            }

            timer += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// 対象が自分より右側にいるか判定します
    /// </summary>
    /// <param name="dir">対象への方向ベクトル</param>
    /// <returns>右側にいるならtrue、左側ならfalse、プレイヤーがいなければfalse</returns>
    private bool IsTargetToRight()
    {
        if (playerTransform == null)
            return false;
        Vector2 dir = playerTransform.position - transform.position;
        return dir.x > 0;
    }

    private void OnDisable()
    {
        // 非アクティブになったらTweenを確実に切る
        if (floatTween != null)
            floatTween.Kill();
    }
}

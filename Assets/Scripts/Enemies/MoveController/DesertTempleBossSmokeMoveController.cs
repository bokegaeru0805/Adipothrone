using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using MyGame.CameraControl;
using UnityEngine;

[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class DesertTempleBossSmokeMoveController : MonoBehaviour
{
    private const string RIGHTARM_SHOOT_POOLTAG = "DesertTempleGolemShoot";
    private const string LEFTARM_SHOOT_POOLTAG = "DesertTempleBossShoot1";

    [Header("移動の設定")]
    [SerializeField]
    private float leftBound;

    [SerializeField]
    private float rightBound;

    [SerializeField]
    private float initialPositionY;

    [Tooltip("縦幅の半径")]
    [SerializeField]
    private float heightRadius = 1.5f;

    [Tooltip("1周にかかる時間 (秒)")]
    [SerializeField]
    private float cyclePeriod = 4.0f;

    [Tooltip("ノイズの振動の大きさ (0だと振動なし)")]
    [SerializeField]
    private float noiseAmplitude = 0.1f; // 微小な振幅

    [Tooltip("ノイズの振動の速さ (基本周期の何倍速で揺れるか)")]
    [SerializeField]
    private float noiseFrequency = 12.0f; // 整数にすると端の座標がズレません
    #region 右腕の攻撃の設定

    [Header("右腕の攻撃の設定")]
    [Tooltip("右腕の攻撃の間隔（秒）")]
    [SerializeField]
    private float rightArmAttackInterval = 3.0f;

    [Tooltip("右腕の攻撃の弾の生成時間（秒）")]
    [SerializeField]
    private float rightArmAttackSpawnTime = 0.5f;

    [Tooltip("右腕の弾の攻撃の速度")]
    [SerializeField]
    private float rightArmAttackSpeed = 5.0f;

    [Tooltip("右腕の弾の生成位置のオフセット")]
    [SerializeField]
    private Vector2 rightArmAttackSpawnOffset = Vector2.zero;
    #endregion

    #region 左腕の攻撃の設定
    [Header("左腕の攻撃の設定")]
    [Tooltip("左腕の攻撃の間隔（秒）")]
    [SerializeField]
    private float leftArmAttackInterval = 4.0f;

    [Tooltip("左腕の攻撃の弾の生成時間（秒）")]
    [SerializeField]
    private float leftArmAttackSpawnTime = 0.5f;

    [Tooltip("左腕の弾の攻撃の速度")]
    [SerializeField]
    private float leftArmAttackSpeed = 5.0f;

    [Tooltip("左腕攻撃の発射角度の最小値 (度) ※左(-X)を0度とする")]
    [SerializeField]
    private float leftArmAttackMinAngle = -30f;

    [Tooltip("左腕攻撃の発射角度の最大値 (度) ※左(-X)を0度とする")]
    [SerializeField]
    private float leftArmAttackMaxAngle = 30f;

    [Tooltip("左腕の弾の生成位置のオフセット")]
    [SerializeField]
    private Vector2 leftArmAttackSpawnOffset = Vector2.zero;
    #endregion

    #region 両腕の攻撃の設定

    [Header("両腕の攻撃の設定")]
    [Tooltip("両腕の攻撃の間隔（秒）")]
    [SerializeField]
    private float bothArmsAttackInterval = 5.0f;

    [Tooltip("両腕の攻撃の弾の生成時間（秒）")]
    [SerializeField]
    private float bothArmsAttackSpawnTime = 0.5f;

    [Tooltip("両腕の攻撃後の待機時間（秒）")]
    [SerializeField]
    private float bothArmsAttackWaitTime = 1.0f;

    [Tooltip("両腕の弾の個数")]
    [SerializeField]
    private int bothArmsAttackBulletCount = 6;

    [Tooltip("両腕の弾の攻撃の速度")]
    [SerializeField]
    private float bothArmsAttackSpeed = 4.0f;

    [Tooltip("両腕の弾の生成位置のオフセット")]
    [SerializeField]
    private Vector2 bothArmsAttackSpawnOffset = Vector2.zero;
    #endregion

    [Space(40)]
    [Header("基本コンポーネント")]
    [SerializeField]
    private GameObject leftArmObject = null;

    [SerializeField]
    private GameObject rightArmObject = null;

    [Header("設定項目")]
    [SerializeField]
    private Transform playerTransform = null;

    [Header("スプライト画像設定")]
    [Tooltip("通常時の右腕のスプライト")]
    [SerializeField]
    private Sprite rightArmNormalSprite = null;

    [Tooltip("攻撃時の右腕のスプライト")]
    [SerializeField]
    private Sprite rightArmAttackSprite = null;

    [Tooltip("両手攻撃時の右腕のスプライト")]
    [SerializeField]
    private Sprite rightArmBothAttackSprite = null;

    [Tooltip("通常時の左腕のスプライト")]
    [SerializeField]
    private Sprite leftArmNormalSprite = null;

    [Tooltip("攻撃時の左腕のスプライト")]
    [SerializeField]
    private Sprite leftArmAttackSprite = null;

    [Tooltip("両手攻撃時の左腕のスプライト")]
    [SerializeField]
    private Sprite leftArmBothAttackSprite = null;

    [Header("その他の設定")]
    [Tooltip("腕が上下する幅")]
    [SerializeField]
    private float armFloatAmplitude = 0.5f;

    [Tooltip("腕の上下振動にかかる時間")]
    [SerializeField]
    private float armFloatDuration = 2f;

    [Tooltip("両腕攻撃時の弾の展開最大半径")]
    [SerializeField]
    private float bothArmsAttackRadius = 1.5f;

    [Tooltip("両腕攻撃時の弾の回転速度 (度/秒)")]
    [SerializeField]
    private float bothArmsAttackRotationSpeed = 720.0f;
    private float widthRadius = 3.0f; //横幅の半径
    private float currentMoveTime; // 移動経過時間
    private float rightArmAttackTimer = 0f; // 右腕攻撃タイマー
    private float leftArmAttackTimer = 0f; // 左腕攻撃タイマー
    private float bothArmsAttackTimer = 0f; // 両腕攻撃タイマー
    private int lastPhaseIndex = 0; // πの倍数を通過したかを判定するためのインデックス
    private bool rightFlag = false;
    private bool isRightArmAttacking = false; // 右手攻撃実行中
    private bool isLeftArmAttacking = false; // 左手攻撃実行中
    private bool isBothArmsAttacking = false; // 両手攻撃実行中
    private bool isBothArmsPending = false; // 両手攻撃のタイマーが満了し、他の攻撃の終了待ち状態
    private Vector2 centerPosition; // 中心位置

    private enum MovementPattern
    {
        Standard, // 基本の楕円
        Figure8, // 8の字
        EasedHover, // イージング(片側は膨らみ、もう片側は凹むような、歪んだ楕円)
        Astroid, // 星型（アストロイド）
    }

    private MovementPattern movementPattern = MovementPattern.Standard; // 動きのパターン
    private SpriteRenderer rightArmSpriteRenderer;
    private SpriteRenderer leftArmSpriteRenderer;
    private CriWare.Assets.CriAtomSePlayer sePlayer;

    // Tween管理用変数
    private Tweener leftArmTween;
    private Tweener rightArmTween;
    private float leftArmDefaultY;
    private float rightArmDefaultY;

    private void Awake()
    {
        if (leftArmObject == null || rightArmObject == null)
        {
            Debug.LogError($"{this.name}の基本コンポーネントが設定されていません。");
            return;
        }

        if (
            rightArmNormalSprite == null
            || rightArmAttackSprite == null
            || rightArmBothAttackSprite == null
            || leftArmNormalSprite == null
            || leftArmAttackSprite == null
            || leftArmBothAttackSprite == null
        )
        {
            Debug.LogError($"{this.name}のスプライト設定が不足しています。");
            return;
        }

        sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();

        // パーツのSpriteRendererを取得
        rightArmSpriteRenderer = rightArmObject.GetComponent<SpriteRenderer>();
        leftArmSpriteRenderer = leftArmObject.GetComponent<SpriteRenderer>();

        //腕の初期ローカルY座標を保存しておく（基準点にするため）
        leftArmDefaultY = leftArmObject.transform.localPosition.y;
        rightArmDefaultY = rightArmObject.transform.localPosition.y;
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
        //中心座標の決定
        centerPosition = new Vector2((leftBound + rightBound) / 2, initialPositionY);

        // スプライト初期化
        if (rightArmSpriteRenderer != null)
            rightArmSpriteRenderer.sprite = rightArmNormalSprite;
        if (leftArmSpriteRenderer != null)
            leftArmSpriteRenderer.sprite = leftArmNormalSprite;

        currentMoveTime = 0f;
        lastPhaseIndex = 0;

        // タイマーとフラグのリセット
        rightArmAttackTimer = 0f;
        bothArmsAttackTimer = 0f;
        isRightArmAttacking = false;
        isLeftArmAttacking = false;
        isBothArmsAttacking = false;
        isBothArmsPending = false;

        movementPattern = MovementPattern.Standard;
        rightFlag = IsTargetToRight();
        UpdateFacingDirection(rightFlag); //すべてのパーツの向きを更新

        StartArmMovingAnimation(); //腕の上下振動アニメーション開始
    }

    private void FixedUpdate()
    {
        // 敵の動きがポーズされているかどうかを確認
        // もしポーズされていれば何もせずに戻る
        if (TimeManager.instance.isEnemyMovePaused)
        {
            return;
        }

        bool isTargetCurrentlyRight = IsTargetToRight();
        if (rightFlag != isTargetCurrentlyRight)
        {
            rightFlag = isTargetCurrentlyRight;
            UpdateFacingDirection(rightFlag); //すべてのパーツの向きを更新
        }
        UpdateMovement(); // 移動の更新
        HandleAttackTimers(); // タイマー処理
    }

    /// <summary>
    /// 移動に関する計算と座標更新を行う
    /// </summary>
    private void UpdateMovement()
    {
        // 時間を経過させる
        currentMoveTime += Time.deltaTime;

        // シータ（角度）を計算: 0 から 2π の範囲で変化
        float theta = (2.0f * Mathf.PI * currentMoveTime) / cyclePeriod;

        // 現在の theta が π の何倍の区間にいるかを計算 (0, 1, 2, ...)
        int currentPhaseIndex = Mathf.FloorToInt(theta / Mathf.PI);

        // 前回のフレームと区間が異なれば（つまり 0, π, 2π... を跨いだら）
        if (currentPhaseIndex > lastPhaseIndex)
        {
            ChangeToRandomPattern();
            lastPhaseIndex = currentPhaseIndex;
        }

        // パターンに応じたオフセット（ズレ）を計算
        Vector2 offset = CalculateOffset(theta, movementPattern);

        // 座標を更新 (中心点 + オフセット)
        transform.position = centerPosition + new Vector2(offset.x, offset.y);
    }

    // <summary>
    /// 指定された角度(theta)とパターンに基づいて位置オフセットを計算する
    /// </summary>
    private Vector2 CalculateOffset(float theta, MovementPattern pattern)
    {
        float x = 0f;
        float y = 0f;

        // 基本のX座標計算 (x = a * cosθ)
        float basicX = widthRadius * Mathf.Cos(theta);

        //　引数の pattern を使用して分岐
        switch (pattern)
        {
            case MovementPattern.Standard:
                x = basicX;
                y = heightRadius * Mathf.Sin(theta);
                break;

            case MovementPattern.Figure8:
                x = basicX;
                // y = b * sin(2θ)
                y = heightRadius * Mathf.Sin(2.0f * theta);
                break;

            case MovementPattern.EasedHover:
                x = basicX;
                // y = b * sin^3θ
                y = heightRadius * Mathf.Pow(Mathf.Sin(theta), 3.0f);
                break;

            case MovementPattern.Astroid:
                // x = a * cos^3θ, y = b * sin^3θ
                x = widthRadius * Mathf.Pow(Mathf.Cos(theta), 3.0f);
                y = heightRadius * Mathf.Pow(Mathf.Sin(theta), 3.0f);
                break;
            default:
                x = basicX;
                y = heightRadius * Mathf.Sin(theta);
                break;
        }

        // Y軸にノイズを加算する
        // noiseAmplitudeが0より大きい場合のみ計算
        if (noiseAmplitude > 0f)
        {
            // theta * noiseFrequency で細かく振動させる
            y += noiseAmplitude * Mathf.Sin(theta * noiseFrequency);
        }

        return new Vector2(x, y);
    }

    /// <summary>
    /// 定義されているMovementPatternの中からランダムに一つ選んで設定する
    /// </summary>
    private void ChangeToRandomPattern()
    {
        // Enumの値を配列として全て取得
        var values = System.Enum.GetValues(typeof(MovementPattern));

        // ランダムなインデックス決定
        int randomIndex = Random.Range(0, values.Length);

        // 新しいパターンを適用
        movementPattern = (MovementPattern)values.GetValue(randomIndex);

        //Debug.Log("移動パターン変更: " + movementPattern.ToString());
    }

    /// <summary>
    /// 腕の上下動アニメーションを開始します
    /// </summary>
    private void StartArmMovingAnimation()
    {
        // 既存のTweenがあれば一度破棄してリセット
        leftArmTween?.Kill();
        rightArmTween?.Kill();

        // 位置を初期位置に戻す
        ResetArmPositions();

        // 左腕のアニメーション
        // 現在のY座標から +amplitude 分だけ移動し、Yoyoで戻ってくる動きを無限ループ(-1)させる
        leftArmTween = leftArmObject
            .transform.DOLocalMoveY(leftArmDefaultY + armFloatAmplitude, armFloatDuration)
            .SetEase(Ease.InOutSine) // 生き物らしい滑らかな動き
            .SetLoops(-1, LoopType.Yoyo) // 往復ループ
            .SetLink(leftArmObject); // オブジェクトが消えたらTweenも自動消滅させる安全策

        // 右腕のアニメーション
        rightArmTween = rightArmObject
            .transform.DOLocalMoveY(rightArmDefaultY + armFloatAmplitude, armFloatDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetDelay(armFloatDuration / 4) // 左腕とずらして動かす
            .SetLink(rightArmObject);
    }

    /// <summary>
    /// 腕のローカルY座標を初期位置に戻す
    /// </summary>
    private void ResetArmPositions()
    {
        leftArmObject.transform.localPosition = new Vector3(
            leftArmObject.transform.localPosition.x,
            leftArmDefaultY,
            leftArmObject.transform.localPosition.z
        );
        rightArmObject.transform.localPosition = new Vector3(
            rightArmObject.transform.localPosition.x,
            rightArmDefaultY,
            rightArmObject.transform.localPosition.z
        );
    }

    /// <summary>
    /// 各種攻撃タイマーの更新と発動判定
    /// </summary>
    private void HandleAttackTimers()
    {
        //  両手攻撃が実行中の場合
        // 他の攻撃は発生させず、その両手の攻撃が完了するまでタイマーを動かさない
        if (isBothArmsAttacking)
            return;

        // 両手攻撃の開始時間が来て、待機状態に入っている場合
        if (isBothArmsPending)
        {
            // 他の攻撃（右手攻撃）が完了しているかチェック
            if (!isRightArmAttacking && !isLeftArmAttacking)
            {
                // 全ての攻撃が完了しているので、両手攻撃を開始
                isBothArmsPending = false; // 待機解除
                StartCoroutine(PerformBothArmsAttack());
            }
            // 待機中は、これ以降の処理（右手攻撃タイマーなど）は行わない
            return;
        }

        // 両手攻撃タイマーの更新
        bothArmsAttackTimer += Time.deltaTime;
        if (bothArmsAttackTimer >= bothArmsAttackInterval)
        {
            bothArmsAttackTimer = 0f;
            // ここで即座に攻撃せず「待機フラグ」を立てる
            // これにより、次のフレームから上記 "2" のブロックに入り、他の攻撃の終了を待つようになる
            isBothArmsPending = true;
            return;
        }

        // 右手攻撃タイマーの更新
        // 右手攻撃中ではなく、かつ両手攻撃が待機中でもない場合のみ進む
        if (!isRightArmAttacking)
        {
            rightArmAttackTimer += Time.deltaTime;
            if (rightArmAttackTimer >= rightArmAttackInterval)
            {
                rightArmAttackTimer = 0f;
                StartCoroutine(PerformRightArmAttack());
            }
        }

        // 左手攻撃タイマーの更新
        // 左手攻撃中ではなく、かつ両手攻撃が待機中でもない場合のみ進む
        if (!isLeftArmAttacking)
        {
            leftArmAttackTimer += Time.deltaTime;
            if (leftArmAttackTimer >= leftArmAttackInterval)
            {
                leftArmAttackTimer = 0f;
                StartCoroutine(PerformLeftArmAttack());
            }
        }
    }

    /// <summary>
    /// 右腕攻撃コルーチン
    /// </summary>
    private IEnumerator PerformRightArmAttack()
    {
        isRightArmAttacking = true;

        //右腕のスプライトを攻撃用に変更
        rightArmSpriteRenderer.sprite = rightArmAttackSprite;

        // まず仮の位置で取得
        GameObject bullet = ObjectPooler.SceneInstance.SpawnFromPool(
            RIGHTARM_SHOOT_POOLTAG,
            rightArmObject.transform.position,
            Quaternion.identity
        );

        if (bullet == null)
            yield break;

        // 右腕の子オブジェクトに設定して追従させる
        bullet.transform.SetParent(rightArmObject.transform);

        // オフセット位置に配置 (右向きか左向きかでX座標を反転させる必要がある場合はここで調整)
        Vector3 spawnLocalPos = rightArmAttackSpawnOffset;
        bullet.transform.localPosition = spawnLocalPos;

        // 3. スケールを0にしてから徐々に大きくする (DoTween)
        bullet.transform.localScale = Vector3.zero;
        bullet.transform.DOScale(Vector3.one, rightArmAttackSpawnTime).SetEase(Ease.OutBack);

        // 生成にかかる時間待機
        yield return new WaitForSeconds(rightArmAttackSpawnTime);

        // --- 発射処理 ---

        if (bullet != null && bullet.activeInHierarchy)
        {
            // 親子関係を解除 (これで腕の動きに追従しなくなる)
            bullet.transform.SetParent(null);

            // // プレイヤーへの方向を計算
            // Vector3 targetPos =
            //     (playerTransform != null) ? playerTransform.position : (Vector3)transform.position;
            // Vector2 direction = (targetPos - bullet.transform.position).normalized;

            //直線方向へ発射
            Vector2 direction = rightFlag ? Vector2.right : Vector2.left;

            // Rigidbody2Dを取得して速度を与える
            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = direction * rightArmAttackSpeed;
            }
            else
            {
                Debug.LogWarning("弾にRigidbody2Dがついていません");
            }

            // SE再生
            sePlayer.Play(SE_EnemyAction.Shoot_Water1);
            CameraManager.instance?.PlayCustomShake(1.0f, 2.0f, 0.3f); //カメラ揺れ
        }

        //右腕のスプライトを通常用に戻す
        rightArmSpriteRenderer.sprite = rightArmNormalSprite;

        isRightArmAttacking = false;
    }

    /// <summary>
    /// 左腕攻撃コルーチン
    /// </summary>
    private IEnumerator PerformLeftArmAttack()
    {
        isLeftArmAttacking = true;

        // 左腕のスプライトを攻撃用に変更
        leftArmSpriteRenderer.sprite = leftArmAttackSprite;

        // まず仮の位置で取得
        GameObject bullet = ObjectPooler.SceneInstance.SpawnFromPool(
            LEFTARM_SHOOT_POOLTAG,
            leftArmObject.transform.position,
            Quaternion.identity
        );

        if (bullet == null)
            yield break;
        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.simulated = false; // 一時的に物理演算を無効化
        }
        // 左腕の子オブジェクトに設定して追従させる
        bullet.transform.SetParent(leftArmObject.transform);

        // オフセット位置に配置
        bullet.transform.localPosition = leftArmAttackSpawnOffset;

        // スケールを0にしてから徐々に大きくする (DoTween)
        bullet.transform.localScale = Vector3.zero;
        bullet.transform.DOScale(Vector3.one, leftArmAttackSpawnTime).SetEase(Ease.OutBack);

        // 生成にかかる時間待機
        yield return new WaitForSeconds(leftArmAttackSpawnTime);

        // --- 発射処理 ---

        if (bullet != null && bullet.activeInHierarchy)
        {
            // 親子関係を解除 (これで腕の動きに追従しなくなる)
            bullet.transform.SetParent(null);

            // 角度計算
            // 1. 最小～最大の範囲からランダムな角度を取得
            float randomAngle = Random.Range(leftArmAttackMinAngle, leftArmAttackMaxAngle);

            // 2. 左方向(-X)を0度として、Z軸回転を加える
            // Quaternion.Euler(0, 0, angle) * Vector2.left で「左を基準に回転したベクトル」を作る
            Vector2 baseDirection = Quaternion.Euler(0, 0, randomAngle) * Vector2.left;

            // 3. 最終的な発射ベクトルを決定
            Vector2 direction = baseDirection;

            // rightFlagがtrue（右向き）の場合は、X成分を反転させて右側へ飛ばす
            if (rightFlag)
            {
                direction.x *= -1;
            }

            // Rigidbody2Dを有効化して速度を与える
            if (rb != null)
            {
                rb.simulated = true; // 物理演算を有効化
                rb.velocity = direction.normalized * leftArmAttackSpeed;
            }
            else
            {
                Debug.LogWarning("弾にRigidbody2Dがついていません");
            }

            // SE再生
            sePlayer.Play(SE_EnemyAction.Drop_Metal);
        }

        // 左腕のスプライトを通常用に戻す
        leftArmSpriteRenderer.sprite = leftArmNormalSprite;

        isLeftArmAttacking = false;
    }

    /// <summary>
    /// 両腕攻撃コルーチン
    /// </summary>
    private IEnumerator PerformBothArmsAttack()
    {
        // 攻撃状態にする
        isBothArmsAttacking = true;

        // 腕の上下アニメーションを一時停止し、位置をリセットする
        leftArmTween?.Pause();
        rightArmTween?.Pause();
        ResetArmPositions();

        // スプライト変更
        rightArmSpriteRenderer.sprite = rightArmBothAttackSprite;
        leftArmSpriteRenderer.sprite = leftArmBothAttackSprite;

        // 弾を一斉に生成する
        List<GameObject> bullets = new List<GameObject>();
        List<Transform> bulletTransforms = new List<Transform>(); // 最適化のためTransformをキャッシュ

        // 生成基準位置の計算 (向きを考慮)
        Vector3 baseOffset = bothArmsAttackSpawnOffset;
        if (rightFlag)
            baseOffset.x *= -1;
        Vector3 spawnCenter = transform.position + baseOffset; // ボスの中心 + オフセット

        for (int i = 0; i < bothArmsAttackBulletCount; i++)
        {
            GameObject b = ObjectPooler.SceneInstance.SpawnFromPool(
                RIGHTARM_SHOOT_POOLTAG,
                spawnCenter,
                Quaternion.identity
            );

            if (b != null)
            {
                b.transform.localScale = Vector3.zero; // 最初は極小
                bullets.Add(b);
                bulletTransforms.Add(b.transform);
                b.tag = GameConstants.ImmuneEnemyTagName;
            }
        }

        // 回転・拡大演出
        float elapsedTime = 0f;
        CameraManager.instance?.PlayCustomShake(0.3f, 8.0f, bothArmsAttackSpawnTime); //カメラ揺れ

        while (elapsedTime < bothArmsAttackSpawnTime)
        {
            sePlayer.Play(SE_EnemyAction.GearTurn); // 回転SEをループ再生

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / bothArmsAttackSpawnTime;
            float easeProgress = DOVirtual.EasedValue(0, 1, progress, Ease.OutCubic); // 拡大のイージング

            // 現在の回転角度 (時間とともに回転する)
            float currentBaseAngle = elapsedTime * bothArmsAttackRotationSpeed;

            // 現在の半径 (時間とともに広がる)
            float currentRadius = Mathf.Lerp(0f, bothArmsAttackRadius, easeProgress);

            // 現在のスケール (時間とともに大きくなる)
            Vector3 currentScale = Vector3.Lerp(Vector3.zero, Vector3.one, easeProgress);

            // 生成基準位置を再計算（ボス自体が動いているため毎フレーム更新が必要）
            spawnCenter = transform.position + baseOffset;

            // 全弾の座標更新
            for (int i = 0; i < bullets.Count; i++)
            {
                if (bullets[i] == null || !bullets[i].activeInHierarchy)
                    continue;

                // 円周上の角度計算 (均等配置 + 全体回転)
                float angle = currentBaseAngle + (360f / bothArmsAttackBulletCount * i);
                float rad = angle * Mathf.Deg2Rad;

                // 座標計算
                Vector3 pos =
                    spawnCenter
                    + new Vector3(
                        Mathf.Cos(rad) * currentRadius,
                        Mathf.Sin(rad) * currentRadius,
                        0
                    );

                bulletTransforms[i].position = pos;
                bulletTransforms[i].localScale = currentScale;
                bulletTransforms[i].rotation = Quaternion.identity;
            }

            yield return null;
        }

        // 回転SE停止
        sePlayer.Stop();

        // 発射処理 (法線方向へ飛ばす)
        spawnCenter = transform.position + baseOffset; // 発射時の中心位置
        CameraManager.instance?.PlayCustomShake(3.5f, 2.0f, 0.3f); //カメラ揺れ

        foreach (var b in bullets)
        {
            if (b == null || !b.activeInHierarchy)
                continue;

            Rigidbody2D rb = b.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                // 中心から弾へのベクトルを進行方向とする
                Vector2 direction = (b.transform.position - spawnCenter).normalized;

                // 万が一中心と重なってゼロベクトルになった場合の保険
                if (direction == Vector2.zero)
                    direction = Vector2.up;

                rb.velocity = direction * bothArmsAttackSpeed;
                rb.tag = GameConstants.DamageableEnemyTagName; // 通常の敵弾タグに戻す
            }

            // 弾ごとのSE再生
            var bulletSePlayer = b.GetComponent<CriWare.Assets.CriAtomSePlayer>();
            if (bulletSePlayer != null)
            {
                bulletSePlayer.Play(SE_EnemyAction.Shoot_Water1);
            }
        }

        // 攻撃後の待機時間
        yield return new WaitForSeconds(bothArmsAttackWaitTime);

        // スプライトを戻す
        rightArmSpriteRenderer.sprite = rightArmNormalSprite;
        leftArmSpriteRenderer.sprite = leftArmNormalSprite;

        // 腕のアニメーション再開
        leftArmTween?.Play();
        rightArmTween?.Play();

        isBothArmsAttacking = false;
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
    /// 全てのパーツの向き（flipX）を一括で更新します。
    /// Pivot調整済みのため、位置の反転処理は不要です。
    /// </summary>
    /// <param name="isFacingRight">右を向いているか</param>
    private void UpdateFacingDirection(bool isFacingRight)
    {
        //弾の子オブジェクトの向きを合わせるため
        //SpriteRendererを用いずに、Transformの回転で対応
        this.transform.localRotation = new Quaternion(
            0,
            isFacingRight ? 180 : 0,
            0,
            Quaternion.identity.w
        );
    }

    private void OnDestroy()
    {
        leftArmTween?.Kill();
        rightArmTween?.Kill();
    }

    private void OnDrawGizmosSelected()
    {
        // 実行中以外でも中心位置を計算して表示できるようにする
        Vector2 drawCenter = Application.isPlaying
            ? centerPosition
            : new Vector2((leftBound + rightBound) / 2, initialPositionY);

        if (!Application.isPlaying)
        {
            widthRadius = (rightBound - leftBound) / 2;
        }

        float step = 0.1f; // 描画の細かさ

        // Enumのすべてのパターンをループして描画
        foreach (MovementPattern pattern in System.Enum.GetValues(typeof(MovementPattern)))
        {
            // パターンごとに色を変える
            switch (pattern)
            {
                case MovementPattern.Standard:
                    Gizmos.color = Color.white;
                    break;
                case MovementPattern.Figure8:
                    Gizmos.color = Color.yellow;
                    break;
                case MovementPattern.EasedHover:
                    Gizmos.color = Color.cyan;
                    break;
                case MovementPattern.Astroid:
                    Gizmos.color = Color.magenta;
                    break;
                default:
                    Gizmos.color = Color.gray;
                    break;
            }

            // 現在選択されているパターンは強調（不透明）、それ以外は半透明にする
            if (pattern != movementPattern)
            {
                Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.3f);
            }

            Vector2 prevPos = drawCenter + CalculateOffset(0, pattern);

            // 0 から 2π まで線を描画
            for (float t = step; t <= 2.0f * Mathf.PI + step; t += step)
            {
                Vector2 nextPos = drawCenter + CalculateOffset(t, pattern);
                Gizmos.DrawLine(prevPos, nextPos);
                prevPos = nextPos;
            }
        }

        // --- 左腕攻撃の範囲描画 ---
        if (leftArmObject != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f); // 赤色半透明

            // 1. 発射基準位置の計算 (実行中かどうかで計算元を変える)
            // rightFlagの状態も考慮する必要があるため、Application.isPlayingで分岐
            bool isRight = Application.isPlaying ? rightFlag : false; // エディタ停止中は左向き(false)と仮定

            Vector3 armPos = leftArmObject.transform.position;
            Vector3 offset = leftArmAttackSpawnOffset;
            if (isRight)
                offset.x *= -1;

            Vector3 spawnCenter = armPos + offset; // ここが発射原点

            // 2. 範囲の線を描画
            float rayLength = 6.0f; // 線の長さ

            // 最小角度のベクトル
            Vector2 minDir = Quaternion.Euler(0, 0, leftArmAttackMinAngle) * Vector2.left;
            if (isRight)
                minDir.x *= -1;

            // 最大角度のベクトル
            Vector2 maxDir = Quaternion.Euler(0, 0, leftArmAttackMaxAngle) * Vector2.left;
            if (isRight)
                maxDir.x *= -1;

            // 線を描く
            Gizmos.DrawLine(spawnCenter, spawnCenter + (Vector3)minDir * rayLength);
            Gizmos.DrawLine(spawnCenter, spawnCenter + (Vector3)maxDir * rayLength);

            // 扇形を簡易的に描画（分割して線を引く）
            int segments = 10;
            Vector3 prevPoint = spawnCenter + (Vector3)minDir * rayLength;
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                float currentAngle = Mathf.Lerp(leftArmAttackMinAngle, leftArmAttackMaxAngle, t);

                Vector2 currentDir = Quaternion.Euler(0, 0, currentAngle) * Vector2.left;
                if (isRight)
                    currentDir.x *= -1;

                Vector3 nextPoint = spawnCenter + (Vector3)currentDir * rayLength;
                Gizmos.DrawLine(prevPoint, nextPoint); // 外周を結ぶ
                prevPoint = nextPoint;
            }

            // 中心と外周を結ぶ線を薄く引く（扇形っぽく見えるように）
            Gizmos.DrawLine(spawnCenter, prevPoint);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using CriWare;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// DesertTempleGolemTurretBossの分身（クローン）を制御するスクリプト。
/// 本体の命令に従って、レーザー（パターンB）や連続着弾（パターンC）などの攻撃を同期して行います。
/// </summary>
[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class DesertTempleGolemTurretBossClone : MonoBehaviour
{
    private const string CLONE_LOCKON_MARK_POOLTAG = "CloneLockOnMarkEffect";

    #region --- インスペクター設定 ---

    #region 基本設定
    [Header("分身の独自設定")]
    [SerializeField, Tooltip("連続着弾攻撃（パターンC）で狙うターゲットの数")]
    private int multiShootTargetCount = 3; // 本体とは独立して設定可能
    #endregion

    #region パーツ設定
    [Header("パーツ設定")]
    [SerializeField, Tooltip("照準のピボット（ビームの回転の中心）")]
    private Transform aimPivot;

    [SerializeField, Tooltip("頭のピボット（上下反転の判定用）")]
    private Transform headPivot;

    [SerializeField, Tooltip("ビームの当たり判定を持つオブジェクト")]
    private GameObject beamObject;

    [SerializeField, Tooltip("予測線を描画するLineRenderer")]
    private LineRenderer predictionLine;

    [SerializeField, Tooltip("顔部分のSpriteRenderer（顔の向き変更用）")]
    private SpriteRenderer faceSpriteRenderer;
    #endregion

    #region 演出設定
    [Header("フェード演出設定")]
    [SerializeField, Tooltip("登場・退場時にフェードさせるSpriteRendererのリスト")]
    private List<SpriteRenderer> fadeTargetRenderers = new List<SpriteRenderer>();
    #endregion

    #endregion --- インスペクター設定 ---

    #region --- 内部変数 ---

    #region 外部引き継ぎデータ
    // --- 外部から受け取る顔スプライト ---
    private Sprite defaultFaceSprite;
    private Sprite lookUpFaceSprite;
    private Sprite lookDownFaceSprite;

    // --- 領域設定（本体から引き継ぎ） ---
    private float leftBound;
    private float rightBound;
    private float groundY;
    private float ceilingY;
    #endregion

    #region コンポーネントキャッシュ
    private Transform playerTransform;
    private Animator animator;
    private CriWare.Assets.CriAtomSePlayer _sePlayer;
    private SpriteRenderer beamSpriteRenderer;
    private BoxCollider2D beamCollider;
    private ContactDamageController beamDamageController;
    #endregion

    #region 状態管理フラグ
    private bool isActiveClone = false; // 現在アクティブとして動作しているか
    private LayerMask obstacleLayer;
    #endregion

    #region ビーム制御パラメータ
    private float defaultBeamHeight;
    private float targetBeamLength;
    #endregion

    #region 顔の向き状態管理
    private enum FaceType
    {
        Default,
        LookUp,
        LookDown,
    }

    private FaceType currentFaceType = FaceType.Default;
    private const float FACE_ANGLE_THRESHOLD = 25f; // 顔の向きが変わる角度の閾値
    #endregion

    #endregion --- 内部変数 ---

    #region --- Unityライフサイクル ---

    /// <summary>
    /// コンポーネントの初期化を行います。
    /// 初期状態では各種オブジェクトを非表示にします。
    /// </summary>
    private void Awake()
    {
        // 予測線が貫通しないように地形レイヤーを取得
        obstacleLayer = LayerMask.GetMask(GameConstants.PHYSICS_LAYER_NAME_GROUND);

        animator = this.GetComponent<Animator>();
        _sePlayer = this.GetComponent<CriWare.Assets.CriAtomSePlayer>();

        // ビーム関連コンポーネントのキャッシュと初期化
        if (beamObject != null)
        {
            beamObject.SetActive(false);
            beamSpriteRenderer = beamObject.GetComponent<SpriteRenderer>();
            beamCollider = beamObject.GetComponent<BoxCollider2D>();
            beamDamageController = beamObject.GetComponent<ContactDamageController>();
            if (beamSpriteRenderer != null)
                defaultBeamHeight = beamSpriteRenderer.size.y;
        }

        if (predictionLine != null)
            predictionLine.gameObject.SetActive(false);

        // 初期状態は非アクティブにして待機
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Animatorなどの内部処理が終わった後の、フレームの最後に呼び出されます。
    /// ここで強制的にスプライトを適用し続けることで、Animatorによる意図しない上書きを防ぎます。
    /// </summary>
    private void LateUpdate()
    {
        // ポーズ中は処理しない
        if (TimeManager.instance.isEnemyMovePaused)
            return;
        if (faceSpriteRenderer == null)
            return;

        // 現在の顔の状態に応じてスプライトを強制適用する
        switch (currentFaceType)
        {
            case FaceType.LookUp:
                if (lookUpFaceSprite != null)
                    faceSpriteRenderer.sprite = lookUpFaceSprite;
                break;
            case FaceType.LookDown:
                if (lookDownFaceSprite != null)
                    faceSpriteRenderer.sprite = lookDownFaceSprite;
                break;
            case FaceType.Default:
            default:
                if (defaultFaceSprite != null)
                    faceSpriteRenderer.sprite = defaultFaceSprite;
                break;
        }
    }

    #endregion --- Unityライフサイクル ---

    #region --- パブリックメソッド (本体からのAPI) ---

    /// <summary>
    /// 本体から情報を引き継いでクローンを初期化します。
    /// </summary>
    /// <param name="player">プレイヤーのTransform</param>
    /// <param name="left">行動範囲の左端</param>
    /// <param name="right">行動範囲の右端</param>
    /// <param name="ground">地面のY座標</param>
    /// <param name="ceiling">天井のY座標</param>
    /// <param name="defSprite">通常時の顔スプライト</param>
    /// <param name="upSprite">上向きの顔スプライト</param>
    /// <param name="downSprite">下向きの顔スプライト</param>
    public void Setup(
        Transform player,
        float left,
        float right,
        float ground,
        float ceiling,
        Sprite defSprite,
        Sprite upSprite,
        Sprite downSprite
    )
    {
        playerTransform = player;
        leftBound = left;
        rightBound = right;
        groundY = ground;
        ceilingY = ceiling;

        // 顔スプライトを本体から共有
        defaultFaceSprite = defSprite;
        lookUpFaceSprite = upSprite;
        lookDownFaceSprite = downSprite;
    }

    /// <summary>
    /// クローンの出現演出を行います。
    /// フェードインしながら画面に登場します。
    /// </summary>
    public void Show()
    {
        if (isActiveClone)
            return;

        isActiveClone = true;
        gameObject.SetActive(true);
        animator.SetTrigger("IdleTrigger");

        // 登場SEの再生
        _sePlayer.Play(SE_EnemyAction.Spawn2);

        // フェードイン演出 (インスペクターで登録した全てのSpriteRendererが対象)
        SetAlpha(0f); // まずは完全に透明にしてから
        FadeTo(1f, 0.5f).SetEase(Ease.InQuad);
    }

    /// <summary>
    /// クローンの退場処理を行います。
    /// Destroyせずにフェードアウトして非表示にし、オブジェクトプールへの返却に備えます。
    /// </summary>
    public void Despawn()
    {
        if (!isActiveClone)
            return;

        isActiveClone = false;

        // 実行中の攻撃コルーチン（レーザーや連続着弾のタメ）を全て強制停止
        StopAllCoroutines();

        // 攻撃中のビームや予測線が残っていれば強制的に非表示にする
        if (beamObject != null)
            beamObject.SetActive(false);
        if (predictionLine != null)
            predictionLine.gameObject.SetActive(false);

        // 再生中の攻撃SE（チャージ音など）を全て停止
        if (_sePlayer != null)
        {
            _sePlayer.Stop();
        }

        // アウトラインなどのエフェクトを消すためにタグを外す
        this.tag = GameConstants.UNTAGGED_TAG_NAME;

        var sePlayerComponent = GetComponent<CriWare.Assets.CriAtomSePlayer>();
        if (sePlayerComponent != null)
        {
            sePlayerComponent.StopOnDisable = false; // 非表示になっても消滅SEが途切れないように一時的にフラグを解除
            sePlayerComponent.Play(SE_EnemyAction.Disappear1); // 消滅SE再生
        }

        // フェードアウト実行 -> 完了したら非アクティブ化
        Sequence seq = FadeTo(0f, 0.5f);
        seq.OnComplete(() =>
        {
            gameObject.SetActive(false);
            if (sePlayerComponent != null)
                sePlayerComponent.StopOnDisable = true; // 次回の利用のためにフラグを元に戻す
        });
    }

    #endregion --- パブリックメソッド (本体からのAPI) ---

    #region --- 攻撃パターン制御 ---

    #region パターンB: レーザー攻撃
    /// <summary>
    /// パターンB（通常レーザー）を本体と同期して実行する外部呼び出し用メソッド。
    /// </summary>
    public void ExecutePatternB_Laser(
        float aimingDuration,
        float lockOnDuration,
        float expandSpeed,
        float firingDuration,
        Color aimColor,
        Color lockOnColor,
        float lineWidth,
        int damage
    )
    {
        if (!isActiveClone)
            return;

        StartCoroutine(
            LaserRoutine(
                aimingDuration,
                lockOnDuration,
                expandSpeed,
                firingDuration,
                aimColor,
                lockOnColor,
                lineWidth,
                damage
            )
        );
    }

    /// <summary>
    /// パターンBの実際の処理を行うコルーチン。
    /// </summary>
    private IEnumerator LaserRoutine(
        float aimingDuration,
        float lockOnDuration,
        float expandSpeed,
        float firingDuration,
        Color aimColor,
        Color lockOnColor,
        float lineWidth,
        int damage
    )
    {
        // 予測線の初期設定
        if (predictionLine != null)
        {
            predictionLine.gameObject.SetActive(true);
            predictionLine.startWidth = lineWidth;
            predictionLine.endWidth = lineWidth;
            predictionLine.startColor = aimColor;
            predictionLine.endColor = aimColor;
        }

        // --- 1. 追尾フェーズ ---
        float timer = 0f;
        CriAtomExPlayback chargeSePlayback = _sePlayer.Play(SE_EnemyAction.LaserCharge1); // チャージSE再生
        while (timer < aimingDuration)
        {
            if (playerTransform != null && aimPivot != null)
            {
                // プレイヤーの方向を向くように滑らかに回転
                Vector2 targetDir = (
                    (Vector2)playerTransform.position - (Vector2)aimPivot.position
                ).normalized;
                float targetAngle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;
                aimPivot.rotation = Quaternion.Lerp(
                    aimPivot.rotation,
                    Quaternion.Euler(0, 0, targetAngle),
                    Time.deltaTime * 5.0f
                );

                // 照準の角度に合わせて顔の向きを更新
                UpdateFaceSpriteByDirection(aimPivot.right);
            }

            DrawPredictionLine();

            // 予測線のアニメーション（スクロール）
            if (predictionLine.material != null)
                predictionLine.material.mainTextureOffset -= new Vector2(3.0f * Time.deltaTime, 0);

            timer += Time.deltaTime;
            yield return null;
        }

        // --- 2. ロックオンフェーズ ---
        if (predictionLine != null)
        {
            predictionLine.startColor = lockOnColor;
            predictionLine.endColor = lockOnColor;
        }

        timer = 0f;
        chargeSePlayback.Stop(); // チャージSE停止
        CriAtomExPlayback lockOnSePlayback = _sePlayer.Play(SE_EnemyAction.LaserCharge_Full1); // ロックオンSE再生
        while (timer < lockOnDuration)
        {
            // 追尾は止めるが、線の長さ計算と顔の向き更新は継続
            DrawPredictionLine();
            if (aimPivot != null)
                UpdateFaceSpriteByDirection(aimPivot.right);

            // 警告演出：予測線を脈打たせる
            float pulseWidth = lineWidth + Mathf.PingPong(Time.time * 5f, 0.15f);
            predictionLine.startWidth = pulseWidth;
            predictionLine.endWidth = pulseWidth;

            timer += Time.deltaTime;
            yield return null;
        }
        lockOnSePlayback.Stop(); // ロックオンSE停止

        // --- 3. 発射フェーズ ---
        if (predictionLine != null)
        {
            predictionLine.gameObject.SetActive(false);
            predictionLine.startWidth = lineWidth;
        }

        // ビームに攻撃力を設定
        if (beamDamageController != null)
        {
            beamDamageController.SetNormalDamage(damage);
        }

        CriAtomExPlayback laserSePlayback = _sePlayer.Play(SE_EnemyAction.Laser1); // 発射音再生
        if (beamObject != null)
            beamObject.SetActive(true);
        ObjectPooler.SceneInstance.SpawnFromPool(
            DesertTempleGolemTurretBossMoveController.FLASH_EFFECT_POOLTAG,
            aimPivot.position,
            Quaternion.identity
        ); // 発射フラッシュエフェクト

        // ビームを指定速度で目標の長さまで伸ばす
        float currentLength = 0f;
        UpdateBeamSize(currentLength);
        while (currentLength < targetBeamLength)
        {
            currentLength += expandSpeed * Time.deltaTime;
            if (currentLength > targetBeamLength)
                currentLength = targetBeamLength;
            UpdateBeamSize(currentLength);
            yield return null;
        }

        yield return new WaitForSeconds(firingDuration);

        // --- 4. 終了処理 ---
        laserSePlayback.Stop(); // 発射音停止
        if (beamObject != null)
            beamObject.SetActive(false);

        currentFaceType = FaceType.Default; // 顔の向きを正面に戻す
    }
    #endregion パターンB: レーザー攻撃

    #region パターンC: 連続着弾攻撃
    /// <summary>
    /// パターンC（連続着弾）を本体と同期して実行する外部呼び出し用メソッド。
    /// </summary>
    public void ExecutePatternC_MultiShoot(
        float interval,
        float speed,
        float warningInterval,
        int damage
    )
    {
        if (!isActiveClone)
            return;
        StartCoroutine(MultiShootRoutine(interval, speed, warningInterval, damage));
    }

    /// <summary>
    /// パターンCの実際の処理を行うコルーチン。
    /// </summary>
    private IEnumerator MultiShootRoutine(
        float interval,
        float speed,
        float warningInterval,
        int damage
    )
    {
        List<Vector2> targetPositions = new List<Vector2>();
        List<GameObject> warningMarks = new List<GameObject>();

        // --- 1. ターゲットの決定と予告マーク表示フェーズ ---
        for (int i = 0; i < multiShootTargetCount; i++) // 自身の独自ターゲット数を使用
        {
            // 範囲内からランダムな座標を生成
            float randX = Random.Range(leftBound, rightBound);
            float randY = Random.Range(groundY, ceilingY);
            Vector2 targetPos = new Vector2(randX, randY);
            targetPositions.Add(targetPos);

            // 予告マークの出現位置（照準ピボット、なければ自身）
            Vector2 spawnPos =
                aimPivot != null ? (Vector2)aimPivot.position : (Vector2)transform.position;

            // 予告マークを aimPivot の位置から生成
            GameObject mark = ObjectPooler.SceneInstance.SpawnFromPool(
                CLONE_LOCKON_MARK_POOLTAG,
                spawnPos,
                Quaternion.identity
            );
            warningMarks.Add(mark);
            _sePlayer.Play(SE_EnemyAction.LockOn1); // 予告音を再生

            // DOTweenを用いて、目標座標まで滑らかに移動（射出）させる
            if (mark != null)
            {
                mark.transform.DOMove(targetPos, warningInterval).SetEase(Ease.OutQuart);
            }

            // 移動完了まで待機
            yield return new WaitForSeconds(warningInterval);
        }

        // 発射前のタメ
        yield return new WaitForSeconds(0.5f);

        // --- 2. 発射フェーズ ---
        for (int i = 0; i < targetPositions.Count; i++)
        {
            Vector2 target = targetPositions[i];
            GameObject mark = warningMarks[i];
            Vector2 spawnPos = aimPivot != null ? aimPivot.position : transform.position;

            // 発射するターゲットに向けて顔の向きを更新
            UpdateFaceSpriteByDirection(target - spawnPos);

            // 弾の生成
            GameObject bullet = ObjectPooler.SceneInstance.SpawnFromPool(
                DesertTempleGolemTurretBossMoveController.MULTI_SHOOT_BULLET_POOLTAG,
                spawnPos,
                Quaternion.identity
            );

            if (bullet != null)
            {
                // 弾に攻撃力を設定
                var damageController = bullet.GetComponent<ContactDamageController>();
                if (damageController != null)
                {
                    damageController.SetNormalDamage(damage);
                }

                _sePlayer.Play(SE_EnemyAction.Shoot3); // 発射音を再生
                // 弾の追従と消去を管理するコルーチンを個別に起動
                StartCoroutine(TrackAndDestroyBullet(bullet, target, mark, speed));
            }
            else
            {
                // 万が一弾が生成できなかった場合は、予告マークを直接プールへ返却
                if (mark != null && mark.activeInHierarchy)
                    mark.GetComponent<PoolableObject>()?.ReturnToPool();
            }

            yield return new WaitForSeconds(interval);
        }

        currentFaceType = FaceType.Default; // 全て撃ち終わったら顔を正面に戻す
    }

    /// <summary>
    /// パターンC用のローカルコルーチン。
    /// 弾を目標地点まで動かし、到達したら着弾エフェクトを出して、弾と予告マークを消去（プールへ返却）します。
    /// </summary>
    /// <param name="bullet">移動させる弾のGameObject</param>
    /// <param name="targetPos">着弾目標の座標</param>
    /// <param name="warningMark">着弾地点に表示されている予告マークのGameObject</param>
    /// <param name="speed">弾の移動速度</param>
    private IEnumerator TrackAndDestroyBullet(
        GameObject bullet,
        Vector2 targetPos,
        GameObject warningMark,
        float speed
    )
    {
        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // 目標への方向を計算し、速度と角度を設定
            Vector2 dir = (targetPos - (Vector2)bullet.transform.position).normalized;
            rb.velocity = dir * speed;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        // 弾が目標地点に到達するか、一定時間経過するまで監視
        float timeout = 5.0f; // タイムアウト時間
        while (bullet.activeInHierarchy && timeout > 0)
        {
            // 目標付近（誤差範囲内）に到達したら返却処理
            if (Vector2.Distance(bullet.transform.position, targetPos) < 0.5f)
            {
                // 着弾エフェクトの生成
                ObjectPooler.SceneInstance.SpawnFromPool(
                    DesertTempleGolemTurretBossMoveController.IMPACT_EFFECT_POOLTAG,
                    targetPos,
                    Quaternion.identity
                );

                // 弾と予告マークをPoolableObjectを通じてプールに戻す
                bullet.GetComponent<PoolableObject>()?.ReturnToPool();
                if (warningMark != null && warningMark.activeInHierarchy)
                    warningMark.GetComponent<PoolableObject>()?.ReturnToPool();

                yield break; // コルーチンを終了
            }
            timeout -= Time.deltaTime;
            yield return null;
        }

        // タイムアウトした場合は安全のためPoolableObjectを通じてプールに戻す
        if (bullet.activeInHierarchy)
            bullet.GetComponent<PoolableObject>()?.ReturnToPool();
        if (warningMark != null && warningMark.activeInHierarchy)
            warningMark.GetComponent<PoolableObject>()?.ReturnToPool();
    }
    #endregion パターンC: 連続着弾攻撃

    #endregion --- 攻撃パターン制御 ---

    #region --- 内部ヘルパーメソッド ---

    #region ビーム・予測線関連
    /// <summary>
    /// 障害物に向けてレイキャストを飛ばし、予測線とビームの目標長さを計算して描画します。
    /// </summary>
    private void DrawPredictionLine()
    {
        if (aimPivot == null || predictionLine == null)
            return;

        Vector2 direction = aimPivot.right;
        Vector2 origin = aimPivot.position;
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, 25.0f, obstacleLayer);

        Vector3 endPosition;
        if (hit.collider != null)
        {
            endPosition = hit.point; // 壁に当たった場所まで
            targetBeamLength = hit.distance;
        }
        else
        {
            endPosition = origin + direction * 25.0f; // 当たらなければ最大射程まで
            targetBeamLength = 25.0f;
        }

        predictionLine.SetPosition(0, origin);
        predictionLine.SetPosition(1, endPosition);
    }

    /// <summary>
    /// ビームの長さ（スプライトとコライダー）を更新します。
    /// </summary>
    private void UpdateBeamSize(float length)
    {
        if (beamSpriteRenderer != null)
            beamSpriteRenderer.size = new Vector2(length, defaultBeamHeight);

        if (beamCollider != null)
        {
            beamCollider.size = new Vector2(length, beamCollider.size.y);
            // 左端を基準にするため半分だけ右にオフセットをずらす
            beamCollider.offset = new Vector2(length / 2f, beamCollider.offset.y);
        }
    }
    #endregion

    #region 顔・スプライト関連
    /// <summary>
    /// 指定された方向ベクトルに基づいて、顔の向き状態を更新します。
    /// </summary>
    /// <param name="direction">狙っている方向のベクトル</param>
    private void UpdateFaceSpriteByDirection(Vector2 direction)
    {
        if (faceSpriteRenderer == null)
            return;

        // 左右の向きを無視して、上下の傾き角度を計算
        float angle = Mathf.Atan2(direction.y, Mathf.Abs(direction.x)) * Mathf.Rad2Deg;

        // 自身の上下が反対（逆さま）かどうかを判定
        bool isUpsideDown = false;

        // headPivotが設定されていればそのZ回転を、なければ自身のZ回転を確認
        float zAngle = headPivot != null ? headPivot.eulerAngles.z : transform.eulerAngles.z;

        // Z回転が90度〜270度の間（≒180度）であれば逆さまとみなす
        if (zAngle > 90f && zAngle < 270f)
        {
            isUpsideDown = true;
        }

        // 逆さまの場合は、ワールドでの上下と顔にとっての上下が逆になるため角度を反転
        if (isUpsideDown)
        {
            angle = -angle;
        }

        if (angle > FACE_ANGLE_THRESHOLD)
            currentFaceType = FaceType.LookUp;
        else if (angle < -FACE_ANGLE_THRESHOLD)
            currentFaceType = FaceType.LookDown;
        else
            currentFaceType = FaceType.Default;
    }
    #endregion

    #region フェード演出関連
    /// <summary>
    /// リストに登録された全パーツの透明度を一括変更するSequenceを返します。
    /// </summary>
    /// <param name="targetAlpha">目標の透明度 (0f ～ 1f)</param>
    /// <param name="duration">フェードにかける時間</param>
    private Sequence FadeTo(float targetAlpha, float duration)
    {
        Sequence seq = DOTween.Sequence();
        foreach (var sr in fadeTargetRenderers)
        {
            if (sr != null)
            {
                seq.Join(sr.DOFade(targetAlpha, duration));
            }
        }
        return seq;
    }

    /// <summary>
    /// リストに登録された全パーツの透明度を即座にセットします。
    /// 初期化時などに使用します。
    /// </summary>
    private void SetAlpha(float alpha)
    {
        foreach (var sr in fadeTargetRenderers)
        {
            if (sr != null)
            {
                SetSpriteAlpha(sr, alpha);
            }
        }
    }

    /// <summary>
    /// 個別SpriteRendererの透明度を直接書き換えるヘルパーメソッド。
    /// </summary>
    private void SetSpriteAlpha(SpriteRenderer sr, float alpha)
    {
        Color c = sr.color;
        c.a = alpha;
        sr.color = c;
    }
    #endregion

    #endregion --- 内部ヘルパーメソッド ---
}

using System.Collections;
using System.Collections.Generic;
using Effekseer;
using UnityEngine;

public class StoneGolemMoveController : MonoBehaviour
{
    [Header("腕の設定")]
    [SerializeField]
    private BodyArmConfig[] configs;

    [System.Serializable]
    public class BodyArmConfig
    {
        public Sprite bodySprite; // 表示するボディのスプライト
        public Sprite armSprite; // 対応するアームのスプライト
        public Vector2 armLocalPosition; // 対応するローカル座標
    }

    [Header("ハンマーの設定")]
    [SerializeField]
    private BodyHammerConfig[] hammerConfigs;

    [System.Serializable]
    public class BodyHammerConfig
    {
        public Sprite bodySprite; // 表示するボディのスプライト
        public Vector2 hammerLocalPosition; // 対応するローカル座標
        public float hammerAngle; // 対応する角度(左向き時の)
    }

    [Header("必殺技の設定")]
    [SerializeField]
    private EnemyData rockEnemyData = null; // 岩の敵データ

    [Header("基本設定")]
    [SerializeField]
    private Transform playerTransform; // プレイヤーのTransform

    [SerializeField]
    private bool useAppearanceEffect = false; // 登場演出を行うかどうか

    [Header("ダメージ数値")]
    [SerializeField]
    private int meleeAttackDamage = 0; // 近距離攻撃のダメージ量

    [SerializeField]
    private int crawlingRockDamage = 0; // 這う岩のダメージ量

    [SerializeField]
    private int hammerDamage = 0; // ハンマー攻撃のダメージ量

    [SerializeField]
    private int chargeAttackDamage = 0; // チャージ攻撃のダメージ量

    [Header("攻撃パラメータ")]
    [SerializeField, Tooltip("強力な技までに必要な攻撃回数")]
    private int strongAttackCount = 5;

    [SerializeField, Tooltip("一度に生成する岩の数")]
    private int rocksPerWave = 5;

    [SerializeField, Tooltip("岩の生成回数")]
    private int rockWaves = 5;

    [Header("HP閾値 (割合で指定)")]
    [SerializeField, Tooltip("HPがこの割合以下になったらダッシュを開始")]
    private float dashHpThresholdRatio = 70; // ダッシュ開始HP割合

    [SerializeField, Tooltip("HPがこの割合以下になったらチャージ攻撃を開始")]
    private float chargeAttackHpThresholdRatio = 40; // 地面踏みつけ

    [SerializeField, Tooltip("HPがこの割合以下になったら雑魚召喚を一度開始")]
    private float summonHpThresholdRatio = 50f; // 雑魚召喚

    [Header("間合い・移動パラメータ")]
    [SerializeField, Tooltip("近距離攻撃を行うための間合い")]
    private float attackRange = 3f;

    [SerializeField, Tooltip("通常の移動速度")]
    private float walkSpeed = 5f;

    [SerializeField, Tooltip("ダッシュ時の移動速度")]
    private float dashSpeed = 12f;

    [SerializeField, Tooltip("溜め技移行前のダッシュ速度")]
    private float chargeDashSpeed = 30f; // チャージダッシュの速度

    [SerializeField, Tooltip("歩行時の最大移動距離")]
    private float walkStepMaxDistance = 2.0f;

    [SerializeField, Tooltip("ダッシュ停止するまでの距離")]
    private float dashStopDistance = 1.0f;

    [SerializeField, Tooltip("這う岩の移動速度")]
    private float crawlingRockSpeed = 15f;

    [SerializeField, Tooltip("這う岩が画面外に出てから消えるまでの猶予距離")]
    private float crawlingRockDestroyBuffer = 0.3f;

    [SerializeField]
    private float rockOffsetX = 1f; // 岩の着弾位置のX方向オフセット

    [SerializeField, Tooltip("岩が到達する高さ")]
    private float peakHeight = 10f; // 岩が到達する最大の高さ（ローカルY座標）

    [SerializeField]
    private Vector2 slashEffectOffset = new Vector2(-1.1f, 2.15f); // スラッシュエフェクトのオフセット(左向き時)

    [SerializeField]
    private float chargeEffectOffsetY = 3.5f; // チャージエフェクトのY方向オフセット

    [SerializeField]
    private float chargeEffectScale = 16f; // チャージエフェクトのスケール

    [SerializeField, Tooltip("衝撃波エフェクトのX方向オフセット")]
    private float shockWaveEffectOffsetX = 1.12f; // 衝撃波エフェクトのX方向オフセット

    [SerializeField]
    private float dustEffectOffsetX = 2.5f; // ダストエフェクトのX方向オフセット

    [SerializeField, Tooltip("這う岩のダストエフェクトが発生する間隔")]
    private float crawlingRockDustEffectInterval = 0.5f; // 這う岩のダストエフェクトが発生する間隔

    [Header("行動範囲")]
    [SerializeField]
    private float leftBound = 0f; // 左端の座標

    [SerializeField]
    private float rightBound = 0f; // 右端の座標

    [Header("時間パラメータ")]
    [SerializeField]
    private float meleeAttackCooldown = 1.0f; // 近距離攻撃後の待機時間

    [SerializeField]
    private float rangedAttackCooldown = 1.5f; // 遠距離攻撃後の待機時間

    [SerializeField]
    private float hammerChargeTime = 4f; //ハンマー攻撃前のチャージ時間

    [SerializeField]
    private float rockWaveInterval = 0.75f;

    [SerializeField]
    private float hammerAttackCooldown = 1.0f; // ハンマー攻撃後の待機時間

    [Header("ゲームオブジェクト設定")]
    [SerializeField]
    private GameObject armObject; // アームのオブジェクト

    [SerializeField]
    private GameObject hammerObject; // ハンマーのオブジェクト

    [SerializeField]
    private EffekseerEmitter shockWaveEffect; // ショックウェーブエフェクト

    [SerializeField]
    private EffekseerEmitter chargeEffect; // チャージエフェクト

    private enum CommandType
    {
        None,
        Walk, // 移動
        Dash, // ダッシュ
        MeleeAttack, // 近距離攻撃
        RangedAttack, // 遠距離攻撃
        SummonMinions, // 雑魚召喚
        ChargeAttack // チャージ攻撃
        ,
    }

    private CommandType currentCommand = CommandType.None;
    private CommandType lastCommand = CommandType.None;

    private void SetCommand(CommandType command)
    {
        lastCommand = currentCommand;
        currentCommand = command;
    }

    private const float walkDefaultSpeed = 5.0f; // デフォルトの歩行速度
    private const float attackAnimationTime = 0.750f; // 攻撃アニメーションの時間
    private const float hammerReadyAnimationTime = 0.333f; // ハンマー構えアニメーションの時間
    private const float hammerAttackAnimationTime = 0.250f; // ハンマー攻撃アニメーションの時間
    private float dustEffectDuration => rockWaves * rockWaveInterval + hammerAttackCooldown; // ダストエフェクトの持続時間
    private const float rockAngularSpeed = 100f; // 岩の回転速度
    private float crawlingDustEffectOffsetX => crawlingRockSpeed / 3.5f; // 這う岩のダストエフェクトのX方向オフセット
    private const float slashEffectDefaultDuration = 0.35f; // スラッシュエフェクトのデフォルト持続時間
    private int totalActions = 0;
    private int bossMaxHP; //最大HP
    private int bossHP; //現在のHP
    private bool rightFlag = false; // 右向きフラグ
    private bool hasSummonedMinions = false;
    private bool isMoveStarted = false; // 移動が開始されたかどうか
    private const string crawlingRockPoolTag = "StoneGolem_CrawlingRock"; // 這う岩のプールタグ名
    private const string rockPoolTag = "StoneGolem_Rock"; // 岩のプールタグ名
    private const string slashEffectPoolTag = "StoneGolem_SlashEffect"; // スラッシュエフェクトのプールタグ名
    private const string dustEffectPoolTag = "StoneGolem_DustEffect"; // ダストエフェクトのプールタグ名
    private const string crawlingDustEffectPoolTag = "StoneGolem_CrawlingDustEffect"; //這う岩のダストエフェクトのプールタグ名
    private IDamageable hpscript;
    private Rigidbody2D rbody;
    private Animator animator;
    private Sprite previousBodySprite = null;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer armRenderer;
    private Transform armTransform;
    private SpriteRenderer hammerRenderer;
    private Transform hammerTransform;
    private List<GameObject> linkedObjectsToDestroy = new List<GameObject>();

    private void RegisterLinkedObject(GameObject obj)
    {
        if (obj != null && !linkedObjectsToDestroy.Contains(obj))
        {
            linkedObjectsToDestroy.Add(obj);
        }
    }

    private void Awake()
    {
        spriteRenderer = this.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError($" {gameObject.name} にSpriteRendererが見つかりません。");
        }

        if (armObject != null)
        {
            armRenderer = armObject.GetComponent<SpriteRenderer>();
            if (armRenderer == null)
            {
                Debug.LogError($" {armObject.name} にSpriteRendererが見つかりません。");
            }
            armTransform = armObject.transform;
            ContactDamageController armStateController =
                armObject.GetComponent<ContactDamageController>();
            if (armStateController == null)
            {
                Debug.LogError($" {armObject.name} にEnemyStateControllerが見つかりません。");
            }
            else
            {
                armStateController.SetDamageAmount(meleeAttackDamage); // アームのダメージ量を設定
            }
            armObject.tag = GameConstants.DamageableEnemyTagName; // アームのタグを設定
            armObject.SetActive(false); // 初期状態ではアームオブジェクトを非表示にする
        }
        else
        {
            Debug.LogError("アームオブジェクトが設定されていません。");
        }

        if (hammerObject != null)
        {
            hammerRenderer = hammerObject.GetComponent<SpriteRenderer>();
            if (hammerRenderer == null)
            {
                Debug.LogError($" {hammerObject.name} にSpriteRendererが見つかりません。");
            }
            hammerTransform = hammerObject.transform;
            ContactDamageController stateController =
                hammerObject.GetComponent<ContactDamageController>();
            if (stateController == null)
            {
                Debug.LogError($" {hammerObject.name} にEnemyStateControllerが見つかりません。");
            }
            else
            {
                stateController.SetDamageAmount(hammerDamage); // ハンマーのダメージ量を設定
            }
            hammerObject.tag = GameConstants.DamageableEnemyTagName; // ハンマーのタグを設定
            hammerObject.SetActive(false); // 初期状態ではハンマーオブジェクトを非表示にする
        }
        else
        {
            Debug.LogError("ハンマーオブジェクトが設定されていません。");
        }

        if (rockEnemyData == null)
        {
            Debug.LogError("rockEnemyDataが設定されていません。");
        }

        if (chargeEffect == null)
        {
            Debug.LogError("ChargeEffect が設定されていません。");
        }

        if (shockWaveEffect == null)
        {
            Debug.LogError("ShockWaveEffect が設定されていません。");
        }

        rbody = this.GetComponent<Rigidbody2D>();
        if (rbody == null)
        {
            Debug.LogError($" {gameObject.name} にRigidbody2Dが見つかりません。");
        }
        else
        {
            rbody.constraints = RigidbodyConstraints2D.FreezeRotation;
            rbody.velocity = Vector2.zero; // 初期速度をゼロに設定
        }

        animator = this.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError($" {gameObject.name} にAnimatorが見つかりません。");
        }
    }

    private void Start()
    {
        if (hammerRenderer != null)
        {
            //Awakeで行ってはいけない
            hammerRenderer.material.EnableKeyword("FADE_ON");
        }
        ResetState();
    }

    public void ResetState()
    {
        if (playerTransform == null)
        {
            playerTransform = GameObject
                .FindGameObjectWithTag(GameConstants.PlayerTagName)
                .transform;
            if (playerTransform == null)
            {
                Debug.LogError($"{this.name}はPlayerObjectを見つけられませんでした");
            }
        }

        hpscript = this.GetComponent<BossHealth>(); //hpのscriptを取得
        if (hpscript == null)
        {
            Debug.LogError($"{this.name}はBossHealthスクリプトを持っていません");
            return;
        }
        bossMaxHP = hpscript.MaxHP; //最大HPを取得
        bossHP = bossMaxHP; //現在のHPを初期化

        // 破棄するオブジェクトのリストを初期化
        DestroyLinkedObjects();

        this.tag = GameConstants.ImmuneEnemyTagName; // タグをダメージを受けない敵のタグに設定
        rightFlag = false; // 初期は左向き

        if (useAppearanceEffect)
        {
            animator.SetTrigger("spawnTrigger"); // 登場アニメーションをトリガー
        }

        this.GetComponent<BossHealth>().enabled = false; // BossHealthスクリプトを無効化
        isMoveStarted = false; // 移動開始フラグをリセット
    }

    private void FixedUpdate()
    {
        if (playerTransform == null)
            return;

        //敵の動きがポーズされているかどうかを確認
        // もしポーズされていればRigidbody2Dを無効化する
        if (TimeManager.instance.isEnemyMovePaused)
        {
            if (rbody.simulated)
                rbody.simulated = false;
            return;
        }
        else if (!rbody.simulated)
            rbody.simulated = true;

        if (!isMoveStarted)
        {
            this.GetComponent<BossHealth>().enabled = true; // BossHealthスクリプトを有効化
            isMoveStarted = true;
        }

        // プレイヤーの位置と自分の位置のX座標の差を計算
        float distanceToPlayer = Mathf.Abs(playerTransform.position.x - transform.position.x);
        bossHP = hpscript.CurrentHP; //現在のHPを取得
        float hpPercent = (float)bossHP / (float)bossMaxHP;
        spriteRenderer.flipX = rightFlag; // スプライトの向きを更新

        if (currentCommand == CommandType.None)
        {
            // 雑魚召喚（1度だけ）
            if (!hasSummonedMinions && hpPercent <= summonHpThresholdRatio / 100f)
            {
                Debug.Log("雑魚召喚！");
                lastCommand = CommandType.SummonMinions;
                hasSummonedMinions = true;
                totalActions++;
                return;
            }

            // 地面踏みつけ + 跳ね石爆撃
            if (
                hpPercent <= chargeAttackHpThresholdRatio / 100f
                && totalActions >= strongAttackCount
                && (lastCommand != CommandType.Walk || lastCommand != CommandType.Dash)
            )
            {
                SetCommand(CommandType.ChargeAttack);
                StartCoroutine(ChargeAttack());
                totalActions = 0;
                return;
            }

            // HPがx%以下のとき：ダッシュ
            if (hpPercent <= dashHpThresholdRatio / 100f)
            {
                if (distanceToPlayer > attackRange)
                {
                    if (lastCommand == CommandType.Dash)
                    {
                        SetCommand(CommandType.RangedAttack);
                        StartCoroutine(RangedAttack());
                    }
                    else
                    {
                        // 3分の1の確率でダッシュ（それ以外は遠距離攻撃）
                        if (Random.value < 1f / 3f)
                        {
                            SetCommand(CommandType.Dash);
                            StartCoroutine(DashTowardsPlayer());
                        }
                        else
                        {
                            SetCommand(CommandType.RangedAttack);
                            StartCoroutine(RangedAttack());
                        }
                    }
                }
                else
                {
                    SetCommand(CommandType.MeleeAttack);
                    StartCoroutine(MeleeAttack());
                }
            }
            // 通常時：歩き
            else
            {
                if (distanceToPlayer > attackRange)
                {
                    if (lastCommand == CommandType.Walk)
                    {
                        SetCommand(CommandType.RangedAttack);
                        StartCoroutine(RangedAttack());
                    }
                    else
                    {
                        SetCommand(CommandType.Walk);
                        StartCoroutine(WalkStepTowardsPlayer());
                    }
                }
                else
                {
                    SetCommand(CommandType.MeleeAttack);
                    StartCoroutine(MeleeAttack());
                }
            }

            totalActions++;
        }
    }

    /// <summary>
    /// プレイヤーの方向に一定距離だけ歩いて移動するコルーチン。
    /// </summary>
    private IEnumerator WalkStepTowardsPlayer()
    {
        Vector2 start = this.transform.position;
        Vector2 target = new Vector2(playerTransform.position.x, start.y);

        // 進行方向を決定（右:+1、左:-1）
        float dir = Mathf.Sign(target.x - start.x);

        float vx = walkSpeed * dir; // 水平方向の速度
        rightFlag = dir > 0; // 向きを更新

        rbody.velocity = new Vector2(vx, rbody.velocity.y); // Rigidbodyを使って移動

        float time = walkStepMaxDistance / Mathf.Abs(vx); // 移動にかかる時間

        animator.SetBool("isWalking", true); // 歩きアニメーションを開始
        yield return new WaitForSeconds(time); // 指定時間待機
        animator.SetBool("isWalking", false); // 歩きアニメーションを停止

        // 指定距離に達したら停止（x方向の速度をゼロにする）
        rbody.velocity = new Vector2(0f, rbody.velocity.y);

        // 行動記録を更新
        SetCommand(CommandType.None);
    }

    private IEnumerator DashTowardsPlayer()
    {
        float dir = Mathf.Sign(playerTransform.position.x - transform.position.x);
        rightFlag = dir > 0; // 向きを更新
        animator.SetBool("isWalking", true); // 歩きアニメーションを開始

        // ダッシュしながら一定距離以内に入るまで進む
        while (Mathf.Abs(playerTransform.position.x - transform.position.x) > dashStopDistance)
        {
            rbody.velocity = new Vector2(dashSpeed * dir, rbody.velocity.y);
            yield return null;
        }
        animator.SetBool("isWalking", false); // 歩きアニメーションを停止
        // 停止
        rbody.velocity = new Vector2(0f, rbody.velocity.y);

        // 行動記録を更新
        SetCommand(CommandType.None);
    }

    private IEnumerator MeleeAttack()
    {
        float dir = Mathf.Sign(playerTransform.position.x - this.transform.position.x);
        rightFlag = dir > 0; // 向きを更新
        animator.SetTrigger("attackTrigger"); // 攻撃アニメーションをトリガー
        StartCoroutine(UpdateArmForDuration(attackAnimationTime)); // アームの見た目を更新
        yield return new WaitForSeconds(attackAnimationTime * 0.7f); // 攻撃アニメーションの時間を待機1
        SEManager.instance?.PlayEnemyActionSE(SE_EnemyAction.Kick1); // 攻撃の効果音を鳴らす
        yield return new WaitForSeconds(attackAnimationTime * 0.3f); // 攻撃アニメーションの時間を待機2
        yield return new WaitForSeconds(meleeAttackCooldown); // 攻撃後の待機時間

        //行動記録を更新
        SetCommand(CommandType.None);
    }

    private IEnumerator RangedAttack()
    {
        Vector3 myPos = this.transform.position; //自分の座標を保存
        float dir = Mathf.Sign(playerTransform.position.x - myPos.x);
        rightFlag = dir > 0; // 向きを更新

        animator.SetTrigger("attackTrigger"); // 攻撃アニメーションをトリガー
        StartCoroutine(UpdateArmForDuration(attackAnimationTime)); // アームの見た目を更新
        yield return new WaitForSeconds(attackAnimationTime * 0.7f); // 攻撃アニメーションの時間を待機1
        SEManager.instance?.PlayEnemyActionSE(SE_EnemyAction.Kick1); // 攻撃の効果音を鳴らす
        yield return new WaitForSeconds(attackAnimationTime * 0.3f); // 攻撃アニメーションの時間を待機2

        GameObject crawlingRock = ObjectPooler.instance.SpawnFromPool(
            crawlingRockPoolTag,
            transform.position,
            Quaternion.identity
        ); // 這う岩のプレハブを生成

        if (crawlingRock == null)
        {
            //行動記録を更新
            SetCommand(CommandType.None);
            yield break; // プールから取得できなかった場合は中断
        }

        crawlingRock.tag = GameConstants.DamageableEnemyTagName; //這う岩のタグを設定
        ContactDamageController stateController =
            crawlingRock.GetComponent<ContactDamageController>();
        if (stateController == null)
        {
            Debug.LogError($"{crawlingRock.name}にEnemyStateControllerが見つかりません。");
        }
        else
        {
            stateController.SetDamageAmount(crawlingRockDamage); // 這う岩のダメージ量を設定
        }
        myPos.y += 1f; // 這う岩の位置を自分の位置から少し上にずらす
        crawlingRock.transform.position = myPos; //弾の位置を設定

        //岩の画像の半分の幅を取得する
        var rockRenderer = crawlingRock.GetComponent<SpriteRenderer>();
        float rockHalfWidth = 0f;
        if (rockRenderer != null)
        {
            // bounds.extents.x でワールド空間でのスプライトの半分の幅を取得
            rockHalfWidth = rockRenderer.bounds.extents.x;
        }
        else
        {
            Debug.LogWarning(
                "crawlingRockPrefabにSpriteRendererがありません。消去判定が不正確になります。"
            );
        }

        // Rigidbody2Dが必要
        if (crawlingRock.TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.velocity = new Vector2(crawlingRockSpeed * dir, 0); // 這う岩の初速を設定
            StartCoroutine(ManageCrawlingRockLifetime(crawlingRock, rockHalfWidth));
        }

        yield return new WaitForSeconds(rangedAttackCooldown); // 攻撃後の待機時間

        //行動記録を更新
        SetCommand(CommandType.None);
    }

    private IEnumerator ChargeAttack()
    {
        // スプライトサイズとオフセットの補正を取得
        float spriteHalfWidth = spriteRenderer.bounds.extents.x;

        bool toLeft = Random.Range(0, 2) == 0; // 左右どちらに移動するかランダム決定

        // 移動先のX座標を決定（スプライトのサイズ分オフセット）
        float targetX = toLeft ? leftBound + spriteHalfWidth : rightBound - spriteHalfWidth;

        // 開始位置
        float dir = Mathf.Sign(targetX - transform.position.x);
        float vx = chargeDashSpeed * dir;
        rightFlag = dir > 0;

        animator.SetBool("isWalking", true);

        while (true)
        {
            float distance = targetX - transform.position.x;

            // 距離が小さければ終了
            if (Mathf.Abs(distance) <= 0.05f)
                break;

            // 移動方向と現在の進行方向が逆転したら止める（オーバーシュート防止）
            if (Mathf.Sign(distance) != dir)
                break;

            // 移動処理
            rbody.velocity = new Vector2(vx, rbody.velocity.y);
            yield return null;
        }

        // 停止処理
        rbody.velocity = new Vector2(0f, rbody.velocity.y);
        animator.SetBool("isWalking", false);

        // 位置の微調整（念のため）
        Vector2 pos = this.transform.position;
        pos.x = targetX;
        transform.position = pos;

        rightFlag = toLeft; // 向きを更新

        // ハンマー構えアニメーションをトリガー
        animator.SetTrigger("hammerReadyTrigger");
        //ハンマー見た目を更新するコルーチンを開始
        StartCoroutine(UpdateHammer());
        // チャージエフェクトを再生
        if (chargeEffect != null)
        {
            EffekseerEmitter chargeEffectInstance = Instantiate(chargeEffect); //エフェクトを生成
            chargeEffectInstance.transform.SetParent(this.transform); //エフェクトの親をこのオブジェクトに設定
            Vector2 chargeEffectPos = pos; //自分の座標をコピー
            chargeEffectPos.y += chargeEffectOffsetY; //エフェクトのy座標を調整
            chargeEffectInstance.transform.position = chargeEffectPos; //エフェクトの位置を指定
            chargeEffectInstance.speed =
                GameConstants.ChargeEffectDefaultDuration / hammerChargeTime; //エフェクトの速度を指定
            chargeEffectInstance.transform.localScale = new Vector2(
                chargeEffectScale,
                chargeEffectScale
            ); //エフェクトの大きさを指定
            chargeEffectInstance.Play(); //エフェクトを再生
        }

        hammerObject.tag = "Untagged"; // ハンマーのタグを一時的に未設定にする（ダメージを受けない敵のタグに変更）

        float elapsed = 0f;

        while (elapsed < hammerChargeTime)
        {
            float t = elapsed / hammerChargeTime;
            float value = Mathf.Lerp(0.41f, 0f, t);
            hammerRenderer.material.SetFloat("_FadeAmount", value);
            elapsed += Time.deltaTime;
            if (SEManager.instance != null)
            {
                if (!SEManager.instance.IsPlayingEnemyActionSE(SE_EnemyAction.ChargePower1))
                {
                    SEManager.instance.PlayEnemyActionSE(SE_EnemyAction.ChargePower1); //チャージの効果音を鳴らす
                }
            }
            yield return null;
        }

        hammerObject.tag = GameConstants.DamageableEnemyTagName; // ハンマーのタグをダメージを受ける敵のタグに戻す
        // ハンマー攻撃アニメーションをトリガー
        animator.SetTrigger("hammerAttackTrigger");
        // スラッシュエフェクトを生成
        SpawnSlashEffect();
        // スキル名UIを表示
        GameUIManager.instance?.ShowSkillNameUI("地砕");
        SEManager.instance?.PlayEnemyActionSE(SE_EnemyAction.Impact_iron1); //ハンマー攻撃の効果音を鳴らす
        //ハンマー攻撃アニメーションの時間分待機する
        yield return new WaitForSeconds(hammerAttackAnimationTime);

        if (shockWaveEffect != null)
        {
            EffekseerEmitter shockWaveEffectInstance = Instantiate(shockWaveEffect); //エフェクトを生成
            shockWaveEffectInstance.transform.SetParent(this.transform); //エフェクトの親をこのオブジェクトに設定
            Vector2 shockWavePos = this.transform.position; //自分の座標をコピー
            shockWavePos.x += rightFlag ? shockWaveEffectOffsetX : -shockWaveEffectOffsetX; //エフェクトのx座標を調整
            shockWaveEffectInstance.transform.position = shockWavePos; //エフェクトの位置を指定
            float shockwaveRange =
                2 * Mathf.Max(Mathf.Abs(pos.x - leftBound), Mathf.Abs(pos.x - rightBound)); //エフェクトの大きさを取得
            shockWaveEffectInstance.transform.localScale = new Vector3(
                shockwaveRange,
                shockwaveRange,
                0
            ); //エフェクトの大きさを指定
            shockWaveEffectInstance.Play(); //エフェクトを再生
        }
        // ダストエフェクトを生成
        SpawnDustEffect();

        for (int wave = 0; wave < rockWaves; wave++)
        {
            for (int i = 0; i < rocksPerWave; i++)
            {
                // 岩を生成（足元から）
                GameObject rock = ObjectPooler.instance.SpawnFromPool(
                    rockPoolTag,
                    transform.position,
                    Quaternion.identity
                );

                if (rock == null)
                    continue; // プールから取得できなかった場合はスキップ

                SEManager.instance?.PlayFieldSE(SE_Field.Collapse3); // 岩生成の効果音を鳴らす
                var rockHPscript = rock.GetComponent<EnemyHealth>();
                if (rockHPscript == null)
                {
                    Debug.LogError($"{rock.name}にEnemyHealthスクリプトが見つかりません。");
                }
                else
                {
                    rockHPscript.Initialize(rockEnemyData); // 岩の敵データを設定
                }
                rock.tag = GameConstants.DamageableEnemyTagName; // 岩のタグを設定
                ContactDamageController stateController =
                    rock.GetComponent<ContactDamageController>();
                if (stateController == null)
                {
                    Debug.LogError($"{rock.name}にEnemyStateControllerが見つかりません。");
                }
                else
                {
                    stateController.SetDamageAmount(chargeAttackDamage); // 岩のダメージ量を設定
                }

                // 岩の半径を取得（SphereCollider or CircleCollider2D を想定）
                float radius = 0.5f; // デフォルト値
                if (rock.TryGetComponent<CircleCollider2D>(out var collider))
                {
                    radius = collider.radius;
                }

                // 初期位置（自分の真下＋岩の半径分上にずらす）
                Vector3 spawnPos = transform.position;
                spawnPos.y += radius * 1.1f; // 少し上にずらす
                rock.transform.position = spawnPos;

                // Rigidbody2Dが必要
                if (rock.TryGetComponent<Rigidbody2D>(out var rb))
                {
                    // GravityScale を考慮して重力加速度を算出
                    float gravity = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);

                    // 着弾X範囲を決定
                    float minX,
                        maxX;
                    if (toLeft)
                    {
                        minX = transform.position.x + rockOffsetX;
                        maxX = rightBound;
                    }
                    else
                    {
                        minX = leftBound;
                        maxX = transform.position.x - rockOffsetX;
                    }

                    float rockTargetX = Random.Range(minX, maxX);

                    // 水平方向と垂直方向の距離
                    float dx = rockTargetX - spawnPos.x;
                    float dy = peakHeight; // 上昇量

                    // 垂直速度: vy = √(2gh)
                    float rockVy = Mathf.Sqrt(2f * gravity * dy);

                    // 到達時間 t = vy / g
                    float t = rockVy / gravity;

                    // 水平方向の速度 vx = dx / (2t)  ※頂点が中間点の前提
                    float rockVx = dx / (2f * t);

                    // 初速ベクトルを作成
                    Vector2 initialVelocity = new Vector2(rockVx, rockVy);
                    rb.velocity = initialVelocity;
                    rb.angularVelocity = rockAngularSpeed; // 岩の回転速度を設定
                }
            }
            // 次のウェーブまで少し待つ
            yield return new WaitForSeconds(rockWaveInterval);

            if (wave == 0)
            {
                hammerObject.tag = GameConstants.ImmuneEnemyTagName; // ハンマーのタグをダメージを受けない敵のタグに設定
            }
        }

        // ハンマー攻撃後の待機時間
        yield return new WaitForSeconds(hammerAttackCooldown);

        // ハンマー攻撃完了アニメーションをトリガー
        animator.SetTrigger("hammerFinishTrigger");
        //ハンマーの見た目を更新するコルーチンを停止
        StopCoroutine(UpdateHammer());
        if (armObject != null)
        {
            hammerObject.SetActive(false); // ハンマーオブジェクトを無効化
        }

        // 行動記録を更新
        SetCommand(CommandType.None);
    }

    private IEnumerator UpdateArmForDuration(float duration)
    {
        // 指定された時間だけ処理を維持
        float elapsed = 0f;
        previousBodySprite = spriteRenderer.sprite;
        armObject.SetActive(true); // アームオブジェクトを有効化
        armObject.tag = GameConstants.ImmuneEnemyTagName; // アームのタグをダメージを受けない敵のタグに設定

        while (elapsed < duration)
        {
            if (spriteRenderer == null || spriteRenderer.sprite == previousBodySprite)
            {
                elapsed += Time.deltaTime;
                yield return null;
                continue;
            }

            foreach (var config in configs)
            {
                if (spriteRenderer.sprite == config.bodySprite)
                {
                    // アームの見た目と位置を一時的に同期
                    if (armRenderer != null)
                    {
                        armRenderer.sprite = config.armSprite;

                        // 右向きか否かに応じてアームの向きを調整
                        if (rightFlag)
                            armRenderer.flipX = true;
                        else
                            armRenderer.flipX = false;
                    }
                    else
                    {
                        Debug.LogError("アームのSpriteRendererが見つかりません。");
                    }

                    if (armTransform != null)
                    {
                        Vector3 localPos = config.armLocalPosition;

                        // 右向きか否かに応じてアームの向きを調整
                        if (rightFlag)
                            localPos.x *= -1f;

                        armTransform.localPosition = localPos;
                    }

                    //腕の当たり判定の調整
                    if (
                        config == configs[0]
                        && armObject.tag == GameConstants.DamageableEnemyTagName
                    )
                    {
                        // アームのタグをダメージを受けない敵のタグに設定
                        armObject.tag = GameConstants.ImmuneEnemyTagName;
                    }
                    else if (
                        config != configs[0]
                        && armObject.tag == GameConstants.ImmuneEnemyTagName
                    )
                    {
                        // アームのタグをダメージを受ける敵のタグに戻す
                        armObject.tag = GameConstants.DamageableEnemyTagName;
                    }

                    previousBodySprite = config.bodySprite;

                    break;
                }
            }

            previousBodySprite = spriteRenderer.sprite;
            elapsed += Time.deltaTime;
            yield return null;
        }

        armObject.SetActive(false); // アームオブジェクトを無効化
    }

    private IEnumerator UpdateHammer()
    {
        // 指定された時間だけ処理を維持
        float elapsed = 0f;
        previousBodySprite = spriteRenderer.sprite;
        bool isHammerActive = false;

        while (true)
        {
            if (spriteRenderer == null || spriteRenderer.sprite == previousBodySprite)
            {
                elapsed += Time.deltaTime;
                yield return null;
                continue;
            }

            foreach (var config in hammerConfigs)
            {
                if (spriteRenderer.sprite == config.bodySprite)
                {
                    if (!isHammerActive)
                    {
                        hammerObject.SetActive(true);
                        isHammerActive = true;
                    }

                    // ハンマーの見た目と位置を一時的に同期
                    if (hammerRenderer != null)
                    {
                        // 右向きか否かに応じてハンマーの向きを調整
                        if (rightFlag)
                            hammerRenderer.flipX = true;
                        else
                            hammerRenderer.flipX = false;
                    }
                    else
                    {
                        Debug.LogError("ハンマーのSpriteRendererが見つかりません。");
                    }

                    if (hammerTransform != null)
                    {
                        Vector3 localPos = config.hammerLocalPosition;
                        float adjustedRotationZ = config.hammerAngle;

                        // 右向きか否かに応じてハンマーの向きと角度を調整
                        if (rightFlag)
                        {
                            localPos.x *= -1f;
                            adjustedRotationZ *= -1f;
                        }

                        hammerTransform.localPosition = localPos;
                        hammerTransform.localRotation = Quaternion.Euler(0, 0, adjustedRotationZ);
                    }

                    previousBodySprite = config.bodySprite;

                    break;
                }
            }

            previousBodySprite = spriteRenderer.sprite;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator ManageCrawlingRockLifetime(GameObject crawlingRock, float rockHalfWidth)
    {
        if (crawlingRock == null)
            yield break;

        Rigidbody2D crawlingRockRigidbody = crawlingRock.GetComponent<Rigidbody2D>(); //Rigidbody2Dコンポーネントを取得
        float existenceDuration = Mathf.Abs(rightBound - leftBound) / crawlingRockSpeed; // 這う岩の存在時間を計算

        // 岩の生存時間を計測するタイマーを追加
        float lifeTimer = 0f;
        float elapsedTime = crawlingRockDustEffectInterval; //最初にすぐエフェクトを出すために初期化
        while (true)
        {
            if (crawlingRock == null)
                yield break;

            if (!TimeManager.instance.isEnemyMovePaused) //敵の動きがポーズされていない場合
            {
                if (crawlingRockRigidbody != null && !crawlingRockRigidbody.simulated)
                    crawlingRockRigidbody.simulated = true; //物理挙動を再起動する

                // ポーズ中でなければ、生存時間タイマーを進める
                lifeTimer += Time.deltaTime;

                // 生存時間に達したらオブジェクトを破棄して終了
                if (lifeTimer >= existenceDuration)
                {
                    ObjectPooler.instance.ReturnToPool(crawlingRockPoolTag, crawlingRock);
                    yield break;
                }

                Vector3 pos = crawlingRock.transform.position;
                bool isOutOfBounds = false;

                // 右に移動中（速度が正）の場合
                if (crawlingRockRigidbody.velocity.x > 0)
                {
                    // 岩の右端が、右の境界線を越えたら消去
                    if (pos.x + rockHalfWidth + crawlingRockDestroyBuffer > rightBound)
                    {
                        isOutOfBounds = true;
                    }
                }
                // 左に移動中（速度が負）の場合
                else
                {
                    // 岩の左端が、左の境界線を越えたら消去
                    if (pos.x - rockHalfWidth - crawlingRockDestroyBuffer < leftBound)
                    {
                        isOutOfBounds = true;
                    }
                }

                if (isOutOfBounds)
                {
                    ObjectPooler.instance.ReturnToPool(crawlingRockPoolTag, crawlingRock);
                    yield break;
                }

                elapsedTime += Time.deltaTime; //経過時間を更新
                if (elapsedTime >= crawlingRockDustEffectInterval)
                {
                    elapsedTime = 0f; // 経過時間をリセット
                    GameObject crawlingDustEffect = ObjectPooler.instance.SpawnFromPool(
                        crawlingDustEffectPoolTag,
                        crawlingRock.transform.position
                            + new Vector3(
                                rightFlag ? crawlingDustEffectOffsetX : -crawlingDustEffectOffsetX,
                                0f,
                                0f
                            ),
                        Quaternion.identity
                    ); // 這う岩のダストエフェクトを生成

                    Vector3 scale = crawlingDustEffect.transform.localScale; // スケールを取得
                    // 右向きか否かに応じてスケールを調整
                    scale.x =
                        Mathf.Abs(scale.x) * (crawlingRockRigidbody.velocity.x >= 0 ? -1f : 1f);
                    crawlingDustEffect.transform.localScale = scale; // スケールを適用
                    ParticleSystem ps = crawlingDustEffect.GetComponent<ParticleSystem>();
                    if (ps == null)
                    {
                        Debug.LogError(
                            "CrawlingDustEffectPrefab に ParticleSystem が見つかりません。"
                        );
                        ObjectPooler.instance.ReturnToPool(
                            crawlingDustEffectPoolTag,
                            crawlingDustEffect
                        );
                        yield break;
                    }
                    var main = ps.main;
                    float destroyTime = main.duration + 0.01f; // パーティクルの持続時間を取得
                    main.loop = false; // ループしないように設定
                    crawlingDustEffect.SetActive(true); // パーティクルを有効化
                    ObjectPooler.instance.ReturnToPoolAfterDelay(
                        crawlingDustEffectPoolTag,
                        crawlingDustEffect,
                        destroyTime
                    ); // プールに一定時間後に返却
                }

                if (!SEManager.instance.IsPlayingFieldSE(SE_Field.GroundRumble1))
                {
                    SEManager.instance.PlayFieldSE(SE_Field.GroundRumble1); // 這う岩の移動音を鳴らす
                }
            }
            else
            {
                if (crawlingRockRigidbody != null)
                    crawlingRockRigidbody.simulated = false; //物理挙動を止める
            }

            yield return null; //1フレーム待って次のフレームで再評価する（フリーズ防止）
        }
    }

    public void SpawnDustEffect()
    {
        Vector2 effectPosition = new Vector2(
            transform.position.x + (rightFlag ? dustEffectOffsetX : -dustEffectOffsetX),
            transform.position.y
        );
        // エフェクトを生成
        GameObject effectObj = ObjectPooler.instance.SpawnFromPool(
            dustEffectPoolTag,
            effectPosition,
            Quaternion.identity
        );

        // ParticleSystem コンポーネント取得
        ParticleSystem ps = effectObj.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            main.duration = dustEffectDuration + 0.5f; // パーティクルの持続時間を設定
            main.loop = false; // ループしない
            main.startLifetime = dustEffectDuration + 0.5f; // パーティクルのライフタイムを設定

            ps.Play();
        }

        // duration + 少し余裕を持って削除
        ObjectPooler.instance.ReturnToPoolAfterDelay(
            dustEffectPoolTag,
            effectObj,
            dustEffectDuration + 0.5f
        );
    }

    public void SpawnSlashEffect()
    {
        GameObject effectObj = ObjectPooler.instance.SpawnFromPool(
            slashEffectPoolTag,
            transform.position,
            transform.rotation
        ); // エフェクトを生成

        if (effectObj == null)
            return;

        effectObj.transform.SetParent(this.transform); //自分の子オブジェクトにする
        effectObj.transform.localPosition = new Vector3(
            rightFlag ? -slashEffectOffset.x : slashEffectOffset.x,
            slashEffectOffset.y,
            0f
        ); // エフェクトの位置を設定
        effectObj.transform.localRotation = Quaternion.Euler(90f, 10f, 0f); // エフェクトの回転を設定
        effectObj.transform.localScale = new Vector3(rightFlag ? -1f : 1f, 1f, 1f); // 右向きか否かに応じてX軸を反転

        // ParticleSystem コンポーネント取得
        ParticleSystem ps = effectObj.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Play();
            // duration + 少し余裕を持って削除
            ObjectPooler.instance.ReturnToPoolAfterDelay(
                slashEffectPoolTag,
                effectObj,
                slashEffectDefaultDuration + 0.5f
            );
        }
        else
        {
            Debug.LogError("SlashEffectPrefab に ParticleSystem が見つかりません。");
            ObjectPooler.instance.ReturnToPoolAfterDelay(slashEffectPoolTag, effectObj, 0); // エフェクトがない場合は削除
            return;
        }
    }

    private void OnDestroy()
    {
        DestroyLinkedObjects();
    }

    private void DestroyLinkedObjects()
    {
        foreach (GameObject obj in linkedObjectsToDestroy)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
        linkedObjectsToDestroy.Clear();

        ObjectPooler.instance.ReturnAllToPool(); // すべてのオブジェクトをプールに返却
    }

    private void OnDrawGizmos()
    {
        // 境界が未設定なら描画しない
        if (leftBound == 0 || rightBound == 0)
        {
            return;
        }

        // ----- 行動範囲の中心座標 -----
        Vector3 center = new Vector3(
            (leftBound + rightBound) / 2f,
            transform.position.y + 3.5f, //上にずらして描画
            transform.position.z
        );

        // ----- 四角形のサイズ -----
        Vector3 size = new Vector3(
            rightBound - leftBound,
            7.5f, // 高さ（上下の視認性用）
            0.1f // 厚み（奥行きは視認用に薄く）
        );

        // ----- 塗りつぶし：オレンジの半透明 -----
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f); // RGBA（オレンジ・半透明）
        Gizmos.DrawCube(center, size);

        // ----- 枠線：赤 -----
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(center, size);
    }
}

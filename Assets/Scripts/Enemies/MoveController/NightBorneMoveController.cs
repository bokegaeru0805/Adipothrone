using System.Collections;
using System.Collections.Generic;
using AIE2D;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 背景オブジェクトとその目標色を格納するクラス
/// </summary>
[System.Serializable]
public class BackgroundTint
{
    public GameObject backgroundObject;
    public Color targetColor = Color.white;
}

[RequireComponent(typeof(Rigidbody2D))]
public class NightBorneMoveController : MonoBehaviour, IEnemyResettable
{
    [Header("剣の子オブジェクトの設定")]
    [SerializeField]
    private BodySwordConfig[] configs;

    [System.Serializable]
    public class BodySwordConfig
    {
        public Sprite bodySprite; // 表示するボディのスプライト
        public Sprite swordSprite; // 対応する剣のスプライト
        public Vector2 swordLocalPosition; // 対応するローカル座標

        [Header("当たり判定の設定")]
        public Vector2 swordColliderOffset; // 剣のコライダーのオフセット
        public Vector2 swordColliderSize = Vector2.one; // 剣のコライダーのサイズ
    }

    /// <summary>
    /// ファンネルの攻撃パターンごとの設定を管理するクラス
    /// </summary>
    [System.Serializable]
    public class FunnelAttackSetting
    {
        public FunnelAttackPattern attackPattern;

        [Tooltip("このファンネル攻撃が連携で発動可能になるまでの待機時間（秒）")]
        public float readyTime = 2.0f;
    }

    [Header("基本設定")]
    [SerializeField]
    private Transform playerTransform; // プレイヤーのTransform

    [SerializeField]
    private Sprite defaultSprite;

    [SerializeField, Tooltip("敵のタイプ")]
    private EnemyVariant variantType = EnemyVariant.None;

    [Header("Chapter1 背景色変更の設定")]
    [SerializeField, Tooltip("このリストは 'variantType' が 'Chapter1' の時のみ使用されます")]
    private List<BackgroundTint> chapter1Backgrounds = new List<BackgroundTint>();

    [SerializeField, Tooltip("Chapter1のボリューム")]
    private Volume chapter1Volume;

    [SerializeField, Tooltip("背景色が変化するのにかかる時間（秒）")]
    private float backgroundColorChangeDuration = 2.0f;

    [Header("ダメージ数値")]
    [SerializeField, Tooltip("近距離攻撃のダメージ量")]
    private int meleeAttackDamage = 0; // 近距離攻撃のダメージ量

    [SerializeField, Tooltip("遠距離攻撃のダメージ量")]
    private int funnelAttackDamage = 0; // 遠距離攻撃のダメージ量

    [SerializeField, Tooltip("HPが半分以上の時のターゲット攻撃のダメージ量")]
    private int funnelTargetAttackDamageAboveHalf = 0; // HPが半分以上の時のターゲット攻撃のダメージ量

    [SerializeField, Tooltip("HPが半分以下の時のターゲット攻撃のダメージ量")]
    private int funnelTargetAttackDamageBelowHalf = 0; // HPが半分以下の時のターゲット攻撃のダメージ量

    [Header("間合い・移動パラメータ")]
    [SerializeField, Tooltip("プレイヤーがこの距離に入るまで待機します")]
    private float activationRange = 10f;

    [SerializeField, Tooltip("近距離攻撃を行うための間合い")]
    private float meleeAttackRange = 5f; // 近距離攻撃を行うための間合い

    [Header("位置パラメータ")]
    [SerializeField, Tooltip("左の端のx座標(ワールド座標)")]
    private float leftBound = 0f; // 左の端のx座標（ワールド座標）

    [SerializeField, Tooltip("右の端のx座標(ワールド座標)")]
    private float rightBound = 0f; // 右の端のx座標（ワールド座標）

    [SerializeField, Tooltip("ファンネルの待機高さ(ローカルY座標)")]
    private float funnelWaitHeight = 10f; // ファンネルの待機高さ（ローカルY座標）

    [Header("時間パラメータ")]
    [SerializeField]
    private float meleeAttackCooldown = 1.5f; // 近距離攻撃後の待機時間

    [Header("ゲームオブジェクト設定")]
    [SerializeField, Tooltip("剣のゲームオブジェクト")]
    private GameObject swordObject; // 剣のゲームオブジェクト

    [SerializeField, Tooltip("ファンネルのプレハブ")]
    private List<GameObject> funnelPrefabs = new List<GameObject>(); // ファンネルのプレハブリスト

    [SerializeField, Tooltip("ファンネルの剣データ")]
    private BladeWeaponData funnelBladeData;

    [Header("ファンネルの待機中の設定")]
    [SerializeField, Tooltip("円運動の半径")]
    private float radius = 3.5f;

    [SerializeField, Tooltip("一周にかかる秒数")]
    private float secondsPerRevolution = 3f;

    [SerializeField, Tooltip("ファンネルの運動の中心となる初期位置(ローカル座標)")]
    private Vector2 initialPosition = Vector2.zero;

    [SerializeField, Range(0f, 1f), Tooltip("色の明度の最小値")]
    private float minBrightness = 0.3f;

    [SerializeField, Range(0f, 1f), Tooltip("色の明度の最大値")]
    private float maxBrightness = 1.0f;

    [Header("ファンネルの待機状態への移動の設定")]
    [SerializeField, Tooltip("ファンネルの移動の中心となる位置(ローカル座標)")]
    private Vector2 funnelMovementCenter = new Vector2(-1.5f, 12f);

    [SerializeField, Tooltip("待機位置への移動にかかる時間(秒)")]
    private float positioningDuration = 1.0f;

    [Header("ファンネルの予備動作の設定")]
    [SerializeField, Tooltip("予備動作にかける時間（秒）")]
    private float preparationTime = 0.5f;

    [SerializeField, Tooltip("予備動作で後退する距離")]
    private float recoilDistance = 3f;

    [Header("ファンネルの攻撃設定")]
    [SerializeField, Tooltip("プレイヤーを狙う弾の速度")]
    private float targetAttackSpeed = 40f;

    [SerializeField, Tooltip("プレイヤーを狙う際の連射間隔")]
    private float targetAttackInterval = 0.5f;

    [SerializeField, Tooltip("ランダムに狙う弾の連射間隔")]
    private float randomAttackInterval = 0.3f;

    [SerializeField, Tooltip("ファンネルが定位置へ戻る速度")]
    private float funnelReturnSpeed = 30f;

    [Range(0f, 1f), Tooltip("StraightDown攻撃時、ファンネルがプレイヤーの近くに配置される確率")]
    [SerializeField]
    private float chanceToSpawnNearPlayer = 0.3f;

    [Tooltip(
        "↑の確率抽選に成功した際、プレイヤーの左右どれくらいの範囲にファンネルを配置するかの距離"
    )]
    [SerializeField]
    private float spawnNearPlayerRange = 3.0f;

    [SerializeField, Tooltip("各ファンネル攻撃の連携準備時間の設定")]
    private List<FunnelAttackSetting> funnelAttackSettings = new List<FunnelAttackSetting>();

    [SerializeField, Tooltip("このスプライトに切り替わった時にファンネル攻撃を開始します")]
    private Sprite funnelTriggerSprite;

    private float theta = 0f; // 現在の角度（度数法）
    private float funnelWidth = 1.0f; // ファンネルの幅
    private const float attackDefaultAnimationTime = 1.0f; // 攻撃のデフォルトアニメーション時間
    private int sortingLayerID = 0; //自分のソーティングレイヤーID
    private int sortingOrder = 0; //自分のソーティングオーダー
    private bool isMoveStarted = false; // 移動が開始されたかどうか
    private bool isDefeated = false; // ボスが倒されたかどうか
    private bool rightFlag = false; // 右向きかどうか
    private bool isMeleeAttacking = false; // 現在、近距離攻撃中か
    private bool canMeleeAttack = true; // 近距離攻撃がクールダウン中でなく、実行可能か
    private bool hasTriggeredFunnelAttack = false; // 連携攻撃がトリガーされたかを管理するフラグ
    private bool hasAttackStarted = false; // 攻撃シーケンスが開始されたかどうかのフラグ
    private bool isHPbelowHalf = false; // HPが半分以下になったかどうかのフラグ
    private const string spawnAnimationClipName = "NightBorne_spawn";
    private Rigidbody2D rbody;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Sprite previousBodySprite = null;
    private Sprite funnelBladeSprite = null;
    private SpriteRenderer swordRenderer;
    private Transform swordTransform;
    private CapsuleCollider2D swordCollider;
    private Coroutine swordCoroutine;
    private StaticAfterImageEffect2DPlayer afterImage;
    private UniqueBossHealth bossHealth;
    private List<GameObject> linkedObjectsToDestroy = new List<GameObject>();

    // ファンネルのコンポーネントと状態をキャッシュするためのリスト
    private List<SpriteRenderer> funnelRenderers = new List<SpriteRenderer>();
    private List<Color> initialFunnelColors = new List<Color>();
    private List<bool> hasNotifiedAtZero = new List<bool>();
    private List<bool> hasNotifiedAt180 = new List<bool>();

    /// <summary>
    /// ファンネルの状態を定義
    /// </summary>
    private enum FunnelState
    {
        Circling, // 円運動中
        Positioning, // 待機位置へ移動中
        Standby, // 待機完了
        Attacking // 攻撃中
        ,
    }

    private FunnelState currentFunnelState = FunnelState.Circling; // ファンネルの現在の状態
    private float positioningTimer = 0f; // 移動開始からの経過時間タイマー
    private List<float> funnelStartAngles = new List<float>(); // 各ファンネルの移動開始時の角度
    private List<float> funnelStartRadii = new List<float>(); // 各ファンネルの移動開始時の半径
    private List<float> funnelTargetRadii = new List<float>(); // 各ファンネルの目標半径
    private List<Vector2> funnelTargetPositions = new List<Vector2>(); // StartPositioningMode内でローカル変数からメンバー変数に変更

    public enum FunnelAttackPattern
    {
        StraightDown, // 真下
        Cross, // 交差
        TargetPlayer, // プレイヤー狙い
        Random // ランダム
        ,
    }

    private FunnelAttackPattern currentAttackPattern = FunnelAttackPattern.StraightDown;

    /// <summary>
    /// 各ファンネルの個別状態を定義
    /// </summary>
    private enum IndividualFunnelState
    {
        InCircle, // 円運動中/待機中
        Waiting, //待機中
        Attacking, // 攻撃のため直進中
        Returning // 定位置へ帰還中
        ,
    }

    private List<IndividualFunnelState> funnelStates = new List<IndividualFunnelState>();
    private List<ContactDamageController> funnelDamageControllers =
        new List<ContactDamageController>();
    private List<FunnelProjectile> funnelProjectiles = new List<FunnelProjectile>();
    private float timeSinceFunnelsReturned = 0f; // ファンネルが全て戻ってきてからの経過時間
    private float _currentFunnelReadyTime = 2.0f; //現在の攻撃パターンに対応する準備時間を保持する変数

    // 敵の種類を定義
    private enum EnemyVariant
    {
        None = 0,
        Chapter1 = 1,
    }

    // 背景のスプライトレンダラーとその初期色をキャッシュするための辞書
    private Dictionary<SpriteRenderer, Color> originalBackgroundColors =
        new Dictionary<SpriteRenderer, Color>();

    private void RegisterLinkedObject(GameObject obj)
    {
        if (obj != null && !linkedObjectsToDestroy.Contains(obj))
        {
            linkedObjectsToDestroy.Add(obj);
        }
    }

    private void DestroyLinkedObjects()
    {
        foreach (GameObject obj in linkedObjectsToDestroy)
        {
            if (funnelPrefabs.Contains(obj))
            {
                obj.SetActive(false); // ファンネルプレハブは非アクティブ化
                continue;
            }

            if (obj != null)
            {
                Destroy(obj);
            }
        }

        linkedObjectsToDestroy.Clear();
    }

    private void Awake()
    {
        spriteRenderer = this.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            sortingLayerID = spriteRenderer.sortingLayerID;
            sortingOrder = spriteRenderer.sortingOrder;
            if (defaultSprite != null)
            {
                spriteRenderer.sprite = defaultSprite; // デフォルトのスプライトに設定
            }
        }

        rbody = this.GetComponent<Rigidbody2D>();
        if (rbody != null)
        {
            rbody.constraints = RigidbodyConstraints2D.FreezeRotation;
            rbody.velocity = Vector2.zero; // 初期速度をゼロに設定
        }

        animator = this.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError($" {gameObject.name} にAnimatorが見つかりません。");
        }
        else
        {
            animator.enabled = false; //初期状態ではAnimatorを無効にする
        }

        bossHealth = this.GetComponent<UniqueBossHealth>();
        if (bossHealth == null)
        {
            Debug.LogError($" {gameObject.name} にUniqueBossHealthが見つかりません。");
        }
        else
        {
            // bossHealthが倒された時(OnDefeatedイベント発行時)に、HandleBossDefeatメソッドを呼び出すように登録
            bossHealth.OnDefeated += HandleBossDefeat;
            //rigidbodyの制御を行わないように設定
            bossHealth.SetRigidbodyControl(false);
            //イベントを登録
        }

        if (swordObject != null)
        {
            swordRenderer = swordObject.GetComponent<SpriteRenderer>();
            if (swordRenderer == null)
            {
                Debug.LogError($" {swordObject.name} にSpriteRendererが見つかりません。");
            }

            swordTransform = swordObject.transform;
            var stateController = swordObject.GetComponent<ContactDamageController>();
            if (stateController == null)
            {
                Debug.LogError($" {swordObject.name} にContactDamageControllerが見つかりません。");
            }
            else
            {
                stateController.SetDamageAmount(meleeAttackDamage); // 剣のダメージ量を設定
            }

            swordCollider = swordObject.GetComponent<CapsuleCollider2D>();
            if (swordCollider == null)
            {
                Debug.LogError(
                    $" {swordObject.name} にCapsuleCollider2Dが見つかりません。事前にアタッチしてください。"
                );
            }

            swordObject.tag = GameConstants.DamageableEnemyTagName; // 剣のタグを設定
            swordObject.SetActive(false); // 初期状態では剣オブジェクトを非表示にする
        }
        else
        {
            Debug.LogError("剣オブジェクトが設定されていません。");
        }

        if (funnelBladeData == null)
        {
            Debug.LogError($"{this.name} のファンネルの剣データが設定されていません。");
        }
        else
        {
            funnelBladeSprite = funnelBladeData.itemSprite; // ファンネルの剣スプライトを取得
            funnelWidth = funnelBladeSprite.rect.height / funnelBladeSprite.pixelsPerUnit; // ファンネルの幅を取得
        }

        afterImage = gameObject.GetComponent<StaticAfterImageEffect2DPlayer>();
        if (afterImage == null)
        {
            Debug.LogWarning(
                $"{this.name} に StaticAfterImageEffect2DPlayer コンポーネントが見つかりませんでした。"
            );
        }
        else
        {
            afterImage.SetActive(false); //初期状態では残像を無効化
        }

        foreach (var funnel in funnelPrefabs)
        {
            if (funnel != null)
            {
                funnel.transform.SetParent(null); // 親を解除
                var ContactDamageController = funnel.GetComponent<ContactDamageController>();
                if (ContactDamageController == null)
                {
                    Debug.LogError(
                        $"{funnel.name} に ContactDamageController コンポーネントが見つかりません。"
                    );
                }
            }
        }

        // Chapter1の場合のみ、背景の初期色をキャッシュする
        if (variantType == EnemyVariant.Chapter1)
        {
            foreach (var bg in chapter1Backgrounds)
            {
                if (bg.backgroundObject != null)
                {
                    var renderer = bg.backgroundObject.GetComponent<SpriteRenderer>();
                    if (renderer != null)
                    {
                        // 辞書にレンダラーと現在の色を保存
                        originalBackgroundColors[renderer] = renderer.color;
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"{bg.backgroundObject.name} に SpriteRenderer が見つかりません。"
                        );
                    }
                }
            }
            if (chapter1Volume == null)
            {
                Debug.LogWarning($"{this.name} の Chapter1 のボリュームが設定されていません。");
            }
            else
            {
                chapter1Volume.weight = 0f; // 初期状態ではエフェクトを無効化
            }
        }
    }

    private void Start()
    {
        if (swordRenderer != null)
        {
            //Awakeで行ってはいけない
            swordRenderer.material.EnableKeyword("FADE_ON");
        }

        if (playerTransform == null)
        {
            // FindGameObjectWithTagは負荷が高い場合があるので、?演算子でnullチェックをしています
            playerTransform = GameObject
                .FindGameObjectWithTag(GameConstants.PlayerTagName)
                ?.transform;
            if (playerTransform == null)
            {
                Debug.LogError($"{this.name}はPlayerObjectを見つけられませんでした");
            }
        }

        ResetState();
    }

    public void ResetState()
    {
        if (playerTransform == null)
        {
            // FindGameObjectWithTagは負荷が高い場合があるので、?演算子でnullチェックをしています
            playerTransform = GameObject
                .FindGameObjectWithTag(GameConstants.PlayerTagName)
                ?.transform;
            if (playerTransform == null)
            {
                Debug.LogError($"{this.name}はPlayerObjectを見つけられませんでした");
            }
        }

        // 破棄するオブジェクトのリストを初期化
        DestroyLinkedObjects();

        SetupFunnels(); // ファンネルの初期設定を行う

        this.tag = "Untagged"; // タグを初期化
        isMoveStarted = false; // 移動開始フラグをリセット
        isDefeated = false; // ボスが倒されたフラグをリセット
        animator.enabled = false; // Animatorを無効にする
        spriteRenderer.sprite = defaultSprite; // デフォルトのスプライトに設定
        swordObject.tag = GameConstants.DamageableEnemyTagName; // 剣のタグを設定
        swordObject.SetActive(false); // 初期状態では剣オブジェクトを非表示にする

        //攻撃関係のフラグをリセット
        currentFunnelState = FunnelState.Circling;
        isMeleeAttacking = false;
        canMeleeAttack = true;
        hasTriggeredFunnelAttack = false;
        hasAttackStarted = false;
        isHPbelowHalf = false;

        afterImage?.SetActive(false); //残像を無効化

        if (defaultSprite != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = defaultSprite; // デフォルトのスプライトに設定
        }

        // Chapter1の場合、背景色を即座に元に戻す
        if (variantType == EnemyVariant.Chapter1)
        {
            RevertBackgroundColorsInstantly();
        }
    }

    private void FixedUpdate()
    {
        // まだ起動していない場合、プレイヤーを監視し、条件を満たしたら起動する
        if (!isMoveStarted)
        {
            CheckForPlayerActivation();
            return; // 起動するまで、これ以降の処理は行わない
        }

        // 倒されている場合は、一切の思考・行動を行わないようにするガード節を追加
        if (isDefeated)
        {
            return;
        }

        // --- 角度の更新 (状態に関わらず継続) ---
        if (secondsPerRevolution > 0f)
        {
            theta += (360f / secondsPerRevolution) * Time.deltaTime;
            theta %= 360f;
        }

        // --- 思考ルーチン ---
        if (currentFunnelState == FunnelState.Circling)
        {
            // ファンネルが戻ってきてからの時間を計測
            timeSinceFunnelsReturned += Time.deltaTime;
        }
        else
        {
            // 円運動中でなければ、ファンネル待機時間をリセット
            timeSinceFunnelsReturned = 0f;
        }

        // 近距離攻撃中でなく、かつクールダウンが終わっていれば、攻撃のチャンスをうかがう
        if (!isMeleeAttacking && canMeleeAttack)
        {
            // 条件A: プレイヤーが近距離攻撃の範囲内にいるか
            if (IsPlayerInMeleeRange())
            {
                StartCoroutine(PerformMeleeAttack());
            }
            // 条件B: プレイヤーは近くにいないが、ファンネルの連携攻撃準備が完了しているか
            else if (timeSinceFunnelsReturned >= _currentFunnelReadyTime)
            {
                StartCoroutine(PerformMeleeAttack());
            }
        }

        HandleCirclingMovement(); //ファンネルの円運動の処理

        // --- 状態に応じたファンネルの制御 ---
        switch (currentFunnelState)
        {
            case FunnelState.Circling:
                break;
            case FunnelState.Positioning:
                HandlePositioningMovement();
                break;
            case FunnelState.Standby:
                // 待機状態になったら、一度だけ攻撃シーケンスを開始する
                if (!hasAttackStarted)
                {
                    hasAttackStarted = true;
                    StartCoroutine(FunnelAttackSequence());
                }
                break;
            case FunnelState.Attacking:
                // 攻撃中のファンネル管理メソッドを呼び出す
                HandleAttackingState();
                break;
        }
    }

    /// <summary>
    /// プレイヤーが起動範囲内に入ったかチェックし、条件を満たせば起動する
    /// </summary>
    private void CheckForPlayerActivation()
    {
        if (playerTransform == null)
        {
            return;
        }

        // 1. プレイヤーとの距離を計算
        float distance = Vector2.Distance(transform.position, playerTransform.position);

        // 距離が範囲外なら、まだ起動しない
        if (distance > activationRange)
        {
            return;
        }

        // 2. プレイヤーが自分の「前方」にいるかチェック
        // プレイヤーのX座標と自分のX座標の差を計算
        float playerDirectionX = playerTransform.position.x - transform.position.x;

        // 自分が右向き(rightFlag)のときはプレイヤーが右(差が正)に、
        // 自分が左向きのときはプレイヤーが左(差が負)にいるかを判定
        bool isPlayerInFront =
            (rightFlag && playerDirectionX > 0) || (!rightFlag && playerDirectionX < 0);

        // 距離と方向の条件を両方満たしたら、起動処理を実行
        if (isPlayerInFront)
        {
            isMoveStarted = true; // 起動フラグを立てる
            this.tag = GameConstants.ImmuneEnemyTagName; // タグをダメージを受けない敵のタグに設定
            animator.enabled = true; // Animatorを有効にする
            bossHealth?.ActivateBattle(); // ボス戦を開始する
            // ファンネルをアニメーションに同期してフェードインさせるコルーチンを開始
            StartCoroutine(FadeInFunnels());

            // Chapter1の場合、背景色の変更を開始
            if (variantType == EnemyVariant.Chapter1)
            {
                StartCoroutine(ChangeBackgroundsCoroutine(true)); // true: 目標の色へ
            }
        }
    }

    /// <summary>
    /// ファンネルの情報を取得し、リストにキャッシュする
    /// </summary>
    private void SetupFunnels()
    {
        // 各リストをクリア
        funnelRenderers.Clear();
        initialFunnelColors.Clear();
        hasNotifiedAtZero.Clear();
        hasNotifiedAt180.Clear();
        funnelStates.Clear();
        // インスペクターで設定された各ファンネルの情報を取得
        foreach (var funnel in funnelPrefabs)
        {
            if (funnel != null)
            {
                RegisterLinkedObject(funnel); // ファンネルをリンクリストに登録

                SpriteRenderer sr = funnel.GetComponent<SpriteRenderer>();
                funnelRenderers.Add(sr);

                if (sr != null)
                {
                    initialFunnelColors.Add(sr.color); // SpriteRendererが存在すれば、初期色を保存
                    sr.sortingLayerID = sortingLayerID; //初期化時にソーティングレイヤーを合わせる
                    sr.sortingOrder = sortingOrder; //初期化時にソーティングオーダーを合わせる

                    // フェードイン演出のため、初期状態の透明度を0にする
                    Color transparentColor = sr.color;
                    transparentColor.a = 0f;
                    sr.color = transparentColor;
                }
                else
                {
                    initialFunnelColors.Add(Color.white); // なければ白を仮登録
                    Debug.LogWarning(
                        $"{funnel.name} に SpriteRenderer が見つかりません。色の変更は機能しません。"
                    );
                }

                // デバッグ通知用のフラグを初期化
                hasNotifiedAtZero.Add(false);
                hasNotifiedAt180.Add(false);

                funnelStates.Add(IndividualFunnelState.InCircle); // 各ファンネルの初期状態を設定

                // コンポーネントを一度だけ取得し、リストにキャッシュする
                var damageController = funnel.GetComponent<ContactDamageController>();
                var projectile = funnel.GetComponent<FunnelProjectile>();
                if (damageController == null)
                {
                    Debug.LogError($"{funnel.name} に ContactDamageController が見つかりません。");
                }

                if (projectile == null)
                {
                    Debug.LogError(
                        $"{funnel.name} に FunnelProjectile コンポーネントが見つかりません。予備動作は機能しません。"
                    );
                }
                else
                {
                    projectile.Setup(preparationTime, recoilDistance); // 予備動作の設定
                }

                funnelDamageControllers.Add(damageController);
                funnelProjectiles.Add(projectile);

                funnel.SetActive(false); // 初期状態では非表示
            }
        }
    }

    /// <summary>
    /// ファンネルを待機位置へ移動させるモードを開始する
    /// </summary>
    private void StartPositioningMode()
    {
        // 移動タイマーと攻撃フラグをリセット
        positioningTimer = 0f;
        hasAttackStarted = false;

        // 計算結果を保存するリストを初期化
        funnelTargetPositions.Clear();
        funnelStartAngles.Clear();
        funnelStartRadii.Clear();
        funnelTargetRadii.Clear();

        int count = funnelPrefabs.Count;
        if (count == 0)
            return;

        // --- 色の初期化処理と状態変化処理 ---
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer sr = funnelRenderers[i];
            if (sr != null)
            {
                // キャッシュしておいた初期色をHSVに変換
                Color.RGBToHSV(initialFunnelColors[i], out float h, out float s, out _);
                // 明度(V)だけを更新して新しい色を設定
                sr.color = Color.HSVToRGB(h, s, maxBrightness);
            }

            funnelStates[i] = IndividualFunnelState.Waiting; // 各ファンネルの状態を待機中に変更
        }

        // --- 事前計算 ---
        float targetY = transform.position.y + funnelWaitHeight;

        // 1. 攻撃パターンに応じて、最終的な目標「座標」の計算方法を分岐
        if (currentAttackPattern == FunnelAttackPattern.StraightDown)
        {
            // --- 【StraightDown攻撃の場合】ランダムなX座標を計算 ---
            float minX = leftBound + funnelWidth;
            float maxX = rightBound - funnelWidth;

            // 確率の判定（プレイヤーの近くに配置するかどうか）
            if (playerTransform != null && Random.value < chanceToSpawnNearPlayer)
            {
                // プレイヤーの近くに配置する場合
                float playerX = playerTransform.position.x;
                for (int i = 0; i < count; i++)
                {
                    // プレイヤーのX座標を中心に、一定範囲内にランダムなX座標を生成
                    // 範囲を-3fから3fとしていますが、この値は適宜調整してください
                    float randomOffset = Random.Range(-spawnNearPlayerRange, spawnNearPlayerRange);
                    float targetX = Mathf.Clamp(playerX + randomOffset, minX, maxX);
                    funnelTargetPositions.Add(new Vector2(targetX, targetY));
                }
            }
            else
            {
                // 通常通り、範囲内で完全にランダムに配置する場合
                for (int i = 0; i < count; i++)
                {
                    float targetX = Random.Range(minX, maxX);
                    funnelTargetPositions.Add(new Vector2(targetX, targetY));
                }
            }
        }
        else
        {
            // --- 【それ以外の攻撃の場合】等間隔に配置 ---
            float availableWidth = (rightBound - leftBound) - (funnelWidth * 2f);
            float spacing = (count > 1) ? availableWidth / (count - 1) : 0f;
            for (int i = 0; i < count; i++)
            {
                float targetX = leftBound + funnelWidth + (i * spacing);
                funnelTargetPositions.Add(new Vector2(targetX, targetY));
            }
        }

        // 2. 各ファンネルの初期状態と目標状態を計算して保存
        Vector2 center = (Vector2)transform.position + funnelMovementCenter;
        for (int i = 0; i < count; i++)
        {
            if (funnelPrefabs[i] == null)
                continue;

            // 初期状態の計算
            Vector2 startVec = (Vector2)funnelPrefabs[i].transform.position - center;
            funnelStartAngles.Add(Mathf.Atan2(startVec.y, startVec.x) * Mathf.Rad2Deg);
            funnelStartRadii.Add(startVec.magnitude);

            // 目標状態の計算
            Vector2 targetVec = funnelTargetPositions[i] - center;
            funnelTargetRadii.Add(targetVec.magnitude);
        }

        // 3. SEを再生
        switch (currentAttackPattern)
        {
            case FunnelAttackPattern.StraightDown:
                SEManager.instance?.PlayEnemyActionSEPitch(SE_EnemyAction.MagicWave1, 1.5f);
                break;
            case FunnelAttackPattern.Cross:
                SEManager.instance?.PlayEnemyActionSEPitch(SE_EnemyAction.MagicWave1, 1.3f);
                break;
            case FunnelAttackPattern.TargetPlayer:
            case FunnelAttackPattern.Random:
                SEManager.instance?.PlayEnemyActionSEPitch(SE_EnemyAction.MagicWave1, 1.0f);
                break;
        }

        SEManager.instance?.PlayEnemyActionSE(SE_EnemyAction.SwordSlash2);

        // 状態を移行
        currentFunnelState = FunnelState.Positioning;
    }

    /// <summary>
    /// 円運動中のファンネルの処理
    /// </summary>
    private void HandleCirclingMovement()
    {
        if (funnelPrefabs.Count <= 0)
            return;

        for (int i = 0; i < funnelPrefabs.Count; i++)
        {
            GameObject funnel = funnelPrefabs[i];
            if (funnel == null)
                continue;

            if (funnelStates[i] != IndividualFunnelState.InCircle)
                continue;

            // 座標の計算と適用
            funnel.transform.position = CalculateCirclePositionForFunnel(i);

            // 回転と色の更新
            float angleStep = 360f / funnelPrefabs.Count;
            float funnelAngle = theta + (i * angleStep);
            funnel.transform.eulerAngles = new Vector3(0, funnelAngle + 90, 90);

            SpriteRenderer sr = funnelRenderers[i];
            if (sr != null)
            {
                float radian = funnelAngle * Mathf.Deg2Rad;
                float sinValue = Mathf.Sin(radian);
                float brightness = Mathf.Lerp(maxBrightness, minBrightness, (sinValue + 1f) / 2f);
                Color.RGBToHSV(initialFunnelColors[i], out float h, out float s, out _);
                sr.color = Color.HSVToRGB(h, s, brightness);

                //関数を呼び出して、描画順を更新する
                sr.sortingOrder = CalculateCircleSortingOrderForFunnel(i);
            }
        }
    }

    /// <summary>
    /// 待機位置へ移動中のファンネルの処理 (時間ベースの補間)
    /// </summary>
    private void HandlePositioningMovement()
    {
        if (funnelPrefabs.Count <= 0)
            return;

        // 経過時間を更新し、進行度(0～1)を計算
        positioningTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(positioningTimer / positioningDuration);

        Vector2 center = (Vector2)transform.position + funnelMovementCenter;

        for (int i = 0; i < funnelPrefabs.Count; i++)
        {
            GameObject funnel = funnelPrefabs[i];
            if (funnel == null)
                continue;

            // --- 現在の角度と半径を補間で求める ---
            float startAngle = funnelStartAngles[i];
            float targetAngle = 180f;

            // 常に反時計回りで補間するための調整
            // 目標角度が開始角度より小さい場合、360度を足して大きい値にすることで、
            // LerpAngleが遠回り(反時計回り)を選択するようにする
            if (targetAngle < startAngle)
            {
                targetAngle += 360f;
            }

            // LerpAngleで角度を補間
            float currentAngle = Mathf.Lerp(startAngle, targetAngle, progress);
            // Lerpで半径を補間
            float currentRadius = Mathf.Lerp(funnelStartRadii[i], funnelTargetRadii[i], progress);

            // --- 新しい座標を計算 ---
            float currentRadian = currentAngle * Mathf.Deg2Rad;
            Vector2 offset =
                new Vector2(Mathf.Cos(currentRadian), Mathf.Sin(currentRadian)) * currentRadius;
            Vector2 nextPos = center + offset;

            // 移動方向を計算してZ軸回転を更新
            Vector2 moveDirection = (nextPos - (Vector2)funnel.transform.position);
            if (moveDirection.sqrMagnitude > 0.0001f) // わずかでも動いていれば
            {
                float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
                funnel.transform.eulerAngles = new Vector3(0, 0, angle);
            }

            // 座標を更新
            funnel.transform.position = nextPos;
        }

        // 移動が完了したらStandby状態に移行
        if (progress >= 1.0f)
        {
            currentFunnelState = FunnelState.Standby;
            // 最終的な回転をリセット
            foreach (var funnel in funnelPrefabs)
            {
                if (funnel != null)
                {
                    funnel.transform.eulerAngles = new Vector3(0, 0, -90);
                }
            }
        }
    }

    // <summary>
    /// ファンネルの攻撃パターンに応じて攻撃コルーチンを呼び出す
    /// </summary>
    private IEnumerator FunnelAttackSequence()
    {
        currentFunnelState = FunnelState.Attacking;

        // 現在の攻撃パターンに応じて処理を分岐
        switch (currentAttackPattern)
        {
            case FunnelAttackPattern.StraightDown:
                yield return StartCoroutine(Attack_StraightDown());
                break;
            case FunnelAttackPattern.Cross:
                yield return StartCoroutine(Attack_Cross());
                break;
            case FunnelAttackPattern.TargetPlayer:
                yield return StartCoroutine(Attack_TargetPlayer());
                break;
            case FunnelAttackPattern.Random:
                yield return StartCoroutine(Attack_Random());
                break;
        }
    }

    /// <summary>
    /// [攻撃パターン] 真下にファンネルを一斉に発射する
    /// </summary>
    private IEnumerator Attack_StraightDown()
    {
        for (int i = 0; i < funnelPrefabs.Count; i++)
        {
            GameObject funnel = funnelPrefabs[i];
            if (funnel != null)
            {
                // ファンネルにアタッチされたFunnelProjectileを取得
                var projectile = funnelProjectiles[i];
                if (projectile == null)
                {
                    Debug.LogError(
                        $"{funnel.name} に FunnelProjectile コンポーネントが見つかりません。追加します。"
                    );
                    projectile = funnel.AddComponent<FunnelProjectile>();
                }

                var damageController = funnelDamageControllers[i];
                if (damageController != null)
                {
                    damageController.SetDamageAmount(funnelAttackDamage); // 攻撃ダメージを設定
                }

                // 個別状態を「攻撃中」に設定
                funnelStates[i] = IndividualFunnelState.Attacking;

                // 真下の方向ベクトルを計算
                Vector2 direction = Vector2.down;

                // FunnelProjectileに発射命令
                projectile.Launch(this, direction, targetAttackSpeed);
            }
        }
        yield return null;
    }

    /// <summary>
    /// [攻撃パターン] プレイヤーを狙ってファンネルを順番に発射する
    /// </summary>
    private IEnumerator Attack_TargetPlayer()
    {
        for (int i = 0; i < funnelPrefabs.Count; i++)
        {
            GameObject funnel = funnelPrefabs[i];
            if (funnel != null && playerTransform != null)
            {
                // ファンネルにアタッチされたFunnelProjectileを取得
                var projectile = funnelProjectiles[i];
                if (projectile == null)
                {
                    Debug.LogError(
                        $"{funnel.name} に FunnelProjectile コンポーネントが見つかりません。追加します。"
                    );
                    projectile = funnel.AddComponent<FunnelProjectile>();
                }

                var damageController = funnelDamageControllers[i];
                if (damageController != null)
                {
                    damageController.SetDamageAmount(
                        isHPbelowHalf
                            ? funnelTargetAttackDamageBelowHalf
                            : funnelTargetAttackDamageAboveHalf
                    ); // 攻撃ダメージを設定
                }

                // 個別状態を「攻撃中」に設定
                funnelStates[i] = IndividualFunnelState.Attacking;
                // 発射時点でのプレイヤーの方向を計算
                Vector2 direction = (
                    playerTransform.position - funnel.transform.position
                ).normalized;

                // FunnelProjectileに発射命令
                projectile.Launch(this, direction, targetAttackSpeed);

                // 次の発射まで待つ
                yield return new WaitForSeconds(targetAttackInterval);
            }
        }
    }

    /// <summary>
    /// [攻撃パターン] 中心に向かってファンネルを一斉に発射する
    /// </summary>
    private IEnumerator Attack_Cross()
    {
        //全てのファンネルが狙う、単一の目標座標をランダムに決定する
        float randomX = Random.Range(leftBound + funnelWidth, rightBound - funnelWidth);
        float targetY = this.transform.position.y;
        Vector2 randomTargetPoint = new Vector2(randomX, targetY);

        for (int i = 0; i < funnelPrefabs.Count; i++)
        {
            GameObject funnel = funnelPrefabs[i];
            if (funnel != null)
            {
                // ファンネルにアタッチされたFunnelProjectileを取得
                var projectile = funnelProjectiles[i];
                if (projectile == null)
                {
                    Debug.LogError(
                        $"{funnel.name} に FunnelProjectile コンポーネントが見つかりません。追加します。"
                    );
                    projectile = funnel.AddComponent<FunnelProjectile>();
                }

                var damageController = funnelDamageControllers[i];
                if (damageController != null)
                {
                    damageController.SetDamageAmount(funnelAttackDamage); // 攻撃ダメージを設定
                }

                // 個別状態を「攻撃中」に設定
                funnelStates[i] = IndividualFunnelState.Attacking;

                // 中心に向かう方向ベクトルを計算
                Vector2 direction = (
                    randomTargetPoint - (Vector2)funnel.transform.position
                ).normalized;

                // FunnelProjectileに発射命令
                projectile.Launch(this, direction, targetAttackSpeed);
            }
        }
        yield return null;
    }

    /// <summary>
    /// [攻撃パターン] ランダムな地点へ向かってファンネルを順番に発射する
    /// </summary>
    private IEnumerator Attack_Random()
    {
        // 1. 発射する前に、各ファンネルの目標地点を全て計算しておく
        var randomTargets = new List<Vector2>();
        float targetY = this.transform.position.y; // Y座標は自身の高さに固定

        for (int i = 0; i < funnelPrefabs.Count; i++)
        {
            // X座標を leftBound と rightBound の間でランダムに決定
            float targetX = Random.Range(leftBound, rightBound);
            randomTargets.Add(new Vector2(targetX, targetY));
        }

        // 2. 計算した目標地点に向かって、ファンネルを順番に発射
        for (int i = 0; i < funnelPrefabs.Count; i++)
        {
            GameObject funnel = funnelPrefabs[i];
            if (funnel != null)
            {
                var projectile = funnelProjectiles[i];
                if (projectile == null)
                {
                    Debug.LogError(
                        $"{funnel.name} に FunnelProjectile コンポーネントが見つかりません。追加します。"
                    );
                    projectile = funnel.AddComponent<FunnelProjectile>();
                }

                var damageController = funnelDamageControllers[i];
                if (damageController != null)
                {
                    damageController.SetDamageAmount(funnelAttackDamage); // 攻撃ダメージを設定
                }

                funnelStates[i] = IndividualFunnelState.Attacking;

                // 事前に計算したランダムな目標地点を取得
                Vector2 targetPosition = randomTargets[i];
                // 目標地点への方向を計算
                Vector2 direction = (
                    targetPosition - (Vector2)funnel.transform.position
                ).normalized;

                // FunnelProjectileに発射命令
                projectile.Launch(this, direction, targetAttackSpeed);

                // 次の発射まで待つ
                yield return new WaitForSeconds(randomAttackInterval);
            }
        }
    }

    /// <summary>
    /// 攻撃終了後、最初の円運動状態に戻す
    /// </summary>
    private void ResetToCirclingState()
    {
        // 攻撃が完了したのでフラグをリセット
        hasAttackStarted = false;
        // 状態を円運動に戻す（ファンネルの位置はHandleCirclingMovementで自動的に再配置される）
        currentFunnelState = FunnelState.Circling;
    }

    /// <summary>
    /// FunnelProjectileから呼び出され、ファンネルが画面外に出たことを記録する
    /// </summary>
    public void OnFunnelOffScreen(GameObject funnelObject)
    {
        int index = funnelPrefabs.IndexOf(funnelObject);
        if (index != -1)
        {
            funnelStates[index] = IndividualFunnelState.Returning;
        }
    }

    /// <summary>
    /// 攻撃中のファンネルの状態を監視・更新し、必要なら帰還させる
    /// </summary>
    private void HandleAttackingState()
    {
        bool allFunnelsAreBack = true;

        for (int i = 0; i < funnelPrefabs.Count; i++)
        {
            // まだ攻撃中か帰還中のファンネルがあれば、まだ攻撃は終わっていない
            if (
                funnelStates[i] == IndividualFunnelState.Attacking
                || funnelStates[i] == IndividualFunnelState.Returning
            )
            {
                allFunnelsAreBack = false;
            }

            // 帰還中のファンネルを動かす
            if (funnelStates[i] == IndividualFunnelState.Returning)
            {
                GameObject funnel = funnelPrefabs[i];
                if (funnel == null)
                    continue;

                // 戻るべき円周上の座標を計算
                Vector2 targetPos = CalculateCirclePositionForFunnel(i);
                Vector2 currentPos = funnel.transform.position;

                // 次のフレームの位置を計算
                Vector2 nextPos = Vector2.MoveTowards(
                    currentPos,
                    targetPos,
                    funnelReturnSpeed * Time.deltaTime
                );

                // 移動方向を計算してZ軸回転を更新
                Vector2 moveDirection = (nextPos - currentPos);
                if (moveDirection.sqrMagnitude > 0.0001f) // わずかでも動いていれば
                {
                    float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
                    funnel.transform.eulerAngles = new Vector3(0, 0, angle);
                }

                // 計算した次の位置へ移動
                funnel.transform.position = nextPos;

                // 十分近づいたらファンネルの状態をInCircleに戻す
                if (Vector2.Distance(nextPos, targetPos) < 0.1f)
                {
                    funnelStates[i] = IndividualFunnelState.InCircle;
                }
            }
        }

        // 全てのファンネルが円運動状態に戻ったら、全体のステートをCirclingに戻す
        if (allFunnelsAreBack)
        {
            ResetToCirclingState();
        }
    }

    /// <summary>
    /// 指定されたインデックスのファンネルが円運動でいるべき座標を計算して返す
    /// </summary>
    private Vector2 CalculateCirclePositionForFunnel(int index)
    {
        float angleStep = 360f / funnelPrefabs.Count;
        float funnelAngle = theta + (index * angleStep);
        float radian = funnelAngle * Mathf.Deg2Rad;
        float x = radius * Mathf.Cos(radian);
        return (Vector2)this.transform.position + initialPosition + new Vector2(x, 0);
    }

    /// <summary>
    /// 指定されたインデックスのファンネルが円運動であるべきOrderInLayerを計算して返す
    /// </summary>
    private int CalculateCircleSortingOrderForFunnel(int index)
    {
        if (funnelPrefabs.Count <= 0)
        {
            return sortingOrder;
        }

        // ファンネルの現在の角度を計算
        float angleStep = 360f / funnelPrefabs.Count;
        float funnelAngle = theta + (index * angleStep);

        // 角度のSin値を取得 (-1.0 ~ 1.0)。円の上半分なら正、下半分なら負になる
        float sinValue = Mathf.Sin(funnelAngle * Mathf.Deg2Rad);

        // Sin値が0より大きい（円の上半分）場合は本体より奥(-1)、そうでなければ手前(+1)に設定
        if (sinValue > 0)
        {
            return sortingOrder - 1; // 奥
        }
        else
        {
            return sortingOrder + 1; // 手前
        }
    }

    // <summary>
    /// プレイヤーが近距離攻撃の範囲内にいるかを判定する
    /// </summary>
    private bool IsPlayerInMeleeRange()
    {
        if (playerTransform == null)
            return false;

        // プレイヤーとのX座標の距離を計算
        float distanceToPlayer = Mathf.Abs(transform.position.x - playerTransform.position.x);

        // TODO: 必要であれば、プレイヤーが自分の前方にいるかの判定を追加
        // 現状は距離のみで判定
        return distanceToPlayer <= meleeAttackRange;
    }

    /// <summary>
    /// 全てのファンネルが円運動状態(InCircle)に戻っているかを判定する
    /// </summary>
    private bool AreAllFunnelsInCircle()
    {
        // 全体のステートがCirclingでないなら、ファンネルは戻っていない
        if (currentFunnelState != FunnelState.Circling)
        {
            return false;
        }
        // 各ファンネルの個別状態をチェック
        foreach (var state in funnelStates)
        {
            if (state != IndividualFunnelState.InCircle)
            {
                return false; // 一つでもInCircleでなければfalse
            }
        }
        return true; // 全てInCircleならtrue
    }

    /// <summary>
    /// 近距離攻撃を実行し、クールダウンを管理するコルーチン
    /// </summary>
    private IEnumerator PerformMeleeAttack()
    {
        // 攻撃状態に移行
        isMeleeAttacking = true;
        canMeleeAttack = false;
        hasTriggeredFunnelAttack = false; // 連携攻撃フラグをリセット

        // 確率に応じて攻撃パターンをランダムに決定
        float randomValue = Random.Range(0f, 1f); // 0.0から1.0の間のランダムな値を取得

        if (isHPbelowHalf)
        {
            if (randomValue < 0.4f) // 40%の確率
            {
                currentAttackPattern = FunnelAttackPattern.Cross;
            }
            else if (randomValue < 0.8f) // 40%の確率
            {
                currentAttackPattern = FunnelAttackPattern.TargetPlayer;
            }
            else // 残り20%の確率
            {
                currentAttackPattern = FunnelAttackPattern.Random;
            }
        }
        else
        {
            if (randomValue < 0.5f) // 50%の確率
            {
                currentAttackPattern = FunnelAttackPattern.StraightDown;
            }
            else if (randomValue < 0.8f) // 30%の確率
            {
                currentAttackPattern = FunnelAttackPattern.Cross;
            }
            else // 残り20%の確率
            {
                currentAttackPattern = FunnelAttackPattern.TargetPlayer;
            }
        }

        // 決定した攻撃パターンに対応する準備時間をリストから検索して取得する
        var setting = funnelAttackSettings.Find(s => s.attackPattern == currentAttackPattern);
        if (setting != null)
        {
            // 設定が見つかれば、その準備時間を変数に保存
            _currentFunnelReadyTime = setting.readyTime;
        }
        else
        {
            // もし設定リストに該当するパターンがなければ、デフォルト値を使用し、警告を出す
            _currentFunnelReadyTime = 2.0f;
            Debug.LogWarning(
                $"ファンネル攻撃パターン '{currentAttackPattern}' の準備時間設定が見つかりません。デフォルト値({_currentFunnelReadyTime}秒)を使用します。"
            );
        }

        animator.SetTrigger("attack"); // 近距離攻撃アニメーションを再生

        //剣の見た目の更新を開始
        swordCoroutine = StartCoroutine(UpdateSword());
        //残像を表示する
        afterImage?.SetActive(true);
        // 攻撃アニメーションが終わるのを待つ
        yield return new WaitForSeconds(attackDefaultAnimationTime);
        //剣の見た目の更新を停止
        StopCoroutine(swordCoroutine);
        swordCoroutine = null;
        //残像を非表示する
        afterImage?.SetActive(false);
        // 剣のオブジェクトを無効化
        swordObject.SetActive(false);

        // 攻撃状態を終了
        isMeleeAttacking = false;

        // クールダウン開始
        yield return new WaitForSeconds(meleeAttackCooldown);
        canMeleeAttack = true;
    }

    private IEnumerator UpdateSword()
    {
        previousBodySprite = spriteRenderer.sprite;
        swordObject.tag = GameConstants.DamageableEnemyTagName; // 剣のタグをダメージを受ける敵のタグに設定

        while (true)
        {
            // --- 1. 剣の見た目を同期する処理（スプライトが変化した時だけ実行） ---
            if (spriteRenderer != null && spriteRenderer.sprite != previousBodySprite)
            {
                bool foundMatch = false;
                foreach (var config in configs)
                {
                    if (spriteRenderer.sprite == config.bodySprite)
                    {
                        // 剣の見た目と位置を一時的に同期
                        if (swordRenderer != null)
                        {
                            swordRenderer.sprite = config.swordSprite;

                            // 右向きか否かに応じて剣の向きを調整
                            if (!rightFlag)
                                swordRenderer.flipX = true;
                            else
                                swordRenderer.flipX = false;
                        }

                        if (swordTransform != null)
                        {
                            Vector3 localPos = config.swordLocalPosition;

                            // 右向きか否かに応じて剣の向きを調整
                            if (!rightFlag)
                                localPos.x *= -1f;

                            swordTransform.localPosition = localPos;

                            // --- コライダーの形状と角度を同期 ---
                            if (swordCollider != null)
                            {
                                swordCollider.offset = config.swordColliderOffset;
                                swordCollider.size = config.swordColliderSize;
                            }
                        }

                        previousBodySprite = config.bodySprite;

                        foundMatch = true;
                        break;
                    }
                }

                if (swordObject.activeSelf != foundMatch)
                {
                    swordObject.SetActive(foundMatch);
                }

                // 同期が終わった後に、前のスプライト情報を更新
                previousBodySprite = spriteRenderer.sprite;
            }

            // --- 2. ファンネル攻撃のトリガー判定（毎フレーム実行） ---
            // この判定は、スプライトが「切り替わった瞬間」である必要はなく、
            // 「現在そのスプライトである」限り、毎フレームチェックされるようになります。
            if (
                !hasTriggeredFunnelAttack
                && funnelTriggerSprite != null
                && spriteRenderer.sprite == funnelTriggerSprite
            )
            {
                // 時間経過やファンネルの状態など、他の条件をチェック
                if (AreAllFunnelsInCircle() && timeSinceFunnelsReturned >= _currentFunnelReadyTime)
                {
                    StartPositioningMode(); // ファンネル攻撃のシーケンスを開始
                    hasTriggeredFunnelAttack = true; // この攻撃中は二度と発動しないようにフラグを立てる
                }
            }

            yield return null; // 次のフレームまで待機
        }
    }

    /// <summary>
    /// 起動アニメーションに合わせてファンネルを徐々に表示（フェードイン）させるコルーチン
    /// </summary>
    private IEnumerator FadeInFunnels()
    {
        // 1. spawnAnimationClipNameの名前のアニメーションクリップを探し、その長さを取得する
        float spawnAnimationLength = 0f;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == spawnAnimationClipName)
                {
                    spawnAnimationLength = clip.length;
                    break;
                }
            }
        }

        // もし spawnAnimationClipName クリップが見つからなかったり、長さが0の場合は処理を中断
        if (spawnAnimationLength <= 0f)
        {
            Debug.LogWarning(
                $"'{spawnAnimationClipName}' アニメーションクリップが見つからないか、再生時間が0です。フェードイン処理をスキップします。"
            );
            // 即座に表示するために、元の色に戻す
            for (int i = 0; i < funnelRenderers.Count; i++)
            {
                if (funnelRenderers[i] != null)
                    funnelRenderers[i].color = initialFunnelColors[i];
            }
            yield break; // コルーチンを終了
        }

        // 2. Animatorの再生速度(speed)を考慮して、実際のフェードイン時間を計算
        //    (例: speedが2なら半分の時間、0.5なら倍の時間になる)
        float fadeDuration = spawnAnimationLength;
        if (animator.speed > 0) // ゼロ除算を避ける
        {
            fadeDuration /= animator.speed;
        }

        foreach (var funnel in funnelPrefabs)
        {
            if (funnel != null)
            {
                funnel.SetActive(true);
            }
        }

        // 3. 計算した時間を使って、ファンネルの透明度を0から1へ滑らかに変化させる
        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float currentAlpha = Mathf.Lerp(0f, 1f, elapsedTime / fadeDuration);

            for (int i = 0; i < funnelRenderers.Count; i++)
            {
                if (funnelRenderers[i] != null)
                {
                    // 元のRGB値を保ちつつ、透明度(alpha)だけを更新
                    Color newColor = initialFunnelColors[i];
                    newColor.a = currentAlpha;
                    funnelRenderers[i].color = newColor;
                }
            }
            yield return null; // 次のフレームまで待機
        }

        // 4. 完了後、誤差をなくすために確実に元の色（透明度100%）に戻す
        for (int i = 0; i < funnelRenderers.Count; i++)
        {
            if (funnelRenderers[i] != null)
                funnelRenderers[i].color = initialFunnelColors[i];
        }
    }

    /// <summary>
    /// ボスが倒された時に実行される処理。HPスクリプトのOnDefeatedイベントから呼び出されます。
    /// </summary>
    private void HandleBossDefeat()
    {
        // 既に処理済みなら何もしない
        if (isDefeated)
            return;

        // 1. 倒されたフラグを立て、以降の行動を停止させる
        isDefeated = true;

        // 2. 実行中の全てのアクション（コルーチン）を停止
        StopAllCoroutines();

        // Chapter1の場合、背景色を元に戻す処理を開始
        if (variantType == EnemyVariant.Chapter1)
        {
            StartCoroutine(ChangeBackgroundsCoroutine(false)); // false: 元の色へ
        }

        // 3. 残像などを完全に停止
        if (afterImage != null)
            afterImage.SetActive(false);

        // 4. 関連オブジェクト（ファンネルなど）を全て破棄
        DestroyLinkedObjects();
    }

    /// <summary>
    /// 背景色を滑らかに変更するコルーチン
    /// </summary>
    /// <param name="toTargetColor">trueなら目標の色へ、falseなら元の色へ変更</param>
    private IEnumerator ChangeBackgroundsCoroutine(bool toTargetColor)
    {
        // 制御対象のVolumeが設定されているか確認
        if (chapter1Volume != null)
        {
            // 変更先のWeightを決定 (trueなら1、falseなら0)
            float targetWeight = toTargetColor ? 1f : 0f;

            // DOTweenの汎用Tween機能「DOTween.To」を使い、VolumeのWeightを滑らかに変化させる
            DOTween.To(
                () => chapter1Volume.weight, // 第1引数: 現在の値を取得するラムダ式
                x => chapter1Volume.weight = x, // 第2引数: 取得した値を設定するラムダ式
                targetWeight, // 第3引数: 最終的な目標値
                backgroundColorChangeDuration // 第4引数: 変化にかかる時間
            );
        }

        // 変更対象となる背景オブジェクトがなければ処理を終了
        if (originalBackgroundColors.Count == 0)
            yield break;

        // 各背景オブジェクトに対して色の変更を行う
        foreach (var entry in chapter1Backgrounds)
        {
            if (entry.backgroundObject != null)
            {
                var renderer = entry.backgroundObject.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    // 変更先の色を決定
                    Color finalColor = toTargetColor
                        ? entry.targetColor
                        : originalBackgroundColors[renderer];

                    // DOTweenを使用して滑らかに色を変化させる
                    renderer.DOColor(finalColor, backgroundColorChangeDuration);
                }
            }
        }

        // nullを返すのではなく、Tweenの完了を待つように変更
        // これにより、このコルーチンを呼び出した側で色の変更完了を待つことができます。
        yield return new WaitForSeconds(backgroundColorChangeDuration);
    }

    /// <summary>
    /// 背景色を即座に元の色に戻す
    /// </summary>
    private void RevertBackgroundColorsInstantly()
    {
        // 辞書に保存された元の色に即座に戻す
        foreach (var pair in originalBackgroundColors)
        {
            // DOTweenのアニメーションが実行中であれば停止する
            pair.Key.DOKill();
            pair.Key.color = pair.Value;
        }

        // VolumeのWeightも即座に元に戻す
        if (chapter1Volume != null)
        {
            chapter1Volume.weight = 0f;
        }
    }

    /// <summary>
    /// このオブジェクトが破棄される際に、イベントの購読を解除します。
    /// </summary>
    private void OnDestroy()
    {
        if (bossHealth != null)
        {
            bossHealth.OnDefeated -= HandleBossDefeat;
        }

        RevertBackgroundColorsInstantly();
    }

    private void OnEnable()
    {
        if (bossHealth != null)
        {
            bossHealth.OnHPChanged += CheckHP;
        }
    }

    private void OnDisable()
    {
        if (bossHealth != null)
        {
            bossHealth.OnHPChanged -= CheckHP;
        }

        RevertBackgroundColorsInstantly();
    }

    private void CheckHP(int _hp)
    {
        if (bossHealth == null)
        {
            return;
        }

        if (!isHPbelowHalf && bossHealth.NormalizedHP <= 0.5f)
        {
            isHPbelowHalf = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector2 center =
            new Vector2(transform.position.x, transform.position.y) + funnelMovementCenter;
        Gizmos.DrawSphere(center, 0.5f); // ファンネルの移動中心を表示

        // ----- 行動範囲の中心座標 -----
        center = new Vector3(
            (leftBound + rightBound) / 2f,
            transform.position.y + funnelWaitHeight,
            transform.position.z
        );

        // ----- 四角形のサイズ -----
        Vector3 size = new Vector3(
            rightBound - leftBound,
            2f, // 高さ
            0.1f // 厚み（奥行きは視認用に薄く）
        );

        // ----- 塗りつぶし：オレンジの半透明 -----
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f); // RGBA（オレンジ・半透明）
        Gizmos.DrawCube(center, size);
    }
}

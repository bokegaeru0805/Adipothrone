using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// 破壊可能なオブジェクト（岩、木など）のHPと破壊処理を管理するクラス。
/// CharacterHealthを継承し、共通のダメージ処理などを利用しつつ、
/// EnemyDataがなくてもHPを設定できるなど、オブジェクト固有の初期化処理を持ちます。
/// プーリングに対応し、破壊時のエフェクト再生機能も備えています。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ObjectHealth : CharacterHealth, IEnemyResettable
{
    [Header("オブジェクト固有設定")]
    [Tooltip("破壊時のフェードアウト時間")]
    [SerializeField]
    private float fadeOutDuration = 0.1f;

    [Tooltip("破壊時にエフェクトとを再生するかどうか")]
    [SerializeField]
    private bool enableDestroyEffect = false;

    [Tooltip("破壊時に再生するエフェクトのプールタグ（任意）")]
    [SerializeField, ShowIf(nameof(enableDestroyEffect))]
    private string destroyEffectPoolTag;

    [Tooltip("破壊エフェクトの大きさ")]
    [SerializeField, ShowIf(nameof(enableDestroyEffect))]
    private float destroyEffectScale = 1.0f;

    [Header("破壊時のSE設定")]
    [Tooltip("破壊時にSEを再生するかどうか")]
    [SerializeField]
    private bool enableDestroySE = false;

    [Tooltip("破壊時の効果音（任意）")]
    [SerializeField, ShowIf(nameof(enableDestroySE))]
    private SeSelector destroySE;

    [Header("破壊アニメーション設定")]
    [Tooltip("破壊時にアニメーションを再生するかどうか")]
    [SerializeField]
    private bool enableDestroyAnimation = false;

    [Tooltip("破壊アニメーションのパラメータ名（Trigger）")]
    [SerializeField, ShowIf(nameof(enableDestroyAnimation))]
    private string destroyAnimationParam = "Destroy";

    [Header("位置リセット設定")]
    [Tooltip("有効にすると、初回起動時の座標を記憶し、リセット時（再出現時）にその座標に戻ります")]
    [SerializeField]
    private bool enablePositionReset = false;

    [Header("破壊時のコライダー設定")]
    [Tooltip("破壊時、指定時間後にコライダーを無効にするかどうか")]
    [SerializeField]
    private bool enableDisableColliderOnDeath = false;

    [Tooltip("破壊からコライダーを無効にするまでの時間（秒）")]
    [SerializeField, ShowIf(nameof(enableDisableColliderOnDeath))]
    private float disableColliderDelay = 0.0f;

    [Tooltip(
        "有効にすると、初回起動時の回転（傾き）を記憶し、リセット時（再出現時）にその回転に戻ります"
    )]
    [SerializeField]
    private bool enableRotationReset = true;

    // --- 内部参照 ---
    private bool isInitialized = false; // 初期化が完了したかどうかのフラグ
    private Vector3 initialPosition; // 記憶した初期座標
    private Quaternion initialRotation; // 記憶した初期回転
    private Rigidbody2D rbody;
    private Transform dropParent;

    /// <summary>
    /// 基本クラスのAwakeを拡張し、オブジェクトに必要なコンポーネントを取得、設定します。
    /// </summary>
    protected override void Awake()
    {
        // 基本クラスのAwake処理（SpriteRendererの取得、オーバーレイ設定など）を実行
        base.Awake();

        // 固有コンポーネントの取得
        rbody = GetComponent<Rigidbody2D>();
        dropParent = this.transform.parent; // ドロップアイテムの親を設定

        // 初回起動時の座標を記憶
        // Startで行うと、EnmeyActivatorのResetState()呼び出しにより、座標が変わってしまう可能性がある
        if (enablePositionReset)
        {
            initialPosition = this.transform.position;
        }

        // 初回起動時の回転を記憶
        if (enableRotationReset)
        {
            initialRotation = this.transform.rotation;
        }
    }

    /// <summary>
    /// ゲーム開始時に、もし外部からInitializeが呼ばれていなければ、
    /// インスペクターの設定またはフォールバック設定で自己初期化します。
    /// </summary>
    private void Start()
    {
        if (!isInitialized)
        {
            // EnemyDataがあればそれを使って初期化
            if (enemyData != null)
            {
                Initialize(enemyData);
            }
            // なければフォールバック値（objectMaxHP）を使って初期化
            else
            {
                InitializeFallback();
            }
        }
    }

    /// <summary>
    /// 外部（スポナーなど）からEnemyDataを使って初期化するためのメソッド。
    /// </summary>
    public void Initialize(EnemyData data)
    {
        if (isInitialized)
            return;

        if (data == null)
        {
            Debug.LogError($"{this.gameObject.name}に設定されるEnemyDataがnullです。");
            return;
        }

        this.enemyData = data;
        MaxHP = enemyData.enemyHP;
        destroyEffectScale = enemyData.destroyeffectScale;
        // 必要ならエフェクトタグなどもデータから上書き可能

        ResetState();
        isInitialized = true;
    }

    /// <summary>
    /// EnemyDataがない場合に、インスペクターのフォールバック設定を使って初期化します。
    /// </summary>
    public void InitializeFallback()
    {
        if (isInitialized)
            return;

        if (enemyData != null)
        {
            // 既存のEnemyDataを使った初期化処理など
        }
        else if (useManualHP)
        {
            // EnemyDataが未設定で、かつ手動HP設定が有効な場合
            MaxHP = manualMaxHP;
            CurrentHP = MaxHP;
        }
        else
        {
            Debug.LogWarning(
                $"[{gameObject.name}] EnemyDataが設定されておらず、手動HP設定(useManualHP)も無効です。",
                this
            );
        }

        ResetState();
        isInitialized = true;
    }

    /// <summary>
    /// 基本クラスから継承した、オブジェクト固有の死亡（破壊）処理。
    /// </summary>
    protected override void OnDeath()
    {
        // --- 1. 破壊エフェクトの再生（フラグで制御） ---
        if (enableDestroyEffect)
        {
            if (
                ObjectPooler.PersistentInstance != null
                && !string.IsNullOrEmpty(destroyEffectPoolTag)
            )
            {
                GameObject effect = ObjectPooler.PersistentInstance.SpawnFromPool(
                    destroyEffectPoolTag,
                    this.transform.position,
                    Quaternion.identity
                );

                if (effect != null)
                {
                    effect.transform.localScale = Vector3.one * destroyEffectScale;
                }
            }
        }

        // 破壊SEの再生（フラグで制御）
        if (enableDestroySE)
        {
            CriWare.Assets.CriAtomSePlayer sePlayer =
                GetComponent<CriWare.Assets.CriAtomSePlayer>();
            if (sePlayer != null)
            {
                sePlayer.Play(destroySE.GetSelectedEnum());
            }
            else
            {
                Debug.LogWarning(
                    "ObjectHealth: 破壊SEを再生する設定ですが、CriAtomSePlayerコンポーネントがアタッチされていません。"
                );
            }
        }

        // --- コライダー無効化の遅延処理 ---
        if (enableDisableColliderOnDeath)
        {
            StartCoroutine(DisableColliderDelayCoroutine(disableColliderDelay));
        }

        // --- 2. 物理挙動の停止 ---
        if (rbody != null && rbody.bodyType != RigidbodyType2D.Static)
        {
            rbody.velocity = Vector2.zero;
        }

        // --- 3. 見た目のリセット ---
        // フェードアウト前の初期状態に戻す（透明度リセット）
        col.a = 1;
        spriteRenderer.color = col;

        // --- 4. 破壊アニメーションと非アクティブ化 ---
        float waitTime = fadeOutDuration;

        // アニメーションが有効かつAnimatorがある場合
        if (enableDestroyAnimation && animator != null)
        {
            // 破壊トリガーをセット
            animator.SetTrigger(destroyAnimationParam);
        }

        // 指定時間後に非アクティブ化（プーリング対応）
        StartCoroutine(DeactivateAfterTime(waitTime));
    }

    /// <summary>
    /// 指定時間後にコライダーを無効にするコルーチン。
    /// </summary>
    /// <param name="delay">待機時間（秒）</param>
    /// <returns></returns>
    private System.Collections.IEnumerator DisableColliderDelayCoroutine(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }
    }

    /// <summary>
    /// HPが0になった後、徐々にフェードアウトさせるための処理。
    /// </summary>
    private void FixedUpdate()
    {
        // HPが0以下で、かつ初期化済みの場合にフェードアウト
        if (Time.timeScale > 0 && CurrentHP <= 0 && isInitialized)
        {
            col.a -= 1.0f / (60.0f * fadeOutDuration);
            // 透明度が負にならないようにクランプ
            col.a = Mathf.Max(col.a, 0f);
            spriteRenderer.color = col;
        }
    }

    /// <summary>
    /// オブジェクトの状態をリセットし、再利用可能な状態にします。
    /// </summary>
    public void ResetState()
    {
        IsDefeated = false;
        CurrentHP = MaxHP;

        // 色と透明度を完全に戻す
        col.a = 1f;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = col;
        }

        // 座標を初期位置に戻す
        if (enablePositionReset)
        {
            this.transform.position = initialPosition;
        }

        // 回転を初期回転に戻す
        if (enableRotationReset)
        {
            this.transform.rotation = initialRotation;

            // Rigidbodyがある場合、回転速度もリセットしておくのが安全です
            // （ただし、StaticなRigidbodyには速度を設定できないためチェックする）
            if (rbody != null && rbody.bodyType != RigidbodyType2D.Static)
            {
                rbody.angularVelocity = 0f;
            }
        }

        // コライダーを再度有効化
        if (enableDisableColliderOnDeath)
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.enabled = true;
            }
        }

        // 物理挙動の再有効化が必要な場合はここで行う
        // if (rbody != null) rbody.isKinematic = false;
    }

    // ドロップアイテムの親オブジェクトを返すように上書き
    public override Transform GetDropParent() => this.dropParent;

    #region 独自機能
    /// <summary>
    /// このオブジェクトの最大HPを外部から動的に変更します。
    /// </summary>
    public void SetMaxHP(int newMaxHP)
    {
        if (newMaxHP <= 0)
        {
            newMaxHP = 1;
        }
        MaxHP = newMaxHP;
        CurrentHP = MaxHP;
    }
    #endregion
}

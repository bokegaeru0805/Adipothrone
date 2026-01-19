using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// プレイヤーに接触した際に、指定した種類のダメージを与える汎用コンポーネント。
/// このコンポーネントが正しく機能するには、物理イベントを受け取るために
/// 自分自身もしくは親オブジェクトにRigidbody2Dがアタッチされている必要があります。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ContactDamageController : MonoBehaviour
{
    /// <summary>
    /// ダメージの種類を定義する列挙型
    /// </summary>
    public enum DamageType
    {
        Normal, // 通常の固定ダメージ
        MaxHPRatio, // 最大HPに対する割合ダメージ
        CurrentHPRatio // 現在HPに対する割合ダメージ
        ,
    }

    #region Damage Settings

    [Header("ダメージ設定")]
    [SerializeField]
    [Tooltip("有効にすると、インスペクターで設定したダメージ値を初期値として使用します。")]
    private bool useInspectorSettings = false;

    [Tooltip("初期ダメージの種類")]
    [AllowNesting]
    [SerializeField, ShowIf(nameof(useInspectorSettings))]
    private DamageType initialDamageType = DamageType.Normal;

    [Tooltip("初期ダメージ値（固定値 または 割合0.0~1.0）")]
    [AllowNesting]
    [SerializeField, ShowIf(nameof(useInspectorSettings))]
    private float initialDamageValue = 1f;

    // 内部で使用する実際のダメージ設定
    private DamageType currentDamageType = DamageType.Normal;
    private float currentDamageValue = 1f;

    #endregion

    #region Knockback Settings

    [Header("ノックバック設定")]
    [SerializeField]
    [Tooltip("有効にすると、インスペクターで設定したノックバック情報を初期値として使用します。")]
    private bool useKnockbackSettings = false;

    [Tooltip("ノックバックの強さ")]
    [AllowNesting]
    [SerializeField, ShowIf(nameof(useKnockbackSettings))]
    private float initialKnockbackForce = 5f;

    [AllowNesting]
    [SerializeField, ShowIf(nameof(useKnockbackSettings))]
    private KnockbackType initialKnockbackType = KnockbackType.HorizontalFromSource;

    [AllowNesting]
    [SerializeField, ShowIf(nameof(ShowFixedDirection))]
    [Tooltip("固定方向へのベクトル（正規化されて計算されます）")]
    private Vector2 initialFixedDirection = Vector2.right;

    // NaughtyAttributes用: 固定ベクトルの設定を表示するかどうか
    private bool ShowFixedDirection =>
        useKnockbackSettings && initialKnockbackType == KnockbackType.FixedVector;

    // 内部で使用する実際のノックバック設定
    private float currentKnockbackForce = 0f;
    private KnockbackType currentKnockbackType = KnockbackType.HorizontalFromSource;
    private Vector2 currentFixedDirection = Vector2.right;

    #endregion

    [Header("判定設定")]
    [SerializeField]
    [Tooltip(
        "指定した場合、このコライダーに接触した時のみダメージを与えます。\n(1つのオブジェクトに感知用と攻撃用のコライダーが同居している場合などに使用)"
    )]
    private Collider2D specificCollider;

    /// <summary>
    /// オブジェクトが有効化されるたびに呼ばれます（生成時やプールからの取り出し時）
    /// </summary>
    private void OnEnable()
    {
        // --- ダメージ設定の初期化 ---
        if (useInspectorSettings)
        {
            currentDamageType = initialDamageType;
            currentDamageValue = initialDamageValue;
        }
        else
        {
            // デフォルト: 1ダメージ
            currentDamageType = DamageType.Normal;
            currentDamageValue = 1f;
        }

        // --- ノックバック設定の初期化 ---
        if (useKnockbackSettings)
        {
            currentKnockbackForce = initialKnockbackForce;
            currentKnockbackType = initialKnockbackType;
            currentFixedDirection = initialFixedDirection;
        }
        else
        {
            // デフォルト
            currentKnockbackForce = GameConstants.PLAYER_DAMAGE_DEFAULT_KNOCKBACK_FORCE;
            currentKnockbackType = KnockbackType.HorizontalFromSource;
            currentFixedDirection = Vector2.right;
        }
    }

    #region External Setup Methods (Damage)

    /// <summary>
    /// 与えるダメージを「通常の固定ダメージ」に設定します。（外部スクリプトからの上書き用）
    /// </summary>
    public void SetNormalDamage(int amount)
    {
        currentDamageType = DamageType.Normal;
        currentDamageValue = amount;
    }

    /// <summary>
    /// 与えるダメージを「最大HPに対する割合ダメージ」に設定します。（外部スクリプトからの上書き用）
    /// </summary>
    public void SetMaxHPRatioDamage(float ratio)
    {
        currentDamageType = DamageType.MaxHPRatio;
        currentDamageValue = ratio;
    }

    /// <summary>
    /// 与えるダメージを「現在HPに対する割合ダメージ」に設定します。（外部スクリプトからの上書き用）
    /// </summary>
    public void SetCurrentHPRatioDamage(float ratio)
    {
        currentDamageType = DamageType.CurrentHPRatio;
        currentDamageValue = ratio;
    }

    #endregion

    #region External Setup Methods (Knockback)

    /// <summary>
    /// ノックバック設定を外部から指定・上書きします。
    /// ※ data.sourcePosition は衝突時に自動的に自身の位置が適用されるため、ここで設定しても無視されます。
    /// </summary>
    /// <param name="data">適用したいノックバック設定（Type, Force, FixedDirection）</param>
    public void SetKnockbackSettings(KnockbackData data)
    {
        currentKnockbackType = data.type;
        currentKnockbackForce = data.force;
        currentFixedDirection = data.fixedDirection;
    }

    #endregion

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 自分のタグが "DamageableEnemy" でなければ何もしない
        if (this.tag != GameConstants.DAMAGEABLE_ENEMY_TAG_NAME)
            return;

        // specificCollider が設定されており、かつ
        // 接触した相手(other)がそのコライダーに触れていない場合は無視する
        if (specificCollider != null && !specificCollider.IsTouching(other))
        {
            return;
        }

        if (other.CompareTag(GameConstants.PLAYER_TAG_NAME))
        {
            if (PlayerManager.instance == null)
            {
                Debug.LogError("PlayerManagerのインスタンスが見つかりません。");
                return;
            }

            // 現在の設定値(current~)を使ってノックバックデータを作成
            KnockbackData data = new KnockbackData
            {
                type = currentKnockbackType,
                sourcePosition = this.transform.position, // 衝突時の自分の位置を使用
                fixedDirection = currentFixedDirection,
                force = currentKnockbackForce,
            };

            // 現在の設定値(current~)を使ってダメージ処理を実行
            switch (currentDamageType)
            {
                case DamageType.Normal:
                    PlayerManager.instance.TakeNormalDamage((int)currentDamageValue, data);
                    break;

                case DamageType.MaxHPRatio:
                    PlayerManager.instance.DamageHPByMaxHPRatio(currentDamageValue, data);
                    break;

                case DamageType.CurrentHPRatio:
                    PlayerManager.instance.DamageHPByCurrentHPRatio(currentDamageValue, data);
                    break;
            }
        }
    }
}

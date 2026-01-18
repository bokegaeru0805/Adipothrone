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

    [Header("初期設定")]
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

    /// <summary>
    /// オブジェクトが有効化されるたびに呼ばれます（生成時やプールからの取り出し時）
    /// </summary>
    private void OnEnable()
    {
        // インスペクター設定を使用する場合、値をロードする
        if (useInspectorSettings)
        {
            currentDamageType = initialDamageType;
            currentDamageValue = initialDamageValue;
        }
        else
        {
            // 使用しない場合のデフォルトリセット（必要に応じて）
            // currentDamageType = DamageType.Normal;
            // currentDamageValue = 0f;
        }
    }

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

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 自分のタグが "DamageableEnemy" でなければ何もしない
        // ※罠などの場合、タグ設定を忘れると動かないので注意
        if (this.tag != GameConstants.DAMAGEABLE_ENEMY_TAG_NAME)
            return;

        if (other.CompareTag(GameConstants.PLAYER_TAG_NAME))
        {
            if (PlayerManager.instance == null)
            {
                Debug.LogError("PlayerManagerのインスタンスが見つかりません。");
                return;
            }

            // 現在の設定値(current~)を使ってダメージ処理を実行
            switch (currentDamageType)
            {
                case DamageType.Normal:
                    PlayerManager.instance.TakeNormalDamage((int)currentDamageValue);
                    break;

                case DamageType.MaxHPRatio:
                    PlayerManager.instance.DamageHPByMaxHPRatio(currentDamageValue);
                    break;

                case DamageType.CurrentHPRatio:
                    PlayerManager.instance.DamageHPByCurrentHPRatio(currentDamageValue);
                    break;
            }
        }
    }
}

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

    private DamageType damageType = DamageType.Normal;
    private float damageValue = 1f;

    /// <summary>
    /// 与えるダメージを「通常の固定ダメージ」に設定します。
    /// </summary>
    /// <param name="amount">固定ダメージ量</param>
    public void SetNormalDamage(int amount)
    {
        damageType = DamageType.Normal;
        damageValue = amount;
    }

    /// <summary>
    /// 与えるダメージを「最大HPに対する割合ダメージ」に設定します。
    /// </summary>
    /// <param name="ratio">最大HPに対する割合 (例: 0.1f = 10%)</param>
    public void SetMaxHPRatioDamage(float ratio)
    {
        damageType = DamageType.MaxHPRatio;
        damageValue = ratio;
    }

    /// <summary>
    /// 与えるダメージを「現在HPに対する割合ダメージ」に設定します。
    /// </summary>
    /// <param name="ratio">現在HPに対する割合 (例: 0.5f = 50%)</param>
    public void SetCurrentHPRatioDamage(float ratio)
    {
        damageType = DamageType.CurrentHPRatio;
        damageValue = ratio;
    }

    /// <summary>
    /// オブジェクトが他のコライダーと接触したときに呼び出される
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 自分のタグが "DamageableEnemy" でなければ何もしない
        if (this.tag != GameConstants.DamageableEnemyTagName)
            return;

        // 接触した相手がプレイヤーかチェック
        if (other.CompareTag(GameConstants.PlayerTagName))
        {
            // PlayerManagerのインスタンスがなければ処理を中断
            if (PlayerManager.instance == null)
            {
                Debug.LogError("PlayerManagerのインスタンスが見つかりません。");
                return;
            }

            // --- PlayerManagerの適切なダメージ関数を呼び出す ---
            // インスペクターで設定されたdamageTypeに応じて処理を分岐
            switch (damageType)
            {
                case DamageType.Normal:
                    // 通常ダメージの場合は、damageValueを整数に変換して渡す
                    PlayerManager.instance.TakeNormalDamage((int)damageValue);
                    break;

                case DamageType.MaxHPRatio:
                    // 最大HP割合ダメージ
                    PlayerManager.instance.DamageHPByMaxHPRatio(damageValue);
                    break;

                case DamageType.CurrentHPRatio:
                    // 現在HP割合ダメージ
                    PlayerManager.instance.DamageHPByCurrentHPRatio(damageValue);
                    break;
            }
        }
    }
}

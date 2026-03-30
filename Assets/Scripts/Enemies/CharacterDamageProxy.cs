using UnityEngine;

#region ダメージ中継クラス
/// <summary>
/// 自身はHPを持たず、受けたダメージを本体の CharacterHealth に転送するプロキシ（代理）クラス。
/// ボスの腕や弱点パーツなど、複数の当たり判定から本体へダメージを連動させるために使用します。
/// </summary>
public class CharacterDamageProxy : MonoBehaviour, IDamageable
{
    #region インスペクター設定
    [Header("連携設定")]
    [Tooltip("ダメージを転送する本体の CharacterHealth スクリプトを設定してください。")]
    [SerializeField]
    private CharacterHealth targetHealth;

    [Header("部位特性")]
    [Tooltip(
        "この部位が受けたダメージにかかる倍率。\n例：弱点なら1.5、硬い装甲なら0.5などに設定します。"
    )]
    [SerializeField, Min(0f)]
    private float damageMultiplier = 1.0f;
    #endregion

    #region IDamageable 実装
    /// <summary>
    /// 本体の最大ヒットポイント (HP) を取得します。
    /// 本体が未設定の場合は安全のため0を返します。
    /// </summary>
    public int MaxHP => targetHealth != null ? targetHealth.MaxHP : 0;

    /// <summary>
    /// 本体の現在のヒットポイント (HP) を取得します。
    /// 本体が未設定の場合は安全のため0を返します。
    /// </summary>
    public int CurrentHP => targetHealth != null ? targetHealth.CurrentHP : 0;

    /// <summary>
    /// 攻撃側から受けたダメージに倍率を計算し、本体の CharacterHealth へ転送します。
    /// </summary>
    /// <param name="damage">攻撃側から与えられた基礎ダメージ量</param>
    public void Damage(int damage)
    {
        // 転送先が設定されていない場合は警告を出して処理を中断
        if (targetHealth == null)
        {
            Debug.LogWarning(
                $"[{this.gameObject.name}] ダメージ転送先の CharacterHealth が設定されていません！"
            );
            return;
        }

        // マイナスや0のダメージはそのまま本体のロジックに委ねる、またはここで弾く
        if (damage <= 0)
            return;

        // 倍率を掛けて四捨五入し、最終的なダメージを算出する
        int finalDamage = Mathf.RoundToInt(damage * damageMultiplier);

        // （オプション）極端に硬い部位（0.1倍など）でダメージが0に切り捨てられるのを防ぐため、
        // 元のダメージが1以上なら最低1ダメージは保証する仕様にする場合は以下のコメントアウトを解除してください。
        /*
        if (finalDamage < 1 && damage > 0)
        {
            finalDamage = 1;
        }
        */

        // 本体の CharacterHealth の Damage メソッドを呼び出し、計算後のダメージを適用する
        targetHealth.Damage(finalDamage);
    }
    #endregion
}
#endregion

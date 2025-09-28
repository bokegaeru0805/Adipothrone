using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class ContactDamageController : MonoBehaviour
{
    private int damageAmount = 1; // プレイヤーに与えるダメージ

    public void SetDamageAmount(int amount) => damageAmount = amount;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 自分のタグが "DamageableEnemy" でなければ何もしない
        if (this.tag != GameConstants.DamageableEnemyTagName)
            return;

        // 接触した相手がプレイヤーなら、ダメージ処理を行う
        if (other.CompareTag(GameConstants.PlayerTagName))
        {
            // プレイヤーにダメージを与えるためのスクリプトを取得
            Heroin_move heroin_Move = other.GetComponent<Heroin_move>();

            if (heroin_Move != null)
            {
                // プレイヤーのHPを減少させる
                heroin_Move.DamageHP(damageAmount);
            }
            else
            {
                Debug.LogWarning("Playerオブジェクトに Heroin_move スクリプトが見つかりません。");
            }
        }
    }
}

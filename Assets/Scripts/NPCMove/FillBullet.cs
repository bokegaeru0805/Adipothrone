using UnityEngine;

/// <summary>
/// Fillの弾にアタッチするクラス
/// PoolableObjectを継承し、敵の弾（特定のプールタグを持つオブジェクト）との衝突判定と相殺処理を行います。
/// </summary>
public class FillBullet : PoolableObject
{
    private string targetEnemyPoolTag = "DesertTempleGolemShoot"; // 相殺対象の敵の弾のプールタグ
    private int currentDamage = 0; // 発射時にコントローラーから設定される攻撃力

    /// <summary>
    /// 弾が発射される際に、攻撃力を設定します。
    /// </summary>
    /// <param name="damage">与えるダメージ量</param>
    public void Setup(int damage)
    {
        currentDamage = damage;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 1. キャラクター（敵やボス本体）へのダメージ判定
        CharacterHealth targetHealth = collision.GetComponent<CharacterHealth>();
        if (targetHealth != null)
        {
            // ダメージを与えて、自身はプールへ返却
            targetHealth.Damage(currentDamage);
            ReturnToPool();
            return; // 処理を終了して下の相殺判定には進まない
        }

        // 2. 敵の弾（降雨攻撃など）との相殺判定
        PoolableObject enemyBullet = collision.GetComponent<PoolableObject>();
        if (enemyBullet != null && enemyBullet.PoolTag == targetEnemyPoolTag)
        {
            // 相手の弾と自分の弾を両方ともプールに返却
            enemyBullet.ReturnToPool();
            ReturnToPool();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CharacterHealth targetHealth = collision.gameObject.GetComponent<CharacterHealth>();
        if (targetHealth != null)
        {
            targetHealth.Damage(currentDamage);
            ReturnToPool();
            return;
        }

        PoolableObject enemyBullet = collision.gameObject.GetComponent<PoolableObject>();
        if (enemyBullet != null && enemyBullet.PoolTag == targetEnemyPoolTag)
        {
            enemyBullet.ReturnToPool();
            ReturnToPool();
        }
    }
}

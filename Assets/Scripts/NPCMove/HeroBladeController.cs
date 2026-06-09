using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(CriWare.Assets.CriAtomSePlayer))]
public class HeroBladeController : MonoBehaviour
{
    private Collider2D _collider;

    // 一回の攻撃（スイング）で既にダメージを与えた敵を記録するリスト
    private HashSet<CharacterHealth> _hitEnemies = new HashSet<CharacterHealth>();

    private int _currentDamage = 0;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();

        // 最初は当たり判定をオフにしておく
        _collider.enabled = false;
    }

    /// <summary>
    /// HeroControllerから攻撃実行直前に呼ばれ、ダメージ値を設定する
    /// </summary>
    /// <param name="damage">今回のスイングで与えるダメージ</param>
    public void Setup(int damage)
    {
        _currentDamage = damage;
    }

    /// <summary>
    /// 攻撃判定を有効にする（Animation Event経由で呼ばれる想定）
    /// </summary>
    public void EnableBlade()
    {
        // 攻撃開始時に、ダメージを与えた敵の履歴をリセットする
        _hitEnemies.Clear();
        _collider.enabled = true;
    }

    /// <summary>
    /// 攻撃判定を無効にする（Animation Event経由で呼ばれる想定）
    /// </summary>
    public void DisableBlade()
    {
        _collider.enabled = false;
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // Colliderがオフの時（もしくは完全に無効な時）は何もしない
        if (!_collider.enabled)
            return;

        // 敵の判定コンポーネントを取得
        var targetHealth = collision.GetComponent<CharacterHealth>();

        if (targetHealth != null)
        {
            // 今回のスイングでまだダメージを与えていない敵であれば
            if (!_hitEnemies.Contains(targetHealth))
            {
                // 履歴に追加して、連続ヒットを防ぐ
                _hitEnemies.Add(targetHealth);

                // ダメージを与える
                targetHealth.Damage(_currentDamage);

                // SEのみを再生（Robot側の仕様に倣ってVanish1やDamage2などを指定）
                SEManager.instance?.Play(SE_EnemyAction.Damage2);
            }
        }
    }
}

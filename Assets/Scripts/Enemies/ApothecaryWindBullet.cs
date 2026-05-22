using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ApothecaryWindBullet : MonoBehaviour
{
    private Rigidbody2D _rb;
    private ContactDamageController _damageController;

    private float _leftBound;
    private float _rightBound;
    private float _bottomBound;
    private float _topBound;
    private float _disappearDelay;

    private bool _isOutOfBounds;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _damageController = GetComponent<ContactDamageController>();
    }

    /// <summary>
    /// 弾が発射される際の初期設定を行います
    /// </summary>
    public void Setup(
        Vector2 direction,
        float speed,
        int damage,
        float knockbackForce,
        float leftBound,
        float rightBound,
        float bottomBound,
        float topBound,
        float disappearDelay
    )
    {
        _leftBound = leftBound;
        _rightBound = rightBound;
        _bottomBound = bottomBound;
        _topBound = topBound;
        _disappearDelay = disappearDelay;

        _isOutOfBounds = false;

        // ダメージの設定
        if (_damageController != null)
        {
            _damageController.SetNormalDamage(damage);

            // 弾の進行方向へ吹き飛ばすノックバック設定を生成して適用する
            KnockbackData kbData = new KnockbackData
            {
                type = KnockbackType.FixedVector,
                fixedDirection = direction.normalized, // 進行方向を直接指定
                force = knockbackForce,
                sourcePosition =
                    Vector2.zero // ContactDamageController側で自動的に現在位置に上書きされるためダミー値
                ,
            };

            _damageController.SetKnockbackSettings(kbData);
        }

        // 物理挙動をリセットして初速を与える（無重力で直進させる）
        _rb.isKinematic = false;
        _rb.gravityScale = 0f; // 重力の影響を受けないようにする
        _rb.velocity = direction.normalized * speed;

        // 弾の向きを進行方向に合わせる
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Update()
    {
        // すでに範囲外判定を受けて消滅待ちの場合は何もしない
        if (_isOutOfBounds)
            return;

        // エリアの境界を越えたかどうかを判定
        Vector3 pos = transform.position;
        if (pos.x < _leftBound || pos.x > _rightBound || pos.y < _bottomBound || pos.y > _topBound)
        {
            _isOutOfBounds = true;
            StartCoroutine(DeactivateAfterDelay());
        }
    }

    /// <summary>
    /// 範囲外に出た後、指定時間待機してから非アクティブ化します
    /// </summary>
    private IEnumerator DeactivateAfterDelay()
    {
        yield return new WaitForSeconds(_disappearDelay);
        gameObject.SetActive(false);
    }
}

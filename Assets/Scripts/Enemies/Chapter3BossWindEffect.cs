using DG.Tweening;
using UnityEngine;

/// <summary>
/// 章ボスのRetreatTeleport時に発射されるWindEffectの挙動を管理するクラスです。
/// </summary>
public class Chapter3BossWindEffect : MonoBehaviour
{
    private ContactDamageController _damageController;
    private SpriteRenderer _spriteRenderer;
    private Tween _moveTween;

    private void Awake()
    {
        _damageController = GetComponent<ContactDamageController>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// 弾が発射される際の初期設定と移動処理を行います。
    /// </summary>
    public void Setup(
        Vector3 startPos,
        float targetX,
        float duration,
        int damage,
        bool isFacingRight
    )
    {
        transform.position = startPos;

        // ボスの向きに合わせて、エフェクトの向きも左右反転させる（右向き0度が基準）
        transform.rotation = isFacingRight
            ? Quaternion.Euler(0f, 0f, 0f)
            : Quaternion.Euler(0f, 180f, 0f);

        // ダメージの設定
        if (_damageController != null)
        {
            _damageController.SetNormalDamage(damage);
        }

        gameObject.SetActive(true);

        // 前回使用時のTweenが残っていれば破棄する
        if (_moveTween != null && _moveTween.IsActive())
        {
            _moveTween.Kill();
        }

        // DOTweenを用いて水平方向に徐々に加速しながら目標の端へ移動する
        _moveTween = transform
            .DOMoveX(targetX, duration)
            .SetEase(Ease.InQuad) // 徐々に加速するイージング
            .OnComplete(() =>
            {
                // 目標の端に到達したら非表示にする
                gameObject.SetActive(false);
            });
    }

    private void OnDisable()
    {
        // 弾がゲームループ外（シーン遷移や被弾時のリセットなど）で非アクティブ化された際の安全対策
        if (_moveTween != null && _moveTween.IsActive())
        {
            _moveTween.Kill();
        }
    }
}

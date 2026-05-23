using System.Collections;
using DG.Tweening;
using UnityEngine;

public class ApothecaryThunder : MonoBehaviour
{
    [Header("子オブジェクトの参照設定")]
    [Tooltip("予兆エフェクト（ParticleSystemがアタッチされたオブジェクト）")]
    [SerializeField]
    private GameObject warningEffectObject;

    [Tooltip(
        "本体エフェクト（SpriteRenderer, Animator, Collider2D, ContactDamageControllerがアタッチされたオブジェクト）"
    )]
    [SerializeField]
    private GameObject damageEffectObject;

    private ParticleSystem _warningParticle;
    private SpriteRenderer _damageSpriteRenderer;
    private Collider2D _damageCollider;
    private ContactDamageController _damageController;
    private Animator _damageAnimator;

    // DOTweenのシーケンスを管理
    private Sequence _thunderSequence;

    private void Awake()
    {
        // 必要なコンポーネントをキャッシュ
        if (warningEffectObject != null)
        {
            _warningParticle = warningEffectObject.GetComponent<ParticleSystem>();
        }

        if (damageEffectObject != null)
        {
            _damageSpriteRenderer = damageEffectObject.GetComponent<SpriteRenderer>();
            _damageCollider = damageEffectObject.GetComponent<Collider2D>();
            _damageController = damageEffectObject.GetComponent<ContactDamageController>();
            _damageAnimator = damageEffectObject.GetComponent<Animator>();
        }
    }

    /// <summary>
    /// 雷の攻撃演出をセットアップして開始します。
    /// </summary>
    public void Setup(
        int damage,
        float warningDuration,
        float transitionDuration,
        float attackDuration
    )
    {
        // 以前のシーケンスが残っていれば破棄
        if (_thunderSequence != null && _thunderSequence.IsActive())
        {
            _thunderSequence.Kill();
        }

        // --- 初期状態のリセット ---
        if (warningEffectObject != null)
        {
            warningEffectObject.SetActive(true);
            warningEffectObject.transform.localScale = Vector3.one; // スケールを(1,1,1)にリセット
            if (_warningParticle != null)
            {
                _warningParticle.Play(true);
            }
        }

        if (damageEffectObject != null)
        {
            damageEffectObject.SetActive(true);

            if (_damageCollider != null)
            {
                _damageCollider.enabled = false; // 表示が完全に終わるまでダメージ判定はオフ
            }

            if (_damageSpriteRenderer != null)
            {
                Color color = _damageSpriteRenderer.color;
                color.a = 0f; // 透明にしておく
                _damageSpriteRenderer.color = color;
            }

            if (_damageController != null)
            {
                _damageController.SetNormalDamage(damage);
            }

            if (_damageAnimator != null)
            {
                // 最初のアニメーション状態にリセット
                _damageAnimator.Play(
                    _damageAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash,
                    -1,
                    0f
                );
            }
        }

        // --- DOTweenによるフェーズ管理 ---
        _thunderSequence = DOTween.Sequence();

        // フェーズ1: 予兆を流したまま指定秒数待機
        _thunderSequence.AppendInterval(warningDuration);

        // フェーズ2: 予兆の縮小と本体のフェードイン（平行処理）
        if (warningEffectObject != null)
        {
            // 緩急(InBack)をつけて(1,1)から(0,0)へ縮小
            _thunderSequence.Append(
                warningEffectObject
                    .transform.DOScale(Vector3.zero, transitionDuration)
                    .SetEase(Ease.InBack)
            );
        }
        else
        {
            _thunderSequence.AppendInterval(transitionDuration);
        }

        if (_damageSpriteRenderer != null)
        {
            // Joinを使ってスケール縮小と平行して本体をフェードインさせる
            _thunderSequence.Join(_damageSpriteRenderer.DOFade(1f, transitionDuration));
        }

        // フェーズ3: フェードイン完了後、ダメージ判定（Collider）を有効にする
        _thunderSequence.AppendCallback(() =>
        {
            if (_warningParticle != null)
            {
                _warningParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (_damageCollider != null)
            {
                _damageCollider.enabled = true; // 完全に表示し終わったここで初めてダメージ判定が発生
            }
        });

        // フェーズ4: 攻撃（落雷）を指定秒数維持する
        _thunderSequence.AppendInterval(attackDuration);

        // フェーズ5: 消滅し、オブジェクトプールへ返却
        _thunderSequence.AppendCallback(() =>
        {
            gameObject.SetActive(false);
        });
    }

    private void OnDestroy()
    {
        // オブジェクト破棄時にTweenをキルしてメモリリークを防ぐ
        if (_thunderSequence != null && _thunderSequence.IsActive())
        {
            _thunderSequence.Kill();
        }
    }
}

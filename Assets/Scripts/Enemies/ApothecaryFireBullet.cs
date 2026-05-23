using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class ApothecaryFireBullet : MonoBehaviour
{
    [Header("子オブジェクトの参照設定")]
    [Tooltip("Fly（空中移動）時の子オブジェクト")]
    [SerializeField]
    private GameObject flyChildObject;

    [Tooltip("Burn（地上炎上）時の子オブジェクト")]
    [SerializeField]
    private GameObject burnChildObject;

    private Rigidbody2D _rb;
    private Animator _animator;

    // 子オブジェクトから取得して切り替えるダメージコントローラー
    private ContactDamageController _flyDamageController;
    private ContactDamageController _burnDamageController;

    private LayerMask _groundLayer;
    private int _solidGroundLayerIndex;

    private int _groundDamage;
    private float _groundDuration;
    private bool _isGrounded;

    // 貫通・確率用の内部変数
    private float _currentBurnProbability;
    private float _probabilityStep;
    private int _maxPierceCount;
    private int _currentHitCount;
    private List<Collider2D> _ignoredColliders = new List<Collider2D>();

    // Animatorのパラメータをハッシュ化してキャッシュ
    private readonly int _flyHash = Animator.StringToHash("FlyTrigger");
    private readonly int _burnHash = Animator.StringToHash("BurnTrigger");

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();

        // 子オブジェクトからそれぞれのコンポーネントを事前にキャッシュ
        if (flyChildObject != null)
        {
            _flyDamageController = flyChildObject.GetComponent<ContactDamageController>();
        }
        if (burnChildObject != null)
        {
            _burnDamageController = burnChildObject.GetComponent<ContactDamageController>();
        }

        // 弾が検知するレイヤー（硬い地面 ＋ 薄い足場）
        _groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );

        // 100%炎上させるための「硬い地面」のレイヤーインデックス
        _solidGroundLayerIndex = LayerMask.NameToLayer(GameConstants.PHYSICS_LAYER_NAME_GROUND);
    }

    /// <summary>
    /// 弾が発射される際の初期設定を行います
    /// </summary>
    public void Setup(
        Vector2 direction,
        float speed,
        int airDamage,
        int groundDamage,
        float groundDuration,
        float initialBurnProbability,
        int maxPierceCount
    )
    {
        _groundDamage = groundDamage;
        _groundDuration = groundDuration;
        _isGrounded = false;

        // 貫通関連の初期化
        _currentBurnProbability = initialBurnProbability;
        _maxPierceCount = maxPierceCount;
        _currentHitCount = 0;
        _ignoredColliders.Clear();

        // 限界回数に達した時にちょうど1.0(100%)になるような1回あたりの上昇幅を計算
        if (_maxPierceCount > 0)
        {
            _probabilityStep = (1.0f - initialBurnProbability) / _maxPierceCount;
        }
        else
        {
            _probabilityStep = 0f;
            _currentBurnProbability = 1.0f; // 貫通させない設定の場合は最初から100%
        }

        // 子オブジェクトの初期アクティブ状態を設定（Fly側を有効、Burn側を無効）
        if (flyChildObject != null)
            flyChildObject.SetActive(true);
        if (burnChildObject != null)
            burnChildObject.SetActive(false);

        // 空中用のダメージを子オブジェクトのコントローラーに設定
        if (_flyDamageController != null)
        {
            _flyDamageController.SetNormalDamage(airDamage);
        }

        // 初期状態のアニメーション（空中の弾）に設定
        if (_animator != null)
            _animator.SetTrigger(_flyHash);

        // 物理挙動をリセットして初速を与える
        _rb.isKinematic = false;
        _rb.velocity = direction.normalized * speed;

        // 初期発射時の向きを進行方向に向ける
        UpdateRotation();
    }

    private void Update()
    {
        if (_isGrounded)
            return;

        // 上昇中も含めて、移動中は常に進行方向を向くように回転を更新する
        UpdateRotation();

        // 下降中のみ着地・すり抜け判定を行う
        if (_rb.velocity.y <= 0)
        {
            // 自身の中心から真下へ向けてRaycastで地面を検知（貫通するため複数取得）
            RaycastHit2D[] hits = Physics2D.RaycastAll(
                transform.position,
                Vector2.down,
                0.5f,
                _groundLayer
            );

            // 弾に近いものから順に処理できるよう距離でソート
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                // すでにすり抜けた足場は無視する
                if (hit.collider == null || _ignoredColliders.Contains(hit.collider))
                    continue;

                // 1. 完全に硬い地面(GROUNDレイヤー)の場合は、確率によらず100%炎上
                if (hit.collider.gameObject.layer == _solidGroundLayerIndex)
                {
                    HandleLanding(hit);
                    break;
                }
                else
                {
                    // 2. 薄い足場(OBJECT_GROUND等)の場合は確率で判定
                    if (
                        _currentHitCount >= _maxPierceCount
                        || UnityEngine.Random.value <= _currentBurnProbability
                    )
                    {
                        // 炎上する
                        HandleLanding(hit);
                        break;
                    }
                    else
                    {
                        // 貫通（すり抜け）する
                        _currentHitCount++;
                        _currentBurnProbability += _probabilityStep; // 確率を上昇
                        _ignoredColliders.Add(hit.collider); // 今回すり抜けた床を無視リストに追加

                        // 落下を継続させるため、ここでループを抜けて次のフレームへ
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 弾の向きを現在の進行方向（Rigidbody2Dの速度ベクトル）に合わせて回転させます
    /// </summary>
    private void UpdateRotation()
    {
        if (_rb.velocity.sqrMagnitude > 0.01f)
        {
            // デフォルトが真右（Vector2.right）なので、Atan2で算出した角度をそのまま適用
            float angle = Mathf.Atan2(_rb.velocity.y, _rb.velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    /// <summary>
    /// 着地（炎上）が確定した瞬間の処理を行います
    /// </summary>
    /// <param name="hit">地面のコライダーとの衝突情報</param>
    private void HandleLanding(RaycastHit2D hit)
    {
        _isGrounded = true;

        // 物理演算を停止してその場に固定する
        _rb.velocity = Vector2.zero;
        _rb.isKinematic = true;

        // 着地した瞬間に向きをデフォルト（回転なし）に戻す
        transform.rotation = Quaternion.identity;

        // PivotがBottom（足元基準）なので、座標をRaycastが衝突した地面のポイントぴったりに補正する
        transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);

        // 子オブジェクトを切り替える（Fly用コライダーを無効にし、Burn用コライダーを有効化）
        if (flyChildObject != null)
            flyChildObject.SetActive(false);
        if (burnChildObject != null)
            burnChildObject.SetActive(true);

        // 炎上用のダメージを新しく有効化された子オブジェクト側のコントローラーに設定
        if (_burnDamageController != null)
        {
            _burnDamageController.SetNormalDamage(_groundDamage);
        }

        // 炎上アニメーションへ移行
        if (_animator != null)
            _animator.SetTrigger(_burnHash);

        // 一定時間後に非アクティブ化してプールへ返却する
        StartCoroutine(BurnAndDeactivate());
    }

    private IEnumerator BurnAndDeactivate()
    {
        yield return new WaitForSeconds(_groundDuration);
        gameObject.SetActive(false);
    }
}

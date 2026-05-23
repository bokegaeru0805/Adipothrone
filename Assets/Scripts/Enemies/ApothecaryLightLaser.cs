using DG.Tweening;
using UnityEngine;

public class ApothecaryLightLaser : MonoBehaviour
{
    [Header("子オブジェクトの参照")]
    [Tooltip("予測線を描画するLineRenderer")]
    [SerializeField]
    private LineRenderer predictionLine;

    [Tooltip("レーザー本体（DrawMode=Tiled, Pivot=LeftCenterを想定）")]
    [SerializeField]
    private SpriteRenderer laserSpriteRenderer;

    [Tooltip("ダメージ判定（大きさは予め最終形に設定しておくこと）")]
    [SerializeField]
    private BoxCollider2D damageCollider;

    [Tooltip("ダメージ設定用コンポーネント")]
    [SerializeField]
    private ContactDamageController damageController;

    private float _defaultHeight;

    private void Awake()
    {
        if (laserSpriteRenderer != null)
        {
            // TiledモードのYサイズ(太さ)を記録しておく
            _defaultHeight = laserSpriteRenderer.size.y;
        }

        if (predictionLine != null)
        {
            // 確実な描画のためにローカルスペース設定を無効化（ワールド座標でセットする）
            predictionLine.useWorldSpace = true;
        }
    }

    /// <summary>
    /// プールから呼び出された際の初期化を行います
    /// </summary>
    public void Setup(int damage)
    {
        if (damageController != null)
        {
            damageController.SetNormalDamage(damage);
        }

        // 全ての表示と判定をオフにしておく
        if (predictionLine != null)
            predictionLine.gameObject.SetActive(false);
        if (laserSpriteRenderer != null)
            laserSpriteRenderer.gameObject.SetActive(false);
        if (damageCollider != null)
            damageCollider.enabled = false;

        // アニメーション用にスケールのY(太さ)を0にリセットしておく
        transform.localScale = new Vector3(1f, 0f, 1f);

        if (laserSpriteRenderer != null)
        {
            // TiledモードのWidth(長さ)も0にリセット
            laserSpriteRenderer.size = new Vector2(0f, _defaultHeight);
        }
    }

    /// <summary>
    /// 予測線を指定した角度、長さ、太さ、色で更新します
    /// </summary>
    public void UpdatePredictionLine(
        Vector3 origin,
        float angle,
        float length,
        float width,
        Color color
    )
    {
        if (predictionLine == null)
            return;

        if (!predictionLine.gameObject.activeSelf)
        {
            predictionLine.gameObject.SetActive(true);
        }

        // 親オブジェクトを回転させる
        transform.position = origin;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // 予測線の位置を設定（ワールド座標）
        predictionLine.SetPosition(0, origin);
        Vector3 endPos = origin + (transform.right * length);
        predictionLine.SetPosition(1, endPos);

        // 太さを動的に適用（始点と終点の両方に適用して均一な太さにする）
        predictionLine.startWidth = width;
        predictionLine.endWidth = width;

        // マテリアルの色を動的に適用
        predictionLine.startColor = color;
        predictionLine.endColor = color;
    }

    /// <summary>
    /// 予測線を消し、レーザー本体の展開アニメーションを開始します
    /// </summary>
    public void Fire(float expandDuration, float length)
    {
        if (predictionLine != null)
            predictionLine.gameObject.SetActive(false);
        if (laserSpriteRenderer != null)
            laserSpriteRenderer.gameObject.SetActive(true);

        // DOTweenのSequenceを用いて、長さ(Width)と太さ(ScaleY)を同時に展開する
        Sequence seq = DOTween.Sequence();

        if (laserSpriteRenderer != null)
        {
            seq.Join(
                DOTween
                    .To(
                        () => laserSpriteRenderer.size,
                        x =>
                        {
                            // 1. スプライトのサイズを更新 (TiledのWidth変更)
                            laserSpriteRenderer.size = x;

                            // 2. リアルタイムにBoxCollider2DのサイズとオフセットをTiledの Width (x) に合わせて同期させる
                            if (damageCollider != null)
                            {
                                // コライダーの横幅を現在のスプライトのWidthに合わせる
                                damageCollider.size = new Vector2(x.x, damageCollider.size.y);
                                // Pivot=LeftCenterのため、右側にWidthの半分だけオフセットをずらす
                                damageCollider.offset = new Vector2(
                                    x.x / 2f,
                                    damageCollider.offset.y
                                );
                            }
                        },
                        new Vector2(length, _defaultHeight),
                        expandDuration
                    )
                    .SetEase(Ease.OutQuad)
            );
        }

        seq.Join(transform.DOScaleY(1f, expandDuration).SetEase(Ease.OutQuad));
    }

    /// <summary>
    /// ダメージ判定を有効にします（完全に展開し終わった後に呼ばれます）
    /// </summary>
    public void EnableDamage()
    {
        if (damageCollider != null)
        {
            damageCollider.enabled = true;
        }
    }

    /// <summary>
    /// ダメージ判定をオフにし、レーザーを細くしながら消滅させます
    /// </summary>
    public void End(float endDuration)
    {
        if (damageCollider != null)
            damageCollider.enabled = false;

        // スケールのYを0に戻して消去
        transform
            .DOScaleY(0f, endDuration)
            .SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                gameObject.SetActive(false);
            });
    }
}

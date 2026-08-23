using System.Collections;
using UnityEngine;

/// <summary>
/// 弾のバウンド挙動と生存時間・フェードアウトを管理するコンポーネント。
/// Collider2D (Is Trigger = true) と Rigidbody2D を必要とします。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class BouncingBulletController : MonoBehaviour
{
    /// <summary>
    /// 弾が地面へ着地（バウンド）した地点を通知します。
    /// </summary>
    public event System.Action<Vector2> Bounced;

    #region 設定項目
    private LayerMask groundLayer;

    [Header("バウンド設定")]
    [SerializeField]
    [Tooltip("最大バウンド回数")]
    private int maxBounces = 3;

    [SerializeField]
    [Tooltip("バウンド後の最大到達高さ")]
    private float bounceHeight = 2.0f;

    [Header("フェード設定")]
    [SerializeField]
    [Tooltip("消滅時のフェードアウトにかかる時間（秒）")]
    private float fadeDuration = 0.5f;
    #endregion

    #region プライベート変数
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Collider2D myCollider;

    private int currentBounces = 0;
    private bool isFading = false;
    private float lifeTimer = 0f;
    private float maxLifetime = 3f;
    #endregion

    #region 初期化処理
    private void Awake()
    {
        groundLayer = LayerMask.GetMask(
            GameConstants.PHYSICS_LAYER_NAME_GROUND,
            GameConstants.PHYSICS_LAYER_NAME_OBJECT_GROUND
        );

        myCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        // オブジェクトプール等で再利用される際の初期化
        currentBounces = 0;
        isFading = false;
        lifeTimer = 0f;

        // 色と透明度をリセット
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = 1f;
            spriteRenderer.color = c;
        }
    }

    /// <summary>
    /// 生成元（ガーゴイル側など）から生存時間を設定するための初期化メソッド
    /// </summary>
    /// <param name="lifetime">最大生存時間（秒）</param>
    public void Initialize(float lifetime)
    {
        maxLifetime = lifetime;
    }
    #endregion

    #region 更新・判定処理
    private void FixedUpdate()
    {
        if (isFading)
            return;

        // 弾が下に向かって落ちている時（Y軸の速度が 0 以下の時）のみ地面判定を行う
        if (rb.velocity.y <= 0)
        {
            // 弾の幅を持たせた箱（BoxCast）のサイズを設定
            Vector2 boxSize = new Vector2(myCollider.bounds.size.x, 0.05f);

            // 次のフレームで移動する距離 + めり込み防止用のわずかな余裕
            float checkDistance = Mathf.Abs(rb.velocity.y) * Time.fixedDeltaTime + 0.1f;

            // コライダーの中心から下に向かって箱を飛ばし、地面を予測検知する
            RaycastHit2D hit = Physics2D.BoxCast(
                myCollider.bounds.center,
                boxSize,
                0f,
                Vector2.down,
                checkDistance,
                groundLayer
            );

            if (hit.collider != null)
            {
                if (currentBounces < maxBounces)
                {
                    currentBounces++;

                    // 貫通・めり込み防止：弾の下端が地面の表面にぴったり接するように位置を補正
                    float diffY = transform.position.y - myCollider.bounds.min.y;
                    transform.position = new Vector3(
                        transform.position.x,
                        hit.point.y + diffY,
                        transform.position.z
                    );

                    Bounced?.Invoke(hit.point);
                    Bounce();
                }
                else
                {
                    // バウンド回数が指定回数を超えたらフェードアウト開始
                    StartCoroutine(FadeAndDisable());
                }
            }
        }
    }

    private void Update()
    {
        if (isFading)
            return;

        lifeTimer += Time.deltaTime;

        // 生存時間を超えたらフェードアウトして消える
        if (lifeTimer >= maxLifetime)
        {
            StartCoroutine(FadeAndDisable());
        }

        // 速度ベクトルから角度を計算し、弾をその方向へ回転させる
        // 速度が極端に小さい場合（停止時など）は回転させないように制限を設ける
        if (rb.velocity.sqrMagnitude > 0.01f)
        {
            // Atan2を用いてY方向とX方向の速度からラジアンを求め、度に変換する
            float angle = Mathf.Atan2(rb.velocity.y, rb.velocity.x) * Mathf.Rad2Deg;

            // Z軸に対して算出した角度分だけ回転させる
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }
    #endregion

    #region バウンド・フェード処理
    /// <summary>
    /// 地面検知時に上方向の速度を与え、バウンドさせる処理
    /// </summary>
    private void Bounce()
    {
        // 横に移動し続けるため、X方向の速度は維持
        float currentVelocityX = rb.velocity.x;

        // バウンド後の最大到達高さ(h)から、必要な上向きの初速(v)を計算： v = √(2gh)
        float gravity = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
        float bounceVelocityY = Mathf.Sqrt(2f * gravity * bounceHeight);

        rb.velocity = new Vector2(currentVelocityX, bounceVelocityY);
    }

    /// <summary>
    /// 徐々に透明になり、完全に消えたら非アクティブ化するコルーチン
    /// </summary>
    private IEnumerator FadeAndDisable()
    {
        isFading = true;

        // フェード中は物理挙動を停止し、その場に留まらせる
        rb.velocity = Vector2.zero;
        rb.gravityScale = 0f;

        Color initialColor = spriteRenderer.color;
        float elapsed = 0f;

        // 徐々に透明にする
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            spriteRenderer.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
            yield return null; // 1フレーム待機
        }

        // 完全に透明になったら非アクティブ化して使い回せるようにする
        gameObject.SetActive(false);
    }
    #endregion
}

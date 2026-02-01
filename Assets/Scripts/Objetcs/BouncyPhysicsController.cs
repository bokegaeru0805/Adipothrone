using UnityEngine;

public class BoundPhysicsController : MonoBehaviour
{
    private Rigidbody2D rb;
    private Vector2 lastVelocity; // 衝突直前の速度を保持

    [Header("設定")]
    [Tooltip("壁として判定する角度の閾値（0.5なら45度以上の急斜面や壁を壁とみなす）")]
    [SerializeField]
    private float wallNormalThreshold = 0.5f;

    [Tooltip("最低速度制限（壁にハマって止まるのを防ぐ）")]
    [SerializeField]
    private float minSpeed = 5.0f;

    [Tooltip("移動方向に合わせて回転させるか")]
    [SerializeField]
    private bool rotateTowardsDirection = true;

    [Header("転がり維持設定")]
    [Tooltip("速度維持を行う力（大きくしすぎると不自然に急加速する）")]
    [SerializeField]
    private float speedCorrectionForce = 5.0f;

    // --- 内部変数 ---
    private float rotationMultiplier; // 回転速度調整用

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        // 自身のコライダーを取得して、その大きさから半径を割り出す
        Collider2D col = GetComponent<Collider2D>();

        // bounds.extents.x は「バウンディングボックスの半分の幅」＝「ワールド空間での半径」
        // スケール変更（Transform Scale）も考慮された値が取れます
        float radius = col.bounds.extents.x;

        // 計算ロジック:
        // 1回転（360度）で進む距離（円周） = 2 * π * r
        // 単位距離（1メートル）進むのに必要な回転角度 = 360 / (2 * π * r)
        // 整理すると: 180 / (π * r)
        rotationMultiplier = (180f / Mathf.PI) / radius;
    }

    void Update()
    {
        // 移動速度に合わせて見た目を回転させる
        // 右(Xプラス)に進むときは時計回り(Zマイナス)させるため、velocity.xにマイナスを掛ける
        // Time.deltaTimeを掛けることで、フレームレートに依存せず滑らかに回す
        float rotateAmount = -rb.velocity.x * rotationMultiplier * Time.deltaTime;
        transform.Rotate(0, 0, rotateAmount);
    }

    void FixedUpdate()
    {
        // 物理演算の直前の速度を常に記録しておく
        // (OnCollisionEnter2Dが呼ばれる時点では、すでに衝突して速度が変わっているため)
        lastVelocity = rb.velocity;

        // 最低速度を下回っている場合
        if (rb.velocity.magnitude < minSpeed)
        {
            // 進行方向に力を加えて加速させる（AddForceを使うことで自然に加速）
            // ForceMode2D.Force は継続的な力を加えるモード
            rb.AddForce(rb.velocity.normalized * speedCorrectionForce, ForceMode2D.Force);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // 相手も鉄球（BoundPhysicsControllerを持っている）なら、
        // スクリプトによる強制反射を行わず、物理演算に任せて「ゴツン」と衝突させる
        if (collision.gameObject.GetComponent<BoundPhysicsController>() != null)
        {
            return;
        }

        // 衝突時の接触点は1つとは限らない（床と壁の角などでは複数発生する）ため、
        // 全ての接触点をループして「壁」に当たっていないか確認する
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            Vector2 normal = contact.normal;
            //Debug.Log($"Contact normal: {normal},{i}", this);

            // --- 壁判定 ---
            // 法線のX成分の絶対値が大きい = 横向きの面（壁）に当たった
            if (Mathf.Abs(normal.x) > wallNormalThreshold)
            {
                // --- 反射処理 ---
                // 記録しておいた「衝突前の速度」を使って反射ベクトルを計算
                Vector2 reflectVelocity = Vector2.Reflect(lastVelocity, normal);

                // 速度の大きさを維持（または最低速度を保証）して適用
                float speed = Mathf.Max(lastVelocity.magnitude, minSpeed);
                rb.velocity = reflectVelocity.normalized * speed;

                //Debug.Log($"Bounced off wall with new velocity: {rb.velocity}", this);
                // 壁で跳ね返ったら、他の接触点（床など）の判定は不要なので処理を終える
                return;
            }
        }
    }
}

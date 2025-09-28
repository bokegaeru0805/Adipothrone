using UnityEngine;

public class Enemy1MoveController : MonoBehaviour, IEnemyResettable
{
    [Header("敵のタイプ")]
    [SerializeField]
    private EnemyVariant variantType = EnemyVariant.None; //敵の種類を設定

    [Header("設定項目")]
    [SerializeField]
    private EnemyActivator activator = null; // 親のEnemyActivatorコンポーネント

    [Header("移動タイプを選択")]
    [SerializeField]
    private MoveType moveType = MoveType.None; // 移動タイプの選択

    [Header("移動・攻撃の基本設定")]
    [SerializeField]
    private float speed = 0; // 移動スピード

    [Header("HorizontalSineを選択した場合に必要")]
    [SerializeField, Tooltip("振幅")]
    private float swingBound = 0; //振幅

    [SerializeField, Tooltip("周期")]
    private float cycletime = 0; //周期

    [Header("必要ならば設定")]
    [SerializeField]
    private float leftBound = 0; // 左端の位置

    [SerializeField]
    private float rightBound = 0; // 右端の位置

    // 敵の種類を定義
    private enum EnemyVariant
    {
        None = 0,
        TutorialStage = 1,
    }

    private enum MoveType
    {
        None = 0,
        Horizontal = 10,
        HorizontalSine = 20,
    }

    private int damage = 0; // 攻撃力
    private float swingtime = 0; //y軸移動の時間を保存
    private bool movingRight = true; // 右に移動中かどうか
    private Rigidbody2D rbody;
    private SpriteRenderer spriteRenderer;
    private EnemyHealth enemyHP;

    private void Awake()
    {
        if (moveType == MoveType.None)
        {
            Debug.LogError(
                $"{this.gameObject.name}の移動タイプが設定されていません。MoveTypeを選択してください。"
            );
            return;
        }

        switch (variantType)
        {
            case EnemyVariant.TutorialStage:
                damage = 9;
                break;
            default:
                Debug.LogError($"{this.name}のEnemyVariantが設定されていません。");
                break;
        }

        if (activator == null)
        {
            activator = GetComponentInParent<EnemyActivator>();
            if (activator == null)
            {
                Debug.LogWarning(
                    $"{this.name}の親にEnemyActivatorが見つかりませんでした。移動範囲の自動設定は行いません。"
                );
            }
        }

        spriteRenderer = this.GetComponent<SpriteRenderer>();

        rbody = GetComponent<Rigidbody2D>(); //Rigidbody2Dコンポーネントを取得
        if (rbody == null)
        {
            Debug.LogError($"{this.gameObject.name}にRigidbody2Dコンポーネントがありません。");
            return;
        }

        enemyHP = this.GetComponent<EnemyHealth>();
        {
            if (enemyHP == null)
            {
                Debug.LogError($"{this.gameObject.name}にEnemyHealthコンポーネントがありません。");
                return;
            }
        }
    }

    private void Start()
    {
        spriteRenderer.flipX = true; //画像の左右の向きを初期化する
        swingtime = 0;

        //アクティベーターから座標に関する情報を取得
        if (activator != null)
        {
            var activatorCollider = activator.GetComponent<Collider2D>();
            if (activatorCollider != null)
            {
                if (leftBound == 0)
                {
                    leftBound = activatorCollider.bounds.min.x; // アクティベーターの左端を取得
                }

                if (rightBound == 0)
                {
                    rightBound = activatorCollider.bounds.max.x; // アクティベーターの右端を取得
                }
            }
        }

        //攻撃力の設定
        ContactDamageController contactDamageController = GetComponent<ContactDamageController>();
        if (contactDamageController != null)
        {
            contactDamageController.SetDamageAmount(damage); // 攻撃力を設定
        }
        else
        {
            Debug.LogWarning(
                $"{this.gameObject.name}にEnemyStateControllerコンポーネントがありません。"
            );
        }
    }

    public void ResetState()
    {
        // 敵の状態をリセットするメソッド
        spriteRenderer.flipX = true; //画像の左右の向きを初期化する
        swingtime = 0; //y軸移動の時間の初期化
        movingRight = true; // 右に移動中に初期化
        this.tag = GameConstants.DamageableEnemyTagName; // タグを初期化

        if (rbody != null)
        {
            rbody.velocity = Vector2.zero; // 速度をリセット
            rbody.simulated = true; // 物理挙動を再起動
            rbody.constraints = RigidbodyConstraints2D.FreezeRotation; //回転を停止する
        }

        if (enemyHP != null)
        {
            // 自分のHPをリセット
            enemyHP.ResetState();
        }
        else
        {
            Debug.LogWarning($"{this.gameObject.name}にenemy_HPコンポーネントがありません。");
        }
    }

    private void FixedUpdate()
    {
        // 敵の動きが一時停止されているかどうかを確認
        if (TimeManager.instance.isEnemyMovePaused)
        {
            if (rbody != null && rbody.simulated)
                rbody.simulated = false; //物理挙動を停止する
        }
        else
        {
            if (rbody != null && !rbody.simulated)
                rbody.simulated = true; //物理挙動を再起動する

            switch (moveType)
            {
                case MoveType.Horizontal:
                    HorizonrtalMove();
                    break;
                case MoveType.HorizontalSine:
                    HorizontalSineMove();
                    break;
                default:
                    Debug.LogWarning($"{this.gameObject.name}の移動タイプが不正です。");
                    break;
            }
        }
    }

    private void HorizonrtalMove()
    {
        // 現在の速度を設定
        if (movingRight)
        {
            rbody.velocity = new Vector2(speed, rbody.velocity.y);
        }
        else
        {
            rbody.velocity = new Vector2(-speed, rbody.velocity.y);
        }

        // 端に到達したら方向を反転
        if (transform.position.x >= rightBound)
        {
            movingRight = false;
        }
        else if (transform.position.x <= leftBound)
        {
            movingRight = true;
        }

        // 左右に向きを変える
        spriteRenderer.flipX = movingRight;
    }

    private void HorizontalSineMove()
    {
        swingtime += Time.deltaTime;

        if (swingtime >= cycletime)
        {
            swingtime = 0; //y軸移動の時間の初期化
        }

        // 現在の速度を設定
        if (movingRight)
        {
            rbody.velocity = new Vector2(
                speed,
                (swingBound * 2 * Mathf.PI / cycletime)
                    * Mathf.Cos(2 * Mathf.PI * swingtime / cycletime)
            );
        }
        else
        {
            rbody.velocity = new Vector2(
                -speed,
                (swingBound * 2 * Mathf.PI / cycletime)
                    * Mathf.Cos(2 * Mathf.PI * swingtime / cycletime)
            );
        }

        // 端に到達したら方向を反転
        if (transform.position.x >= rightBound)
        {
            movingRight = false;
        }
        else if (transform.position.x <= leftBound)
        {
            movingRight = true;
        }

        // 左右に向きを変える
        spriteRenderer.flipX = movingRight;
    }

    private void OnDrawGizmosSelected()
    {
        // 境界が未設定なら描画しない
        if (leftBound == 0 || rightBound == 0)
        {
            return;
        }

        // ----- 行動範囲の中心座標 -----
        Vector3 center = new Vector3(
            (leftBound + rightBound) / 2f,
            transform.position.y - 0.25f, // 少し下にずらして描画
            transform.position.z
        );

        // ----- 四角形のサイズ -----
        Vector3 size = new Vector3(
            rightBound - leftBound,
            1.5f, // 高さ（上下の視認性用）
            0.1f // 厚み（奥行きは視認用に薄く）
        );

        // ----- 塗りつぶし：オレンジの半透明 -----
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f); // RGBA（オレンジ・半透明）
        Gizmos.DrawCube(center, size);

        // ----- 枠線：赤 -----
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(center, size);
    }
}

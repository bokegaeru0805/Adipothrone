using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Effekseer;
using UnityEngine;

public class Robot_shoot_move : MonoBehaviour
{
    #region 外部コンポーネント参照
    //================================================================================
    // 他のスクリプトやエフェクトなど、外部のアセットへの参照
    //================================================================================
    private PlayerEffectManager playerEffectManager;

    [SerializeField]
    private EffekseerEmitter hitEffect;
    #endregion


    #region 弾の基本パラメータ
    //================================================================================
    // 武器データ(ShootWeaponData)から初期化される、弾の基本的な性能値
    //================================================================================
    ShootWeaponData currentShootData = null; // 現在の武器データ
    private int shootPower = 0; // 弾そのものの攻撃力
    private float shootSpeed = 0; // 弾の速度
    public float vanishTime { get; private set; } = 0; // 弾が消滅するまでの時間
    private float cooldownTime = 1.0f; // 同一の敵に再度ダメージを与えるまでのクールタイム
    private float wpCost = 0f; // 消費WP
    private int penetrationLimitCount = 0; // 最大貫通数
    private ShootWeaponData.ShootMoveType moveType = ShootWeaponData.ShootMoveType.None; // 弾の移動タイプ
    #endregion


    #region 特殊な移動タイプ用のパラメータ
    //================================================================================
    // 特定のmoveTypeでのみ使用される設定値
    //================================================================================
    [Header("3-Way弾用の設定")]
    [SerializeField, Tooltip("上下の弾が広がる高さ")]
    private float height = 1.5f;
    #endregion


    #region 実行中の状態管理
    //================================================================================
    // 弾が発射されてから消えるまでの間に変化する、内部的な状態変数
    //================================================================================
    public bool isStarted { get; private set; } = false; // 生成と初期化が完了したか
    private Dictionary<GameObject, float> enemyCooldowns = new Dictionary<GameObject, float>(); // ヒットした敵ごとのクールダウンタイマー
    private int currentPenetrationCount = 0; // 現在の貫通数
    private bool isMoveRight = true; // 弾の移動方向（true: 右, false: 左）
    private Vector2 initialPosition; // 弾が発射された初期位置
    private bool isSubBullet = false; // 自身が複製された弾（サブ弾）かどうかのフラグ
    #endregion

    /// <summary>
    /// Robot_moveから武器データを受け取り、自身のパラメータを設定する
    /// </summary>
    public void InitializeBullet(ShootWeaponData data, bool moveRight)
    {
        this.isMoveRight = moveRight;

        if (data != null)
        {
            currentShootData = data;
            this.GetComponent<SpriteRenderer>().sprite = data.itemSprite; // スプライトを設定
            shootPower = data.power;
            wpCost = data.wpCost;
            vanishTime = data.vanishTime;
            shootSpeed = data.shootSpeed;
            cooldownTime = data.cooldownTime;
            penetrationLimitCount = data.penetrationLimitCount;
            moveType = data.moveType;

            // Colliderの設定
            CircleCollider2D collider = this.GetComponent<CircleCollider2D>();
            if (collider != null)
            {
                collider.offset = data.colliderOffset;
                collider.radius = data.colliderRadius;
            }

            // アニメーションの設定
            if (data.shootAnimation != null)
            {
                Animator animator = this.GetComponent<Animator>();
                animator.enabled = true; // アニメーションを有効化
                animator.Play(data.shootAnimation.name);
            }
        }
        else
        {
            Debug.LogWarning("Shootデータがnullのため、弾を初期化できません。");
            Destroy(gameObject);
            return;
        }

        Rigidbody2D newrbody = this.gameObject.GetComponent<Rigidbody2D>();
        this.gameObject.GetComponent<SpriteRenderer>().flipX = !moveRight; //弾の画像の向きを決定
        currentPenetrationCount = 0; //貫通数を初期化
        Destroy(this.gameObject, vanishTime); //指定した時間後に弾を消す

        // 自身がメイン弾であり、タイプがParallel3Wayの場合のみ複製処理を行う
        if (!isSubBullet && moveType == ShootWeaponData.ShootMoveType.Parallel3Way)
        {
            // 上方向の弾を複製
            CreateSubBullet(1f);
            // 下方向の弾を複製
            CreateSubBullet(-1f);
            newrbody.AddForce(
                new Vector2((isMoveRight ? 1 : -1) * shootSpeed, 0),
                ForceMode2D.Impulse
            ); //弾の速度を設定
            isStarted = true; //生成が完了した
        }
        else if (moveType == ShootWeaponData.ShootMoveType.Straight)
        {
            // 直線移動の場合は、初速を設定してすぐに発射
            newrbody.AddForce(
                new Vector2((isMoveRight ? 1 : -1) * shootSpeed, 0),
                ForceMode2D.Impulse
            ); //弾の速度を設定
            isStarted = true; //生成が完了した
        }
        else
        {
            Debug.LogWarning("不明な弾の移動タイプ: " + moveType);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 上下斜めに動くサブ弾を生成し、初期化する
    /// </summary>
    private void CreateSubBullet(float yDirection)
    {
        // 自身を複製してサブ弾を生成
        GameObject subBulletGO = Instantiate(
            this.gameObject,
            transform.position,
            Quaternion.identity
        );
        Robot_shoot_move subBulletScript = subBulletGO.GetComponent<Robot_shoot_move>();

        // サブ弾として初期化

        subBulletScript.isSubBullet = true;
        subBulletScript.InitializeBullet(currentShootData, isMoveRight);
        subBulletScript.StartCoroutine(subBulletScript.SubBulletMovement(yDirection));
    }

    /// <summary>
    /// サブ弾の特殊な動き（斜め→平行）を制御するコルーチン
    /// </summary>
    private IEnumerator SubBulletMovement(float yDirection)
    {
        initialPosition = this.transform.position; // 初期位置を記録

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            yield break;

        // 1. 初速を斜め方向に設定
        float horizontalVelocity = (isMoveRight ? 1 : -1) * shootSpeed;
        rb.velocity = new Vector2(horizontalVelocity, yDirection * shootSpeed / 2);
        Debug.Log($"SubBulletMovement: Initial velocity set to {rb.velocity}");

        // 2. 指定した高さに到達するまで待機
        while (Mathf.Abs(transform.position.y - initialPosition.y) < height)
        {
            yield return null; // 1フレーム待つ
        }

        // 3. 指定の高さに到達したら、垂直方向の速度を0にして平行移動に切り替える
        rb.velocity = new Vector2(rb.velocity.x, 0);
    }

    private void Start()
    {
        playerEffectManager = PlayerEffectManager.instance;
        if (playerEffectManager == null)
        {
            Debug.LogWarning("PlayerEffectManagerが見つかりません。ロボットの弾を生成できません。");
            Destroy(this.gameObject);
            return;
        }
    }

    private void FixedUpdate()
    {
        foreach (var key in enemyCooldowns.Keys.ToList())
        {
            enemyCooldowns[key] -= Time.fixedDeltaTime;
            if (enemyCooldowns[key] <= 0f)
            {
                enemyCooldowns.Remove(key);
            }
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        IDamageable hpScript = collision.GetComponent<IDamageable>();
        // IDamageableがない場合は何もしない
        if (hpScript != null)
        {
            MonoBehaviour mb = hpScript as MonoBehaviour;
            if (mb.enabled == false)
            {
                return; // IDamageableが無効化されている場合は何もしない
            }

            //接触した敵オブジェクトを取得
            GameObject enemy = collision.gameObject;

            // まだその敵オブジェクトがクールタイム中なら何もしない
            if (enemyCooldowns.ContainsKey(enemy))
                return;

            enemyCooldowns[enemy] = cooldownTime; // クールタイム開始
            currentPenetrationCount++; //貫通数を増やす
            if (hitEffect != null)
            {
                //ヒット時のエフェクトを再生
                hitEffect.transform.position = this.transform.position;
                hitEffect.Play();
            }

            // ヒット処理
            int damageSumAmount = playerEffectManager.CalculateFinalAttackPower(shootPower);

            hpScript.Damage(damageSumAmount); // ダメージ量を指定

            if (wpCost > 0)
            {
                // WPを消費
                PlayerManager.instance?.AddWpConsumptionBuffer(wpCost);
            }

            if (currentPenetrationCount >= penetrationLimitCount) //貫通数が上限に達したら消える
            {
                Destroy(this.gameObject, 0);
            }

            return; //処理を終了
        }

        if (!collision.isTrigger)
        {
            if (collision.CompareTag(GameConstants.PlayerTagName))
            {
                //プレイヤーに当たった場合は何もしない
                return;
            }
            Destroy(this.gameObject, 0); //弾が壁(敵以外)に当たったら消える
        }
    }
}
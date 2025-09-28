using System.Collections.Generic;
using System.Linq;
using Effekseer;
using UnityEngine;

public class Robot_blade_move : MonoBehaviour
{
    private PlayerEffectManager playerEffectManager;
    private PlayerManager playerManager;
    public bool isBladeSwinging { get; private set; } = false;
    public float BladeLength { get; private set; } = 0;

    [Header("Faboの剣にはKinematicなRigidbody2Dが必要")]
    [SerializeField]
    private GameObject RobotObject; //ロボットのオブジェクト

    [SerializeField]
    private GameObject RobotBladeParticle;

    [SerializeField]
    private EffekseerEffectAsset effect; // .efk を指定

    // 敵ごとのクールタイムタイマー
    private Dictionary<GameObject, float> enemyCooldowns = new Dictionary<GameObject, float>();
    private int bladePower = 0; //剣そのものの攻撃力
    private float cooldownTime = 1.0f; // クールタイム（秒）
    public float attackTime { get; private set; } = 1.0f;
    public float moveTime { get; private set; } = 1.0f;
    private float wpCost = 0f; // WP消費量
    private bool rightFlag = true;
    public bool isStarted { get; private set; } = false; //生成が完了したかどうか
    private Sprite sprite;
    private Vector2 newColliderOffset = Vector2.zero;
    private Vector2 newColliderSize = Vector2.zero;
    private CapsuleCollider2D capsuleCollider;
    private SpriteRenderer spriteRenderer;
    private Robot_move robotMoveScript;
    private BladeWeaponData attack;

    private void Awake()
    {
        capsuleCollider = this.gameObject.GetComponent<CapsuleCollider2D>();
        spriteRenderer = this.gameObject.GetComponent<SpriteRenderer>();

        if (RobotObject == null)
        {
            RobotObject = transform.parent.gameObject;
            if (RobotObject == null)
            {
                Debug.LogError("RobotObjectが設定されていません。");
            }
        }

        robotMoveScript = RobotObject.GetComponent<Robot_move>();
        if (robotMoveScript == null)
        {
            Debug.LogError("Robot_moveスクリプトが見つかりません。");
        }
    }

    /// <summary>
    /// Robot_moveから武器データを受け取り、自身のパラメータを更新する
    /// </summary>
    public void SetBladeData(BladeWeaponData data)
    {
        attack = data; // BladeWeaponDataをattack変数にキャッシュ
        if (attack != null)
        {
            sprite = attack.itemSprite;
            spriteRenderer.sprite = sprite;
            bladePower = attack.power;
            wpCost = attack.wpCost;
            cooldownTime = attack.cooldownTime;
            attackTime = attack.attackTime;
            newColliderOffset = attack.ColliderOffset;
            newColliderSize = attack.ColliderSize;

            capsuleCollider.offset = newColliderOffset;
            capsuleCollider.size = newColliderSize;
            RobotBladeParticle.GetComponent<RobotBladeParticle>().BladeLenght = attack
                .ColliderSize
                .x;
        }
        else
        {
            Debug.LogWarning($"Bladeデータがnullです。");
        }
    }

    private void Start()
    {
        playerEffectManager = PlayerEffectManager.instance;
        if (playerEffectManager == null)
        {
            Debug.LogError("PlayerEffectManagerが見つかりません。ロボットの剣の動きに影響します。");
        }

        playerManager = PlayerManager.instance;
        if (playerManager == null)
        {
            Debug.LogError("PlayerManagerが見つかりません。ロボットの剣の動きに影響します。");
        }
    }

    private void FixedUpdate()
    {
        // 初期化が完了していない場合は何もしない
        if (!GameManager.isFirstGameSceneOpen)
            return;

        isBladeSwinging = robotMoveScript.isBladeSwinging;

        if (isBladeSwinging)
        {
            if (!capsuleCollider.enabled)
            {
                capsuleCollider.enabled = true; //当たり判定を得る(見た目上はそうなっていないが、大丈夫)
            }
        }
        else
        {
            rightFlag = robotMoveScript.rightFlag;
            this.transform.rotation = Quaternion.Euler(0f, 0f, rightFlag ? -30 : 210); //非攻撃時は剣の向きを逐一変える

            if (capsuleCollider.enabled)
            {
                capsuleCollider.enabled = false; //当たり判定を失くす
            }
        }

        // タイマーを減らす（必要に応じてクリア）
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
        if (hpScript == null)
        {
            return; // IDamageableがない場合は何もしない
        }

        MonoBehaviour mb = hpScript as MonoBehaviour;
        if (mb.enabled == false)
        {
            return; // IDamageableが無効化されている場合は何もしない
        }

        GameObject enemy = collision.gameObject;

        // まだクールタイム中なら何もしない
        if (enemyCooldowns.ContainsKey(enemy))
            return;

        // クールタイム開始
        enemyCooldowns[enemy] = cooldownTime;

        // 衝突点（自分のCollider上の、collisionに最も近い点）
        Vector2 contactPoint = capsuleCollider.ClosestPoint(collision.transform.position);
        if (effect != null)
        {
            var handle = EffekseerSystem.PlayEffect(effect, contactPoint);
            //エフェクトを再生
        }

        //様々な効果を考慮した攻撃力を計算
        int damageSumAmount = playerEffectManager.CalculateFinalAttackPower(bladePower);

        //ダメージ量を指定
        hpScript.Damage(damageSumAmount);

        if (wpCost > 0)
        {
            // WPを消費
            playerManager?.AddWpConsumptionBuffer(wpCost);
        }
    }

    private void OnEnable()
    {
        // 初期化が完了していない場合は何もしない
        if (!GameManager.isFirstGameSceneOpen)
            return;

        //生成が完了したフラグを立てる
        isStarted = true;
        //向きの変数を初期化
        rightFlag = robotMoveScript.rightFlag;
        //画像の角度を初期化
        this.transform.rotation = Quaternion.Euler(0f, 0f, rightFlag ? 60 : 120);
    }

    private void OnDisable()
    {
        //生成が完了したフラグを下げる
        isStarted = false;
    }
}
